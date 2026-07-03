using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Artwork;
using TaiChuWeb_V2.Models.Artwork;

namespace TaiChuWeb_V2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArtworkController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ArtworkController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetGallery([FromQuery] int offset = 0, [FromQuery] int limit = 20)
        {
            // 基础校验：单次加载最多允许 50 张，防止被爬虫暴力抓取
            if (limit > 50) limit = 50;

            // 1. 构建查询基准（不立即执行）
            var query = _context.Artworks
                .AsNoTracking()
                .Where(a => a.IsApproved); // 只展示审核通过的

            // 2. 获取总数（用于前端判断是否到底）
            var total = await query.CountAsync();

            // 3. 分页查询并转换 DTO
            var artworks = await query
                .OrderByDescending(a => a.UploadAt)
                .Skip(offset) // 跳过前面的
                .Take(limit) // 取当前的
                .AsSplitQuery()
                .Select(a => new ArtworkItemDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    UploadAt = a.UploadAt,
                    CoverImageUrl = a.Images.Where(i => i.IsCover).Select(i => i.ImageUrl).FirstOrDefault()
                                    ?? a.Images.OrderBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault(),
                    AuthorName = a.Uploader.Username,
                    AuthorAvatar = a.Uploader.Profile != null ? a.Uploader.Profile.Avatar : null,
                    ImageCount = a.Images.Count,

                    // 填入你迁移过来的统计数据
                    LikesCount = a.LikesCount,
                    CommentsCount = a.CommentsCount,
                    ViewCount = a.ViewCount
                })
                .ToListAsync();

            return Ok(new
            {
                Total = total,
                Data = artworks,
                HasMore = offset + limit < total // 核心：告诉前端后面还有没有数据
            });
        }


        [HttpGet("{id}")]
        public async Task<ActionResult<ArtworkDetailDto>> GetArtworkDetail(int id)
        {
            var artwork = await _context.Artworks
                .Include(a => a.Uploader)
                    .ThenInclude(u => u.Profile)
                .Include(a => a.Images)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (artwork == null)
            {
                return NotFound(new { message = "作品跑丢了~" });
            }

            var dto = new ArtworkDetailDto
            {
                Id = artwork.Id,
                Title = artwork.Title,
                Description = artwork.Description,
                UploadAt = artwork.UploadAt,
                Author = new AuthorDto
                {
                    Username = artwork.Uploader.Username,
                    Avatar = artwork.Uploader.Profile?.Avatar,
                    Bio = artwork.Uploader.Profile?.Bio
                },
                Images = artwork.Images.Select(img => new ArtworkImageDto
                {
                    Url = img.ImageUrl,
                    Caption = img.Caption
                }).ToList(),

                // ========== 水印配置 ==========
                WatermarkType = artwork.WatermarkType ?? "text",
                WatermarkEnabled = artwork.WatermarkEnabled,
                WatermarkText = artwork.WatermarkText ?? "",
                WatermarkPosition = artwork.WatermarkPosition ?? "bottom-right",
                WatermarkFontSize = artwork.WatermarkFontSize,
                WatermarkOpacity = artwork.WatermarkOpacity,
                WatermarkColor = artwork.WatermarkColor ?? "#ffffff",
                WatermarkRotation = artwork.WatermarkRotation,
                WatermarkImageUrl = artwork.WatermarkImageUrl,
                WatermarkImageWidth = artwork.WatermarkImageWidth,
                WatermarkImageHeight = artwork.WatermarkImageHeight,
                WatermarkImageScale = artwork.WatermarkImageScale,
                WatermarkImageOpacity = artwork.WatermarkImageOpacity
            };

            return Ok(dto);
        }
    }
}