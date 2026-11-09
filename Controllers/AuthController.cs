using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumRetroApi.DTOs;
using ScrumRetroApi.Services;

namespace ScrumRetroApi.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public class AuthController(IAuthService auth, ICompanyService companies) : ControllerBase
{
    /// <summary>Autentica e retorna o JWT.</summary>
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct) =>
        Ok(await auth.LoginAsync(request, ct));

    /// <summary>Cadastro público (Member ou Manager). Cria a conta e já retorna o JWT.</summary>
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterRequest request, CancellationToken ct)
    {
        var result = await auth.RegisterAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    /// <summary>Tipos de acesso (valor, rótulo, descrição) para montar a tela de cadastro.</summary>
    [HttpGet("roles")]
    public ActionResult<IReadOnlyList<RoleOptionDto>> Roles() => Ok(auth.GetRoleOptions());

    /// <summary>Empresas ativas (id e nome) para o picker da tela de cadastro.</summary>
    [HttpGet("companies")]
    public async Task<ActionResult<IReadOnlyList<CompanyOptionDto>>> Companies(CancellationToken ct) =>
        Ok(await companies.OptionsAsync(ct));
}
