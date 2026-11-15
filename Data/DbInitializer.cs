using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScrumRetroApi.Common;
using ScrumRetroApi.Models;

namespace ScrumRetroApi.Data;

public static class DbInitializer
{
    public static async Task InitializeAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<AppDbContext>();
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");
        var seed = services.GetRequiredService<IOptions<SeedAdminOptions>>().Value;

        await db.Database.EnsureCreatedAsync();

        // Só semeia se não existir nenhum administrador (evita recriar um admin removido de propósito).
        // Atenção: se o MESMO banco já foi usado por outra instância (ex.: sua máquina local), o admin dela vale
        // e o Seed__Admin__* configurado agora é ignorado, sem erro.
        var existingAdmins = await db.Users
            .Where(u => u.Role == UserRole.Admin && !u.IsDeleted)
            .Select(u => u.Email)
            .ToListAsync();
        if (existingAdmins.Count > 0)
        {
            logger.LogInformation(
                "Seed do admin ignorado: já existe(m) administrador(es) neste banco: {Emails}. O Seed:Admin atual NÃO altera usuários existentes.",
                string.Join(", ", existingAdmins));
            return;
        }

        var email = InputRules.NormalizeEmail(seed.Email);
        var v = new ValidationBuilder();
        InputRules.FullName(v, seed.FullName);
        InputRules.Email(v, email);
        InputRules.Password(v, seed.Password, "password");
        if (v.HasErrors)
        {
            try { v.ThrowIfAny(); }
            catch (RequestValidationException ex)
            {
                // Só nomes de campo e regras violadas: nunca o valor da senha.
                logger.LogWarning("Seed do admin ignorado: configuração 'Seed:Admin' inválida -> {Errors}",
                    string.Join(" | ", ex.Errors!.SelectMany(e => e.Value.Select(m => $"{e.Key}: {m}"))));
            }

            return;
        }

        if (seed.Password != seed.Password.Trim())
            logger.LogWarning("Seed:Admin:Password tem espaço/quebra de linha no início ou no fim: confira a variável no Render.");

        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            logger.LogWarning("Seed do admin ignorado: já existe um usuário (não administrador) com o e-mail configurado.");
            return;
        }

        db.Users.Add(UserFactory.Create(seed.FullName, email, null, null, null, UserRole.Admin, seed.Password));
        await db.SaveChangesAsync();
        logger.LogInformation("Administrador inicial criado: {Email}", email);
    }
}
