using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumRetroApi.Common;
using ScrumRetroApi.DTOs;
using ScrumRetroApi.Services;

namespace ScrumRetroApi.Controllers;

[ApiController]
[Authorize]
public class SuggestionsController(ISuggestionService suggestions) : ControllerBase
{
    /// <summary>Sugestões de sprints anteriores do projeto ainda não tomadas (anônimas).</summary>
    [HttpGet("api/sprints/{sprintId:guid}/suggestions/carried-over")]
    public async Task<ActionResult<IReadOnlyList<SuggestionTrackingDto>>> CarriedOver(
        Guid sprintId, CancellationToken ct) =>
        Ok(await suggestions.CarriedOverAsync(sprintId, ct));

    /// <summary>Atualiza os eixos independentes: isTaken (tomada) e isUseful (útil). Admin ou Manager do projeto.</summary>
    [HttpPatch("api/suggestions/{trackingId:guid}/status")]
    [Authorize(Roles = RoleNames.AdminOrManager)]
    public async Task<ActionResult<SuggestionTrackingDto>> UpdateStatus(
        Guid trackingId, UpdateSuggestionStatusRequest request, CancellationToken ct) =>
        Ok(await suggestions.UpdateStatusAsync(trackingId, request, ct));
}
