using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace TaiChuWeb_V2.Services.World;

/// <summary>
/// 创作统计的公共算法：统计页（WorldStatsController）和后台里程碑检测（WorldMilestoneWorker）共用，
/// 保证两边算出来的字数、创作天数、连续创作一模一样（不然统计页显示 1 万字，里程碑却说没到）。
/// 日期按北京时间分天。
/// </summary>
public static class WorldStatsCalc
{
    public static readonly TimeSpan Beijing = TimeSpan.FromHours(8);
    private static readonly Regex CardRef = new(@"!?\{CARD:[0-9a-fA-F-]{36}\}", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    /// <summary>一次创作活动：什么时候、谁（开始记录前的老条目没有人）、哪个条目、做了什么</summary>
    public sealed record Activity(DateTime At, Guid? UserId, Guid CardId, string Action);

    public sealed record Summary(int ActiveDays, int CurrentStreak, int LongestStreak, int Last7, int Last30,
        List<object> Heatmap, int[] Hours, int[] Weekdays);

    /// <summary>一个条目的字数、时间线事件数、图片数</summary>
    public sealed record CardMeasure(int Words, int Events, int Images);

    /// <summary>
    /// 量一个条目：字数 = 正文 + 属性 + 时间线事件 + 自定义文本块。
    /// visitor = true 时只算访客能看到的：对访客隐藏的文本块、图片块、地图块和时间线事件都不算。
    /// </summary>
    public static CardMeasure Measure(string? description, string? attributes, string? timelineEvents, string? layout,
        string? galleryImages, string? coverImage, bool visitor)
    {
        var events = ArrayOf(timelineEvents).Where(e => !visitor || !IsHiddenEvent(e)).ToList();
        var blocks = WorldLayout.Blocks(layout).Where(b => !visitor || !b.Hidden).ToList();
        var images = ArrayOf(galleryImages).Count + (string.IsNullOrWhiteSpace(coverImage) ? 0 : 1)
            + blocks.Count(b => b.Kind is "media" or "map");
        var text = string.Join(" ", new[]
        {
            description ?? "",
            string.Join(" ", ArrayOf(attributes).Select(a => $"{Str(a, "value")}")),
            string.Join(" ", events.Select(e => $"{Str(e, "title")} {Str(e, "description")}")),
            string.Join(" ", blocks.Where(b => b.Kind == "text").Select(b => $"{b.Title} {b.Text}")),
        });
        return new CardMeasure(CountWords(text), events.Count, images);
    }

    /// <summary>
    /// 活动 = 修改记录 +（开始记录之前建的条目按建立时间算一次新建）。
    /// trackingSince：修改记录从什么时候开始有；untracked：按建立时间补算的老条目个数。
    /// </summary>
    public static List<Activity> BuildActivity(IEnumerable<Activity> revisions, IEnumerable<(Guid Id, DateTime CreatedAt)> cards,
        out DateTime? trackingSince, out int untracked)
    {
        var activity = revisions.ToList();
        DateTime? since = activity.Count > 0 ? activity.Min(r => r.At) : null;
        var old = cards.Where(c => since == null || c.CreatedAt < since.Value).ToList();
        activity.AddRange(old.Select(c => new Activity(c.CreatedAt, null, c.Id, WorldHistoryService.Create)));
        trackingSince = since;
        untracked = old.Count;
        return activity;
    }

    public static Summary Summarize(List<Activity> activity)
    {
        var today = Day(DateTime.UtcNow);
        var perDay = activity.GroupBy(a => Day(a.At)).ToDictionary(g => g.Key, g => g.Count());
        var days = perDay.Keys.OrderBy(d => d).ToList();

        // 连续创作：今天还没写也不算断（从昨天往前数）
        var current = 0;
        var cursor = perDay.ContainsKey(today) ? today : today.AddDays(-1);
        while (perDay.ContainsKey(cursor)) { current++; cursor = cursor.AddDays(-1); }
        var longest = 0;
        var run = 0;
        DateTime? prev = null;
        foreach (var d in days)
        {
            run = prev is DateTime p && (d - p).Days == 1 ? run + 1 : 1;
            longest = Math.Max(longest, run);
            prev = d;
        }

        var hours = new int[24];
        var weekdays = new int[7];
        foreach (var a in activity)
        {
            var local = DateTime.SpecifyKind(a.At, DateTimeKind.Utc).Add(Beijing);
            hours[local.Hour]++;
            weekdays[((int)local.DayOfWeek + 6) % 7]++; // 周一 = 0
        }
        return new Summary(
            days.Count, current, longest,
            perDay.Where(p => p.Key > today.AddDays(-7)).Sum(p => p.Value),
            perDay.Where(p => p.Key > today.AddDays(-30)).Sum(p => p.Value),
            // 活跃日历：全部有活动的日子（前端按「最近一年 / 某一年」切换）；只有日期和次数，没有人名
            perDay.OrderBy(p => p.Key)
                .Select(p => (object)new { d = p.Key.ToString("yyyy-MM-dd"), n = p.Value }).ToList(),
            hours, weekdays);
    }

    /// <summary>有创作的日子（北京时间日期，从早到晚），里程碑推算「哪天达成的」用</summary>
    public static List<DateTime> ActiveDayList(IEnumerable<Activity> activity) =>
        activity.Select(a => Day(a.At)).Distinct().OrderBy(d => d).ToList();

    /// <summary>北京时间的日期</summary>
    public static DateTime Day(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(Beijing).Date;

    /// <summary>北京时间某一天的中午，换成 UTC 存库（只知道哪天、不知道几点时用；取中午免得换时区后跑到前一天）</summary>
    public static DateTime NoonUtc(DateTime beijingDay) => DateTime.SpecifyKind(beijingDay.Date.AddHours(12) - Beijing, DateTimeKind.Utc);

    /// <summary>字数：去掉引用标记和空白后的字符数（中文一个字算一个）</summary>
    public static int CountWords(string? text) =>
        string.IsNullOrEmpty(text) ? 0 : Spaces.Replace(CardRef.Replace(text, ""), "").Length;

    public static List<JsonNode> ArrayOf(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return (JsonNode.Parse(json) as JsonArray)?.Where(n => n != null).Select(n => n!).ToList() ?? new(); }
        catch { return new(); }
    }

    public static string Str(JsonNode node, string key)
    {
        if (node is not JsonObject o) return node is JsonValue ? node.ToString() : "";
        // 前端存的是 camelCase，后端序列化的是 PascalCase，两种都认
        var v = o[key] ?? o[char.ToUpperInvariant(key[0]) + key[1..]];
        return v is JsonValue ? v.ToString() : "";
    }

    /// <summary>时间线事件是不是「对访客隐藏」</summary>
    private static bool IsHiddenEvent(JsonNode node) =>
        string.Equals(Str(node, "hidden"), "true", StringComparison.OrdinalIgnoreCase);
}
