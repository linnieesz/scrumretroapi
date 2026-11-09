namespace ScrumRetroApi.Models;

/// <summary>Empresa (tenant lógico): agrupa usuários e delimita onde um Manager pode buscar membros.</summary>
public class Company
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Nome (fantasia) exibido nas telas.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Razão social (opcional).</summary>
    public string? LegalName { get; set; }

    /// <summary>CNPJ normalizado: 14 caracteres, sem pontuação (aceita o formato alfanumérico), único.</summary>
    public string Cnpj { get; set; } = string.Empty;

    /// <summary>Empresa inativa deixa de aparecer no cadastro e não aceita novos usuários.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Membros da empresa (usuários vinculados).</summary>
    public List<User> Members { get; set; } = new();
}
