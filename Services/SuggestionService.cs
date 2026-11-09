using Microsoft.EntityFrameworkCore;
using ScrumRetroApi.Common;
using ScrumRetroApi.Data;
using ScrumRetroApi.DTOs;

namespace ScrumRetroApi.Services;

public interface ISuggestionService
{
    Task<IReadOnlyList<SuggestionTrackingDto>> CarriedOverAsync(Guid sprintId, CancellationToken ct);
    Task<SuggestionTrackingDto> UpdateStatusAsync(Guid trackingId, UpdateSuggestionStatusRequest request,
        CancellationToken ct);
}

public sealed class SuggestionService(AppDbContext db, ICurrentUser current, IProjectAccessService access)
    : ISuggestionService
{
    public async Task<IReadOnlyList<SuggestionTrackingDto>> CarriedOverAsync(Guid sprintId, CancellationToken ct)
    {
        var sprint = await access.GetSprintForViewAsync(sprintId, ct);
        var projectId = sprint.ProjectId;
        var number = sprint.Number;

        // Sugestões de sprints anteriores do mesmo projeto que ainda não foram tomadas. Sem autoria.
        var items = await db.SuggestionTrackings.AsNoTracking()
            .Where(t => t.ProjectId == projectId && !t.IsTaken && t.Card.Sprint.Number < number)
            .Select(t => new SuggestionTrackingDto(
                t.Id,
                t.Card.Content,
                t.Card.Sprint.Number,
                t.IsTaken,
                t.IsUseful,
                t.UpdatedAt))
            .ToListAsync(ct);

        return items.Shuffled();
    }

    public async Task<SuggestionTrackingDto> UpdateStatusAsync(Guid trackingId,
        UpdateSuggestionStatusRequest request, CancellationToken ct)
    {
        if (!current.IsManagerOrAdmin)
            throw new ForbiddenException("Apenas Admin e Manager podem atualizar o status das sugestões.");

        if (request.IsTaken is null && request.IsUseful is null)
        {
            throw new RequestValidationException(new Dictionary<string, string[]>
            {
                ["isTaken"] = new[] { "Informe ao menos um dos campos: isTaken ou isUseful." }
            });
        }

        var tracking = await db.SuggestionTrackings
            .Include(t => t.Card).ThenInclude(c => c.Sprint)
            .FirstOrDefaultAsync(t => t.Id == trackingId, ct)
            ?? throw new NotFoundException("Sugestão não encontrada.");

        await access.GetProjectForManageAsync(tracking.ProjectId, ct);

        if (request.IsTaken.HasValue) tracking.IsTaken = request.IsTaken.Value;
        if (request.IsUseful.HasValue) tracking.IsUseful = request.IsUseful.Value;
        tracking.UpdatedAt = DateTime.UtcNow;
        tracking.UpdatedById = current.Id;

        await db.SaveChangesAsync(ct);

        return new SuggestionTrackingDto(tracking.Id, tracking.Card.Content, tracking.Card.Sprint.Number,
            tracking.IsTaken, tracking.IsUseful, tracking.UpdatedAt);
    }
}
