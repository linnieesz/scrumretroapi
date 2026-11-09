namespace ScrumRetroApi.Models;

public class Sprint
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = null!;
    public int Number { get; set; } // sequencial por projeto (1, 2, 3...)
    public SprintStatus Status { get; set; } = SprintStatus.Open;

    /// <summary>Data de início planejada (dia útil). Depois de uma sprint encerrada, é o próximo dia útil.</summary>
    public DateOnly StartDate { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public List<Card> Cards { get; set; } = new();
}

public class Card
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid SprintId { get; set; }
    public Sprint Sprint { get; set; } = null!;

    // Guardado apenas para "meus cards". NUNCA deve ser exposto no quadro ou em sugestões.
    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public CeremonyType Ceremony { get; set; }
    public CardType Type { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public SuggestionTracking? Tracking { get; set; }
}

public class SuggestionTracking
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CardId { get; set; }
    public Card Card { get; set; } = null!;
    public Guid ProjectId { get; set; }

    // Eixo 1: a sugestão foi tomada (implementada)? false = "não foi tomada".
    public bool IsTaken { get; set; }

    // Eixo 2 (independente): foi útil? true = "foi útil", false = "foi inútil", null = ainda não avaliada.
    public bool? IsUseful { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedById { get; set; }
}
