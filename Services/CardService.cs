using Microsoft.EntityFrameworkCore;
using ScrumRetroApi.Common;
using ScrumRetroApi.Data;
using ScrumRetroApi.DTOs;
using ScrumRetroApi.Models;

namespace ScrumRetroApi.Services;

public interface ICardService
{
    Task<CardDto> CreateAsync(Guid sprintId, CreateCardRequest request, CancellationToken ct);
    Task<IReadOnlyList<CardDto>> MineAsync(Guid sprintId, CeremonyType? ceremony, CancellationToken ct);
    Task<BoardDto> BoardAsync(Guid sprintId, CeremonyType? ceremony, CancellationToken ct);
    Task<CardDto> UpdateAsync(Guid cardId, UpdateCardRequest request, CancellationToken ct);
    Task DeleteAsync(Guid cardId, CancellationToken ct);
}

public sealed class CardService(AppDbContext db, ICurrentUser current, IProjectAccessService access) : ICardService
{
    public async Task<CardDto> CreateAsync(Guid sprintId, CreateCardRequest request, CancellationToken ct)
    {
        var v = new ValidationBuilder();
        if (request.Ceremony is null || !Enum.IsDefined(request.Ceremony.Value))
            v.Add("ceremony", "A cerimônia é obrigatória (Review ou Retrospective).");
        if (request.Type is null || !Enum.IsDefined(request.Type.Value))
            v.Add("type", "O tipo do card é obrigatório.");
        InputRules.RequiredText(v, "content", request.Content, 1000,
            "O conteúdo do card é obrigatório.", "O conteúdo do card deve ter no máximo 1000 caracteres.");
        v.ThrowIfAny();

        var sprint = await access.GetSprintForViewAsync(sprintId, ct);
        if (sprint.Status == SprintStatus.Closed)
            throw new ConflictException("Não é possível criar cards em uma sprint encerrada.");

        var now = DateTime.UtcNow;
        var card = new Card
        {
            SprintId = sprint.Id,
            AuthorId = current.Id, // usado apenas em "meus cards"
            Ceremony = request.Ceremony!.Value,
            Type = request.Type!.Value,
            Content = request.Content.Trim(),
            CreatedAt = now
        };

        // Toda sugestão de melhoria gera um registro de acompanhamento para as próximas sprints.
        if (card.Type == CardType.ImprovementSuggestion)
        {
            card.Tracking = new SuggestionTracking
            {
                ProjectId = sprint.ProjectId,
                IsTaken = false,
                IsUseful = null,
                UpdatedAt = now
            };
        }

        db.Cards.Add(card);
        await db.SaveChangesAsync(ct);
        return CardDto.From(card);
    }

    public async Task<IReadOnlyList<CardDto>> MineAsync(Guid sprintId, CeremonyType? ceremony, CancellationToken ct)
    {
        var sprint = await access.GetSprintForViewAsync(sprintId, ct);
        var uid = current.Id;

        var query = db.Cards.AsNoTracking().Where(c => c.SprintId == sprint.Id && c.AuthorId == uid);
        if (ceremony.HasValue)
        {
            var cer = ceremony.Value;
            query = query.Where(c => c.Ceremony == cer);
        }

        var cards = await query.OrderBy(c => c.CreatedAt).ToListAsync(ct);
        return cards.Select(CardDto.From).ToList();
    }

    public async Task<BoardDto> BoardAsync(Guid sprintId, CeremonyType? ceremony, CancellationToken ct)
    {
        var sprint = await access.GetSprintForViewAsync(sprintId, ct);

        var query = db.Cards.AsNoTracking().Where(c => c.SprintId == sprint.Id);
        if (ceremony.HasValue)
        {
            var cer = ceremony.Value;
            query = query.Where(c => c.Ceremony == cer);
        }

        // A projeção NÃO inclui autor nem data/hora: nada que permita rastrear a autoria (nem para Admin/Manager).
        var items = await query
            .Select(c => new BoardCardDto(c.Id, c.Ceremony, c.Type, c.Content))
            .ToListAsync(ct);

        var shuffled = items.Shuffled(); // ordem diferente a cada consulta
        return new BoardDto(sprint.Id, sprint.Number, sprint.Status, ceremony, shuffled.Count, shuffled);
    }

    public async Task<CardDto> UpdateAsync(Guid cardId, UpdateCardRequest request, CancellationToken ct)
    {
        var v = new ValidationBuilder();
        InputRules.RequiredText(v, "content", request.Content, 1000,
            "O conteúdo do card é obrigatório.", "O conteúdo do card deve ter no máximo 1000 caracteres.");
        v.ThrowIfAny();

        var card = await FindOwnCardAsync(cardId, ct);
        card.Content = request.Content.Trim();
        await db.SaveChangesAsync(ct);
        return CardDto.From(card);
    }

    public async Task DeleteAsync(Guid cardId, CancellationToken ct)
    {
        var card = await FindOwnCardAsync(cardId, ct);
        db.Cards.Remove(card); // o acompanhamento da sugestão sai em cascata
        await db.SaveChangesAsync(ct);
    }

    private async Task<Card> FindOwnCardAsync(Guid cardId, CancellationToken ct)
    {
        var card = await db.Cards.FirstOrDefaultAsync(c => c.Id == cardId, ct)
                   ?? throw new NotFoundException("Card não encontrado.");

        if (card.AuthorId != current.Id)
            throw new ForbiddenException("Você só pode alterar ou excluir os seus próprios cards.");

        var sprint = await access.GetSprintForViewAsync(card.SprintId, ct);
        if (sprint.Status == SprintStatus.Closed)
            throw new ConflictException("Não é possível alterar cards de uma sprint encerrada.");

        return card;
    }
}
