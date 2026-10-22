using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Services.Artworks;

namespace TaiChuWeb_V2.Services.Publish;

public class ArtPublishHandler(ArtworkPublicationService service, ILogger<ArtPublishHandler> logger) : ILingMaiPublishHandler
{
    public string SupportType => "art";
    public async Task<IActionResult> ExecutePublishAsync(Guid noteId, string userId, int? categoryId, string? projectId = null)
    {
        if (!Guid.TryParse(userId, out var id)) return new UnauthorizedResult();
        try { return new OkObjectResult(await service.PublishNoteAsync(noteId, id)); }
        catch (ArtworkRequestException ex) { return new ObjectResult(new { message = ex.Message }) { StatusCode = ex.Status }; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Artwork publication failed for {NoteId}", noteId);
            return new ObjectResult(new { message = "作品发布失败，请检查图片与水印后重试" }) { StatusCode = 500 };
        }
    }
}
