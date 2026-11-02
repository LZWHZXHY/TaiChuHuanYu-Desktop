using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Notify;

namespace TaiChuWeb_V2.Services.World;

/// <summary>
/// 后台检测创作里程碑：每 30 分钟扫一遍「有变化的世界」，算一次统计，新达成的里程碑记下来，
/// 并给创建者和所有协作者发一条站内通知（同一次达成好几个合成一条）。
/// 不碰条目保存流程：靠「数据指纹」（条目数、最后修改时间、最新修改记录编号、小说数和字数）判断世界有没有变，没变的直接跳过。
///
/// 静默补记（避免一上线老世界一下收到一堆通知）：
/// 每种里程碑第一次被检测到的时间记在 WorldMilestoneKeys.AddedAt。一个世界的某个里程碑如果是「第一次被检查」
/// （这个世界从没扫过，或者上次扫描早于这种里程碑出现），而且世界建立在这种里程碑出现之前，
/// 就当作上线前已经达成：照样记下来（Silent = 1），但不发通知。以后新加里程碑种类也一样自动静默补记。
/// </summary>
public class WorldMilestoneWorker(IServiceScopeFactory scopes, ILogger<WorldMilestoneWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);
    /// <summary>启动后先等一会儿再扫，别和启动时的建表、缓存预热挤在一起</summary>
    private static readonly TimeSpan FirstDelay = TimeSpan.FromMinutes(2);
    public const string NotifyType = "world_milestone";

    public sealed class ScanRow
    {
        public Guid ProjectId { get; set; }
        public string Fingerprint { get; set; } = "";
        public DateTime ScannedAt { get; set; }
    }

    public sealed class KeyRow
    {
        public string MilestoneKey { get; set; } = "";
        public DateTime AddedAt { get; set; }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(FirstDelay, stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await ScanAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "创作里程碑检测失败，30 分钟后再试"); }
            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }

    private async Task ScanAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // 取整到秒：这个时间会写进库再读出来比较，去掉零头免得数据库四舍五入后比较出错
        var now = new DateTime(DateTime.UtcNow.Ticks / TimeSpan.TicksPerSecond * TimeSpan.TicksPerSecond, DateTimeKind.Utc);
        var today = WorldStatsCalc.Day(now);

        // 1. 登记里程碑种类（新种类第一次出现的时间 = 它的「上线时间」）
        var keys = await ReadKeysAsync(db, ct);
        var missing = WorldMilestoneCatalog.All.Where(d => !keys.ContainsKey(d.Key)).ToList();
        foreach (var def in missing)
        {
            var key = def.Key;
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT IGNORE INTO WorldMilestoneKeys (MilestoneKey, AddedAt) VALUES ({key}, {now})", ct);
        }
        if (missing.Count > 0) keys = await ReadKeysAsync(db, ct);
        // 最晚上线的里程碑种类：上次扫描比它早的世界都要重新检查一遍（让新种类静默补记）
        var latestKey = WorldMilestoneCatalog.All.Select(d => keys.TryGetValue(d.Key, out var t) ? t : now).DefaultIfEmpty(now).Max();

        // 2. 一次查出所有世界的「数据指纹」（分开几条按世界分组的查询，不做跨表关联）
        var worlds = await db.WorldProjects.AsNoTracking()
            .Select(p => new { p.Id, p.Name, p.OwnerId, p.CreatedAt }).ToListAsync(ct);
        var cardAgg = await db.WorldCards.AsNoTracking().GroupBy(c => c.ProjectId)
            .Select(g => new { ProjectId = g.Key, Count = g.Count(), Updated = g.Max(c => c.UpdatedAt) })
            .ToDictionaryAsync(x => x.ProjectId, ct);
        var revAgg = await db.WorldCardRevisions.AsNoTracking().GroupBy(r => r.ProjectId)
            .Select(g => new { ProjectId = g.Key, MaxId = g.Max(r => r.Id) })
            .ToDictionaryAsync(x => x.ProjectId, x => x.MaxId, ct);
        var novelAgg = (await db.WorldNovels.AsNoTracking().Where(n => n.ProjectId != null).GroupBy(n => n.ProjectId)
                .Select(g => new { ProjectId = g.Key, Count = g.Count(), Words = g.Sum(n => (long)n.WordCount), Updated = g.Max(n => n.UpdatedAt) })
                .ToListAsync(ct))
            .Where(x => x.ProjectId.HasValue).ToDictionary(x => x.ProjectId!.Value);
        var scans = new Dictionary<Guid, ScanRow>();
        foreach (var s in await db.Database.SqlQueryRaw<ScanRow>("SELECT ProjectId, Fingerprint, ScannedAt FROM WorldMilestoneScans").ToListAsync(ct))
            scans[s.ProjectId] = s;

        // 3. 有变化的世界逐个算
        var alive = new HashSet<Guid>();
        var scanned = 0;
        foreach (var w in worlds)
        {
            ct.ThrowIfCancellationRequested();
            alive.Add(w.Id);
            cardAgg.TryGetValue(w.Id, out var ca);
            revAgg.TryGetValue(w.Id, out var maxRev);
            novelAgg.TryGetValue(w.Id, out var na);
            var fingerprint = $"{ca?.Count ?? 0}|{ca?.Updated.Ticks ?? 0}|{maxRev}|{na?.Count ?? 0}|{na?.Words ?? 0}|{na?.Updated.Ticks ?? 0}";
            scans.TryGetValue(w.Id, out var scan);
            var need = scan == null || scan.Fingerprint != fingerprint || scan.ScannedAt < latestKey
                // 「建立满 1 年」这种不靠编辑，日子到了就算：上次扫描之后跨过了周年也要算一次
                || AgeCrossed(w.CreatedAt, scan.ScannedAt, today);
            if (!need) continue;
            try
            {
                await ScanWorldAsync(db, w.Id, w.Name, w.OwnerId, w.CreatedAt, scan?.ScannedAt, fingerprint, keys, now, ct);
                scanned++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex) { logger.LogWarning(ex, "检测世界 {World} 的创作里程碑失败", w.Id); }
            db.ChangeTracker.Clear();
            // 第一次上线要把所有世界都算一遍，稍微停一下，别把数据库压满
            await Task.Delay(20, ct);
        }

        // 4. 已经删掉的世界，记录也清掉
        foreach (var gone in scans.Keys.Where(id => !alive.Contains(id)).ToList())
        {
            var id = gone.ToString();
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM WorldMilestones WHERE ProjectId = {id}", ct);
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM WorldMilestoneScans WHERE ProjectId = {id}", ct);
        }
        if (scanned > 0) logger.LogInformation("创作里程碑：本轮检测了 {Count} 个有变化的世界", scanned);
    }

    private static async Task<Dictionary<string, DateTime>> ReadKeysAsync(AppDbContext db, CancellationToken ct)
    {
        var rows = await db.Database.SqlQueryRaw<KeyRow>("SELECT MilestoneKey, AddedAt FROM WorldMilestoneKeys").ToListAsync(ct);
        var result = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows) result[row.MilestoneKey] = row.AddedAt;
        return result;
    }

    /// <summary>上次扫描（不含那天）到今天之间，有没有跨过某个「建立满 N」的日子</summary>
    private static bool AgeCrossed(DateTime createdAt, DateTime lastScan, DateTime today)
    {
        var lastDay = WorldStatsCalc.Day(lastScan);
        return WorldMilestoneCatalog.All.Where(d => d.Kind == "age")
            .Select(d => WorldMilestoneCatalog.AgeDate(d, createdAt))
            .Any(date => date > lastDay && date <= today);
    }

    /// <summary>算一个世界：记下新达成的里程碑、更新扫描指纹、给成员发通知</summary>
    private async Task ScanWorldAsync(AppDbContext db, Guid projectId, string name, Guid ownerId, DateTime createdAt,
        DateTime? prevScan, string fingerprint, Dictionary<string, DateTime> keys, DateTime now, CancellationToken ct)
    {
        var today = WorldStatsCalc.Day(now);
        var pid = projectId.ToString();
        var cards = await db.WorldCards.AsNoTracking().Where(c => c.ProjectId == projectId)
            .Select(c => new { c.Id, c.Description, c.Attributes, c.TimelineEvents, c.Layout, c.CreatedAt }).ToListAsync(ct);
        var revisions = await db.WorldCardRevisions.AsNoTracking()
            .Where(r => r.ProjectId == projectId && r.Action != WorldHistoryService.Baseline)
            .Select(r => new { r.CreatedAt, r.UserId, r.CardId, r.Action }).ToListAsync(ct);
        var novelWords = await db.WorldNovels.AsNoTracking().Where(n => n.ProjectId == projectId).SumAsync(n => (long)n.WordCount, ct);

        // 和统计页（成员看到的）同一套算法
        long words = cards.Sum(c => (long)WorldStatsCalc.Measure(c.Description, c.Attributes, c.TimelineEvents, c.Layout, null, null, visitor: false).Words);
        var activity = WorldStatsCalc.BuildActivity(
            revisions.Select(r => new WorldStatsCalc.Activity(r.CreatedAt, r.UserId, r.CardId, r.Action)),
            cards.Select(c => (c.Id, c.CreatedAt)), out _, out _);
        var summary = WorldStatsCalc.Summarize(activity);
        var metrics = new WorldMilestoneCatalog.Metrics(cards.Count, words, novelWords, summary.LongestStreak, summary.CurrentStreak, summary.ActiveDays, createdAt);
        var recorded = await WorldMilestoneStore.ReadAsync(db, projectId, ct);

        var cardTimes = cards.Select(c => c.CreatedAt).OrderBy(t => t).ToList();
        var activeDays = WorldStatsCalc.ActiveDayList(activity);
        var fresh = new List<WorldMilestoneCatalog.Def>();
        foreach (var def in WorldMilestoneCatalog.All)
        {
            if (recorded.ContainsKey(def.Key) || !WorldMilestoneCatalog.Reached(def, metrics, today)) continue;
            // 第一次检查这个里程碑、世界又建在它上线之前 → 上线前就达成了，静默补记
            // （种类没登记上的也按静默处理：宁可少发，不要乱发）
            var silent = !keys.TryGetValue(def.Key, out var addedAt)
                || (prevScan is DateTime prev ? prev < addedAt : createdAt < addedAt);
            // 补记的尽量推算出哪天达成的；新达成的就是现在（最多晚 30 分钟）
            DateTime? achievedAt = silent ? Estimate(def, cardTimes, activeDays, createdAt) : now;
            var key = def.Key;
            var value = WorldMilestoneCatalog.Metric(def, metrics, today);
            // INSERT IGNORE + 主键：就算两台服务器同时在扫，也只有一台插得进去，通知只发一次
            var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT IGNORE INTO WorldMilestones (ProjectId, MilestoneKey, ReachedValue, AchievedAt, Silent, CreatedAt) VALUES ({pid}, {key}, {value}, {achievedAt}, {silent}, {now})", ct);
            if (inserted > 0 && !silent) fresh.Add(def);
        }

        await db.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO WorldMilestoneScans (ProjectId, Fingerprint, ScannedAt) VALUES ({pid}, {fingerprint}, {now})
ON DUPLICATE KEY UPDATE Fingerprint = VALUES(Fingerprint), ScannedAt = VALUES(ScannedAt)", ct);

        if (fresh.Count == 0) return;
        try
        {
            var collaborators = await db.WorldCollaborators.AsNoTracking().Where(c => c.ProjectId == projectId)
                .Select(c => c.UserId).ToListAsync(ct);
            var recipients = collaborators.Append(ownerId).Distinct().ToList();
            var titles = string.Join("、", fresh.Select(d => d.Title));
            var world = Clip(name, 40);
            var content = fresh.Count == 1
                ? $"世界观《{world}》达成了新的创作里程碑：{titles}！"
                : $"世界观《{world}》一下子达成了 {fresh.Count} 个创作里程碑：{titles}！";
            var notify = new NotificationService(db);
            foreach (var userId in recipients)
                notify.Add(userId, null, NotifyType, "world", pid, Clip(content, 190), $"/world/project/{projectId}?view=stats");
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 里程碑已经记下了，通知没发出去也不重试（不然可能重复发）
            logger.LogWarning(ex, "发送创作里程碑通知失败 world={World}", projectId);
        }
    }

    /// <summary>
    /// 推算「上线前就达成的」里程碑是哪天达成的：
    /// 第 N 个词条 = 现存条目里第 N 个建立的时间；累计创作 N 天 = 第 N 个有创作的日子；
    /// 连续创作 N 天 = 第一次连续到第 N 天的那天；建立满 N = 周年那天。字数推算不出来，留空（页面上显示「上线前已达成」）。
    /// </summary>
    private static DateTime? Estimate(WorldMilestoneCatalog.Def def, List<DateTime> cardTimes, List<DateTime> activeDays, DateTime createdAt)
    {
        var index = (int)Math.Min(int.MaxValue, def.Threshold) - 1;
        if (def.Kind == "cards")
            return index >= 0 && index < cardTimes.Count ? DateTime.SpecifyKind(cardTimes[index], DateTimeKind.Utc) : null;
        if (def.Kind == "active")
            return index >= 0 && index < activeDays.Count ? WorldStatsCalc.NoonUtc(activeDays[index]) : null;
        if (def.Kind == "streak")
        {
            var run = 0;
            DateTime? prev = null;
            foreach (var d in activeDays)
            {
                run = prev is DateTime p && (d - p).Days == 1 ? run + 1 : 1;
                if (run >= def.Threshold) return WorldStatsCalc.NoonUtc(d);
                prev = d;
            }
            return null;
        }
        if (def.Kind == "age") return WorldStatsCalc.NoonUtc(WorldMilestoneCatalog.AgeDate(def, createdAt));
        return null;
    }

    private static string Clip(string? text, int max)
    {
        text ??= "";
        return text.Length <= max ? text : text[..max] + "…";
    }
}
