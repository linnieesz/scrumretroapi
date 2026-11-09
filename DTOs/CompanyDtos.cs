using System.ComponentModel.DataAnnotations;
using ScrumRetroApi.Models;

namespace ScrumRetroApi.DTOs;

/// <summary>Visão completa (somente Admin).</summary>
public sealed record CompanyDto(
    Guid Id,
    string Name,
    string? LegalName,
    string Cnpj,
    bool IsActive,
    int MemberCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>Item do picker de empresa no cadastro (público): só id e nome, sem CNPJ nem contagens.</summary>
public sealed record CompanyOptionDto(Guid Id, string Name);

/// <summary>Resultado da busca de membros dentro da empresa. Propositalmente SEM e-mail.</summary>
public sealed record CompanyMemberDto(Guid Id, string FullName, string? Department, UserRole Role);

public class CreateCompanyRequest
{
    [Required(ErrorMessage = "O nome da empresa é obrigatório.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "O nome da empresa deve ter entre 2 e 150 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "A razão social deve ter no máximo 200 caracteres.")]
    public string? LegalName { get; set; }

    [Required(ErrorMessage = "O CNPJ é obrigatório.")]
    [StringLength(25, ErrorMessage = "O CNPJ deve ter no máximo 25 caracteres.")]
    public string Cnpj { get; set; } = string.Empty;
}

public class UpdateCompanyRequest
{
    [Required(ErrorMessage = "O nome da empresa é obrigatório.")]
    [StringLength(150, MinimumLength = 2, ErrorMessage = "O nome da empresa deve ter entre 2 e 150 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(200, ErrorMessage = "A razão social deve ter no máximo 200 caracteres.")]
    public string? LegalName { get; set; }

    [Required(ErrorMessage = "O CNPJ é obrigatório.")]
    [StringLength(25, ErrorMessage = "O CNPJ deve ter no máximo 25 caracteres.")]
    public string Cnpj { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe se a empresa está ativa.")]
    public bool? IsActive { get; set; }
}
