using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.LingMai;

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

            var result = comments.Select(c => new
            {
                id = c.Id,
                userId = c.UserId,
                userName = map.TryGetValue(c.UserId, out var n) ? n : "已注销用户",
                content = c.Content,
                parentId = c.ParentId,
                createdAt = c.CreatedAt
            }).ToList();

            return Ok(result);
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
    }
}
