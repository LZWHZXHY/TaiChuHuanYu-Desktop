using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers.ChaiCommunity
{
    public class ChaiFeedItem
    {
        public string Id { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty; // work / oc / battle / joint
        public string Title { get; set; } = string.Empty;
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorId { get; set; } // 点击作者进入个人主页
        public string? CoverUrl { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    [Route("api/[controller]")]
    [ApiController]
    public class ChaiFeedController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ChaiFeedController(AppDbContext context) { _context = context; }

        // ===== 柴圈动态流（公开）：作品 + OC + 约战 + 联合，按时间倒序 =====
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Get([FromQuery] int offset = 0, [FromQuery] int limit = 20)
        {
            if (limit < 1 || limit > 50) limit = 20;
            if (offset < 0) offset = 0;

            var items = new List<ChaiFeedItem>();

            var works = await _context.ChaiWorks.Where(w => w.Status == "published")
                .OrderByDescending(w => w.CreatedAt).Take(offset + limit)
                .Select(w => new ChaiFeedItem { Id = w.Id.ToString(), Type = "work", Title = w.Title, AuthorName = w.AuthorName, AuthorId = w.AuthorId.ToString(), CoverUrl = w.CoverUrl, CreatedAt = w.CreatedAt })
                .ToListAsync();
            items.AddRange(works);

            var ocs = await _context.StickmanCharacters.Where(c => c.Status == "published")
                .OrderByDescending(c => c.CreatedAt).Take(offset + limit)
                .Select(c => new ChaiFeedItem { Id = c.Id.ToString(), Type = "oc", Title = c.Title, AuthorName = c.AuthorName, AuthorId = c.AuthorId.ToString(), CoverUrl = c.CoverUrl, CreatedAt = c.CreatedAt })
                .ToListAsync();
            items.AddRange(ocs);

            var joints = await _context.JointActivities.Where(j => j.Status == "open" || j.Status == "ongoing")
                .OrderByDescending(j => j.CreatedAt).Take(offset + limit)
                .Select(j => new ChaiFeedItem { Id = j.Id.ToString(), Type = "joint", Title = j.Title, AuthorName = j.OrganizerName, AuthorId = j.OrganizerType == "user" ? j.OrganizerId.ToString() : null, CoverUrl = j.CoverUrl, CreatedAt = j.CreatedAt })
                .ToListAsync();
            items.AddRange(joints);

            var battles = await _context.Battles.Where(b => b.Status != "cancelled")
                .OrderByDescending(b => b.CreatedAt).Take(offset + limit)
                .Include(b => b.Participants)
                .ToListAsync();
            foreach (var b in battles)
            {
                items.Add(new ChaiFeedItem
                {
                    Id = b.Id,
                    Type = "battle",
                    Title = b.Title,
                    AuthorName = b.Participants.OrderBy(p => p.JoinedAt).Select(p => p.UserName).FirstOrDefault() ?? "匿名",
                    AuthorId = b.Participants.OrderBy(p => p.JoinedAt).Select(p => (Guid?)p.UserId).FirstOrDefault()?.ToString(),
                    CoverUrl = b.CoverUrl,
                    CreatedAt = b.CreatedAt
                });
            }

            var sorted = items.OrderByDescending(i => i.CreatedAt).Skip(offset).Take(limit).ToList();
            var hasMore = items.Count > offset + limit;
            return Ok(new { items = sorted, hasMore });
        }
    }
}
