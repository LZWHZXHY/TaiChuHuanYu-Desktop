using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaiChuWeb_V2.DTOs.Artwork;
using TaiChuWeb_V2.Services.Artworks;

namespace TaiChuWeb_V2.Controllers.Artworks;

[ApiController, Route("api/[controller]"), Authorize]
public class NewArtworkController(ArtworkPublicationService service, ILogger<NewArtworkController> logger) : ControllerBase
{
    [HttpPost("direct-upload"), Consumes("multipart/form-data")]
    public async Task<IActionResult> DirectUpload([FromForm] GalleryDirectUploadDto dto)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        try { return Ok(await service.UploadAsync(userId, dto)); }
        catch (ArtworkRequestException ex) { return StatusCode(ex.Status, new { message = ex.Message }); }
        catch (Exception ex)
        {
            logger.LogError(ex, "Artwork upload failed for {UserId}", userId);
            return StatusCode(500, new { message = "发布失败，请稍后重试；若启用水印，请确认图片可解码且水印有效" });
        }
    }
}
