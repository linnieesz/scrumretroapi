namespace ScrumRetroApi.Models;

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty; // sempre em minúsculas
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? Phone { get; set; }
    public string? Bio { get; set; }
    public UserRole Role { get; set; } = UserRole.Member;

    /// <summary>Empresa do usuário. Obrigatória para Manager e Member; opcional para Admin.</summary>
    public Guid? CompanyId { get; set; }
    public Company? Company { get; set; }

    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DeletedAt { get; set; }
}
