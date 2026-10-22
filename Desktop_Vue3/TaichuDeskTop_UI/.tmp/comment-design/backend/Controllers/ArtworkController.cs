using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using TaiChuWeb_V2.Services.Artworks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Artwork;
using TaiChuWeb_V2.DTOs.Artwork;

namespace TaiChuWeb_V2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ArtworkController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly ArtworkEngagementService _engagement;
        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        public ArtworkController(AppDbContext context, ArtworkEngagementService engagement)
        {
            _context = context;
            _engagement = engagement;
        }

        [HttpGet]
        public async Task<IActionResult> GetGallery([FromQuery] int offset = 0, [FromQuery] int limit = 20, [FromQuery] string? search = null, [FromQuery] string? rankPeriod = null)
        {
            // 基础校验：单次加载最多允许 50 张，防止被爬虫暴力抓取
            offset = Math.Max(0, offset);
            limit = Math.Clamp(limit, 1, 50);

            // 1. 构建查询基准（不立即执行）
            var query = _context.Artworks
                .AsNoTracking()
                .Where(a => a.IsApproved && (a.Status == "published" || a.Status == "" || a.Status == null)); // 只展示审核通过的

            if (search?.Length > 100) return BadRequest(new { message = "搜索词不能超过 100 字" });
            if (!string.IsNullOrWhiteSpace(search)) {
                var term = search.Trim();
                query = query.Where(a => a.Title.Contains(term) || (a.Description != null && a.Description.Contains(term)) || a.Uploader.Username.Contains(term) || _context.TagAssignments.Any(t => t.EntityType == "Artwork" && t.EntityId == a.Id.ToString() && t.Tag != null && t.Tag.Name.Contains(term)));
            }
            if (rankPeriod != null && rankPeriod is not ("all" or "day" or "week" or "month" or "year")) return BadRequest(new { message = "不支持的榜单周期" });
            var since = rankPeriod switch { "day" => DateTime.UtcNow.AddDays(-1), "week" => DateTime.UtcNow.AddDays(-7), "month" => DateTime.UtcNow.AddMonths(-1), "year" => DateTime.UtcNow.AddYears(-1), _ => DateTime.MinValue };
            var likes = _context.UserInteractions.Where(i => i.TargetType == "Artwork" && i.ActionType == "Like" && i.CreatedAt >= since);
            if (rankPeriod != null && rankPeriod != "all") query = query.Where(a => likes.Any(i => i.TargetId == a.Id.ToString()));
            var ordered = rankPeriod == null ? query.OrderByDescending(a => a.UploadAt).ThenByDescending(a => a.Id)
                : query.OrderByDescending(a => likes.Where(i => i.TargetId == a.Id.ToString()).Select(i => i.UserId).Distinct().Count()).ThenByDescending(a => a.UploadAt).ThenByDescending(a => a.Id);

            // 2. 获取总数（用于前端判断是否到底）
            var total = await query.CountAsync();

            // 3. 分页查询并转换 DTO
            var artworks = await ordered
                .Skip(offset) // 跳过前面的
                .Take(limit) // 取当前的
                .AsSplitQuery()
                .Select(a => new ArtworkItemDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    RankingLikes = rankPeriod == null ? null : likes.Where(i => i.TargetId == a.Id.ToString()).Select(i => i.UserId).Distinct().Count(),
                    UploadAt = a.UploadAt,
                    CoverImageUrl = a.Images.Where(i => i.IsCover).Select(i => i.ImageUrl).FirstOrDefault()
                                    ?? a.Images.OrderBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault(),
                    AuthorName = a.Uploader.Username,
                    AuthorAvatar = a.Uploader.Profile != null ? a.Uploader.Profile.Avatar : null,
                    ImageCount = a.Images.Count,

                    // 填入你迁移过来的统计数据
                    ViewCount = a.ViewCount
                })
                .ToListAsync();

            var ids = artworks.Select(a => a.Id).ToArray();
            var stats = await _engagement.Stats(CurrentUserId).Where(a => Enumerable.Contains(ids, a.Id)).ToDictionaryAsync(a => a.Id);
            foreach (var item in artworks) item.SetStats(stats[item.Id]);

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

            if (artwork == null || (!(artwork.IsApproved && (artwork.Status == "published" || artwork.Status == "" || artwork.Status == null)) &&
                artwork.UploaderId != CurrentUserId && !await ArtworkPublicationService.CanManageAsync(_context, CurrentUserId)))
            {
                return NotFound(new { message = "作品跑丢了~" });
            }

            var dto = new ArtworkDetailDto
            {
                Id = artwork.Id,
                Title = artwork.Title,
                Description = artwork.Description,
                CanInteract = artwork.IsApproved && (artwork.Status == "published" || artwork.Status == "" || artwork.Status == null),
                CanEdit = artwork.UploaderId == CurrentUserId,
                OriginalNoteId = artwork.UploaderId == CurrentUserId ? artwork.OriginalNoteId : null,
                UploadAt = artwork.UploadAt,
                Author = new AuthorDto
                {
                    Username = artwork.Uploader.Username,
                    Avatar = artwork.Uploader.Profile?.Avatar,
                    Bio = artwork.Uploader.Profile?.Bio
                },
                Images = artwork.Images.OrderBy(img => img.Id).Select(img => new ArtworkImageDto
                {
                    Url = img.ImageUrl,
                    IsCover = img.IsCover,
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

            dto.Tags = await _context.TagAssignments.Where(t => t.EntityType == "Artwork" && t.EntityId == id.ToString() && t.Tag != null).Select(t => t.Tag!.Name).ToListAsync();
            dto.SetStats(await _engagement.Stats(CurrentUserId).SingleAsync(a => a.Id == id));
            return Ok(dto);
        }

        [Authorize, HttpPut("{id:int}")]
        public Task<IActionResult> Edit(int id, [FromBody] ArtworkEditDto dto, [FromServices] ArtworkPublicationService publication)
            => CurrentUserId is Guid userId ? CommentResult(() => publication.EditAsync(id, userId, dto))
                : Task.FromResult<IActionResult>(Unauthorized());

        [HttpGet("{id:int}/comments")]
        public Task<IActionResult> GetComments(int id, [FromQuery] int offset = 0, [FromQuery] int limit = 20)
            => CommentResult(() => _engagement.GetCommentsAsync(id, CurrentUserId, offset, limit));

        [Authorize, HttpPost("{id:int}/comments")]
        public Task<IActionResult> AddComment(int id, [FromBody] CreateArtworkCommentDto dto)
            => CurrentUserId is Guid userId ? CommentResult(() => _engagement.AddCommentAsync(id, userId, dto))
                : Task.FromResult<IActionResult>(Unauthorized());

        [Authorize, HttpDelete("{id:int}/comments/{commentId:guid}")]
        public Task<IActionResult> DeleteComment(int id, Guid commentId)
            => CurrentUserId is Guid userId ? CommentResult(() => _engagement.DeleteCommentAsync(id, commentId, userId))
                : Task.FromResult<IActionResult>(Unauthorized());

        private async Task<IActionResult> CommentResult(Func<Task<object>> action)
        {
            try { return Ok(await action()); }
            catch (ArtworkRequestException ex) { return StatusCode(ex.Status, new { message = ex.Message }); }
        }
    }
}