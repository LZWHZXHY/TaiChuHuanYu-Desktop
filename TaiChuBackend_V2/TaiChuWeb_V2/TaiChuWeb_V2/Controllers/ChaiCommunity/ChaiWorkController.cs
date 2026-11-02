using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Chai.stickman;
using TaiChuWeb_V2.Models.ChaiCommunity;
using TaiChuWeb_V2.Models.Interact;

namespace TaiChuWeb_V2.Controllers.ChaiCommunity
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChaiWorkController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ChaiWorkController(AppDbContext context) { _context = context; }

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        // ============================================================
        // 列表（公开，只显示已发布作品）
        // ============================================================
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<ChaiWorkListResponse>> GetList(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            [FromQuery] string? keyword = null,
            [FromQuery] string? tag = null,
            [FromQuery] string? sort = "latest")
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 12;

            var query = _context.ChaiWorks.Where(w => w.Status == "published");

            if (!string.IsNullOrWhiteSpace(keyword))
                query = query.Where(w => w.Title.Contains(keyword) || (w.Description != null && w.Description.Contains(keyword)));

            if (!string.IsNullOrWhiteSpace(tag))
                query = query.Where(w => w.Tags.Contains(tag));

            query = sort switch
            {
                "hot" => query.OrderByDescending(w => w.Views),
                _ => query.OrderByDescending(w => w.CreatedAt)
            };

            var total = await query.CountAsync();
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(w => new ChaiWorkBriefDto
                {
                    Id = w.Id,
                    Title = w.Title,
                    CoverUrl = w.CoverUrl,
                    MediaType = w.MediaType,
                    AuthorName = w.AuthorName,
                    AuthorId = w.AuthorId,
                    Status = w.Status,
                    Views = w.Views,
                    TagsText = w.Tags,
                    CreatedAt = w.CreatedAt
                })
                .ToListAsync();

            // 点赞 / 收藏 / 评论数单独查再对上：以前在 SQL 里拿作品 Id 转字符串去和评论表比较，
            // 两张表排序规则不同时整个作品列表报错
            var workIds = items.Select(x => x.Id.ToString()).ToList();
            if (workIds.Count > 0)
            {
                var interactions = await _context.UserInteractions
                    .Where(i => i.TargetType == "ChaiWork" && workIds.Contains(i.TargetId) && (i.ActionType == "Like" || i.ActionType == "Favorite"))
                    .Select(i => new { i.TargetId, i.ActionType })
                    .ToListAsync();
                var comments = await _context.Comments
                    .Where(c => c.TargetType == "work" && workIds.Contains(c.TargetId) && c.Content != "")
                    .Select(c => c.TargetId)
                    .ToListAsync();
                foreach (var item in items)
                {
                    var key = item.Id.ToString();
                    item.LikeCount = interactions.Count(i => string.Equals(i.TargetId, key, StringComparison.OrdinalIgnoreCase) && i.ActionType == "Like");
                    item.FavoriteCount = interactions.Count(i => string.Equals(i.TargetId, key, StringComparison.OrdinalIgnoreCase) && i.ActionType == "Favorite");
                    item.CommentCount = comments.Count(c => string.Equals(c, key, StringComparison.OrdinalIgnoreCase));
                }
            }

            foreach (var item in items)
                item.Tags = Deserialize<List<string>>(item.TagsText ?? "[]");

            return Ok(new ChaiWorkListResponse { Items = items, Total = total, Page = page, PageSize = pageSize });
        }

        // ============================================================
        // 详情（草稿仅作者可见）
        // ============================================================
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<ActionResult<ChaiWorkDto>> GetDetail(Guid id)
        {
            var work = await _context.ChaiWorks.FirstOrDefaultAsync(w => w.Id == id);
            if (work == null) return NotFound(new { message = "作品不存在" });

            if (work.Status == "draft" && work.AuthorId != CurrentUserId)
                return NotFound(new { message = "作品不存在" });

            if (work.Status == "published")
            {
                work.Views += 1;
                await _context.SaveChangesAsync();
            }

            return Ok(ToDto(work, true));
        }

        // ============================================================
        // 创建
        // ============================================================
        [HttpPost]
        public async Task<ActionResult<ChaiWorkDto>> Create([FromBody] CreateChaiWorkRequest req)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Title) || string.IsNullOrWhiteSpace(req.MediaUrl))
                return BadRequest(new { message = "标题和媒体地址不能为空" });

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            var work = new ChaiWork
            {
                Title = req.Title.Trim(),
                Description = req.Description?.Trim(),
                MediaUrl = req.MediaUrl,
                CoverUrl = req.CoverUrl,
                MediaType = string.IsNullOrWhiteSpace(req.MediaType) ? "video" : req.MediaType,
                AuthorId = CurrentUserId.Value,
                AuthorName = user?.Username ?? "柴圈创作者",
                Status = req.Status == "published" ? "published" : "draft",
                Tags = JsonSerializer.Serialize(req.Tags ?? new List<string>()),
                RelatedCharacterIds = JsonSerializer.Serialize(req.RelatedCharacterIds ?? new List<Guid>()),
                RelatedBattleId = req.RelatedBattleId,
                RelatedJointId = req.RelatedJointId,
                CreatedAt = DateTime.UtcNow
            };

            _context.ChaiWorks.Add(work);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetDetail), new { id = work.Id }, ToDto(work, false));
        }

        // ============================================================
        // 更新
        // ============================================================
        [HttpPut("{id:guid}")]
        public async Task<ActionResult<ChaiWorkDto>> Update(Guid id, [FromBody] UpdateChaiWorkRequest req)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            var work = await _context.ChaiWorks.FirstOrDefaultAsync(w => w.Id == id);
            if (work == null) return NotFound(new { message = "作品不存在" });
            if (work.AuthorId != CurrentUserId.Value) return Forbid();

            if (req.Title != null) work.Title = req.Title.Trim();
            if (req.Description != null) work.Description = req.Description.Trim();
            if (req.MediaUrl != null) work.MediaUrl = req.MediaUrl;
            if (req.CoverUrl != null) work.CoverUrl = req.CoverUrl;
            if (req.MediaType != null) work.MediaType = req.MediaType;
            if (req.Status != null) work.Status = req.Status;
            if (req.Tags != null) work.Tags = JsonSerializer.Serialize(req.Tags);
            if (req.RelatedCharacterIds != null) work.RelatedCharacterIds = JsonSerializer.Serialize(req.RelatedCharacterIds);
            if (req.RelatedBattleId != null) work.RelatedBattleId = req.RelatedBattleId;
            if (req.RelatedJointId != null) work.RelatedJointId = req.RelatedJointId;
            work.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(ToDto(work, false));
        }

        // ============================================================
        // 删除
        // ============================================================
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            var work = await _context.ChaiWorks.FirstOrDefaultAsync(w => w.Id == id);
            if (work == null) return NotFound(new { message = "作品不存在" });
            if (work.AuthorId != CurrentUserId.Value) return Forbid();

            _context.ChaiWorks.Remove(work);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ============================================================
        // 点赞 / 收藏（复用 UserInteraction，同一动作再点一次取消）
        // ============================================================
        [HttpPost("{id:guid}/{kind}")]
        public async Task<IActionResult> ToggleInteraction(Guid id, string kind)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            if (kind is not ("like" or "favorite")) return BadRequest(new { message = "不支持的操作" });

            var work = await _context.ChaiWorks.FirstOrDefaultAsync(w => w.Id == id && w.Status == "published");
            if (work == null) return NotFound(new { message = "作品不存在或未公开" });

            var actionType = kind == "like" ? "Like" : "Favorite";
            var target = id.ToString();
            var existing = await _context.UserInteractions
                .Where(i => i.UserId == CurrentUserId.Value && i.TargetType == "ChaiWork" && i.TargetId == target && i.ActionType == actionType)
                .FirstOrDefaultAsync();

            bool active;
            if (existing != null)
            {
                _context.UserInteractions.Remove(existing);
                active = false;
            }
            else
            {
                _context.UserInteractions.Add(new UserInteraction { UserId = CurrentUserId.Value, TargetId = target, TargetType = "ChaiWork", ActionType = actionType });
                active = true;
            }

            await _context.SaveChangesAsync();
            var count = await _context.UserInteractions.CountAsync(i => i.TargetType == "ChaiWork" && i.TargetId == target && i.ActionType == actionType);
            return Ok(new { active, count });
        }

        // ============================================================
        // DTO 转换
        // ============================================================
        private ChaiWorkDto ToDto(ChaiWork w, bool includeCounts)
        {
            return new ChaiWorkDto
            {
                Id = w.Id,
                Title = w.Title,
                CoverUrl = w.CoverUrl,
                MediaType = w.MediaType,
                AuthorName = w.AuthorName,
                AuthorId = w.AuthorId,
                Status = w.Status,
                Views = w.Views,
                LikeCount = includeCounts ? _context.UserInteractions.Count(i => i.TargetType == "ChaiWork" && i.TargetId == w.Id.ToString() && i.ActionType == "Like") : 0,
                FavoriteCount = includeCounts ? _context.UserInteractions.Count(i => i.TargetType == "ChaiWork" && i.TargetId == w.Id.ToString() && i.ActionType == "Favorite") : 0,
                CommentCount = _context.Comments.Count(c => c.TargetType == "work" && c.TargetId == w.Id.ToString() && c.Content != ""),
                CreatedAt = w.CreatedAt,
                Description = w.Description,
                MediaUrl = w.MediaUrl,
                Tags = Deserialize<List<string>>(w.Tags),
                RelatedCharacterIds = Deserialize<List<Guid>>(w.RelatedCharacterIds),
                RelatedBattleId = w.RelatedBattleId,
                RelatedJointId = w.RelatedJointId,
                UpdatedAt = w.UpdatedAt
            };
        }

        private static T Deserialize<T>(string json) where T : new()
        {
            try { return JsonSerializer.Deserialize<T>(json) ?? new T(); }
            catch { return new T(); }
        }
    }
}
