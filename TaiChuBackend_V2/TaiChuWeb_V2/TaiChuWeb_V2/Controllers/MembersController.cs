using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services.Analytics;

namespace TaiChuWeb_V2.Controllers
{
    /// <summary>
    /// 社区成员列表 + 在线人数（任何人都能看，包括未登录访客）。
    /// 在线 = 最近 5 分钟内出现过（登录用户的任何请求、页面每分钟的心跳都算，见 PresenceTracker）。
    /// 列表里在线的人排在最前面，后面是其他成员（可以按加入时间、等级、名字排）。
    /// 只返回公开资料：名字、头像、等级、加入时间、在线 / 最近来过 / 正在看哪个板块；不含邮箱等私人信息。
    /// 隐身的人（PresenceTracker.IsInvisible）：别人看来不在线、没有「最近来过」；在线人数里还算，但不知道是谁。自己看自己照常显示。
    /// </summary>
    [ApiController]
    [Route("api/members")]
    [AllowAnonymous]
    public class MembersController(AppDbContext db, PresenceTracker tracker, AnalyticsStore store, ILogger<MembersController> logger) : ControllerBase
    {
        private sealed record Row(Guid Id, string Username, string? Avatar, long? Experience, DateTime CreatedAt);

        /// <summary>在线人数（只读内存，不查数据库，工具栏每分钟取一次）</summary>
        [HttpGet("online-count")]
        public IActionResult OnlineCount()
        {
            var (members, guests) = tracker.Snapshot();
            var hidden = members.Count(m => tracker.IsInvisible(m.UserId));
            return Ok(new { members = members.Count - hidden, hidden, guests, total = members.Count + guests });
        }

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        /// <summary>
        /// 成员列表。sort：joined 新加入的在前（默认）/ old 老成员在前 / level 等级高的在前 / name 按名字。
        /// onlineOnly = true 时只返回在线的人。
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> List(string? q, string? sort, bool onlineOnly = false, int page = 1, int pageSize = 48)
        {
            page = Math.Max(1, page);
            pageSize = Math.Clamp(pageSize, 12, 100);
            var (allOnline, guests) = tracker.Snapshot();
            // 隐身的人不算进「在线的成员」（自己除外）
            var me = CurrentUserId;
            var online = allOnline.Where(o => !tracker.IsInvisible(o.UserId) || o.UserId == me).ToList();
            var hidden = allOnline.Count - online.Count;
            var onlineIds = online.Select(o => o.UserId).Distinct().ToList();
            var onlineAt = online.GroupBy(o => o.UserId).ToDictionary(g => g.Key, g => g.Max(o => o.LastUtc));

            var users = db.Users.AsNoTracking();
            var keyword = (q ?? "").Trim();
            if (keyword.Length > 50) keyword = keyword[..50];
            if (keyword.Length > 0) users = users.Where(u => u.Username.Contains(keyword));

            // 在线的人（符合搜索的），最近有动静的在前
            var onlineRows = onlineIds.Count == 0 ? new List<Row>() : await users.Where(u => onlineIds.Contains(u.Id))
                .Select(u => new Row(u.Id, u.Username, u.Profile != null ? u.Profile.Avatar : null, u.Stats != null ? (long?)u.Stats.Experience : null, u.CreatedAt))
                .ToListAsync();
            onlineRows = onlineRows.OrderByDescending(r => onlineAt.GetValueOrDefault(r.Id)).ToList();

            var total = onlineOnly ? onlineRows.Count : await users.CountAsync();
            var offset = (page - 1) * pageSize;
            var items = onlineRows.Skip(offset).Take(pageSize).ToList();
            var need = pageSize - items.Count;
            if (!onlineOnly && need > 0)
            {
                var rest = users.Where(u => !onlineIds.Contains(u.Id));
                rest = sort switch
                {
                    "level" => rest.OrderByDescending(u => u.Stats != null ? u.Stats.Experience : 0).ThenBy(u => u.CreatedAt),
                    "name" => rest.OrderBy(u => u.Username),
                    "old" => rest.OrderBy(u => u.CreatedAt),
                    _ => rest.OrderByDescending(u => u.CreatedAt),
                };
                items.AddRange(await rest.Skip(Math.Max(0, offset - onlineRows.Count)).Take(need)
                    .Select(u => new Row(u.Id, u.Username, u.Profile != null ? u.Profile.Avatar : null, u.Stats != null ? (long?)u.Stats.Experience : null, u.CreatedAt))
                    .ToListAsync());
            }

            // 不在线的人：最后一次来是什么时候（统计表读不出来也不影响列表）
            var lastSeen = new Dictionary<Guid, DateTime>();
            var offline = items.Where(i => !onlineAt.ContainsKey(i.Id) && (!tracker.IsInvisible(i.Id) || i.Id == me)).Select(i => i.Id).ToList();
            try { lastSeen = await store.LastSeenOfAsync(offline); }
            catch (Exception ex) { logger.LogWarning(ex, "读取成员最近来访时间失败"); }

            return Ok(new
            {
                total,
                online = new { members = online.Count, hidden, guests, total = allOnline.Count + guests, matched = onlineRows.Count },
                // 我自己是不是隐身（没登录是 null）
                meInvisible = me is Guid self ? tracker.IsInvisible(self) : (bool?)null,
                items = items.Select(r => new
                {
                    r.Id,
                    r.Username,
                    r.Avatar,
                    Level = UserStats.CalculateLevel(r.Experience ?? 0),
                    JoinedAt = r.CreatedAt,
                    Online = onlineAt.ContainsKey(r.Id),
                    LastSeenAt = onlineAt.TryGetValue(r.Id, out var at) ? at : lastSeen.TryGetValue(r.Id, out var seen) ? seen : (DateTime?)null,
                    // 正在看哪个板块（只有在线、没隐身的人有）
                    Section = onlineAt.ContainsKey(r.Id) && !tracker.IsInvisible(r.Id) ? tracker.SectionOf(r.Id) : null,
                    Invisible = r.Id == me && tracker.IsInvisible(r.Id),
                }),
                hasMore = offset + items.Count < total,
            });
        }
    }
}
