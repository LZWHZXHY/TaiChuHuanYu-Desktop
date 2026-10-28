using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Models.Interact;
using TaiChuWeb_V2.Services.Notify;

namespace TaiChuWeb_V2.Controllers
{
    public class CreateChaiCommentRequest
    {
        public string TargetType { get; set; } = string.Empty;
        public string TargetId { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public Guid? ParentId { get; set; }
    }

    /// <summary>柴圈作品/OC 通用评论（复用 comments 表，TargetType + TargetId 多态）。</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChaiCommentController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ChaiCommentController(AppDbContext context) { _context = context; }

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        [HttpGet("{targetType}/{targetId}")]
        [AllowAnonymous]
        public async Task<IActionResult> List(string targetType, string targetId)
        {
            if (string.IsNullOrWhiteSpace(targetType) || string.IsNullOrWhiteSpace(targetId))
                return BadRequest(new { message = "目标无效" });

            var comments = await _context.Comments
                .Where(c => c.TargetType == targetType && c.TargetId == targetId)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new { c.Id, c.UserId, c.Content, c.ParentId, c.CreatedAt })
                .ToListAsync();

            var guids = comments
                .Select(c => Guid.TryParse(c.UserId, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty).Distinct().ToList();
            var users = await _context.Users.Where(u => guids.Contains(u.Id))
                .Select(u => new { u.Id, u.Username }).ToListAsync();
            var map = users.ToDictionary(u => u.Id.ToString(), u => u.Username);

            // 评论点赞：统计 + 我点过的
            var keys = comments.Select(c => c.Id.ToString()).ToList();
            var likeRows = await _context.UserInteractions
                .Where(i => i.TargetType == "Comment" && i.ActionType == "Like" && keys.Contains(i.TargetId))
                .Select(i => new { i.TargetId, i.UserId })
                .ToListAsync();
            var likeCounts = likeRows.GroupBy(r => r.TargetId).ToDictionary(g => g.Key, g => g.Count());
            var myLiked = CurrentUserId.HasValue
                ? likeRows.Where(r => r.UserId == CurrentUserId.Value).Select(r => r.TargetId).ToHashSet()
                : new HashSet<string>();

            var result = comments.Select(c => new
            {
                id = c.Id,
                userId = c.UserId,
                userName = map.TryGetValue(c.UserId, out var n) ? n : "已注销用户",
                content = c.Content,
                parentId = c.ParentId,
                createdAt = c.CreatedAt,
                likeCount = likeCounts.GetValueOrDefault(c.Id.ToString(), 0),
                liked = myLiked.Contains(c.Id.ToString())
            }).ToList();

            return Ok(result);
        }

        /// <summary>点赞 / 取消点赞评论</summary>
        [HttpPost("{id:guid}/like")]
        public async Task<IActionResult> ToggleLike(Guid id)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            var comment = await _context.Comments.FirstOrDefaultAsync(c => c.Id == id);
            if (comment == null) return NotFound(new { message = "评论不存在" });

            var key = id.ToString();
            var existing = await _context.UserInteractions.FirstOrDefaultAsync(i =>
                i.TargetType == "Comment" && i.TargetId == key && i.ActionType == "Like" && i.UserId == CurrentUserId.Value);

            bool liked;
            if (existing != null) { _context.UserInteractions.Remove(existing); liked = false; }
            else
            {
                _context.UserInteractions.Add(new UserInteraction
                {
                    UserId = CurrentUserId.Value,
                    TargetId = key,
                    TargetType = "Comment",
                    ActionType = "Like"
                });
                liked = true;
            }
            await _context.SaveChangesAsync();
            var count = await _context.UserInteractions.CountAsync(i => i.TargetType == "Comment" && i.TargetId == key && i.ActionType == "Like");
            return Ok(new { liked, count });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateChaiCommentRequest req)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            if (string.IsNullOrWhiteSpace(req.Content)) return BadRequest(new { message = "评论内容不能为空" });
            if (string.IsNullOrWhiteSpace(req.TargetType) || string.IsNullOrWhiteSpace(req.TargetId))
                return BadRequest(new { message = "目标无效" });

            var comment = new Comment
            {
                TargetType = req.TargetType,
                TargetId = req.TargetId,
                UserId = CurrentUserId.Value.ToString(),
                Content = req.Content.Trim(),
                ParentId = req.ParentId,
                CreatedAt = DateTime.UtcNow
            };
            _context.Comments.Add(comment);

            // 站内通知：回复 → 通知被回复者；新评论 → 通知目标作者
            var notifier = new NotificationService(_context);
            var link = BuildLink(req.TargetType, req.TargetId);
            if (req.ParentId.HasValue)
            {
                var parentUserId = await _context.Comments.Where(c => c.Id == req.ParentId).Select(c => c.UserId).FirstOrDefaultAsync();
                if (Guid.TryParse(parentUserId, out var parentId))
                    notifier.Add(parentId, CurrentUserId, "reply", req.TargetType, req.TargetId, "回复了你的评论", link);
            }
            else
            {
                var ownerId = await ResolveOwnerAsync(req.TargetType, req.TargetId);
                notifier.Add(ownerId, CurrentUserId, "comment", req.TargetType, req.TargetId, "评论了你的" + TargetLabel(req.TargetType), link);
            }

            await _context.SaveChangesAsync();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            return Ok(new
            {
                id = comment.Id,
                userId = comment.UserId,
                userName = user?.Username ?? "我",
                content = comment.Content,
                parentId = comment.ParentId,
                createdAt = comment.CreatedAt
            });
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            var comment = await _context.Comments.FirstOrDefaultAsync(c => c.Id == id);
            if (comment == null) return NotFound(new { message = "评论不存在" });
            if (comment.UserId != CurrentUserId.Value.ToString()) return Forbid();

            comment.Content = ""; // 软删除：内容置空
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static string TargetLabel(string targetType) => targetType switch
        {
            "work" => "作品",
            "character" => "角色",
            "MustEat" => "推荐",
            _ => "内容"
        };

        private static string BuildLink(string targetType, string targetId) => targetType switch
        {
            "work" => "/community",
            "character" => "/community",
            "MustEat" => "/must-eat",
            _ => "/"
        };

        private async Task<Guid> ResolveOwnerAsync(string targetType, string targetId)
        {
            if (!Guid.TryParse(targetId, out var id)) return Guid.Empty;
            return targetType switch
            {
                "work" => await _context.ChaiWorks.Where(w => w.Id == id).Select(w => w.AuthorId).FirstOrDefaultAsync(),
                "character" => await _context.StickmanCharacters.Where(c => c.Id == id).Select(c => c.AuthorId).FirstOrDefaultAsync(),
                _ => Guid.Empty
            };
        }
    }
}
