using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumRetroApi.Common;
using ScrumRetroApi.DTOs;
using ScrumRetroApi.Services;

namespace ScrumRetroApi.Controllers;

[ApiController]
[Authorize]
public class SprintsController(ISprintService sprints) : ControllerBase
{
    [HttpGet("api/projects/{projectId:guid}/sprints")]
    public async Task<ActionResult<IReadOnlyList<SprintDto>>> List(Guid projectId, CancellationToken ct) =>
        Ok(await sprints.ListAsync(projectId, ct));

    /// <summary>Cria a próxima sprint do projeto (numeração sequencial). Admin ou Manager do projeto.</summary>
    [HttpPost("api/projects/{projectId:guid}/sprints")]
    [Authorize(Roles = RoleNames.AdminOrManager)]
    public async Task<ActionResult<SprintDto>> Create(Guid projectId, CancellationToken ct)
    {
        var created = await sprints.CreateAsync(projectId, ct);
        return CreatedAtAction(nameof(GetById), new { sprintId = created.Id }, created);
    }

    [HttpGet("api/sprints/{sprintId:guid}")]
    public async Task<ActionResult<SprintDto>> GetById(Guid sprintId, CancellationToken ct) =>
        Ok(await sprints.GetAsync(sprintId, ct));

    [HttpPut("api/sprints/{sprintId:guid}/close")]
    [Authorize(Roles = RoleNames.AdminOrManager)]
    public async Task<ActionResult<SprintDto>> Close(Guid sprintId, CancellationToken ct) =>
        Ok(await sprints.CloseAsync(sprintId, ct));
}
