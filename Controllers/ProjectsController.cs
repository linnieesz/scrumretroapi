using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumRetroApi.Common;
using ScrumRetroApi.DTOs;
using ScrumRetroApi.Services;

namespace ScrumRetroApi.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController(IProjectService projects) : ControllerBase
{
    /// <summary>Admin vê todos; os demais só os seus. onlyMine=true lista só os projetos em que é membro.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ProjectDto>>> List([FromQuery] bool onlyMine, CancellationToken ct) =>
        Ok(await projects.ListAsync(onlyMine, ct));

    [HttpPost]
    [Authorize(Roles = RoleNames.AdminOrManager)]
    public async Task<ActionResult<ProjectDto>> Create(CreateProjectRequest request, CancellationToken ct)
    {
        var created = await projects.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ProjectDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await projects.GetAsync(id, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.AdminOrManager)]
    public async Task<ActionResult<ProjectDto>> Update(Guid id, UpdateProjectRequest request, CancellationToken ct) =>
        Ok(await projects.UpdateAsync(id, request, ct));

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleNames.AdminOrManager)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await projects.DeleteAsync(id, ct);
        return NoContent();
    }

    [HttpGet("{id:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<ProjectMemberDto>>> ListMembers(Guid id, CancellationToken ct) =>
        Ok(await projects.ListMembersAsync(id, ct));

    /// <summary>Insere um membro por e-mail (ou userId). Admin, ou Manager que participa do projeto.</summary>
    [HttpPost("{id:guid}/members")]
    [Authorize(Roles = RoleNames.AdminOrManager)]
    public async Task<ActionResult<ProjectMemberDto>> AddMember(
        Guid id, AddProjectMemberRequest request, CancellationToken ct)
    {
        var member = await projects.AddMemberAsync(id, request, ct);
        return StatusCode(StatusCodes.Status201Created, member);
    }

    [HttpDelete("{id:guid}/members/{userId:guid}")]
    [Authorize(Roles = RoleNames.AdminOrManager)]
    public async Task<IActionResult> RemoveMember(Guid id, Guid userId, CancellationToken ct)
    {
        await projects.RemoveMemberAsync(id, userId, ct);
        return NoContent();
    }
}
