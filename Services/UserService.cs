using Microsoft.EntityFrameworkCore;
using ScrumRetroApi.Common;
using ScrumRetroApi.Data;
using ScrumRetroApi.DTOs;
using ScrumRetroApi.Models;

namespace ScrumRetroApi.Services;

public interface IUserService
{
    Task<UserDto> GetMeAsync(CancellationToken ct);
    Task<UserDto> UpdateMeAsync(UpdateProfileRequest request, CancellationToken ct);
    Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct);
    Task<IReadOnlyList<UserDto>> ListAsync(string? search, UserRole? role, CancellationToken ct);
    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct);
    Task<UserDto> GetAsync(Guid id, CancellationToken ct);
    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
}

public sealed class UserService(AppDbContext db, ICurrentUser current) : IUserService
{
    // ---------- Próprio usuário ----------

    public async Task<UserDto> GetMeAsync(CancellationToken ct) =>
        UserDto.From(await FindAsync(current.Id, ct));

    public async Task<UserDto> UpdateMeAsync(UpdateProfileRequest request, CancellationToken ct)
    {
        var me = await FindAsync(current.Id, ct);
        var email = InputRules.NormalizeEmail(request.Email);

        var v = new ValidationBuilder();
        InputRules.Profile(v, request.FullName, email, request.Department, request.Phone, request.Bio);
        v.ThrowIfAny();

        await EnsureEmailAvailableAsync(email, me.Id, ct);

        // O tipo de acesso (Role) e o status (IsActive) nunca são alterados aqui.
        me.FullName = request.FullName.Trim();
        me.Email = email;
        me.Department = InputRules.Clean(request.Department);
        me.Phone = InputRules.Clean(request.Phone);
        me.Bio = InputRules.Clean(request.Bio);
        me.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return UserDto.From(me);
    }

    public async Task ChangePasswordAsync(ChangePasswordRequest request, CancellationToken ct)
    {
        var me = await FindAsync(current.Id, ct);

        var v = new ValidationBuilder();
        if (string.IsNullOrEmpty(request.CurrentPassword)) v.Add("currentPassword", "A senha atual é obrigatória.");
        InputRules.Password(v, request.NewPassword, "newPassword");
        v.ThrowIfAny();

        if (!PasswordHasher.Verify(request.CurrentPassword, me.PasswordHash, me.PasswordSalt))
            throw new BadRequestException("A senha atual está incorreta.");

        var (hash, salt) = PasswordHasher.HashPassword(request.NewPassword);
        me.PasswordHash = hash;
        me.PasswordSalt = salt;
        me.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // ---------- Administração (Admin) ----------

    public async Task<IReadOnlyList<UserDto>> ListAsync(string? search, UserRole? role, CancellationToken ct)
    {
        EnsureAdmin();
        var query = ApplyFilters(db.Users.AsNoTracking().Include(u => u.Company).Where(u => !u.IsDeleted), search, role);
        var users = await query.OrderBy(u => u.FullName).ToListAsync(ct);
        return users.Select(UserDto.From).ToList();
    }

    public async Task<UserDto> GetAsync(Guid id, CancellationToken ct)
    {
        EnsureAdmin();
        return UserDto.From(await FindAsync(id, ct));
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct)
    {
        EnsureAdmin();
        var email = InputRules.NormalizeEmail(request.Email);

        var v = new ValidationBuilder();
        InputRules.Profile(v, request.FullName, email, request.Department, request.Phone, request.Bio);
        InputRules.Password(v, request.Password);
        if (request.Role is null || !Enum.IsDefined(request.Role.Value))
            v.Add("role", "O tipo de acesso é obrigatório.");
        v.ThrowIfAny();

        await EnsureEmailAvailableAsync(email, null, ct);
        var company = await ResolveCompanyAsync(request.CompanyId, request.Role!.Value, null, ct);

        var user = UserFactory.Create(request.FullName, email, request.Department, request.Phone, request.Bio,
            request.Role!.Value, request.Password);
        user.CompanyId = company?.Id;
        user.Company = company;
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return UserDto.From(user);
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct)
    {
        EnsureAdmin();
        var target = await FindAsync(id, ct);
        var email = InputRules.NormalizeEmail(request.Email);

        var v = new ValidationBuilder();
        InputRules.Profile(v, request.FullName, email, request.Department, request.Phone, request.Bio);
        if (request.Role is null || !Enum.IsDefined(request.Role.Value))
            v.Add("role", "O tipo de acesso é obrigatório.");
        if (request.IsActive is null)
            v.Add("isActive", "Informe se o usuário está ativo.");
        v.ThrowIfAny();

        var newRole = request.Role!.Value;
        var newActive = request.IsActive!.Value;

        await EnsureEmailAvailableAsync(email, target.Id, ct);
        var company = await ResolveCompanyAsync(request.CompanyId, newRole, target.CompanyId, ct);

        // Não permite desativar ou rebaixar o último admin ativo.
        var losingAdmin = target.Role == UserRole.Admin && target.IsActive &&
                          (newRole != UserRole.Admin || !newActive);
        if (losingAdmin)
            await EnsureNotLastActiveAdminAsync(target.Id, ct);

        // Dono de projeto precisa continuar sendo Manager/Admin e ativo.
        var losingOwnerEligibility = (target.IsActive && !newActive) ||
                                     (target.Role != UserRole.Member && newRole == UserRole.Member);
        if (losingOwnerEligibility)
            await EnsureNoOwnedProjectsAsync(target.Id, ct);

        target.FullName = request.FullName.Trim();
        target.Email = email;
        target.Department = InputRules.Clean(request.Department);
        target.Phone = InputRules.Clean(request.Phone);
        target.Bio = InputRules.Clean(request.Bio);
        target.Role = newRole;
        target.CompanyId = company?.Id;
        target.Company = company;
        target.IsActive = newActive;
        target.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
        return UserDto.From(target);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        EnsureAdmin();
        var target = await FindAsync(id, ct);

        if (target.Role == UserRole.Admin && target.IsActive)
            await EnsureNotLastActiveAdminAsync(target.Id, ct);

        await EnsureNoOwnedProjectsAsync(target.Id, ct);

        var now = DateTime.UtcNow;
        target.IsDeleted = true;
        target.IsActive = false;
        target.DeletedAt = now;
        target.UpdatedAt = now;

        // Soft delete: remove as participações em projetos; cards já enviados permanecem (anônimos no quadro).
        var memberships = await db.ProjectMembers.Where(m => m.UserId == target.Id).ToListAsync(ct);
        db.ProjectMembers.RemoveRange(memberships);

        await db.SaveChangesAsync(ct);
    }

    // ---------- Helpers ----------

    private void EnsureAdmin()
    {
        if (!current.IsAdmin)
            throw new ForbiddenException("Apenas administradores podem realizar esta ação.");
    }

    private async Task<User> FindAsync(Guid id, CancellationToken ct) =>
        await db.Users.Include(u => u.Company).FirstOrDefaultAsync(u => u.Id == id && !u.IsDeleted, ct)
        ?? throw new NotFoundException("Usuário não encontrado.");

    private async Task EnsureEmailAvailableAsync(string email, Guid? ignoreUserId, CancellationToken ct)
    {
        var ignore = ignoreUserId ?? Guid.Empty;
        var taken = await db.Users.AnyAsync(u => u.Email == email && u.Id != ignore, ct);
        if (taken) throw new ConflictException("Já existe um usuário cadastrado com este e-mail.");
    }

    private async Task EnsureNotLastActiveAdminAsync(Guid targetId, CancellationToken ct)
    {
        var others = await db.Users.CountAsync(
            u => u.Id != targetId && u.Role == UserRole.Admin && u.IsActive && !u.IsDeleted, ct);
        if (others == 0)
            throw new ConflictException("Não é possível desativar, rebaixar ou excluir o último administrador ativo.");
    }

    private async Task EnsureNoOwnedProjectsAsync(Guid userId, CancellationToken ct)
    {
        if (await db.Projects.AnyAsync(p => p.OwnerId == userId, ct))
            throw new ConflictException(
                "O usuário é dono de projetos. Transfira a propriedade dos projetos antes de desativar, rebaixar ou excluir.");
    }

    /// <summary>Manager e Member precisam de empresa; Admin pode ficar sem. A empresa deve existir e (se nova) estar ativa.</summary>
    private async Task<Company?> ResolveCompanyAsync(Guid? companyId, UserRole role, Guid? currentCompanyId,
        CancellationToken ct)
    {
        if (companyId is null)
        {
            if (role == UserRole.Admin) return null;
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["companyId"] = new[] { "A empresa é obrigatória para Manager e Membro." }
            });
        }

        var company = await db.Companies.FirstOrDefaultAsync(c => c.Id == companyId.Value, ct)
                      ?? throw new RequestValidationException(new Dictionary<string, string[]>
                      {
                          ["companyId"] = new[] { "Empresa não encontrada." }
                      });

        if (!company.IsActive && company.Id != currentCompanyId)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["companyId"] = new[] { "A empresa está inativa." }
            });
        }

        return company;
    }

    private static IQueryable<User> ApplyFilters(IQueryable<User> query, string? search, UserRole? role)
    {
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = $"%{search.Trim()}%";
            query = query.Where(u => EF.Functions.ILike(u.FullName, term) || EF.Functions.ILike(u.Email, term));
        }

        if (role.HasValue)
        {
            var r = role.Value;
            query = query.Where(u => u.Role == r);
        }

        return query;
    }
}
