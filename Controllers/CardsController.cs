using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ScrumRetroApi.DTOs;
using ScrumRetroApi.Models;
using ScrumRetroApi.Services;

namespace ScrumRetroApi.Controllers;

[ApiController]
[Authorize]
public class CardsController(ICardService cards) : ControllerBase
{
    /// <summary>Cria um card (qualquer papel, desde que tenha acesso ao projeto). Sprint encerrada não aceita cards.</summary>
    [HttpPost("api/sprints/{sprintId:guid}/cards")]
    public async Task<ActionResult<CardDto>> Create(Guid sprintId, CreateCardRequest request, CancellationToken ct)
    {
        var created = await cards.CreateAsync(sprintId, request, ct);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    /// <summary>Somente os cards do próprio usuário na sprint.</summary>
    [HttpGet("api/sprints/{sprintId:guid}/cards/mine")]
    public async Task<ActionResult<IReadOnlyList<CardDto>>> Mine(
        Guid sprintId, [FromQuery] CeremonyType? ceremony, CancellationToken ct) =>
        Ok(await cards.MineAsync(sprintId, ceremony, ct));

    /// <summary>Quadro "ao vivo": todos os cards da sprint/cerimônia, anônimos e em ordem embaralhada.</summary>
    [HttpGet("api/sprints/{sprintId:guid}/board")]
    public async Task<ActionResult<BoardDto>> Board(
        Guid sprintId, [FromQuery] CeremonyType? ceremony, CancellationToken ct) =>
        Ok(await cards.BoardAsync(sprintId, ceremony, ct));

    /// <summary>Extra: o autor edita o texto do próprio card (sprint aberta).</summary>
    [HttpPut("api/cards/{cardId:guid}")]
    public async Task<ActionResult<CardDto>> Update(Guid cardId, UpdateCardRequest request, CancellationToken ct) =>
        Ok(await cards.UpdateAsync(cardId, request, ct));

    /// <summary>Extra: o autor exclui o próprio card (sprint aberta).</summary>
    [HttpDelete("api/cards/{cardId:guid}")]
    public async Task<IActionResult> Delete(Guid cardId, CancellationToken ct)
    {
        await cards.DeleteAsync(cardId, ct);
        return NoContent();
    }
}
