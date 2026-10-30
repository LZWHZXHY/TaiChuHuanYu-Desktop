using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers.Users
{
    /// <summary>
    /// 个人主页的「社区活跃」统计：发帖、博客、评论、画作、百科、世界观（含编年史）、小说、柴圈作品、OC、岁月史书、签到、聊天、对局、世界任务……
    /// 每天做了几件事（活跃日历）、加入第几天、活跃天数、连续活跃、几点最活跃。
    /// 只给次数，不给内容；「上站天数」只有本人能看到。日期按北京时间。
    /// </summary>
    [ApiController]
    [Route("api/users/{userId:guid}/activity")]
    [AllowAnonymous]
    public class UserActivityController(AppDbContext db, IServiceScopeFactory scopes, IConfiguration configuration, ILogger<UserActivityController> logger) : ControllerBase
    {
        private static readonly TimeSpan Beijing = TimeSpan.FromHours(8);

        /// <summary>活动种类（前端用同样的 key 显示名字）</summary>
        private static readonly (string Key, string Label)[] Kinds =
        {
            ("post", "帖子"), ("blog", "博客"), ("comment", "评论"), ("artwork", "画作"), ("wiki", "百科"),
            ("world", "世界观"), ("novel", "小说"), ("chai", "柴圈作品"), ("oc", "OC"), ("quest", "世界任务"), ("history", "岁月史书"), ("game", "对局"), ("chat", "聊天"), ("signin", "签到"),
        };

        private sealed record Event(DateTime At, string Kind, bool HasTime);
        private sealed record HistoryRow(long Id, string Kind, string Title, string? Content, string Images, DateTime HappenedOn, string DatePrecision, DateTime CreatedAt);

        [HttpGet]
        public async Task<IActionResult> Get(Guid userId)
        {
            var user = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => new { u.Id, u.CreatedAt }).FirstOrDefaultAsync();
            if (user == null) return NotFound(new { message = "没有这位用户" });
            var isSelf = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var me) && me == userId;
            var idText = userId.ToString();
            // 十几张表互不相关：每张表开自己的数据库连接并行查（远程数据库一条查询要 ~250ms，串行要好几秒）
            async Task<List<Event>> Q(Func<AppDbContext, Task<IEnumerable<Event>>> run)
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    return (await run(scope.ServiceProvider.GetRequiredService<AppDbContext>())).ToList();
                }
                catch (Exception ex) { logger.LogWarning(ex, "社区活跃：读取某一类活动失败"); return new(); }
            }
            static IEnumerable<Event> E(IEnumerable<DateTime> times, string kind, bool hasTime = true) => times.Select(t => new Event(t, kind, hasTime));

            // 岁月史书：记的每一段（被下架的只算给本人看）；要拿最近三段显示在主页上，单独留着
            var historyTask = Task.Run(async () =>
            {
                using var scope = scopes.CreateScope();
                var c = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var q = c.CommunityHistoryEntries.AsNoTracking().Where(e => e.UserId == userId);
                if (!isSelf) q = q.Where(e => !e.Hidden);
                return await q.OrderByDescending(e => e.CreatedAt)
                    .Select(e => new HistoryRow(e.Id, e.Kind, e.Title, e.Content, e.Images, e.HappenedOn, e.DatePrecision, e.CreatedAt)).ToListAsync();
            });

            var tasks = new List<Task<List<Event>>>
            {
                // 帖子 / 博客：作者 = 所在空间的主人
                Q(async c =>
                {
                    var spaceIds = await c.Spaces.AsNoTracking().Where(s => s.UserId == idText).Select(s => s.Id).ToListAsync();
                    if (spaceIds.Count == 0) return Enumerable.Empty<Event>();
                    var published = await c.PublishedNotes.AsNoTracking()
                        .Where(p => spaceIds.Contains(p.SpaceId) && (p.Type == "post" || p.Type == "blog"))
                        .Select(p => new { p.Type, p.PublishedAt }).ToListAsync();
                    return published.Select(p => new Event(p.PublishedAt, p.Type == "post" ? "post" : "blog", true));
                }),
                // 评论（帖子、博客、画作、柴圈作品下面的；岁月史书的评论在另一张表）
                Q(async c => E(await c.Comments.AsNoTracking().Where(x => x.UserId == idText && x.Content != "").Select(x => x.CreatedAt).ToListAsync(), "comment")),
                Q(async c => E(await c.CommunityHistoryComments.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.CreatedAt).ToListAsync(), "comment")),
                // 画作：别人只看得到审核通过、公开的
                Q(async c =>
                {
                    var q = c.Artworks.AsNoTracking().Where(a => a.UploaderId == userId);
                    if (!isSelf) q = q.Where(a => a.IsApproved && (a.Status == "published" || a.Status == "" || a.Status == null));
                    return E(await q.Select(a => a.UploadAt).ToListAsync(), "artwork");
                }),
                // 百科编辑
                Q(async c => E(await c.WikiArticleRevisions.AsNoTracking().Where(r => r.ContributorId == idText).Select(r => r.CreatedAt).ToListAsync(), "wiki")),
                // 世界观条目的新建 / 修改、编年史里写的事件（只给次数，不暴露是哪个世界）
                Q(async c => E(await c.WorldCardRevisions.AsNoTracking().Where(r => r.UserId == userId && r.Action != "baseline").Select(r => r.CreatedAt).ToListAsync(), "world")),
                Q(async c => E(await c.WorldTimelineEvents.AsNoTracking().Where(e => e.CreatedBy == userId).Select(e => e.CreatedAt).ToListAsync(), "world")),
                // 世界观小说：写的章节（别人只算公开小说里的）
                Q(async c =>
                {
                    var q = c.WorldNovels.AsNoTracking().Where(n => n.AuthorId == userId);
                    if (!isSelf) q = q.Where(n => n.IsPublic);
                    var ids = await q.Select(n => n.Id).ToListAsync();
                    return ids.Count == 0 ? Enumerable.Empty<Event>()
                        : E(await c.WorldNovelChapters.AsNoTracking().Where(ch => ids.Contains(ch.NovelId)).Select(ch => ch.CreatedAt).ToListAsync(), "novel");
                }),
                // 柴圈作品、OC（别人只算已发布的）
                Q(async c =>
                {
                    var q = c.ChaiWorks.AsNoTracking().Where(w => w.AuthorId == userId);
                    if (!isSelf) q = q.Where(w => w.Status == "published");
                    return E(await q.Select(w => w.CreatedAt).ToListAsync(), "chai");
                }),
                Q(async c =>
                {
                    var q = c.StickmanCharacters.AsNoTracking().Where(x => x.AuthorId == userId);
                    if (!isSelf) q = q.Where(x => x.Status == "published");
                    return E(await q.Select(x => x.CreatedAt).ToListAsync(), "oc");
                }),
                // 世界任务报告
                Q(async c => E(await c.ServerQuestContributions.AsNoTracking().Where(x => x.UserId == userId && x.Source == "player").Select(x => x.CreatedAt).ToListAsync(), "quest")),
                // 签到（只有日期，不算进「几点活跃」）
                Q(async c => E(await c.UserSignLogs.AsNoTracking().Where(x => x.UserId == userId).Select(x => x.SignDate).ToListAsync(), "signin", false)),
                // 棋类对局（五子棋 / 围棋 / 象棋的本地记录）
                Q(async c => E(await c.BoardGameRecords.AsNoTracking().Where(r => r.Player1Id == userId || r.Player2Id == userId).Select(r => r.CreatedAt).ToListAsync(), "game")),
            };

            // 下面几张表是服务启动后自己建的（聊天、联机对局、上站记录），表不存在就跳过
            var visitDays = 0;
            List<object>? visits = null;
            var rawTask = Task.Run(async () =>
            {
                var list = new List<Event>();
                try
                {
                    await using var c = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));
                    await c.OpenAsync();
                    foreach (var t in await ReadTimesAsync(c, "SELECT CreatedAt FROM chat_messages WHERE UserId=@u AND IsAi=0 AND Deleted=0", idText))
                        list.Add(new Event(t, "chat", true));
                    foreach (var t in await ReadTimesAsync(c, "SELECT m.EndedAt FROM game_match_players p JOIN game_matches m ON m.Id = p.MatchId WHERE p.UserId=@u", idText))
                        list.Add(new Event(t, "game", true));
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
                return list;
            });

            var events = (await Task.WhenAll(tasks)).SelectMany(x => x).Concat(await rawTask).ToList();
            List<HistoryRow> historyRows;
            try { historyRows = await historyTask; }
            catch (Exception ex) { logger.LogWarning(ex, "社区活跃：读取岁月史书失败"); historyRows = new(); }
            events.AddRange(historyRows.Select(h => new Event(h.CreatedAt, "history", true)));

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
