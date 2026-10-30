using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers.Users
{
    /// <summary>
    /// 个人主页的「社区活跃」统计：发帖、博客、评论、画作、百科、世界观、签到、聊天、对局、世界任务……
    /// 每天做了几件事（活跃日历）、加入第几天、活跃天数、连续活跃、几点最活跃。
    /// 只给次数，不给内容；「上站天数」只有本人能看到。日期按北京时间。
    /// </summary>
    [ApiController]
    [Route("api/users/{userId:guid}/activity")]
    [AllowAnonymous]
    public class UserActivityController(AppDbContext db, IConfiguration configuration, ILogger<UserActivityController> logger) : ControllerBase
    {
        private static readonly TimeSpan Beijing = TimeSpan.FromHours(8);

        /// <summary>活动种类（前端用同样的 key 显示名字）</summary>
        private static readonly (string Key, string Label)[] Kinds =
        {
            ("post", "帖子"), ("blog", "博客"), ("comment", "评论"), ("artwork", "画作"), ("wiki", "百科"),
            ("world", "世界观"), ("quest", "世界任务"), ("history", "岁月史书"), ("game", "对局"), ("chat", "聊天"), ("signin", "签到"),
        };

        private sealed record Event(DateTime At, string Kind, bool HasTime);

        [HttpGet]
        public async Task<IActionResult> Get(Guid userId)
        {
            var user = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.Id, u.CreatedAt }).FirstOrDefaultAsync();
            if (user == null) return NotFound(new { message = "没有这位用户" });
            var isSelf = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var me) && me == userId;
            var idText = userId.ToString();
            var events = new List<Event>();

            // 帖子 / 博客：作者 = 所在空间的主人
            var spaceIds = await db.Spaces.AsNoTracking().Where(s => s.UserId == idText).Select(s => s.Id).ToListAsync();
            if (spaceIds.Count > 0)
            {
                var published = await db.PublishedNotes.AsNoTracking()
                    .Where(p => spaceIds.Contains(p.SpaceId) && (p.Type == "post" || p.Type == "blog"))
                    .Select(p => new { p.Type, p.PublishedAt }).ToListAsync();
                events.AddRange(published.Select(p => new Event(p.PublishedAt, p.Type == "post" ? "post" : "blog", true)));
            }
            // 评论（帖子、博客、画作下面的都算）
            events.AddRange((await db.Comments.AsNoTracking().Where(c => c.UserId == idText && c.Content != "")
                .Select(c => c.CreatedAt).ToListAsync()).Select(t => new Event(t, "comment", true)));
            // 画作：别人只看得到审核通过、公开的
            var artQuery = db.Artworks.AsNoTracking().Where(a => a.UploaderId == userId);
            if (!isSelf) artQuery = artQuery.Where(a => a.IsApproved && (a.Status == "published" || a.Status == "" || a.Status == null));
            events.AddRange((await artQuery.Select(a => a.UploadAt).ToListAsync()).Select(t => new Event(t, "artwork", true)));
            // 百科编辑
            events.AddRange((await db.WikiArticleRevisions.AsNoTracking().Where(r => r.ContributorId == idText)
                .Select(r => r.CreatedAt).ToListAsync()).Select(t => new Event(t, "wiki", true)));
            // 世界观条目的新建 / 修改（只给次数，不暴露是哪个世界）
            events.AddRange((await db.WorldCardRevisions.AsNoTracking().Where(r => r.UserId == userId && r.Action != "baseline")
                .Select(r => r.CreatedAt).ToListAsync()).Select(t => new Event(t, "world", true)));
            // 世界任务报告
            events.AddRange((await db.ServerQuestContributions.AsNoTracking().Where(c => c.UserId == userId && c.Source == "player")
                .Select(c => c.CreatedAt).ToListAsync()).Select(t => new Event(t, "quest", true)));
            // 岁月史书：记的每一段（被下架的只算给本人看），在岁月史书里的评论也算「评论」
            var historyQuery = db.CommunityHistoryEntries.AsNoTracking().Where(e => e.UserId == userId);
            if (!isSelf) historyQuery = historyQuery.Where(e => !e.Hidden);
            var historyRows = await historyQuery.OrderByDescending(e => e.CreatedAt)
                .Select(e => new { e.Id, e.Kind, e.Title, e.Content, e.Images, e.HappenedOn, e.DatePrecision, e.CreatedAt }).ToListAsync();
            events.AddRange(historyRows.Select(h => new Event(h.CreatedAt, "history", true)));
            events.AddRange((await db.CommunityHistoryComments.AsNoTracking().Where(c => c.UserId == userId)
                .Select(c => c.CreatedAt).ToListAsync()).Select(t => new Event(t, "comment", true)));
            // 签到（只有日期，不算进「几点活跃」）
            events.AddRange((await db.UserSignLogs.AsNoTracking().Where(s => s.UserId == userId)
                .Select(s => s.SignDate).ToListAsync()).Select(t => new Event(t, "signin", false)));
            // 棋类对局（五子棋 / 围棋 / 象棋的本地记录）
            events.AddRange((await db.BoardGameRecords.AsNoTracking().Where(r => r.Player1Id == userId || r.Player2Id == userId)
                .Select(r => r.CreatedAt).ToListAsync()).Select(t => new Event(t, "game", true)));

            // 下面几张表是服务启动后自己建的（聊天、联机对局、上站记录），表不存在就跳过
            var visitDays = 0;
            List<object>? visits = null;
            try
            {
                await using var c = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));
                await c.OpenAsync();
                foreach (var t in await ReadTimesAsync(c, "SELECT CreatedAt FROM chat_messages WHERE UserId=@u AND IsAi=0 AND Deleted=0", idText))
                    events.Add(new Event(t, "chat", true));
                foreach (var t in await ReadTimesAsync(c, "SELECT m.EndedAt FROM game_match_players p JOIN game_matches m ON m.Id = p.MatchId WHERE p.UserId=@u", idText))
                    events.Add(new Event(t, "game", true));
                if (isSelf)
                {
                    // 上站天数只给本人看
                    var days = await ReadTimesAsync(c, "SELECT Day FROM analytics_user_days WHERE UserId=@u", idText);
                    visitDays = days.Count;
                    var today = Today();
                    visits = days.Where(d => d.Date > today.AddDays(-371)).OrderBy(d => d)
                        .Select(d => (object)new { d = d.ToString("yyyy-MM-dd"), n = 1 }).ToList();
                }
            }
            catch (Exception ex) { logger.LogDebug(ex, "读取聊天 / 对局 / 上站记录失败"); }

            var stats = await db.UserStats.AsNoTracking().Where(s => s.UserId == userId)
                .Select(s => new { s.LikesReceived, s.FollowersCount, s.Experience, s.MaxSignStreak }).FirstOrDefaultAsync();

            // ---- 汇总 ----
            var todayCn = Today();
            var perDay = events.GroupBy(e => Day(e.At, e.HasTime)).ToDictionary(g => g.Key, g => g.ToList());
            var activeDays = perDay.Keys.OrderBy(d => d).ToList();
            var current = 0;
            var cursor = perDay.ContainsKey(todayCn) ? todayCn : todayCn.AddDays(-1);
            while (perDay.ContainsKey(cursor)) { current++; cursor = cursor.AddDays(-1); }
            int longest = 0, run = 0;
            DateTime? prev = null;
            foreach (var d in activeDays) { run = prev is DateTime p && (d - p).Days == 1 ? run + 1 : 1; longest = Math.Max(longest, run); prev = d; }

            var hours = new int[24];
            var weekdays = new int[7];
            foreach (var e in events)
            {
                if (e.HasTime) hours[DateTime.SpecifyKind(e.At, DateTimeKind.Utc).Add(Beijing).Hour]++;
                weekdays[((int)Day(e.At, e.HasTime).DayOfWeek + 6) % 7]++;
            }

            return Ok(new
            {
                joinedAt = user.CreatedAt,
                days = (todayCn - Day(user.CreatedAt, true)).Days + 1,
                firstActiveAt = activeDays.Count > 0 ? activeDays[0].ToString("yyyy-MM-dd") : null,
                totals = new
                {
                    events = events.Count,
                    activeDays = activeDays.Count,
                    currentStreak = current,
                    longestStreak = longest,
                    last7 = perDay.Where(p => p.Key > todayCn.AddDays(-7)).Sum(p => p.Value.Count),
                    last30 = perDay.Where(p => p.Key > todayCn.AddDays(-30)).Sum(p => p.Value.Count),
                    likes = stats?.LikesReceived ?? 0,
                    followers = stats?.FollowersCount ?? 0,
                    maxSignStreak = stats?.MaxSignStreak ?? 0,
                    visitDays = isSelf ? visitDays : (int?)null,
                },
                kinds = Kinds.Select(k => new { key = k.Key, label = k.Label, count = events.Count(e => e.Kind == k.Key) })
                    .Where(k => k.count > 0).OrderByDescending(k => k.count).ToList(),
                // 每天几件事 + 各类分别几件（全部日子，前端可以按年份切换）
                heatmap = perDay.OrderBy(p => p.Key).Select(p => new
                {
                    d = p.Key.ToString("yyyy-MM-dd"),
                    n = p.Value.Count,
                    k = p.Value.GroupBy(e => e.Kind).ToDictionary(g => g.Key, g => g.Count()),
                }),
                visits,
                // 最近记的几段岁月史书（个人主页上直接显示，点了打开那一段）
                history = new
                {
                    count = historyRows.Count,
                    recent = historyRows.Take(3).Select(h => new
                    {
                        h.Id,
                        h.Kind,
                        Title = h.Title.Length > 0 ? h.Title : Excerpt(h.Content),
                        HappenedOn = h.HappenedOn.ToString("yyyy-MM-dd"),
                        Precision = h.DatePrecision,
                        Cover = FirstImage(h.Images),
                    }),
                },
                hours,
                weekdays,
                isSelf,
            });
        }

        private static string Excerpt(string? content)
        {
            var text = System.Text.RegularExpressions.Regex.Replace(content ?? "", @"\s+", " ").Trim();
            return text.Length == 0 ? "" : text.Length > 40 ? text[..40] + "…" : text;
        }

        private static string? FirstImage(string? json)
        {
            try { return System.Text.Json.JsonSerializer.Deserialize<List<string>>(json ?? "[]")?.FirstOrDefault(); }
            catch (System.Text.Json.JsonException) { return null; }
        }

        private static DateTime Today() => DateTime.UtcNow.Add(Beijing).Date;

        /// <summary>北京时间的日期。只有日期的（签到、上站）直接用日期</summary>
        private static DateTime Day(DateTime at, bool hasTime) => hasTime ? DateTime.SpecifyKind(at, DateTimeKind.Utc).Add(Beijing).Date : at.Date;

        private static async Task<List<DateTime>> ReadTimesAsync(MySqlConnection c, string sql, string userId)
        {
            var list = new List<DateTime>();
            try
            {
                await using var cmd = c.CreateCommand();
                cmd.CommandText = sql;
                cmd.Parameters.AddWithValue("@u", userId);
                await using var r = await cmd.ExecuteReaderAsync();
                while (await r.ReadAsync()) if (!r.IsDBNull(0)) list.Add(r.GetDateTime(0));
            }
            catch (MySqlException) { /* 表还没建 */ }
            return list;
        }
    }
}
