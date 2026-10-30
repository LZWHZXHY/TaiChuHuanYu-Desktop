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

    /// <summary>柴圈作品/OC、必吃榜、个人主页留言板通用评论（复用 comments 表，TargetType + TargetId 多态）。</summary>
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
            targetId = NormalizeTargetId(targetType, targetId);

            var comments = await _context.Comments
                .Where(c => c.TargetType == targetType && c.TargetId == targetId)
                .OrderBy(c => c.CreatedAt)
                .Select(c => new { c.Id, c.UserId, c.Content, c.ParentId, c.CreatedAt })
                .ToListAsync();

            var guids = comments
                .Select(c => Guid.TryParse(c.UserId, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty).Distinct().ToList();
            var users = await _context.Users.Where(u => guids.Contains(u.Id))
                .Select(u => new { u.Id, u.Username, Avatar = u.Profile != null ? u.Profile.Avatar : null }).ToListAsync();
            var map = users.ToDictionary(u => u.Id.ToString(), u => u.Username);
            var avatars = users.ToDictionary(u => u.Id.ToString(), u => u.Avatar);

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
                avatar = c.Content == "" ? null : avatars.GetValueOrDefault(c.UserId),
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
            if (req.Content.Trim().Length > 1000) return BadRequest(new { message = "评论不能超过 1000 字" });
            req.TargetId = NormalizeTargetId(req.TargetType, req.TargetId);

            // 个人主页留言板：目标必须是存在的用户。
            if (req.TargetType == ProfileTarget)
            {
                if (!Guid.TryParse(req.TargetId, out var wallOwner) || !await _context.Users.AnyAsync(u => u.Id == wallOwner))
                    return NotFound(new { message = "没有找到这位用户" });
            }
            // 回复必须指向同一个目标下仍存在的评论。
            if (req.ParentId.HasValue && !await _context.Comments.AnyAsync(c => c.Id == req.ParentId && c.TargetType == req.TargetType && c.TargetId == req.TargetId && c.Content != ""))
                return BadRequest(new { message = "回复的评论不存在或已删除" });

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
                var message = req.TargetType == ProfileTarget ? "在你的主页留言了" : "评论了你的" + TargetLabel(req.TargetType);
                notifier.Add(ownerId, CurrentUserId, "comment", req.TargetType, req.TargetId, message, link);
            }

            await _context.SaveChangesAsync();

            var user = await _context.Users.Include(u => u.Profile).FirstOrDefaultAsync(u => u.Id == CurrentUserId);
            return Ok(new
            {
                id = comment.Id,
                userId = comment.UserId,
                userName = user?.Username ?? "我",
                avatar = user?.Profile?.Avatar,
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
            // 本人可以删除自己的评论；主页主人也可以删除自己留言板上的留言。
            var isAuthor = comment.UserId == CurrentUserId.Value.ToString();
            var isWallOwner = comment.TargetType == ProfileTarget && comment.TargetId == CurrentUserId.Value.ToString();
            if (!isAuthor && !isWallOwner) return Forbid();

            comment.Content = ""; // 软删除：内容置空
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private const string ProfileTarget = "profile";

        // 用户 ID 统一成小写标准格式，避免大小写不同导致留言对不上。
        private static string NormalizeTargetId(string targetType, string targetId)
            => targetType == ProfileTarget && Guid.TryParse(targetId, out var id) ? id.ToString() : targetId;

        private static string TargetLabel(string targetType) => targetType switch
        {
            ProfileTarget => "主页",
            "work" => "作品",
            "character" => "角色",
            "MustEat" => "推荐",
            _ => "内容"
        };

        private static string BuildLink(string targetType, string targetId) => targetType switch
        {
            // 直接打开那件作品 / 那个角色（以前都跳到柴圈首页，还得自己找）
            "work" => $"/works/{targetId}",
            "character" => $"/ocs/{targetId}",
            "MustEat" => "/must-eat",
            ProfileTarget => $"/user/{targetId}?tab=guestbook", // 主人打开的是个人中心的留言板标签，别人打开的是公开主页的留言板
            _ => "/"
        };

        private async Task<Guid> ResolveOwnerAsync(string targetType, string targetId)
        {
            if (!Guid.TryParse(targetId, out var id)) return Guid.Empty;
            return targetType switch
            {
                "work" => await _context.ChaiWorks.Where(w => w.Id == id).Select(w => w.AuthorId).FirstOrDefaultAsync(),
                "character" => await _context.StickmanCharacters.Where(c => c.Id == id).Select(c => c.AuthorId).FirstOrDefaultAsync(),
                ProfileTarget => id, // 留言板的主人就是这个用户
                _ => Guid.Empty
            };
        }
    }
}
