using System.Security.Claims;
using ScrumRetroApi.Models;

namespace ScrumRetroApi.Common;

public interface ICurrentUser
{
    Guid Id { get; }
    UserRole Role { get; }
    bool IsAdmin { get; }
    bool IsManagerOrAdmin { get; }
}

public sealed class CurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    private ClaimsPrincipal Principal =>
        accessor.HttpContext?.User ?? throw new UnauthorizedException();

    public Guid Id =>
        Guid.TryParse(Principal.FindFirstValue("sub"), out var id)
            ? id
            : throw new UnauthorizedException("Token inválido.");

    public UserRole Role =>
        Enum.TryParse<UserRole>(Principal.FindFirstValue("role"), out var role)
            ? role
            : throw new UnauthorizedException("Token inválido.");

    public bool IsAdmin => Role == UserRole.Admin;
    public bool IsManagerOrAdmin => Role is UserRole.Admin or UserRole.Manager;
}
