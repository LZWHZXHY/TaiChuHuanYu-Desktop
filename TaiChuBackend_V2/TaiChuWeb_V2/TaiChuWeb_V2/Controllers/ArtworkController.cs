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
                    AuthorId = a.UploaderId,
                    AuthorAvatar = a.Uploader.Profile != null ? a.Uploader.Profile.Avatar : null,
                    ImageCount = a.Images.Count,

                    // 填入你迁移过来的统计数据
                    ViewCount = a.ViewCount
                })
                .ToListAsync();

            var ids = artworks.Select(a => a.Id).ToArray();
            var stats = await _engagement.Stats(CurrentUserId, ids).ToDictionaryAsync(a => a.Id);
            foreach (var item in artworks) item.SetStats(stats[item.Id]);

            return Ok(new
            {
                Total = total,
                Data = artworks,
                HasMore = offset + limit < total // 核心：告诉前端后面还有没有数据
            });
        }


        /// <summary>我收藏的作品（按收藏时间倒序）。</summary>
        [HttpGet("favorites")]
        [Authorize]
        public async Task<IActionResult> MyFavorites([FromQuery] int offset = 0, [FromQuery] int limit = 20)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            offset = Math.Max(0, offset);
            limit = Math.Clamp(limit, 1, 50);
            var uid = CurrentUserId.Value;

            var favQuery = _context.UserInteractions
                .Where(i => i.UserId == uid && i.TargetType == "Artwork" && i.ActionType == "Favorite");
            var total = await favQuery.CountAsync();

            var favs = await favQuery.OrderByDescending(i => i.CreatedAt)
                .Skip(offset).Take(limit)
                .Select(i => i.TargetId)
                .ToListAsync();

            var ids = favs.Select(t => int.TryParse(t, out var n) ? n : 0).Where(n => n > 0).ToList();
            var artworks = await _context.Artworks.AsNoTracking()
                .Where(a => ids.Contains(a.Id) && a.IsApproved && (a.Status == "published" || a.Status == "" || a.Status == null))
                .Select(a => new ArtworkItemDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    UploadAt = a.UploadAt,
                    CoverImageUrl = a.Images.Where(i => i.IsCover).Select(i => i.ImageUrl).FirstOrDefault()
                                     ?? a.Images.OrderBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault(),
                    AuthorName = a.Uploader.Username,
                    AuthorId = a.UploaderId,
                    AuthorAvatar = a.Uploader.Profile != null ? a.Uploader.Profile.Avatar : null,
                    ImageCount = a.Images.Count,
                    ViewCount = a.ViewCount
                })
                .ToListAsync();

            var map = artworks.ToDictionary(a => a.Id);
            var ordered = ids.Where(id => map.ContainsKey(id)).Select(id => map[id]).ToList();
            if (ordered.Count > 0)
            {
                var stats = await _engagement.Stats(uid, ordered.Select(a => a.Id).ToArray()).ToDictionaryAsync(s => s.Id);
                foreach (var a in ordered) a.SetStats(stats[a.Id]);
            }

            return Ok(new { Total = total, Data = ordered, HasMore = offset + favs.Count < total });
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
                OwnerState = artwork.UploaderId == CurrentUserId ? OwnerStateOf(artwork.IsApproved, artwork.Status) : null,
                UploadAt = artwork.UploadAt,
                Author = new AuthorDto
                {
                    Id = artwork.UploaderId,
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

        public sealed class VisibilityDto { public bool Visible { get; set; } }

        /// <summary>作品现在对作者来说是什么状态</summary>
        private static string OwnerStateOf(bool approved, string? status)
        {
            var published = string.IsNullOrEmpty(status) || status == "published";
            if (published) return approved ? "public" : "withdrawn";
            return status!; // reviewing / hidden / rejected……（管理员设的）
        }

        /// <summary>
        /// 作者自己下架 / 重新上架作品。下架后画廊、排行、搜索、主页里都看不到，只有作者自己能打开；评论和点赞保留。
        /// 被管理员隐藏或还在审核的作品不能自己上架。从灵脉发布的作品会同步原稿的公开状态。
        /// </summary>
        [Authorize, HttpPost("{id:int}/visibility")]
        public async Task<IActionResult> SetVisibility(int id, [FromBody] VisibilityDto dto)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var artwork = await _context.Artworks.FirstOrDefaultAsync(a => a.Id == id);
            if (artwork == null) return NotFound(new { message = "作品不存在" });
            if (artwork.UploaderId != userId) return StatusCode(403, new { message = "只能下架自己的作品" });
            var state = OwnerStateOf(artwork.IsApproved, artwork.Status);
            if (state is not ("public" or "withdrawn")) return BadRequest(new { message = "这件作品正在审核或被管理员处理，暂时不能自己上架 / 下架" });
            var note = artwork.OriginalNoteId is Guid noteId ? await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId) : null;
            if (dto.Visible && note?.Status == 2) return BadRequest(new { message = "灵脉原稿已被下架，不能重新上架" });
            artwork.IsApproved = dto.Visible;
            if (note != null) { note.IsPublic = dto.Visible; note.UpdatedAt = DateTime.UtcNow; }
            await _context.SaveChangesAsync();
            return Ok(new { ownerState = OwnerStateOf(artwork.IsApproved, artwork.Status), message = dto.Visible ? "作品已重新上架" : "作品已下架，只有你自己能看到" });
        }

        /// <summary>
        /// 彻底删除作品（作者本人，或有画廊管理权限的管理员）。删除后不能恢复，评论、点赞收藏一起删除；灵脉原稿保留。
        /// </summary>
        [Authorize, HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id, [FromServices] TaiChuWeb_V2.Services.Cos.CosService cos, [FromServices] ILogger<ArtworkController> logger)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var uploader = await _context.Artworks.AsNoTracking().Where(a => a.Id == id).Select(a => (Guid?)a.UploaderId).FirstOrDefaultAsync();
            if (uploader == null) return NotFound(new { message = "作品不存在或已经删除" });
            var isOwner = uploader == userId;
            if (!isOwner && !await ArtworkPublicationService.CanManageAsync(_context, userId)) return StatusCode(403, new { message = "只能删除自己的作品" });
            var result = await ArtworkDeletion.PurgeAsync(_context, cos, logger, id, userId, byAdmin: !isOwner);
            if (result == null) return NotFound(new { message = "作品不存在或已经删除" });
            return Ok(new { message = $"作品《{result.Title}》已彻底删除" });
        }

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