using System.ComponentModel.DataAnnotations;
using ScrumRetroApi.Models;

namespace ScrumRetroApi.DTOs;

public sealed record SprintDto(
    Guid Id,
    Guid ProjectId,
    int Number,
    SprintStatus Status,
    DateOnly StartDate,
    DateTime CreatedAt,
    DateTime? ClosedAt);

/// <summary>Card do próprio autor ("meus cards"). Não há autoria em nenhum outro DTO.</summary>
public sealed record CardDto(
    Guid Id,
    Guid SprintId,
    CeremonyType Ceremony,
    CardType Type,
    string Content,
    DateTime CreatedAt)
{
    public static CardDto From(Card c) => new(c.Id, c.SprintId, c.Ceremony, c.Type, c.Content, c.CreatedAt);
}

/// <summary>Card do quadro: anônimo e irrastreável (sem autor e sem data/hora).</summary>
public sealed record BoardCardDto(Guid Id, CeremonyType Ceremony, CardType Type, string Content);

public sealed record BoardDto(
    Guid SprintId,
    int SprintNumber,
    SprintStatus SprintStatus,
    CeremonyType? Ceremony,
    int TotalCards,
    IReadOnlyList<BoardCardDto> Cards);

public sealed record SuggestionTrackingDto(
    Guid TrackingId,
    string Content,
    int OriginSprintNumber,
    bool IsTaken,
    bool? IsUseful,
    DateTime UpdatedAt);

public class CreateCardRequest
{
    [Required(ErrorMessage = "A cerimônia é obrigatória (Review ou Retrospective).")]
    public CeremonyType? Ceremony { get; set; }

    [Required(ErrorMessage = "O tipo do card é obrigatório.")]
    public CardType? Type { get; set; }

    [Required(ErrorMessage = "O conteúdo do card é obrigatório.")]
    [StringLength(1000, ErrorMessage = "O conteúdo do card deve ter no máximo 1000 caracteres.")]
    public string Content { get; set; } = string.Empty;
}

public class UpdateCardRequest
{
    [Required(ErrorMessage = "O conteúdo do card é obrigatório.")]
    [StringLength(1000, ErrorMessage = "O conteúdo do card deve ter no máximo 1000 caracteres.")]
    public string Content { get; set; } = string.Empty;
}

public class UpdateSuggestionStatusRequest
{
    /// <summary>true = "foi tomada"; false = "não foi tomada". Omitir mantém o valor atual.</summary>
    public bool? IsTaken { get; set; }

    /// <summary>true = "foi útil"; false = "foi inútil". Omitir mantém o valor atual.</summary>
    public bool? IsUseful { get; set; }
}
