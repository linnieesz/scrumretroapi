using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ScrumRetroApi.Common;
using ScrumRetroApi.Data;
using ScrumRetroApi.DTOs;
using ScrumRetroApi.Models;

namespace ScrumRetroApi.Services;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct);
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct);
    IReadOnlyList<RoleOptionDto> GetRoleOptions();
}

public sealed class AuthService(
    AppDbContext db,
    ITokenService tokens,
    IOptionsSnapshot<RegistrationOptions> registration) : IAuthService
{
    private const string InvalidCredentials = "E-mail ou senha inválidos.";

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        var email = InputRules.NormalizeEmail(request.Email);
        var password = request.Password ?? string.Empty;

        var user = await db.Users.Include(u => u.Company)
            .FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted, ct);
        if (user is null)
        {
            PasswordHasher.BurnTime(password);
            throw new UnauthorizedException(InvalidCredentials);
        }

        if (!PasswordHasher.Verify(password, user.PasswordHash, user.PasswordSalt))
            throw new UnauthorizedException(InvalidCredentials);

        if (!user.IsActive)
            throw new ForbiddenException("Sua conta está desativada. Procure um administrador.");

        return BuildResponse(user);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct)
    {
        var email = InputRules.NormalizeEmail(request.Email);

        var v = new ValidationBuilder();
        InputRules.Profile(v, request.FullName, email, request.Department, request.Phone, request.Bio);
        InputRules.Password(v, request.Password);
        if (request.Role is null || !Enum.IsDefined(request.Role.Value))
            v.Add("role", "O tipo de acesso é obrigatório (Member ou Manager).");
        if (request.CompanyId is null)
            v.Add("companyId", "Selecione a empresa.");
        v.ThrowIfAny();

        var role = request.Role!.Value;

        // Administrador nunca é criado pelo cadastro público.
        if (role == UserRole.Admin)
            throw new ForbiddenException("O perfil de administrador não pode ser escolhido no cadastro público.");

        if (role == UserRole.Manager && !registration.Value.AllowSelfAssignedRoles)
            throw new ForbiddenException("O cadastro público aceita apenas o tipo de acesso Membro.");

        // A empresa precisa existir e estar ativa.
        var companyId = request.CompanyId!.Value;
        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == companyId && c.IsActive, ct)
                      ?? throw new RequestValidationException(new Dictionary<string, string[]>
                      {
                          ["companyId"] = new[] { "Empresa não encontrada ou inativa." }
                      });

        if (await db.Users.AnyAsync(u => u.Email == email, ct))
            throw new ConflictException("Já existe um usuário cadastrado com este e-mail.");

        var user = UserFactory.Create(request.FullName, email, request.Department, request.Phone, request.Bio,
            role, request.Password);
        user.CompanyId = company.Id;
        user.Company = company;
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return BuildResponse(user);
    }

    public IReadOnlyList<RoleOptionDto> GetRoleOptions()
    {
        var allowManager = registration.Value.AllowSelfAssignedRoles;
        return new[]
        {
            new RoleOptionDto("Member", "Membro",
                "Participa dos projetos, registra seus cards e acompanha o quadro das sprints.", true),
            new RoleOptionDto("Manager", "Manager",
                "Cria projetos e sprints, gerencia os membros e acompanha as sugestões de melhoria.", allowManager),
            new RoleOptionDto("Admin", "Administrador",
                "Administra usuários e todos os projetos. Só pode ser criado via API por outro administrador.", false)
        };
    }

    private AuthResponse BuildResponse(User user)
    {
        var (token, expiresAt) = tokens.Create(user);
        return new AuthResponse(token, expiresAt, UserDto.From(user));
    }
}
