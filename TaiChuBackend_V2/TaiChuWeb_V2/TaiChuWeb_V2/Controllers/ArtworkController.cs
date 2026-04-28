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
        public async Task<ActionResult<IEnumerable<ArtworkItemDto>>> GetGallery()
        {
            // 1. 从数据库中查询作品，并预加载上传者及其个人资料，以及作品图片
            var artworks = await _context.Artworks
                .AsNoTracking() // 提升只读查询的性能
                .Include(a => a.Uploader)
                    .ThenInclude(u => u.Profile)
                .Include(a => a.Images)
                .OrderByDescending(a => a.UploadAt) // 按上传时间倒序
                .Select(a => new ArtworkItemDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    UploadAt = a.UploadAt,
                    // 逻辑：找一张标记为封面的图，如果没有，就拿 ID 最小的那张图
                    CoverImageUrl = a.Images.Where(i => i.IsCover).Select(i => i.ImageUrl).FirstOrDefault()
                                    ?? a.Images.OrderBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault(),
                    AuthorName = a.Uploader.Username,
                    // 访问 UserProfile 里的 Avatar 路径
                    AuthorAvatar = a.Uploader.Profile != null ? a.Uploader.Profile.Avatar : null,
                    ImageCount = a.Images.Count
                })
                .ToListAsync();

            return Ok(artworks);
        }


        [HttpGet("{id}")]
        public async Task<ActionResult> GetArtworkDetail(int id)
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

            return Ok(new
            {
                artwork.Id,
                artwork.Title,
                artwork.Description,
                artwork.UploadAt,
                Author = new
                {
                    artwork.Uploader.Username,
                    artwork.Uploader.Profile?.Avatar,
                    artwork.Uploader.Profile?.Bio
                },
                // 返回该作品下所有的图片 URL
                Images = artwork.Images.OrderByDescending(i => i.IsCover).Select(i => i.ImageUrl).ToList()
            });
        }
    }
}