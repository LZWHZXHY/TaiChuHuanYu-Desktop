using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World;

[Route("api/world/cards/{cardId}/relations")]
[Authorize]
public class WorldRelationsController(IWorldRelationService relationService) : WorldControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetRelations(Guid cardId) =>
        Ok(await relationService.GetRelationsForCardAsync(cardId, ReaderUserId));

    [HttpPost]
    public async Task<IActionResult> CreateRelation(Guid cardId, [FromBody] CreateRelationDto dto)
    {
        var relation = await relationService.CreateRelationAsync(cardId, CurrentUserId, dto);
        return CreatedAtAction(nameof(GetRelations), new { cardId }, relation);
    }

    [HttpDelete("{relationId}")]
    public async Task<IActionResult> DeleteRelation(Guid cardId, Guid relationId)
    {
        var deleted = await relationService.DeleteRelationAsync(relationId, CurrentUserId, cardId);
        return deleted ? NoContent() : NotFound(new { message = "关联不存在或无权删除" });
    }

    [HttpGet("/api/world/projects/{projectId}/relations")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProjectRelations(Guid projectId) =>
        Ok(await relationService.GetRelationsForProjectAsync(projectId, ReaderUserId));
}
