using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers
{
    /// <summary>站内通知：我的通知列表、未读数、标记已读。</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly AppDbContext _context;
        public NotificationController(AppDbContext context) { _context = context; }

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 1, 50);
            var query = _context.Notifications.Where(n => n.UserId == CurrentUserId.Value);
            var total = await query.CountAsync();
            var unread = await query.CountAsync(n => !n.IsRead);
            var items = await query.OrderByDescending(n => n.CreatedAt)
                .Skip((page - 1) * pageSize).Take(pageSize)
                .Select(n => new { n.Id, n.Type, n.TargetType, n.TargetId, n.Content, n.Link, n.IsRead, n.CreatedAt, n.ActorId })
                .ToListAsync();

            var actorIds = items.Where(i => i.ActorId.HasValue).Select(i => i.ActorId!.Value).Distinct().ToList();
            var actors = await _context.Users.Where(u => actorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Username, Avatar = u.Profile != null ? u.Profile.Avatar : null })
                .ToListAsync();
            var map = actors.ToDictionary(a => a.Id, a => a);

            return Ok(new
            {
                total,
                unread,
                items = items.Select(i => new
                {
                    i.Id, i.Type, i.TargetType, i.TargetId, i.Content, i.Link, i.IsRead, i.CreatedAt,
                    ActorId = i.ActorId,
                    ActorName = i.ActorId.HasValue && map.TryGetValue(i.ActorId.Value, out var a) ? a.Username : "某人",
                    ActorAvatar = i.ActorId.HasValue && map.TryGetValue(i.ActorId.Value, out var a2) ? a2.Avatar : null
                })
            });
        }

        [HttpGet("unread-count")]
        public async Task<IActionResult> UnreadCount()
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            var count = await _context.Notifications.CountAsync(n => n.UserId == CurrentUserId.Value && !n.IsRead);
            return Ok(new { count });
        }

        [HttpPost("{id:guid}/read")]
        public async Task<IActionResult> MarkRead(Guid id)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            var n = await _context.Notifications.FirstOrDefaultAsync(x => x.Id == id && x.UserId == CurrentUserId.Value);
            if (n == null) return NotFound(new { message = "通知不存在" });
            if (!n.IsRead) { n.IsRead = true; await _context.SaveChangesAsync(); }
            return Ok(new { ok = true });
        }

        [HttpPost("read-all")]
        public async Task<IActionResult> MarkAllRead()
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            await _context.Notifications.Where(n => n.UserId == CurrentUserId.Value && !n.IsRead)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsRead, true));
            return Ok(new { ok = true });
        }
    }
}
