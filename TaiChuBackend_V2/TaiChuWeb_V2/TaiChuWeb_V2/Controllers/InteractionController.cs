using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaiChuWeb_V2.Services.Artworks;

namespace TaiChuWeb_V2.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class InteractionController(ArtworkEngagementService service) : ControllerBase
{
    [HttpPost("toggle-action")]
    public async Task<IActionResult> ToggleAction([FromQuery] string targetId, [FromQuery] string targetType,
        [FromQuery] string actionType, [FromQuery] bool? isActive = null)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (targetType != "Artwork" || !int.TryParse(targetId, out var id) || id <= 0)
            return BadRequest(new { message = "不支持的作品或目标类型" });
        try { return Ok(await service.SetInteractionAsync(id, userId, actionType, isActive)); }
        catch (ArtworkRequestException ex) { return StatusCode(ex.Status, new { message = ex.Message }); }
    }
}
