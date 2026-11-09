using System.ComponentModel.DataAnnotations;
using ScrumRetroApi.Models;

namespace ScrumRetroApi.DTOs;

public sealed record UserDto(
    Guid Id,
    string FullName,
    string Email,
    string? Department,
    string? Phone,
    string? Bio,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt,
    Guid? CompanyId,
    string? CompanyName)
{
    /// <summary>Para exibir o nome da empresa, a entidade precisa ter sido carregada com Include(u => u.Company).</summary>
    public static UserDto From(User u) =>
        new(u.Id, u.FullName, u.Email, u.Department, u.Phone, u.Bio, u.Role, u.IsActive, u.CreatedAt,
            u.CompanyId, u.Company?.Name);
}

public sealed record RoleOptionDto(string Value, string Label, string Description, bool AllowedOnRegistration);

public sealed record AuthResponse(string Token, DateTime ExpiresAt, UserDto User);

public class LoginRequest
{
    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [StringLength(254, ErrorMessage = "O e-mail deve ter no máximo 254 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "A senha é obrigatória.")]
    [StringLength(100, ErrorMessage = "A senha deve ter no máximo 100 caracteres.")]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required(ErrorMessage = "O nome completo é obrigatório.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "O nome completo deve ter entre 3 e 150 caracteres.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "O e-mail profissional é obrigatório.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(254, ErrorMessage = "O e-mail deve ter no máximo 254 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "O departamento deve ter no máximo 100 caracteres.")]
    public string? Department { get; set; }

    [StringLength(30, ErrorMessage = "O telefone deve ter no máximo 30 caracteres.")]
    public string? Phone { get; set; }

    [StringLength(500, ErrorMessage = "A bio deve ter no máximo 500 caracteres.")]
    public string? Bio { get; set; }

    /// <summary>Member ou Manager (Admin não é aceito no cadastro público).</summary>
    [Required(ErrorMessage = "O tipo de acesso é obrigatório.")]
    public UserRole? Role { get; set; }

    /// <summary>Empresa escolhida no picker (GET /api/auth/companies).</summary>
    [Required(ErrorMessage = "A empresa é obrigatória.")]
    public Guid? CompanyId { get; set; }

    [Required(ErrorMessage = "A senha é obrigatória.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "A senha deve ter entre 8 e 100 caracteres.")]
    public string Password { get; set; } = string.Empty;
}

public class CreateUserRequest
{
    [Required(ErrorMessage = "O nome completo é obrigatório.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "O nome completo deve ter entre 3 e 150 caracteres.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(254, ErrorMessage = "O e-mail deve ter no máximo 254 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "O departamento deve ter no máximo 100 caracteres.")]
    public string? Department { get; set; }

    [StringLength(30, ErrorMessage = "O telefone deve ter no máximo 30 caracteres.")]
    public string? Phone { get; set; }

    [StringLength(500, ErrorMessage = "A bio deve ter no máximo 500 caracteres.")]
    public string? Bio { get; set; }

    [Required(ErrorMessage = "O tipo de acesso é obrigatório.")]
    public UserRole? Role { get; set; }

    /// <summary>Obrigatória para Manager e Member; opcional para Admin.</summary>
    public Guid? CompanyId { get; set; }

    [Required(ErrorMessage = "A senha é obrigatória.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "A senha deve ter entre 8 e 100 caracteres.")]
    public string Password { get; set; } = string.Empty;
}

public class UpdateUserRequest
{
    [Required(ErrorMessage = "O nome completo é obrigatório.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "O nome completo deve ter entre 3 e 150 caracteres.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(254, ErrorMessage = "O e-mail deve ter no máximo 254 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "O departamento deve ter no máximo 100 caracteres.")]
    public string? Department { get; set; }

    [StringLength(30, ErrorMessage = "O telefone deve ter no máximo 30 caracteres.")]
    public string? Phone { get; set; }

    [StringLength(500, ErrorMessage = "A bio deve ter no máximo 500 caracteres.")]
    public string? Bio { get; set; }

    [Required(ErrorMessage = "O tipo de acesso é obrigatório.")]
    public UserRole? Role { get; set; }

    /// <summary>Obrigatória para Manager e Member; opcional para Admin.</summary>
    public Guid? CompanyId { get; set; }

    [Required(ErrorMessage = "Informe se o usuário está ativo.")]
    public bool? IsActive { get; set; }
}

/// <summary>Edição do próprio perfil. Não permite alterar o tipo de acesso.</summary>
public class UpdateProfileRequest
{
    [Required(ErrorMessage = "O nome completo é obrigatório.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "O nome completo deve ter entre 3 e 150 caracteres.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "O e-mail é obrigatório.")]
    [EmailAddress(ErrorMessage = "Informe um e-mail válido.")]
    [StringLength(254, ErrorMessage = "O e-mail deve ter no máximo 254 caracteres.")]
    public string Email { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "O departamento deve ter no máximo 100 caracteres.")]
    public string? Department { get; set; }

    [StringLength(30, ErrorMessage = "O telefone deve ter no máximo 30 caracteres.")]
    public string? Phone { get; set; }

    [StringLength(500, ErrorMessage = "A bio deve ter no máximo 500 caracteres.")]
    public string? Bio { get; set; }
}

public class ChangePasswordRequest
{
    [Required(ErrorMessage = "A senha atual é obrigatória.")]
    [StringLength(100, ErrorMessage = "A senha atual deve ter no máximo 100 caracteres.")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "A nova senha é obrigatória.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "A nova senha deve ter entre 8 e 100 caracteres.")]
    public string NewPassword { get; set; } = string.Empty;
}
