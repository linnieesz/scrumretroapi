using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumRetroApi.Common;
using ScrumRetroApi.DTOs;
using ScrumRetroApi.Models;
using ScrumRetroApi.Services;

namespace ScrumRetroApi.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize]
public class CompaniesController(ICompanyService companies) : ControllerBase
{
    /// <summary>Lista empresas (CNPJ e contagem de membros). Somente Admin.</summary>
    [HttpGet]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<IReadOnlyList<CompanyDto>>> List([FromQuery] string? search, CancellationToken ct) =>
        Ok(await companies.ListAsync(search, ct));

    /// <summary>Cria uma empresa. Somente Admin.</summary>
    [HttpPost]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<CompanyDto>> Create(CreateCompanyRequest request, CancellationToken ct)
    {
        var created = await companies.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<CompanyDto>> GetById(Guid id, CancellationToken ct) =>
        Ok(await companies.GetAsync(id, ct));

    [HttpPut("{id:guid}")]
    [Authorize(Roles = RoleNames.Admin)]
    public async Task<ActionResult<CompanyDto>> Update(Guid id, UpdateCompanyRequest request, CancellationToken ct) =>
        Ok(await companies.UpdateAsync(id, request, ct));

    /// <summary>
    /// Busca de membros dentro da empresa (ativos, até 50), por nome/departamento. Não retorna e-mail.
    /// Admin: qualquer empresa. Manager: somente a sua.
    /// </summary>
    [HttpGet("{companyId:guid}/members")]
    [Authorize(Roles = RoleNames.AdminOrManager)]
    public async Task<ActionResult<IReadOnlyList<CompanyMemberDto>>> SearchMembers(
        Guid companyId, [FromQuery] string? search, [FromQuery] UserRole? role, CancellationToken ct) =>
        Ok(await companies.SearchMembersAsync(companyId, search, role, ct));
}
