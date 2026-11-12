# ScrumRetroApi

API backend (ASP.NET Core Web API, .NET 10) para facilitar as cerimônias de **Sprint Review** e **Retrospectiva** do Scrum.

## Stack

- .NET 10 / C# · ASP.NET Core Web API com Controllers
- EF Core 10 + **PostgreSQL** (Npgsql; `EnsureCreated()`, sem migrations)
- Autenticação JWT stateless (Bearer)
- Swagger (Swashbuckle 10.x) com esquema `http` / `bearer`
- Pacotes na linha `10.0.10`; `Npgsql.EntityFrameworkCore.PostgreSQL` na linha `10.0.*`
- `global.json` pinando o SDK (`10.0.100`, com `rollForward: latestFeature`)

## Como executar (local)

Precisa de um PostgreSQL. Uma opção rápida com Docker:

```bash
docker run --name scrumretro-pg -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=scrumretro -p 5432:5432 -d postgres:17
```

A connection string **não** fica no repositório. Configure-a com user-secrets:

```bash
cd ScrumRetroApi
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Default" "Host=localhost;Port=5432;Database=scrumretro;Username=postgres;Password=postgres"
dotnet restore
dotnet build
dotnet run                          # perfil "http" -> http://localhost:5000 (padrão)
dotnet run --launch-profile https   # perfil opcional -> https://localhost:5001
```

- Swagger: `http://localhost:5000/swagger` (habilitado em Development; em outros ambientes use `Swagger:Enabled=true`).
- O schema e o admin inicial são criados automaticamente na primeira execução (`EnsureCreated()`).
- `EnsureCreated()` **não evolui** o esquema: se o modelo mudar depois, apague/recrie o banco (ou adote migrations).
- Um `scrumretro.db` (SQLite) de versões anteriores **não é migrado** para o PostgreSQL.
- Para o HTTPS local: `dotnet dev-certs https --trust`.

### Autorizar no Swagger

1. Faça `POST /api/auth/login` (ou `/api/auth/register`) e copie o valor de `token`.
2. Clique em **Authorize** e cole **apenas o token** (sem `Bearer `).

### Admin inicial (seed)

Se não existir nenhum administrador, um é criado a partir de `appsettings.json`:

```json
"Seed": { "Admin": { "FullName": "Administrador do Sistema", "Email": "admin@scrumretro.local", "Password": "Admin@12345" } }
```

**Troque a senha e a `Jwt:Key` antes de qualquer uso fora do ambiente local** (use variáveis de ambiente, ex.:
`Jwt__Key`, `Seed__Admin__Password`, ou `dotnet user-secrets`).

## Configuração (`appsettings.json`)

| Chave | Descrição |
|---|---|
| `ConnectionStrings:Default` | PostgreSQL. **Sem valor padrão** (a aplicação não sobe sem ele). Aceita `Host=...;Database=...;Username=...;Password=...` ou a URL `postgres://usuario:senha@host:5432/banco` do Render |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Key`, `Jwt:ExpirationMinutes` | JWT. A chave precisa ter ≥ 32 bytes e, fora de Development, **não pode** começar com `TROQUE-ESTA-CHAVE` (a aplicação não sobe) |
| `Registration:AllowSelfAssignedRoles` | `true` (padrão): cadastro público aceita Member ou Manager. `false`: só Member; Manager/Admin → **403** |
| `Seed:Admin:*` | Admin inicial |
| `Cors:AllowedOrigins` | Padrão `http://localhost:4200` (Angular) |
| `Swagger:Enabled` | Habilita o Swagger fora de Development |


| Variável | Valor / observação |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| `ConnectionStrings__Default` | Internal Database URL do Postgres (formato `postgres://...`) ou connection string Npgsql |
| `Jwt__Key` | Segredo forte, ≥ 32 caracteres (ex.: `openssl rand -base64 48`). Se ficar o valor de exemplo, a aplicação não sobe |
| `Seed__Admin__Email` | E-mail do primeiro administrador |
| `Seed__Admin__Password` | Senha do primeiro administrador (8–100, letras e números). **Troque o padrão do `appsettings.json`** |
| `Cors__AllowedOrigins__0` | URL do front, **sem barra final** (ex.: `https://meu-app.vercel.app`). Mais origens: `Cors__AllowedOrigins__1`, ... |
| `Swagger__Enabled` | `false` em produção (use `true` só para testar) |

   A porta (`ASPNETCORE_HTTP_PORTS=10000`) e `DOTNET_gcConcurrent=false` já vêm no `Dockerfile`.
4. **Acordar o serviço:** o Free dorme após 15 min sem tráfego e leva ~1 min para voltar. Configure um pinger (UptimeRobot,
   cron-job.org...) fazendo `GET https://<seu-servico>.onrender.com/health` a cada 10 minutos. Um serviço ligado 24 h consome ~720 h
   das 750 h gratuitas do mês.
5. **Front (Vercel):** aponte a URL da API do Angular para `https://<seu-servico>.onrender.com/api` (o front hoje usa
   `core/config/api.config.ts`) e inclua o domínio da Vercel em `Cors__AllowedOrigins__0`.
6. **Primeiro acesso:** o schema e o admin são criados na primeira inicialização; entre com `Seed__Admin__Email`/`Seed__Admin__Password`.

Observações:
- O estado do usuário (ativo, papel) é validado a cada requisição com **cache de 30 s**: desativar, rebaixar ou excluir alguém passa a valer em até 30 s.
- O Render termina o TLS; por isso a aplicação não redireciona HTTP→HTTPS.
- Não há migrations: mudanças de modelo exigem recriar o banco (dados de teste são perdidos) ou adotar migrations.

## Convenções

- **Valores de enum** trafegam como texto:
  - Papel: `Member` (Membro), `Manager`, `Admin` (Administrador)
  - Cerimônia: `Review`, `Retrospective`
  - Tipo de card: `WentWell` (o que foi bom), `WentBad` (o que foi ruim), `CanImprove` (o que pode melhorar), `Praise` (parabenização pela atuação), `ImprovementSuggestion` (sugestão de melhoria)
  - Sprint: `Open` (aberta), `Closed` (encerrada)
- **E-mail**: salvo e comparado em minúsculas (inclusive no login). Único, mesmo entre usuários excluídos.
- **Senha**: 8 a 100 caracteres, com letras e números. PBKDF2-HMAC-SHA256 (210.000 iterações) com salt aleatório.
- **Erros** sempre em JSON padronizado, com mensagens em português:

```json
{
  "status": 400,
  "code": "validacao",
  "message": "Um ou mais campos são inválidos.",
  "errors": { "password": ["A senha deve conter letras e números."] },
  "traceId": "0HN..."
}
```

Códigos: `validacao`, `requisicao_invalida` (400), `nao_autenticado` (401), `acesso_negado` (403), `nao_encontrado` (404), `conflito` (409), `erro_interno` (500).

## Endpoints

Todos exigem JWT, exceto os três marcados como **público**.

### Auth

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| POST | `/api/auth/login` | público | Login; retorna `{ token, expiresAt, user }` |
| POST | `/api/auth/register` | público | Cadastro; cria a conta e já retorna o JWT (201) |
| GET | `/api/auth/roles` | público | Tipos de acesso: `value`, `label`, `description`, `allowedOnRegistration` |
| GET | `/api/auth/companies` | público | Picker de empresa do cadastro: empresas **ativas**, apenas `id` e `name` |

Cadastro: `fullName*`, `email*`, `department`, `phone`, `bio`, `role*` (`Member` ou `Manager`), `companyId*` (empresa do picker), `password*`.
`Admin` no cadastro público retorna sempre **403**. `/api/auth/roles` lista os três tipos; a tela deve exibir apenas os que têm `allowedOnRegistration: true`.

### Users

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/users/me` | autenticado | Meu perfil |
| PUT | `/api/users/me` | autenticado | Edita meu perfil (não altera papel nem status) |
| PUT | `/api/users/me/password` | autenticado | Troca a senha (`currentPassword`, `newPassword`) |
| GET | `/api/users?search=&role=` | Admin | Lista usuários |
| POST | `/api/users` | Admin | Cria usuário (qualquer papel, inclusive Admin). `companyId` obrigatório para Manager/Member |
| GET | `/api/users/{id}` | Admin | Detalhe |
| PUT | `/api/users/{id}` | Admin | Edita (inclui `role`, `isActive` e `companyId`; é por aqui que se vincula a uma empresa um usuário antigo) |
| DELETE | `/api/users/{id}` | Admin | Soft delete |

Regras: não é possível desativar, rebaixar ou excluir o **último admin ativo** (409). Também é bloqueado (409) desativar, rebaixar para Member ou excluir quem é **dono de projetos** — transfira a propriedade antes.

### Companies

Empresas agrupam os usuários. **Só o Admin cria e edita empresas.** Campos: `name` (nome), `legalName` (razão social, opcional), `cnpj` (único; numérico ou alfanumérico, validado com dígito verificador; guardado sem pontuação), `isActive`, `memberCount` e os membros (usuários vinculados).

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/companies?search=` | Admin | Lista empresas (nome, razão social ou CNPJ) |
| POST | `/api/companies` | Admin | Cria empresa (`name`, `legalName?`, `cnpj`) |
| GET | `/api/companies/{id}` | Admin | Detalhe |
| PUT | `/api/companies/{id}` | Admin | Edita (`name`, `legalName`, `cnpj`, `isActive`) |
| GET | `/api/companies/{companyId}/members?search=&role=` | Admin (qualquer empresa); Manager (só a sua) | **Busca de membros dentro da empresa**: ativos, até 50, ordenados por nome. Retorna `id`, `fullName`, `department`, `role` — **sem e-mail**. A busca é só por nome/departamento |

Empresa inativa some do picker do cadastro e não aceita novos usuários (os atuais continuam).

### Projects

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/projects?onlyMine=` | autenticado | Admin vê todos; demais só os seus. `onlyMine=true` → só onde é membro. Traz `ownerId`, `ownerName`, `isOwner`, `isMember` |
| POST | `/api/projects` | Admin, Manager | Corpo: `name`, `description`, `ownerId?`, `memberIds?`. Sem `ownerId`, o dono é quem criou. Só o Admin cria em nome de outro. Manager só inclui `memberIds` da própria empresa (403) |
| GET | `/api/projects/{id}` | Admin ou membro | Detalhe |
| PUT | `/api/projects/{id}` | Admin; Manager membro | Edita nome/descrição. `ownerId` (transferência) só o Admin |
| DELETE | `/api/projects/{id}` | Admin; Manager membro | Exclui (sprints, cards e sugestões saem em cascata) |
| GET | `/api/projects/{id}/members` | Admin ou membro | Lista membros (`userId`, `fullName`, `role`, `isOwner`, `isActive`, `addedAt`; sem e-mail) |
| POST | `/api/projects/{id}/members` | Admin; Manager membro | Insere membro por `userId` (escolhido na busca da empresa) ou `email` digitado. Manager só adiciona pessoas **da própria empresa**; as demais respondem 404 |
| DELETE | `/api/projects/{id}/members/{userId}` | Admin; Manager membro | Remove. O dono não pode ser removido (409) |

O dono precisa ser Manager ou Admin e é sempre membro.

### Sprints

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/projects/{projectId}/sprints` | Admin ou membro | Lista (ordem crescente de número) |
| POST | `/api/projects/{projectId}/sprints` | Admin; Manager membro | Cria a próxima sprint (numeração sequencial, status `Open`). Só com a anterior encerrada (409 se houver sprint aberta). `startDate` = próximo dia útil após o encerramento da anterior (nunca no passado; feriados não considerados) |
| PUT | `/api/sprints/{sprintId}/close` | Admin; Manager membro | Encerra a sprint (409 se já encerrada) |
| GET | `/api/sprints/{sprintId}` | Admin ou membro | *Extra:* detalhe de uma sprint |

### Cards

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| POST | `/api/sprints/{sprintId}/cards` | Admin ou membro do projeto | Cria card (`ceremony`, `type`, `content`). Sprint encerrada → 409 |
| GET | `/api/sprints/{sprintId}/cards/mine?ceremony=` | Admin ou membro | Somente os meus cards |
| GET | `/api/sprints/{sprintId}/board?ceremony=` | Admin ou membro | Quadro "ao vivo": anônimo e embaralhado a cada consulta |
| PUT | `/api/cards/{cardId}` | autor | *Extra:* edita o texto do próprio card (sprint aberta) |
| DELETE | `/api/cards/{cardId}` | autor | *Extra:* exclui o próprio card (sprint aberta) |

O quadro **nunca** retorna autoria (nem data/hora do card), para nenhum papel. O autor fica no banco só para "meus cards".

### Suggestions

| Método | Rota | Acesso | Descrição |
|---|---|---|---|
| GET | `/api/sprints/{sprintId}/suggestions/carried-over` | Admin ou membro | Sugestões de sprints **anteriores** do projeto ainda **não tomadas**, anônimas e embaralhadas |
| PATCH | `/api/suggestions/{trackingId}/status` | Admin; Manager membro | Corpo `{ "isTaken": bool?, "isUseful": bool? }` (ao menos um) |

Todo card `ImprovementSuggestion` cria um registro de acompanhamento. Os dois eixos são independentes:
`isTaken` (`true` = foi tomada / `false` = não foi tomada) e `isUseful` (`true` = foi útil / `false` = foi inútil / `null` = ainda não avaliada).

## Matriz de permissões

| Ação | Admin | Manager | Member |
|---|:-:|:-:|:-:|
| Gerenciar usuários (`/api/users`, exceto `me` e `directory`) | ✔ | ✘ | ✘ |
| Criar/editar empresas | ✔ | ✘ | ✘ |
| Buscar membros na empresa (sem e-mail) | qualquer empresa | só a sua | ✘ |
| Ver projetos | todos | só os seus | só os seus |
| Criar projeto | ✔ (em nome de outro também) | ✔ (só em seu nome) | ✘ |
| Editar/excluir projeto, editar membros | qualquer | só onde participa | ✘ |
| Transferir propriedade | ✔ | ✘ | ✘ |
| Criar/encerrar sprint | qualquer | só onde participa | ✘ |
| Criar card, ver meus cards, ver quadro | ✔ | onde participa | onde participa |
| Atualizar status de sugestão | ✔ | só onde participa | ✘ |

Além dos atributos `[Authorize]` nos controllers, **os serviços revalidam** papel e participação (defesa em profundidade). Além disso, a cada requisição o JWT é conferido contra o banco: usuário desativado/excluído ou com papel alterado perde o acesso em até 30 s (cache da checagem), sem esperar o token expirar.

## Estrutura

```
Controllers/   endpoints finos
Services/      regras de negócio e autorização
Data/          AppDbContext e seed
Models/        entidades e enums
DTOs/          contratos de entrada/saída (mensagens de validação em português)
Common/        exceções, validação, hash de senha, usuário atual, opções
Middleware/    tratamento global de exceções (JSON padronizado)
```

## Decisões e interpretações

- **Admin acessa todos os projetos**, inclusive para criar cards e ver o quadro (continua sem ver autoria).
- **Manager** só edita/exclui projetos, sprints e status de sugestões onde **participa**; "Admin e Manager podem excluir projetos" foi lido com essa mesma restrição para o Manager.
- Só pode haver **uma sprint aberta** por projeto: criar a próxima exige encerrar a atual. O `startDate` usa o fuso `Sprints:TimeZoneId` (padrão `America/Sao_Paulo`). As sugestões "carregadas" são as das sprints de **número menor** que a consultada.
- Extras além da lista de rotas (para completar o CRUD de cards e sprints): `GET /api/sprints/{id}`, `PUT/DELETE /api/cards/{cardId}`. Remova se não quiser.
- Exclusão de usuário é **soft delete**: o e-mail continua reservado; as participações em projetos são removidas; os cards enviados permanecem.

## Pontos de atenção de segurança

0. **Privacidade de e-mails.** Nenhuma listagem de projeto/equipe/busca devolve e-mail; só o Admin enxerga e-mails (`/api/users`). A busca por membros é por nome (não por e-mail), para que não dê para descobrir e-mails por tentativa. Resta que o Manager pode **digitar** um e-mail ao adicionar membro (resposta 404 se não for da empresa dele, sem distinguir "não existe" de "é de outra empresa").
   - `GET /api/auth/companies` é **público** (necessário para o picker do cadastro) e revela os nomes das empresas cadastradas. Se isso for sensível, troque por convite/código de empresa.
   - Usuários **antigos** (anteriores às empresas) ficam sem empresa: um Manager nessa condição não consegue buscar nem adicionar membros até o Admin vinculá-lo (`PUT /api/users/{id}` com `companyId`).

1. **Escolha livre de papel no cadastro público.** Com `AllowSelfAssignedRoles=true`, qualquer pessoa na internet vira **Manager** sozinha. Um Manager pode criar projetos e consultar o diretório de usuários (nome e e-mail de todos os ativos). Em produção, prefira `false` (só Member) e promova pessoas via `PUT /api/users/{id}`, ou proteja o cadastro (convite, domínio de e-mail, aprovação).
2. **Segredos no `appsettings.json`.** A `Jwt:Key` e a senha do admin são apenas valores de desenvolvimento. Troque por variáveis de ambiente/secret manager.
3. **Sem rate limiting nem bloqueio de tentativas** no login/cadastro. O cadastro retorna 409 para e-mail existente (permite enumerar e-mails). Considere `AddRateLimiter`, lockout e captcha.
4. **Anonimato é garantido na API, não no banco.** O autor do card existe no banco (necessário para "meus cards"); quem tiver acesso ao banco ou a logs de SQL pode rastreá-lo. O quadro omite autoria e data/hora e embaralha a ordem, mas times muito pequenos podem se identificar pelo conteúdo do texto.
5. **JWT stateless.** Tokens não são revogáveis individualmente; o risco é mitigado pela checagem de usuário ativo/papel a cada requisição (1 consulta ao banco) e pela expiração curta.
6. **HTTPS.** O TLS é terminado pelo proxy do Render; a aplicação não redireciona HTTP→HTTPS. Se precisar de IP/esquema reais do cliente, configure `ForwardedHeaders`.
7. **CORS** restrito às origens configuradas (`Cors:AllowedOrigins`).
8. **Sem migrations.** `EnsureCreated()` não evolui o esquema; para produção, adote migrations.
