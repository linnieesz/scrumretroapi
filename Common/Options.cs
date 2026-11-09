namespace ScrumRetroApi.Common;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 60;
}

public sealed class RegistrationOptions
{
    public const string SectionName = "Registration";
    public bool AllowSelfAssignedRoles { get; set; } = true;
}

public sealed class SeedAdminOptions
{
    public const string SectionName = "Seed:Admin";
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public sealed class SprintOptions
{
    public const string SectionName = "Sprints";

    /// <summary>Fuso usado para calcular "hoje" e o próximo dia útil (IANA ou Windows). Se inválido, usa o fuso do servidor.</summary>
    public string TimeZoneId { get; set; } = "America/Sao_Paulo";
}
