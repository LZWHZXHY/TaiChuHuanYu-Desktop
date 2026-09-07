using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.DTOs.Artwork;
using TaiChuWeb_V2.Models.Artwork;
using TaiChuWeb_V2.Services.Cos; // 引入你的 CosService

namespace TaiChuWeb_V2.Controllers.Artworks
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NewArtworkController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly CosService _cosService; // 注入腾讯云 COS 服务

        public NewArtworkController(AppDbContext context, CosService cosService)
        {
            _context = context;
            _cosService = cosService;
        }

        [HttpPost("direct-upload")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> DirectUpload([FromForm] GalleryDirectUploadDto dto)
        {
            if (dto.Images == null || dto.Images.Count == 0)
            {
                return BadRequest(new { message = "请至少上传一张画作图片。" });
            }

            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var uploaderId))
            {
                return Unauthorized(new { message = "未检测到有效登录状态，请重新登录。" });
            }

            int validCoverIndex = (dto.CoverIndex >= 0 && dto.CoverIndex < dto.Images.Count)
                ? dto.CoverIndex
                : 0;

            try
            {
                // 1. 事务外完成 COS 上传（避免网络耗时阻塞数据库连接与事务重试）
                var uploadedImages = new List<ArtworkImage>();

                for (int i = 0; i < dto.Images.Count; i++)
                {
                    var file = dto.Images[i];
                    if (file.Length == 0) continue;

                    byte[] fileBytes;
                    using (var ms = new MemoryStream())
                    {
                        await file.CopyToAsync(ms);
                        fileBytes = ms.ToArray();
                    }

                    string extension = Path.GetExtension(file.FileName);
                    string rawName = Path.GetFileNameWithoutExtension(file.FileName);
                    string fileName = $"{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}-{rawName}{extension}";

                    // 上传至腾讯云 COS
                    string imageUrl = await _cosService.UploadAsync(fileBytes, fileName, "artwork/images"); 

            string? caption = (dto.Captions != null && i < dto.Captions.Count)
                ? dto.Captions[i]
                : null;

                    uploadedImages.Add(new ArtworkImage
                    {
                        ImageUrl = imageUrl,
                        Caption = caption,
                        IsCover = (i == validCoverIndex)
                    });
                }

                if (uploadedImages.Count == 0)
                {
                    return BadRequest(new { message = "上传的图片均为空文件，请重新选择。" });
                }

                if (!uploadedImages.Any(img => img.IsCover))
                {
                    uploadedImages.First().IsCover = true;
                }

                // 2. 组装实体
                var artwork = new Artwork
                {
                    Title = dto.Title.Trim(),
                    Description = dto.Description?.Trim(),
                    UploaderId = uploaderId,
                    OriginalNoteId = dto.OriginalNoteId,
                    Status = "published",
                    IsApproved = true,
                    UploadAt = DateTime.UtcNow,

                    WatermarkType = "text",
                    WatermarkEnabled = dto.WatermarkEnabled,
                    WatermarkText = dto.WatermarkText,
                    WatermarkPosition = dto.WatermarkPosition ?? "bottom-right",
                    WatermarkColor = dto.WatermarkColor ?? "#ffffff",
                    Images = uploadedImages
                };

                // 3. 使用 ExecutionStrategy 执行事务，解决 MySqlRetryingExecutionStrategy 报错
                var strategy = _context.Database.CreateExecutionStrategy();
                await strategy.ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();

                    _context.Artworks.Add(artwork);
                    await _context.SaveChangesAsync();

                    await transaction.CommitAsync();
                });

                return Ok(new
                {
                    success = true,
                    message = "画作发布成功！",
                    artworkId = artwork.Id,
                    imagesCount = artwork.Images.Count
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "发布失败，请稍后重试", detail = ex.Message });
            }
        }
    }
}