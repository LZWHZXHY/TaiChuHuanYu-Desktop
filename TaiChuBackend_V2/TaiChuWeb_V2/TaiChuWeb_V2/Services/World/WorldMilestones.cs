using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.World;

/// <summary>
/// 创作里程碑：第 10 / 50 / 100…… 个词条、设定 1 万 / 5 万…… 字、连续创作 7 / 30…… 天、建立满 1 年……
/// 这里只放「有哪些里程碑、怎么算达没达成」，统计页和后台检测（WorldMilestoneWorker）共用。
/// 达成记录存在 WorldMilestones 表（WorldMilestoneSchema 启动时建），每个世界每个里程碑只记一次。
/// </summary>
public static class WorldMilestoneCatalog
{
    /// <summary>
    /// 一个里程碑。Kind：cards 词条数 / words 设定字数 / novel 小说字数 / streak 最长连续创作天数 /
    /// active 累计创作天数 / age 世界建立多久（Years &gt; 0 按周年算，否则按天数 Threshold 算）。
    /// Label 是徽章上的大字，Title 是完整名字（通知里也用它）。
    /// </summary>
    public sealed record Def(string Key, string Kind, long Threshold, string Title, string Label, int Years = 0);

    /// <summary>算里程碑要用到的数</summary>
    public sealed record Metrics(int Cards, long Words, long NovelWords, int LongestStreak, int CurrentStreak, int ActiveDays, DateTime CreatedAt);

    /// <summary>
    /// 全部里程碑（按种类、从小到大）。以后要加新的直接往这里加：新加的 Key 第一次被检测到时，
    /// 老世界早就达成的会静默补记（不发通知），见 WorldMilestoneWorker。Key 一旦上线就不要改。
    /// </summary>
    public static readonly IReadOnlyList<Def> All = Build();

    private static List<Def> Build()
    {
        var list = new List<Def>();
        foreach (var n in new long[] { 10, 50, 100, 200, 500, 1000 })
            list.Add(new Def($"cards_{n}", "cards", n, $"第 {n} 个词条", $"{n}"));
        foreach (var n in new long[] { 10_000, 50_000, 100_000, 300_000, 500_000, 1_000_000 })
            list.Add(new Def($"words_{n}", "words", n, $"设定 {Wan(n)} 字", Wan(n)));
        foreach (var n in new long[] { 10_000, 100_000, 500_000, 1_000_000 })
            list.Add(new Def($"novel_{n}", "novel", n, $"小说 {Wan(n)} 字", Wan(n)));
        foreach (var n in new long[] { 7, 30, 100, 365 })
            list.Add(new Def($"streak_{n}", "streak", n, $"连续创作 {n} 天", $"{n} 天"));
        foreach (var n in new long[] { 30, 100, 365, 1000 })
            list.Add(new Def($"active_{n}", "active", n, $"累计创作 {n} 天", $"{n} 天"));
        list.Add(new Def("age_100d", "age", 100, "建立满 100 天", "100 天"));
        foreach (var y in new[] { 1, 2, 3, 5 })
            list.Add(new Def($"age_{y}y", "age", 365L * y, $"建立满 {y} 年", $"{y} 年", y));
        return list;
    }

    /// <summary>10000 → 「1 万」，1000000 → 「100 万」</summary>
    private static string Wan(long n) => n % 10_000 == 0 ? $"{n / 10_000} 万" : $"{n / 10_000.0:0.#} 万";

    /// <summary>「建立满 N」的那一天（北京时间日期）。建立当天算第 1 天，满 100 天 = 建立日 + 100 天。</summary>
    public static DateTime AgeDate(Def def, DateTime createdAtUtc)
    {
        var start = WorldStatsCalc.Day(createdAtUtc);
        return def.Years > 0 ? start.AddYears(def.Years) : start.AddDays(def.Threshold);
    }

    /// <summary>这个里程碑对应的当前数值（age = 已经建立了几天）</summary>
    public static long Metric(Def def, Metrics m, DateTime today) => def.Kind switch
    {
        "cards" => m.Cards,
        "words" => m.Words,
        "novel" => m.NovelWords,
        "streak" => m.LongestStreak,
        "active" => m.ActiveDays,
        "age" => (today - WorldStatsCalc.Day(m.CreatedAt)).Days,
        _ => 0,
    };

    public static bool Reached(Def def, Metrics m, DateTime today) =>
        def.Kind == "age" ? today >= AgeDate(def, m.CreatedAt) : Metric(def, m, today) >= def.Threshold;

    /// <summary>还差多少（词条个数 / 字数 / 天数）。连续创作看的是「当前连续」还要几天。</summary>
    public static long Remaining(Def def, Metrics m, DateTime today) => Math.Max(0, def.Kind switch
    {
        "age" => (AgeDate(def, m.CreatedAt) - today).Days,
        "streak" => def.Threshold - m.CurrentStreak,
        _ => def.Threshold - Metric(def, m, today),
    });

    /// <summary>进度 0～1（灰着的徽章下面那条小进度条）</summary>
    public static double Progress(Def def, Metrics m, DateTime today)
    {
        double value, total;
        if (def.Kind == "age")
        {
            var start = WorldStatsCalc.Day(m.CreatedAt);
            value = (today - start).Days;
            total = Math.Max(1, (AgeDate(def, m.CreatedAt) - start).Days);
        }
        else
        {
            value = def.Kind == "streak" ? m.CurrentStreak : Metric(def, m, today);
            total = Math.Max(1, def.Threshold);
        }
        return Math.Round(Math.Clamp(value / total, 0, 1), 3);
    }

    /// <summary>给前端的一条里程碑</summary>
    public sealed record View(string Key, string Kind, string Title, string Label, long Threshold,
        bool Achieved, DateTime? AchievedAt, bool Backfilled, bool Pending, long Remaining, double Progress);

    /// <summary>
    /// 统计页上的里程碑墙。
    /// 成员：全部里程碑，达成的点亮；后台每 30 分钟才检测一次，刚达成还没记下来的也先点亮（Pending = 刚刚达成）。
    /// 访客：只给已记录达成的；字数类的还要访客能看到的字数也够才给（不然访客看到「设定 10 万字」，
    /// 公开字数却只有 8 万，等于把隐藏内容的多少漏出去了）。
    /// </summary>
    public static List<View> ForPage(Metrics m, IReadOnlyDictionary<string, WorldMilestoneStore.Row> recorded, bool member)
    {
        var today = WorldStatsCalc.Day(DateTime.UtcNow);
        var result = new List<View>();
        foreach (var def in All)
        {
            recorded.TryGetValue(def.Key, out var row);
            if (!member)
            {
                if (row == null) continue;
                if (def.Kind is "words" or "novel" && Metric(def, m, today) < def.Threshold) continue;
            }
            var reached = row != null || Reached(def, m, today);
            result.Add(new View(def.Key, def.Kind, def.Title, def.Label, def.Threshold,
                reached,
                row?.AchievedAt is DateTime at ? DateTime.SpecifyKind(at, DateTimeKind.Utc) : null,
                row != null && row.Silent != 0,
                row == null && reached,
                reached ? 0 : Remaining(def, m, today),
                reached ? 1 : Progress(def, m, today)));
        }
        return result;
    }
}

/// <summary>里程碑达成记录的读取（原生 SQL：表是启动时建的，不在 EF 模型里）</summary>
public static class WorldMilestoneStore
{
    /// <summary>WorldMilestones 的一行。Silent：1 = 上线时静默补记的（没发通知）；AchievedAt 为空 = 推算不出哪天达成的。</summary>
    public sealed class Row
    {
        public string MilestoneKey { get; set; } = "";
        public long ReachedValue { get; set; }
        public DateTime? AchievedAt { get; set; }
        public long Silent { get; set; }
    }

    /// <summary>某个世界已经达成的里程碑。表还没建好、读失败时返回空的（统计页照常显示，只是里程碑全灰）。</summary>
    public static async Task<Dictionary<string, Row>> ReadAsync(AppDbContext context, Guid projectId, CancellationToken cancellationToken = default)
    {
        try
        {
            var id = projectId.ToString();
            var rows = await context.Database.SqlQuery<Row>(
                    $"SELECT MilestoneKey, ReachedValue, AchievedAt, CAST(Silent AS SIGNED) AS Silent FROM WorldMilestones WHERE ProjectId = {id}")
                .ToListAsync(cancellationToken);
            var result = new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
            foreach (var row in rows) result[row.MilestoneKey] = row;
            return result;
        }
        catch (Exception) when (!cancellationToken.IsCancellationRequested)
        {
            return new Dictionary<string, Row>(StringComparer.OrdinalIgnoreCase);
        }
    }
}
