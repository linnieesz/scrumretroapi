using Microsoft.EntityFrameworkCore;
using ScrumRetroApi.Common;
using ScrumRetroApi.Data;
using ScrumRetroApi.DTOs;
using ScrumRetroApi.Models;

namespace ScrumRetroApi.Services;

public interface ISprintService
{
    Task<IReadOnlyList<SprintDto>> ListAsync(Guid projectId, CancellationToken ct);
    Task<SprintDto> GetAsync(Guid sprintId, CancellationToken ct);
    Task<SprintDto> CreateAsync(Guid projectId, CancellationToken ct);
    Task<SprintDto> CloseAsync(Guid sprintId, CancellationToken ct);
}

public sealed class SprintService(AppDbContext db, IProjectAccessService access, BusinessCalendar calendar)
    : ISprintService
{
    public async Task<IReadOnlyList<SprintDto>> ListAsync(Guid projectId, CancellationToken ct)
    {
        await access.GetProjectForViewAsync(projectId, ct);
        var sprints = await db.Sprints.AsNoTracking()
            .Where(s => s.ProjectId == projectId)
            .OrderBy(s => s.Number)
            .ToListAsync(ct);
        return sprints.Select(ToDto).ToList();
    }

    public async Task<SprintDto> GetAsync(Guid sprintId, CancellationToken ct) =>
        ToDto(await access.GetSprintForViewAsync(sprintId, ct));

    public async Task<SprintDto> CreateAsync(Guid projectId, CancellationToken ct)
    {
        var project = await access.GetProjectForManageAsync(projectId, ct);

        var last = await db.Sprints
            .Where(s => s.ProjectId == project.Id)
            .OrderByDescending(s => s.Number)
            .FirstOrDefaultAsync(ct);

        // A próxima sprint só nasce depois que a anterior foi encerrada (continuidade).
        if (last is not null && last.Status == SprintStatus.Open)
            throw new ConflictException("Encerre a sprint atual antes de criar a próxima.");

        // Início: o próximo dia útil depois do encerramento da sprint anterior (nunca no passado).
        // Primeira sprint do projeto: hoje, ou segunda-feira se hoje for fim de semana.
        var earliest = BusinessCalendar.OnOrNextBusinessDay(calendar.Today());
        var startDate = earliest;
        if (last is not null)
        {
            var closedOn = calendar.ToLocalDate(last.ClosedAt ?? DateTime.UtcNow);
            var afterPrevious = BusinessCalendar.NextBusinessDay(closedOn);
            startDate = afterPrevious > earliest ? afterPrevious : earliest;
        }

        // Numeração sequencial por projeto; o índice único (ProjectId, Number) protege contra corrida.
        var sprint = new Sprint
        {
            ProjectId = project.Id,
            Number = (last?.Number ?? 0) + 1,
            Status = SprintStatus.Open,
            StartDate = startDate,
            CreatedAt = DateTime.UtcNow
        };

        db.Sprints.Add(sprint);
        await db.SaveChangesAsync(ct);
        return ToDto(sprint);
    }

    public async Task<SprintDto> CloseAsync(Guid sprintId, CancellationToken ct)
    {
        var sprint = await access.GetSprintForManageAsync(sprintId, ct);

        if (sprint.Status == SprintStatus.Closed)
            throw new ConflictException("A sprint já está encerrada.");

        sprint.Status = SprintStatus.Closed;
        sprint.ClosedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        return ToDto(sprint);
    }

    private static SprintDto ToDto(Sprint s) =>
        new(s.Id, s.ProjectId, s.Number, s.Status, s.StartDate, s.CreatedAt, s.ClosedAt);
}
