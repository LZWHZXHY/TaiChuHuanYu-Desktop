using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using MySqlConnector;
using TaiChuWeb_V2.Controllers.LingMai;
using TaiChuWeb_V2.Services.Analytics;

namespace TaiChuWeb_V2.Controllers.Admin
{
    /// <summary>
    /// 管理后台 · 社区数据分析：实时在线、活跃、签到、新用户与历史对比；各板块的内容总量和新增（content）。
    /// 权限：System_Monitor（由 AdminAccessFilter 校验）。日期一律按中国时间。
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/Admin/Analytics")]
    public class AdminAnalyticsController(AppDbContext db, PresenceTracker tracker, AnalyticsStore store, IServiceScopeFactory scopes, IConfiguration configuration, ILogger<AdminAnalyticsController> logger) : ControllerBase
    {
        private static string D(DateTime d) => d.ToString("yyyy-MM-dd");

        private async Task<Dictionary<Guid, (string Name, string? Avatar)>> NamesAsync(IEnumerable<Guid> ids)
        {
            var list = ids.Distinct().ToList();
            if (list.Count == 0) return new();
            var users = await db.Users.AsNoTracking().Where(u => list.Contains(u.Id))
                .Select(u => new { u.Id, u.Username, Avatar = u.Profile != null ? u.Profile.Avatar : null }).ToListAsync();
            return users.ToDictionary(u => u.Id, u => (u.Username, u.Avatar));
        }

        /// <summary>实时概况：当前在线、今日峰值、今日活跃/签到/新注册，以及与昨天同一时段的对比。</summary>
        [HttpGet("live")]
        public async Task<IActionResult> Live()
        {
            await store.FlushAsync(tracker);
            var nowUtc = DateTime.UtcNow;
            var today = ChinaClock.Today;
            var yesterday = today.AddDays(-1);
            var todayStartUtc = ChinaClock.DayStartUtc(today);
            var sameTimeYesterdayUtc = nowUtc.AddDays(-1);

            var (members, guests) = tracker.Snapshot();
            var names = await NamesAsync(members.Take(60).Select(m => m.UserId));

            var samples = await store.SamplesAsync(todayStartUtc, nowUtc.AddMinutes(1));
            // 今日峰值：取今天的在线快照与当前在线人数中的最大值
            var currentTotal = members.Count + guests;
            var peakTotal = currentTotal;
            var peakAtUtc = nowUtc;
            foreach (var s in samples)
            {
                var total = s.Members + s.Guests;
                if (total > peakTotal) { peakTotal = total; peakAtUtc = s.AtUtc; }
            }

            var activeToday = (await store.ActiveByDayAsync(today, today)).FirstOrDefault()?.Count ?? 0;
            var activeYesterdaySoFar = await store.ActiveBeforeAsync(yesterday, sameTimeYesterdayUtc);
            var activeYesterday = (await store.ActiveByDayAsync(yesterday, yesterday)).FirstOrDefault()?.Count ?? 0;
            var visitorsToday = (await store.VisitorsByDayAsync(today, today)).FirstOrDefault()?.Count ?? 0;

            var signedToday = await db.UserSignLogs.CountAsync(l => l.SignDate == today);
            var signedYesterdaySoFar = await db.UserSignLogs.CountAsync(l => l.SignDate == yesterday && l.CreatedAt < sameTimeYesterdayUtc);
            var signedYesterday = await db.UserSignLogs.CountAsync(l => l.SignDate == yesterday);

            var newToday = await db.Users.CountAsync(u => u.CreatedAt >= todayStartUtc);
            var newYesterdaySoFar = await db.Users.CountAsync(u => u.CreatedAt >= todayStartUtc.AddDays(-1) && u.CreatedAt < sameTimeYesterdayUtc);
            var newYesterday = await db.Users.CountAsync(u => u.CreatedAt >= todayStartUtc.AddDays(-1) && u.CreatedAt < todayStartUtc);
            var totalUsers = await db.Users.CountAsync();

            return Ok(new
            {
                now = ChinaClock.Now.ToString("yyyy-MM-dd HH:mm"),
                onlineWindowMinutes = (int)PresenceTracker.OnlineWindow.TotalMinutes,
                online = new { total = currentTotal, members = members.Count, guests },
                peak = new { total = peakTotal, at = ChinaClock.ToLocal(peakAtUtc).ToString("HH:mm") },
                onlineUsers = members.Take(60).Select(m => new
                {
                    id = m.UserId,
                    name = names.TryGetValue(m.UserId, out var n) ? n.Name : "（已注销）",
                    avatar = names.TryGetValue(m.UserId, out var a) ? a.Avatar : null,
                    secondsAgo = (int)(nowUtc - m.LastUtc).TotalSeconds,
                }),
                totalUsers,
                today = new { active = activeToday, signed = signedToday, newUsers = newToday, visitors = visitorsToday },
                yesterdaySoFar = new { active = activeYesterdaySoFar, signed = signedYesterdaySoFar, newUsers = newYesterdaySoFar },
                yesterday = new { active = activeYesterday, signed = signedYesterday, newUsers = newYesterday },
            });
        }

        /// <summary>
        /// 每日趋势：最近 N 天，以及紧挨着的前 N 天（用来对比）。
        /// 每天包含：活跃用户、游客、签到人数、新注册、在线峰值。trackingSince 之前没有活跃/在线数据（返回 null）。
        /// </summary>
        [HttpGet("trend")]
        public async Task<IActionResult> Trend([FromQuery] int days = 30)
        {
            days = Math.Clamp(days, 7, 180);
            var today = ChinaClock.Today;
            var start = today.AddDays(-(days - 1));
            var prevStart = start.AddDays(-days);
            var trackingSince = await store.TrackingSinceAsync();
            await store.FlushAsync(tracker);

            var active = (await store.ActiveByDayAsync(prevStart, today)).ToDictionary(x => x.Day, x => x.Count);
            var visitors = (await store.VisitorsByDayAsync(prevStart, today)).ToDictionary(x => x.Day, x => x.Count);
            var peaks = (await store.PeaksByDayAsync(prevStart, today)).ToDictionary(x => x.Day, x => x);
            var signed = (await db.UserSignLogs.AsNoTracking().Where(l => l.SignDate >= prevStart && l.SignDate <= today)
                    .GroupBy(l => l.SignDate).Select(g => new { Day = g.Key, Count = g.Count() }).ToListAsync())
                .ToDictionary(x => x.Day.Date, x => x.Count);
            var registered = await db.Users.AsNoTracking().Where(u => u.CreatedAt >= ChinaClock.DayStartUtc(prevStart))
                .Select(u => new { u.Id, u.CreatedAt }).ToListAsync();
            var newByDay = registered.GroupBy(u => ChinaClock.ToLocal(u.CreatedAt).Date).ToDictionary(g => g.Key, g => g.Count());

            bool Tracked(DateTime d) => trackingSince.HasValue && d >= trackingSince.Value;
            object Row(DateTime d) => new
            {
                date = D(d),
                active = Tracked(d) ? active.GetValueOrDefault(d) : (int?)null,
                visitors = Tracked(d) ? visitors.GetValueOrDefault(d) : (int?)null,
                peakOnline = peaks.TryGetValue(d, out var p) ? p.Total : (int?)null,
                signed = signed.GetValueOrDefault(d),
                newUsers = newByDay.GetValueOrDefault(d),
            };

            var current = Enumerable.Range(0, days).Select(i => Row(start.AddDays(i))).ToList();
            var previous = Enumerable.Range(0, days).Select(i => Row(prevStart.AddDays(i))).ToList();

            // 区间汇总：不重复活跃用户、不重复签到用户、新注册
            async Task<object> Summary(DateTime from, DateTime to)
            {
                var uniqueSigners = await db.UserSignLogs.Where(l => l.SignDate >= from && l.SignDate <= to).Select(l => l.UserId).Distinct().CountAsync();
                var fromUtc = ChinaClock.DayStartUtc(from);
                var toUtc = ChinaClock.DayStartUtc(to.AddDays(1));
                var tracked = trackingSince.HasValue && to >= trackingSince.Value;
                return new
                {
                    uniqueActive = tracked ? await store.UniqueActiveAsync(from, to) : (int?)null,
                    uniqueSigners,
                    newUsers = registered.Count(u => u.CreatedAt >= fromUtc && u.CreatedAt < toUtc),
                    partial = tracked && from < trackingSince!.Value,
                };
            }

            // 新用户留存：本区间注册的用户，注册后第 1 天回来的比例、7 天内回来的比例（只统计已满足观察期的用户）
            var cohort = registered.Where(u => ChinaClock.ToLocal(u.CreatedAt).Date >= start && Tracked(ChinaClock.ToLocal(u.CreatedAt).Date))
                .Select(u => (u.Id, Day: ChinaClock.ToLocal(u.CreatedAt).Date)).Take(3000).ToList();
            var activity = await store.ActiveDaysOfAsync(cohort.Select(c => c.Id).ToList(), start, today);
            var d1Base = cohort.Where(c => c.Day.AddDays(1) < today).ToList();
            var d7Base = cohort.Where(c => c.Day.AddDays(7) < today).ToList();
            int Returned(IEnumerable<(Guid Id, DateTime Day)> users, int window) =>
                users.Count(c => activity.TryGetValue(c.Id, out var set) && Enumerable.Range(1, window).Any(i => set.Contains(c.Day.AddDays(i))));

            return Ok(new
            {
                days,
                trackingSince = trackingSince.HasValue ? D(trackingSince.Value) : null,
                current,
                previous,
                summary = new { current = await Summary(start, today), previous = await Summary(prevStart, start.AddDays(-1)) },
                retention = new
                {
                    d1 = new { users = d1Base.Count, returned = Returned(d1Base, 1) },
                    d7 = new { users = d7Base.Count, returned = Returned(d7Base, 7) },
                },
            });
        }

        /// <summary>某天 24 小时的在线人数曲线（5 分钟一个点），默认同时给出前一天和上周同一天用于对比。</summary>
        [HttpGet("online-curve")]
        public async Task<IActionResult> OnlineCurve([FromQuery] string? date = null)
        {
            var day = DateTime.TryParse(date, out var parsed) ? parsed.Date : ChinaClock.Today;
            async Task<object> Curve(DateTime d)
            {
                var samples = await store.SamplesAsync(ChinaClock.DayStartUtc(d), ChinaClock.DayStartUtc(d.AddDays(1)));
                return new
                {
                    date = D(d),
                    points = samples.Select(s =>
                    {
                        var local = ChinaClock.ToLocal(s.AtUtc);
                        return new { minute = local.Hour * 60 + local.Minute, total = s.Members + s.Guests, members = s.Members };
                    }),
                };
            }
            return Ok(new { day = await Curve(day), previousDay = await Curve(day.AddDays(-1)), lastWeek = await Curve(day.AddDays(-7)) });
        }

        /// <summary>签到分析：连签分布、签到时段分布（近 30 天）、连签排行。</summary>
        [HttpGet("sign")]
        public async Task<IActionResult> Sign()
        {
            var today = ChinaClock.Today;
            var yesterday = today.AddDays(-1);

            // 还在延续中的连签（今天或昨天签过）
            var streaks = await db.UserStats.AsNoTracking()
                .Where(s => s.LastSignDate != null && s.LastSignDate >= yesterday && s.CurrentSignStreak > 0)
                .Select(s => new { s.UserId, s.CurrentSignStreak, s.MaxSignStreak }).ToListAsync();
            var buckets = new (string Label, int Min, int Max)[] { ("1 天", 1, 1), ("2–3 天", 2, 3), ("4–7 天", 4, 7), ("8–14 天", 8, 14), ("15–30 天", 15, 30), ("30 天以上", 31, int.MaxValue) };
            var distribution = buckets.Select(b => new { label = b.Label, count = streaks.Count(s => s.CurrentSignStreak >= b.Min && s.CurrentSignStreak <= b.Max) });

            var top = streaks.OrderByDescending(s => s.CurrentSignStreak).Take(10).ToList();
            var names = await NamesAsync(top.Select(t => t.UserId));

            // 签到时段：近 30 天每次签到发生在几点（中国时间）
            var since = today.AddDays(-29);
            var times = await db.UserSignLogs.AsNoTracking().Where(l => l.SignDate >= since).Select(l => l.CreatedAt).ToListAsync();
            var hours = new int[24];
            foreach (var t in times) hours[ChinaClock.ToLocal(t).Hour]++;

            // 星期分布：近 8 周每个星期几的平均签到人数
            var weekSince = today.AddDays(-55);
            var byDay = await db.UserSignLogs.AsNoTracking().Where(l => l.SignDate >= weekSince)
                .GroupBy(l => l.SignDate).Select(g => new { Day = g.Key, Count = g.Count() }).ToListAsync();
            var weekdays = Enumerable.Range(0, 7).Select(w =>
            {
                var dow = (DayOfWeek)((w + 1) % 7); // 周一开始
                var dates = Enumerable.Range(0, 56).Select(i => weekSince.AddDays(i)).Where(d => d.DayOfWeek == dow && d <= today).ToList();
                var total = byDay.Where(x => x.Day.DayOfWeek == dow).Sum(x => x.Count);
                return new { label = "一二三四五六日"[w].ToString(), average = dates.Count == 0 ? 0 : Math.Round((double)total / dates.Count, 1) };
            });

            return Ok(new
            {
                activeStreakUsers = streaks.Count,
                distribution,
                hours,
                weekdays,
                topStreaks = top.Select(t => new
                {
                    id = t.UserId,
                    name = names.TryGetValue(t.UserId, out var n) ? n.Name : "（已注销）",
                    avatar = names.TryGetValue(t.UserId, out var a) ? a.Avatar : null,
                    streak = t.CurrentSignStreak,
                    best = t.MaxSignStreak,
                }),
            });
        }
    
        // ================= 各板块内容统计 =================

        private sealed record ModuleStat(string Key, string Label, string Group, string? Note, long Total, long? Visible, List<DateTime> Times);

        /// <summary>
        /// 各板块的内容：总数、公开可见的数量、最近 N 天新增（和之前 N 天对比）、每天新增几条。
        /// 每个板块单独开一个数据库连接并行查（数据库在远处时一条查询要 ~250ms，串行十几个板块会很慢）。
        /// 某个板块查失败只影响它自己（返回 error），不影响其他板块。
        /// </summary>
        [HttpGet("content")]
        public async Task<IActionResult> Content([FromQuery] int days = 30)
        {
            days = Math.Clamp(days, 7, 180);
            var today = ChinaClock.Today;
            var start = today.AddDays(-(days - 1));
            var prevStart = start.AddDays(-days);
            var fromUtc = ChinaClock.DayStartUtc(prevStart);

            // 每个板块：(key, 名字, 分组, 说明, 查询)
            var jobs = new List<(string Key, string Label, string Group, string? Note, Func<AppDbContext, Task<(long Total, long? Visible, List<DateTime> Times)>> Run)>
            {
                ("post", "帖子", "论坛 / 博客", null, async c => (
                    await c.PublishedNotes.CountAsync(n => n.Type == "post"),
                    await LingMaiPublishController.VisiblePublishedNotes(c).CountAsync(n => n.Type == "post"),
                    await c.PublishedNotes.Where(n => n.Type == "post" && n.PublishedAt >= fromUtc).Select(n => n.PublishedAt).ToListAsync())),
                ("blog", "博客", "论坛 / 博客", null, async c => (
                    await c.PublishedNotes.CountAsync(n => n.Type == "blog"),
                    await LingMaiPublishController.VisiblePublishedNotes(c).CountAsync(n => n.Type == "blog"),
                    await c.PublishedNotes.Where(n => n.Type == "blog" && n.PublishedAt >= fromUtc).Select(n => n.PublishedAt).ToListAsync())),
                ("comment", "评论", "互动", "帖子、博客、画作、柴圈作品、岁月史书下面的评论", async c => (
                    await c.Comments.CountAsync(x => x.Content != "") + await c.CommunityHistoryComments.CountAsync(),
                    null,
                    (await c.Comments.Where(x => x.Content != "" && x.CreatedAt >= fromUtc).Select(x => x.CreatedAt).ToListAsync())
                        .Concat(await c.CommunityHistoryComments.Where(x => x.CreatedAt >= fromUtc).Select(x => x.CreatedAt).ToListAsync()).ToList())),
                ("artwork", "画廊作品", "画廊", "公开 = 审核通过且没下架", async c => (
                    await c.Artworks.CountAsync(),
                    await c.Artworks.CountAsync(a => a.IsApproved && (a.Status == "published" || a.Status == "" || a.Status == null)),
                    await c.Artworks.Where(a => a.UploadAt >= fromUtc).Select(a => a.UploadAt).ToListAsync())),
                ("wiki", "百科", "百科", "总数 = 词条；新增 = 编辑次数", async c => (
                    await c.WikiArticles.CountAsync(a => !a.IsDeleted),
                    await c.WikiArticles.CountAsync(a => !a.IsDeleted && a.CurrentRevisionId != null),
                    await c.WikiArticleRevisions.Where(r => r.CreatedAt >= fromUtc).Select(r => r.CreatedAt).ToListAsync())),
                ("world", "世界观", "世界观", "公开 = 设为公开的世界；新增 = 新建的世界", async c => (
                    await c.WorldProjects.CountAsync(),
                    await c.WorldProjects.CountAsync(w => w.IsPublic),
                    await c.WorldProjects.Where(w => w.CreatedAt >= fromUtc).Select(w => w.CreatedAt).ToListAsync())),
                ("world-card", "世界观条目", "世界观", "总数 = 条目；新增 = 新建 / 修改 / 删除次数", async c => (
                    await c.WorldCards.CountAsync(),
                    null,
                    await c.WorldCardRevisions.Where(r => r.Action != "baseline" && r.CreatedAt >= fromUtc).Select(r => r.CreatedAt).ToListAsync())),
                ("novel", "世界观小说章节", "世界观", "公开 = 公开小说里的章节", async c => (
                    await c.WorldNovelChapters.CountAsync(),
                    await c.WorldNovelChapters.CountAsync(ch => c.WorldNovels.Any(n => n.Id == ch.NovelId && n.IsPublic)),
                    await c.WorldNovelChapters.Where(ch => ch.CreatedAt >= fromUtc).Select(ch => ch.CreatedAt).ToListAsync())),
                ("chronicle", "编年史事件", "世界观", null, async c => (
                    await c.WorldTimelineEvents.CountAsync(),
                    await c.WorldTimelineEvents.CountAsync(e => !e.Hidden),
                    await c.WorldTimelineEvents.Where(e => e.CreatedAt >= fromUtc).Select(e => e.CreatedAt).ToListAsync())),
                ("history", "岁月史书", "社区", "公开 = 没被下架", async c => (
                    await c.CommunityHistoryEntries.CountAsync(),
                    await c.CommunityHistoryEntries.CountAsync(e => !e.Hidden),
                    await c.CommunityHistoryEntries.Where(e => e.CreatedAt >= fromUtc).Select(e => e.CreatedAt).ToListAsync())),
                ("chai-work", "柴圈作品", "柴圈", "公开 = 已发布", async c => (
                    await c.ChaiWorks.CountAsync(),
                    await c.ChaiWorks.CountAsync(w => w.Status == "published"),
                    await c.ChaiWorks.Where(w => w.CreatedAt >= fromUtc).Select(w => w.CreatedAt).ToListAsync())),
                ("oc", "OC 人设", "柴圈", "公开 = 已发布", async c => (
                    await c.StickmanCharacters.CountAsync(),
                    await c.StickmanCharacters.CountAsync(w => w.Status == "published"),
                    await c.StickmanCharacters.Where(w => w.CreatedAt >= fromUtc).Select(w => w.CreatedAt).ToListAsync())),
                ("quest", "世界任务报告", "我的世界", "玩家提交的报告（不含管理员直接记录）", async c => (
                    await c.ServerQuestContributions.CountAsync(x => x.Source == "player"),
                    null,
                    await c.ServerQuestContributions.Where(x => x.Source == "player" && x.CreatedAt >= fromUtc).Select(x => x.CreatedAt).ToListAsync())),
                ("signin", "签到", "社区", "签到次数", async c => (
                    await c.UserSignLogs.CountAsync(),
                    null,
                    await c.UserSignLogs.Where(l => l.CreatedAt >= fromUtc).Select(l => l.CreatedAt).ToListAsync())),
                ("like", "点赞 / 收藏", "互动", null, async c => (
                    await c.UserInteractions.CountAsync(x => x.ActionType == "Like" || x.ActionType == "Favorite"),
                    null,
                    await c.UserInteractions.Where(x => (x.ActionType == "Like" || x.ActionType == "Favorite") && x.CreatedAt >= fromUtc).Select(x => x.CreatedAt).ToListAsync())),
                ("follow", "关注", "互动", null, async c => (
                    await c.Follows.CountAsync(),
                    null,
                    await c.Follows.Where(f => f.CreatedAt >= fromUtc).Select(f => f.CreatedAt).ToListAsync())),
            };

            var tasks = jobs.Select(async j =>
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var c = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var (total, visible, times) = await j.Run(c);
                    return (Stat: new ModuleStat(j.Key, j.Label, j.Group, j.Note, total, visible, times), Error: (string?)null);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "内容统计 {Key} 失败", j.Key);
                    return (Stat: new ModuleStat(j.Key, j.Label, j.Group, j.Note, 0, null, new()), Error: "读取失败");
                }
            }).ToList();
            // 聊天室、联机对局是服务启动后自己建的表，用原始 SQL 读
            tasks.Add(RawModuleAsync("chat", "聊天室发言", "聊天室", "不含 AI 的发言、已删除的",
                "SELECT COUNT(*) FROM chat_messages WHERE IsAi=0 AND Deleted=0", "SELECT CreatedAt FROM chat_messages WHERE IsAi=0 AND Deleted=0 AND CreatedAt >= @from", fromUtc));
            tasks.Add(RawModuleAsync("game", "联机对局", "太虚游", "斗地主 / 象棋 / 围棋联机局（本地对局见「棋类记录」）",
                "SELECT COUNT(*) FROM game_matches", "SELECT EndedAt FROM game_matches WHERE EndedAt >= @from", fromUtc));
            var results = await Task.WhenAll(tasks);

            var dates = Enumerable.Range(0, days).Select(i => start.AddDays(i)).ToList();
            return Ok(new
            {
                days,
                dates = dates.Select(D),
                modules = results.Select(r =>
                {
                    var local = r.Stat.Times.Select(t => ChinaClock.ToLocal(DateTime.SpecifyKind(t, DateTimeKind.Utc)).Date).ToList();
                    var perDay = local.Where(d => d >= start).GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());
                    return new
                    {
                        key = r.Stat.Key,
                        label = r.Stat.Label,
                        group = r.Stat.Group,
                        note = r.Stat.Note,
                        total = r.Stat.Total,
                        visible = r.Stat.Visible,
                        current = local.Count(d => d >= start),
                        previous = local.Count(d => d >= prevStart && d < start),
                        today = local.Count(d => d == today),
                        series = dates.Select(d => perDay.GetValueOrDefault(d)),
                        error = r.Error,
                    };
                }),
            });
        }

        private async Task<(ModuleStat Stat, string? Error)> RawModuleAsync(string key, string label, string group, string? note, string totalSql, string timesSql, DateTime fromUtc)
        {
            try
            {
                await using var c = new MySqlConnection(configuration.GetConnectionString("DefaultConnection"));
                await c.OpenAsync();
                long total;
                await using (var cmd = c.CreateCommand()) { cmd.CommandText = totalSql; total = Convert.ToInt64(await cmd.ExecuteScalarAsync()); }
                var times = new List<DateTime>();
                await using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = timesSql;
                    cmd.Parameters.AddWithValue("@from", fromUtc);
                    await using var r = await cmd.ExecuteReaderAsync();
                    while (await r.ReadAsync()) if (!r.IsDBNull(0)) times.Add(r.GetDateTime(0));
                }
                return (new ModuleStat(key, label, group, note, total, null, times), null);
            }
            catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.NoSuchTable)
            {
                return (new ModuleStat(key, label, group, note, 0, null, new()), null); // 表还没建（还没人用过）
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "内容统计 {Key} 失败", key);
                return (new ModuleStat(key, label, group, note, 0, null, new()), "读取失败");
            }
        }
}
}
