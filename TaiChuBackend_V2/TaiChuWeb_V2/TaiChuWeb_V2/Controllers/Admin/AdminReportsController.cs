using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Notify;

namespace TaiChuWeb_V2.Controllers.Admin
{
    /// <summary>
    /// 管理面板「举报」：看用户的举报、打开被举报的内容、标记「已处理」或「不处理」，处理结果通知举报人。
    /// 同一个内容被多人举报时合在一起显示几个人举报了。权限：User_Audit（AdminAccessFilter）。
    /// 真正的处理（下架作品、删评论、下架岁月史书……）在各自的页面上做，这里只记录和通知。
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/admin/reports")]
    public class AdminReportsController(AppDbContext db) : ControllerBase
    {
        public sealed class HandleDto
        {
            /// <summary>resolved 已处理 / dismissed 不处理</summary>
            public string? Status { get; set; }
            public string? Note { get; set; }
            /// <summary>同一个内容的其他待处理举报也一起处理</summary>
            public bool All { get; set; } = true;
        }

        private static readonly Dictionary<string, string> TypeLabels = new()
        {
            ["post"] = "帖子", ["blog"] = "博客", ["artwork"] = "画作", ["comment"] = "评论", ["history"] = "岁月史书",
            ["history-comment"] = "岁月史书评论", ["work"] = "柴圈作品", ["character"] = "OC", ["user"] = "用户",
            ["world"] = "世界观", ["novel"] = "小说", ["other"] = "其他",
        };

        [HttpGet]
        public async Task<IActionResult> List([FromQuery] string status = "pending", [FromQuery] int page = 1)
        {
            page = Math.Max(1, page);
            const int pageSize = 30;
            var q = db.ContentReports.AsNoTracking();
            if (status is "pending" or "resolved" or "dismissed") q = q.Where(r => r.Status == status);
            var rows = await q.OrderByDescending(r => r.Id).Skip((page - 1) * pageSize).Take(pageSize + 1).ToListAsync();
            var hasMore = rows.Count > pageSize;
            rows = rows.Take(pageSize).ToList();

            // 同一个内容一共被几个人举报（还没处理的）
            var targetIds = rows.Select(r => r.TargetId).Distinct().ToList();
            var sameTarget = (await db.ContentReports.AsNoTracking().Where(r => r.Status == "pending" && targetIds.Contains(r.TargetId))
                    .Select(r => new { r.TargetType, r.TargetId }).ToListAsync())
                .GroupBy(r => r.TargetType + ":" + r.TargetId).ToDictionary(g => g.Key, g => g.Count());

            var userIds = rows.Select(r => r.ReporterId).Concat(rows.Where(r => r.HandledBy != null).Select(r => r.HandledBy!.Value)).Distinct().ToList();
            var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
            // 被举报的是用户：顺便给出名字
            var reportedUserIds = rows.Where(r => r.TargetType == "user").Select(r => Guid.TryParse(r.TargetId, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().ToList();
            var reportedUsers = reportedUserIds.Count == 0 ? new Dictionary<Guid, string>() : await db.Users.AsNoTracking().Where(u => reportedUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);

            var counts = await db.ContentReports.AsNoTracking().GroupBy(r => r.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
            return Ok(new
            {
                counts = new
                {
                    pending = counts.FirstOrDefault(c => c.Key == "pending")?.Count ?? 0,
                    resolved = counts.FirstOrDefault(c => c.Key == "resolved")?.Count ?? 0,
                    dismissed = counts.FirstOrDefault(c => c.Key == "dismissed")?.Count ?? 0,
                },
                hasMore,
                items = rows.Select(r => new
                {
                    r.Id,
                    r.TargetType,
                    TypeLabel = TypeLabels.GetValueOrDefault(r.TargetType) ?? r.TargetType,
                    r.TargetId,
                    TargetTitle = r.TargetType == "user" && Guid.TryParse(r.TargetId, out var tu) && reportedUsers.TryGetValue(tu, out var tn) ? tn : r.TargetTitle,
                    TargetUrl = r.TargetUrl ?? (r.TargetType == "user" ? $"/user/{r.TargetId}" : null),
                    r.Reason,
                    r.Detail,
                    r.Status,
                    Reporter = new { Id = r.ReporterId, Username = users.GetValueOrDefault(r.ReporterId) ?? "已注销的用户" },
                    HandledByName = r.HandledBy is Guid h ? users.GetValueOrDefault(h) ?? "管理员" : null,
                    HandledAt = r.HandledAt is DateTime ha ? DateTime.SpecifyKind(ha, DateTimeKind.Utc) : (DateTime?)null,
                    r.HandleNote,
                    CreatedAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc),
                    SameTargetPending = sameTarget.GetValueOrDefault(r.TargetType + ":" + r.TargetId),
                }),
            });
        }

        /// <summary>待处理的有几条（管理面板菜单上的小红点）</summary>
        [HttpGet("pending-count")]
        public async Task<IActionResult> PendingCount() => Ok(new { count = await db.ContentReports.CountAsync(r => r.Status == "pending") });

        [HttpPost("{id:long}/handle")]
        public async Task<IActionResult> Handle(long id, [FromBody] HandleDto dto)
        {
            if (dto.Status is not ("resolved" or "dismissed")) return BadRequest(new { message = "处理结果不对" });
            var report = await db.ContentReports.FirstOrDefaultAsync(r => r.Id == id);
            if (report == null) return NotFound(new { message = "这条举报不存在" });
            var note = (dto.Note ?? "").Trim();
            if (note.Length > 300) note = note[..300];
            var me = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var g) ? g : (Guid?)null;
            var list = dto.All
                ? await db.ContentReports.Where(r => r.TargetType == report.TargetType && r.TargetId == report.TargetId && (r.Status == "pending" || r.Id == id)).ToListAsync()
                : new List<Models.Reports.ContentReport> { report };
            var notify = new NotificationService(db);
            var label = TypeLabels.GetValueOrDefault(report.TargetType) ?? "内容";
            var title = string.IsNullOrWhiteSpace(report.TargetTitle) ? label : $"{label}「{(report.TargetTitle.Length > 30 ? report.TargetTitle[..30] + "…" : report.TargetTitle)}」";
            foreach (var r in list)
            {
                r.Status = dto.Status;
                r.HandledBy = me;
                r.HandledAt = DateTime.UtcNow;
                r.HandleNote = note.Length == 0 ? null : note;
                // 通知举报人（不写管理员是谁）
                notify.Add(r.ReporterId, null, "system", "report", r.Id.ToString(),
                    dto.Status == "resolved"
                        ? $"你举报的{title}已经处理了{(note.Length > 0 ? "：" + note : "")}，谢谢你帮忙维护社区"
                        : $"你举报的{title}管理员看过了，暂时不处理{(note.Length > 0 ? "：" + note : "")}",
                    null);
            }
            await db.SaveChangesAsync();
            return Ok(new { message = $"已{(dto.Status == "resolved" ? "标记为已处理" : "标记为不处理")}（{list.Count} 条举报），已通知举报人", handled = list.Count });
        }
    }
}
