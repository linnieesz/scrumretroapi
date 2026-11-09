using Microsoft.EntityFrameworkCore;
using ScrumRetroApi.Common;
using ScrumRetroApi.Data;
using ScrumRetroApi.DTOs;
using ScrumRetroApi.Models;

namespace ScrumRetroApi.Services;

public interface IProjectService
{
    Task<IReadOnlyList<ProjectDto>> ListAsync(bool onlyMine, CancellationToken ct);
    Task<ProjectDto> GetAsync(Guid id, CancellationToken ct);
    Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct);
    Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct);
    Task DeleteAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ProjectMemberDto>> ListMembersAsync(Guid id, CancellationToken ct);
    Task<ProjectMemberDto> AddMemberAsync(Guid id, AddProjectMemberRequest request, CancellationToken ct);
    Task RemoveMemberAsync(Guid id, Guid userId, CancellationToken ct);
}

public sealed class ProjectService(AppDbContext db, ICurrentUser current, IProjectAccessService access)
    : IProjectService
{
    public async Task<IReadOnlyList<ProjectDto>> ListAsync(bool onlyMine, CancellationToken ct)
    {
        var uid = current.Id;
        var query = db.Projects.AsNoTracking().AsQueryable();

        // Admin vê todos; os demais só os seus. onlyMine restringe também o Admin aos projetos em que é membro.
        if (!current.IsAdmin || onlyMine)
            query = query.Where(p => p.Members.Any(m => m.UserId == uid));

        // Ordena ANTES de projetar para o record: o EF não traduz OrderBy/Where sobre membros de um construtor.
        return await ProjectDtos(query.OrderBy(p => p.Name), uid).ToListAsync(ct);
    }

    public async Task<ProjectDto> GetAsync(Guid id, CancellationToken ct)
    {
        await access.GetProjectForViewAsync(id, ct);
        return await LoadDtoAsync(id, ct);
    }

    public async Task<ProjectDto> CreateAsync(CreateProjectRequest request, CancellationToken ct)
    {
        if (!current.IsManagerOrAdmin)
            throw new ForbiddenException("Apenas Admin e Manager podem criar projetos.");

        ValidateProject(request.Name, request.Description);

        var ownerId = request.OwnerId ?? current.Id;
        if (ownerId != current.Id && !current.IsAdmin)
            throw new ForbiddenException("Apenas o Admin pode criar projetos em nome de outra pessoa.");

        await GetEligibleOwnerAsync(ownerId, ct);

        var memberIds = (request.MemberIds ?? new List<Guid>()).Append(ownerId).Distinct().ToList();

        // Manager só pode incluir pessoas da própria empresa (o Admin não tem essa restrição).
        var others = memberIds.Where(id => id != current.Id).ToList();
        if (!current.IsAdmin && others.Count > 0)
            await EnsureSameCompanyAsync(others, ct);
        var validIds = await db.Users
            .Where(u => memberIds.Contains(u.Id) && !u.IsDeleted && u.IsActive)
            .Select(u => u.Id)
            .ToListAsync(ct);
        var invalid = memberIds.Except(validIds).ToList();
        if (invalid.Count > 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["memberIds"] = new[] { $"Usuários inexistentes ou inativos: {string.Join(", ", invalid)}." }
            });
        }

        var now = DateTime.UtcNow;
        var project = new Project
        {
            Name = request.Name.Trim(),
            Description = InputRules.Clean(request.Description),
            OwnerId = ownerId,
            CreatedAt = now,
            UpdatedAt = now
        };
        foreach (var memberId in memberIds)
            project.Members.Add(new ProjectMember { UserId = memberId, AddedAt = now });

        db.Projects.Add(project);
        await db.SaveChangesAsync(ct);

        return await LoadDtoAsync(project.Id, ct);
    }

    public async Task<ProjectDto> UpdateAsync(Guid id, UpdateProjectRequest request, CancellationToken ct)
    {
        var project = await access.GetProjectForManageAsync(id, ct);
        ValidateProject(request.Name, request.Description);

        var now = DateTime.UtcNow;

        if (request.OwnerId.HasValue && request.OwnerId.Value != project.OwnerId)
        {
            if (!current.IsAdmin)
                throw new ForbiddenException("Apenas o Admin pode transferir a propriedade do projeto.");

            var newOwner = await GetEligibleOwnerAsync(request.OwnerId.Value, ct);
            project.OwnerId = newOwner.Id;

            // O dono sempre é membro.
            var alreadyMember = await db.ProjectMembers.AnyAsync(
                m => m.ProjectId == project.Id && m.UserId == newOwner.Id, ct);
            if (!alreadyMember)
                db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = newOwner.Id, AddedAt = now });
        }

        project.Name = request.Name.Trim();
        project.Description = InputRules.Clean(request.Description);
        project.UpdatedAt = now;

        await db.SaveChangesAsync(ct);
        return await LoadDtoAsync(project.Id, ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct)
    {
        var project = await access.GetProjectForManageAsync(id, ct);
        db.Projects.Remove(project); // sprints, cards, sugestões e membros saem em cascata (FK ON DELETE CASCADE)
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ProjectMemberDto>> ListMembersAsync(Guid id, CancellationToken ct)
    {
        await access.GetProjectForViewAsync(id, ct);
        return await ToMemberDtos(
                db.ProjectMembers.AsNoTracking()
                    .Where(m => m.ProjectId == id && !m.User.IsDeleted)
                    .OrderBy(m => m.User.FullName))
            .ToListAsync(ct);
    }

    public async Task<ProjectMemberDto> AddMemberAsync(Guid id, AddProjectMemberRequest request, CancellationToken ct)
    {
        var project = await access.GetProjectForManageAsync(id, ct);

        var email = InputRules.NormalizeEmail(request.Email);
        if (request.UserId is null && email.Length == 0)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["email"] = new[] { "Informe o e-mail (ou o id) do usuário a ser inserido no projeto." }
            });
        }

        User? user;
        if (request.UserId.HasValue)
        {
            var userId = request.UserId.Value;
            user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted && u.IsActive, ct);
        }
        else
        {
            var v = new ValidationBuilder();
            InputRules.Email(v, email);
            v.ThrowIfAny();
            user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && !u.IsDeleted && u.IsActive, ct);
        }

        if (user is not null && !current.IsAdmin)
        {
            // Para o Manager, pessoas de outras empresas "não existem" (não revela se o e-mail está cadastrado).
            var myCompanyId = await GetMyCompanyIdAsync(ct);
            if (myCompanyId is null) throw NoCompanyException();
            if (user.CompanyId != myCompanyId) user = null;
        }

        if (user is null)
            throw new NotFoundException("Nenhum usuário ativo foi encontrado na sua empresa com os dados informados.");

        var alreadyMember = await db.ProjectMembers.AnyAsync(
            m => m.ProjectId == project.Id && m.UserId == user.Id, ct);
        if (alreadyMember)
            throw new ConflictException("Este usuário já participa do projeto.");

        db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = user.Id, AddedAt = DateTime.UtcNow });
        project.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var targetId = user.Id;
        return await ToMemberDtos(
                db.ProjectMembers.AsNoTracking()
                    .Where(m => m.ProjectId == project.Id && m.UserId == targetId))
            .FirstAsync(ct);
    }

    public async Task RemoveMemberAsync(Guid id, Guid userId, CancellationToken ct)
    {
        var project = await access.GetProjectForManageAsync(id, ct);

        if (userId == project.OwnerId)
            throw new ConflictException("O dono do projeto não pode ser removido enquanto for o dono.");

        var membership = await db.ProjectMembers.FirstOrDefaultAsync(
            m => m.ProjectId == id && m.UserId == userId, ct)
            ?? throw new NotFoundException("Este usuário não participa do projeto.");

        db.ProjectMembers.Remove(membership);
        project.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
    }

    // ---------- Helpers ----------

    private static void ValidateProject(string? name, string? description)
    {
        var v = new ValidationBuilder();
        InputRules.RequiredText(v, "name", name, 150,
            "O nome do projeto é obrigatório.", "O nome do projeto deve ter no máximo 150 caracteres.");
        InputRules.OptionalText(v, "description", "A descrição", description, 2000);
        v.ThrowIfAny();
    }

    private async Task<User> GetEligibleOwnerAsync(Guid userId, CancellationToken ct)
    {
        var owner = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && !u.IsDeleted && u.IsActive, ct)
                    ?? throw new BadRequestException("O dono informado não existe ou está inativo.");

        if (owner.Role == UserRole.Member)
            throw new BadRequestException("O dono do projeto deve ser um Manager ou um Admin.");

        return owner;
    }

    private async Task<ProjectDto> LoadDtoAsync(Guid id, CancellationToken ct)
    {
        var uid = current.Id;
        return await ProjectDtos(db.Projects.AsNoTracking().Where(p => p.Id == id), uid).SingleAsync(ct);
    }

    private static IQueryable<ProjectDto> ProjectDtos(IQueryable<Project> query, Guid uid) =>
        query.Select(p => new ProjectDto(
            p.Id,
            p.Name,
            p.Description,
            p.OwnerId,
            p.Owner.FullName,
            p.OwnerId == uid,
            p.Members.Any(m => m.UserId == uid),
            p.Members.Count,
            p.CreatedAt,
            p.UpdatedAt));

    private async Task<Guid?> GetMyCompanyIdAsync(CancellationToken ct)
    {
        var uid = current.Id;
        return await db.Users.Where(u => u.Id == uid).Select(u => u.CompanyId).FirstOrDefaultAsync(ct);
    }

    private static ForbiddenException NoCompanyException() =>
        new("Você não está vinculado a uma empresa. Peça a um administrador para vinculá-lo.");

    private async Task EnsureSameCompanyAsync(List<Guid> userIds, CancellationToken ct)
    {
        var myCompanyId = await GetMyCompanyIdAsync(ct);
        if (myCompanyId is null) throw NoCompanyException();

        var cid = myCompanyId.Value;
        var outside = await db.Users.CountAsync(
            u => userIds.Contains(u.Id) && (u.CompanyId == null || u.CompanyId != cid), ct);
        if (outside > 0)
            throw new ForbiddenException("Você só pode adicionar pessoas da sua própria empresa.");
    }

    // ProjectMemberDto não carrega e-mail: nenhuma lista de projeto expõe e-mails de usuários.
    private static IQueryable<ProjectMemberDto> ToMemberDtos(IQueryable<ProjectMember> query) =>
        query.Select(m => new ProjectMemberDto(
            m.UserId,
            m.User.FullName,
            m.User.Role,
            m.UserId == m.Project.OwnerId,
            m.User.IsActive,
            m.AddedAt));
}
