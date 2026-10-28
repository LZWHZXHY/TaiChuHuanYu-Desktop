using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World;

[Route("api/world/projects/{projectId}/cards")]
[Authorize]
public class WorldCardsController(IWorldCardService cardService) : WorldControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetCards(Guid projectId, [FromQuery] string? type = null) =>
        Ok(await cardService.GetCardSummariesByProjectAsync(projectId, ReaderUserId, type));

    [HttpGet("references")]
    [AllowAnonymous]
    public async Task<IActionResult> GetReferences(Guid projectId) =>
        Ok(await cardService.GetReferencesAsync(projectId, ReaderUserId));

    [HttpPost("batch")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBatch(Guid projectId, [FromBody] Guid[] ids)
    {
        if (ids.Length > 100) return BadRequest(new { message = "每批最多读取 100 个条目" });
        return Ok(await cardService.GetCardsByIdsAsync(projectId, ids, ReaderUserId));
    }

    [HttpGet("timeline")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTimeline(Guid projectId) =>
        Ok(await cardService.GetTimelineAsync(projectId, ReaderUserId));

    [HttpGet("{cardId}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCard(Guid projectId, Guid cardId)
    {
        var card = await cardService.GetCardByIdAsync(cardId, ReaderUserId, projectId);
        return card == null ? NotFound(new { message = "卡片不存在或无权访问" }) : Ok(card);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCard(Guid projectId, [FromBody] CreateCardDto dto)
    {
        var card = await cardService.CreateCardAsync(projectId, CurrentUserId, dto);
        return CreatedAtAction(nameof(GetCard), new { projectId, cardId = card.Id }, card);
    }

    [HttpPut("{cardId}")]
    public async Task<IActionResult> UpdateCard(Guid projectId, Guid cardId, [FromBody] UpdateCardDto dto)
    {
        var card = await cardService.UpdateCardAsync(cardId, CurrentUserId, dto, projectId);
        return card == null ? NotFound(new { message = "卡片不存在或无权修改" }) : Ok(card);
    }

    [HttpDelete("{cardId}")]
    public async Task<IActionResult> DeleteCard(Guid projectId, Guid cardId)
    {
        var deleted = await cardService.DeleteCardAsync(cardId, CurrentUserId, projectId);
        return deleted ? NoContent() : NotFound(new { message = "卡片不存在或无权删除" });
    }

    [HttpGet("{cardId}/backlinks")]
    [AllowAnonymous]
    public async Task<IActionResult> GetBacklinks(Guid projectId, Guid cardId)
        => Ok(await cardService.GetBacklinksAsync(cardId, ReaderUserId, projectId));
}
