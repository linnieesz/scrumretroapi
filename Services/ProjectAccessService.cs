using Microsoft.EntityFrameworkCore;
using ScrumRetroApi.Common;
using ScrumRetroApi.Data;
using ScrumRetroApi.Models;

namespace ScrumRetroApi.Services;

/// <summary>Regras centrais de acesso a projetos e sprints, reutilizadas pelos demais serviços.</summary>
public interface IProjectAccessService
{
    /// <summary>Admin, ou usuário que participa do projeto.</summary>
    Task<Project> GetProjectForViewAsync(Guid projectId, CancellationToken ct);

    /// <summary>Admin, ou Manager que participa do projeto.</summary>
    Task<Project> GetProjectForManageAsync(Guid projectId, CancellationToken ct);

    Task<Sprint> GetSprintForViewAsync(Guid sprintId, CancellationToken ct);
    Task<Sprint> GetSprintForManageAsync(Guid sprintId, CancellationToken ct);
}

public sealed class ProjectAccessService(AppDbContext db, ICurrentUser current) : IProjectAccessService
{
    public async Task<Project> GetProjectForViewAsync(Guid projectId, CancellationToken ct)
    {
        var project = await db.Projects.FirstOrDefaultAsync(p => p.Id == projectId, ct)
                      ?? throw new NotFoundException("Projeto não encontrado.");

        if (!current.IsAdmin)
        {
            var uid = current.Id;
            var isMember = await db.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == uid, ct);
            if (!isMember) throw new ForbiddenException("Você não participa deste projeto.");
        }

        return project;
    }

    public async Task<Project> GetProjectForManageAsync(Guid projectId, CancellationToken ct)
    {
        if (!current.IsManagerOrAdmin)
            throw new ForbiddenException("Apenas Admin e Manager podem realizar esta ação.");

        return await GetProjectForViewAsync(projectId, ct);
    }

    public async Task<Sprint> GetSprintForViewAsync(Guid sprintId, CancellationToken ct)
    {
        var sprint = await db.Sprints.FirstOrDefaultAsync(s => s.Id == sprintId, ct)
                     ?? throw new NotFoundException("Sprint não encontrada.");
        await GetProjectForViewAsync(sprint.ProjectId, ct);
        return sprint;
    }

    public async Task<Sprint> GetSprintForManageAsync(Guid sprintId, CancellationToken ct)
    {
        if (!current.IsManagerOrAdmin)
            throw new ForbiddenException("Apenas Admin e Manager podem realizar esta ação.");

        return await GetSprintForViewAsync(sprintId, ct);
    }
}
