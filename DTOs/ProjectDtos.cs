using System.ComponentModel.DataAnnotations;
using ScrumRetroApi.Models;

namespace ScrumRetroApi.DTOs;

public sealed record ProjectDto(
    Guid Id,
    string Name,
    string? Description,
    Guid OwnerId,
    string OwnerName,
    bool IsOwner,
    bool IsMember,
    int MemberCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record ProjectMemberDto(
    Guid UserId,
    string FullName,
    UserRole Role,
    bool IsOwner,
    bool IsActive,
    DateTime AddedAt);

public class CreateProjectRequest
{
    [Required(ErrorMessage = "O nome do projeto é obrigatório.")]
    [StringLength(150, ErrorMessage = "O nome do projeto deve ter no máximo 150 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "A descrição deve ter no máximo 2000 caracteres.")]
    public string? Description { get; set; }

    /// <summary>Opcional. Se omitido, o dono é quem está logado. Só o Admin pode informar outra pessoa.</summary>
    public Guid? OwnerId { get; set; }

    public List<Guid>? MemberIds { get; set; }
}

public class UpdateProjectRequest
{
    [Required(ErrorMessage = "O nome do projeto é obrigatório.")]
    [StringLength(150, ErrorMessage = "O nome do projeto deve ter no máximo 150 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000, ErrorMessage = "A descrição deve ter no máximo 2000 caracteres.")]
    public string? Description { get; set; }

    /// <summary>Opcional. Só o Admin pode transferir a propriedade.</summary>
    public Guid? OwnerId { get; set; }
}

public class AddProjectMemberRequest
{
    /// <summary>E-mail do usuário a inserir (alternativa a userId).</summary>
    [StringLength(254, ErrorMessage = "O e-mail deve ter no máximo 254 caracteres.")]
    public string? Email { get; set; }

    /// <summary>Id do usuário a inserir (alternativa a email).</summary>
    public Guid? UserId { get; set; }
}
