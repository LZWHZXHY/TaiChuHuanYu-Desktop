using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World;

/// <summary>
/// 世界观创作统计：词条树、字数、创作天数、连续创作、活跃日历、修改次数、谁写得最多……
/// 只有创建者和协作者能看。修改次数来自修改记录（WorldCardRevisions），开始记录之前建的条目按建立时间算一次「新建」。
/// 日期按北京时间分天。
/// </summary>
[Route("api/world")]
[Authorize]
public class WorldStatsController(AppDbContext context) : WorldControllerBase
{
    private static readonly TimeSpan Beijing = TimeSpan.FromHours(8);
    private static readonly Regex CardRef = new(@"!?\{CARD:[0-9a-fA-F-]{36}\}", RegexOptions.Compiled);
    private static readonly Regex Spaces = new(@"\s+", RegexOptions.Compiled);

    private sealed record Activity(DateTime At, Guid? UserId, Guid CardId, string Action);

    /// <summary>某个世界的创作统计</summary>
    [HttpGet("projects/{projectId:guid}/stats")]
    public async Task<IActionResult> Project(Guid projectId)
    {
        var me = CurrentUserId;
        var project = await context.WorldProjects.AsNoTracking().MemberOf(me).Where(p => p.Id == projectId)
            .Select(p => new { p.Id, p.Name, p.OwnerId, p.CreatedAt }).FirstOrDefaultAsync();
        if (project == null) return StatusCode(403, new { message = "只有世界的创建者和协作者能看创作统计" });

        var cards = await context.WorldCards.AsNoTracking().Where(c => c.ProjectId == projectId)
            .Select(c => new { c.Id, c.Title, c.Type, c.SubType, c.Description, c.Attributes, c.TimelineEvents, c.Layout, c.GalleryImages, c.CoverImage, c.CreatedAt, c.UpdatedAt })
            .ToListAsync();
        var cardIds = cards.Select(c => c.Id).ToList();
        var revisions = await context.WorldCardRevisions.AsNoTracking()
            .Where(r => r.ProjectId == projectId && r.Action != WorldHistoryService.Baseline)
            .Select(r => new { r.CreatedAt, r.UserId, r.CardId, r.Action, r.Title }).ToListAsync();
        var relations = await context.WorldRelations.AsNoTracking().CountAsync(r => cardIds.Contains(r.SourceCardId));
        var novels = await context.WorldNovels.AsNoTracking().Where(n => n.ProjectId == projectId)
            .Select(n => new { n.WordCount }).ToListAsync();

        // 每个条目的字数、配图、时间线事件
        var cardInfo = cards.Select(c =>
        {
            var events = ArrayOf(c.TimelineEvents);
            var images = ArrayOf(c.GalleryImages).Count + (string.IsNullOrWhiteSpace(c.CoverImage) ? 0 : 1)
                + WorldLayout.Blocks(c.Layout).Count(b => b.Kind is "media" or "map");
            var text = string.Join(" ", new[]
            {
                c.Description ?? "",
                string.Join(" ", ArrayOf(c.Attributes).Select(a => $"{Str(a, "value")}")),
                string.Join(" ", events.Select(e => $"{Str(e, "title")} {Str(e, "description")}")),
                string.Join(" ", WorldLayout.Blocks(c.Layout).Where(b => b.Kind == "text").Select(b => $"{b.Title} {b.Text}")),
            });
            return new { c.Id, c.Title, Type = string.IsNullOrWhiteSpace(c.Type) ? "未分类" : c.Type, c.SubType, Words = CountWords(text), Events = events.Count, Images = images };
        }).ToList();

        // 活动：修改记录 +（开始记录之前建的条目按建立时间算一次新建）
        var trackingSince = revisions.Count > 0 ? revisions.Min(r => r.CreatedAt) : (DateTime?)null;
        var activity = revisions.Select(r => new Activity(r.CreatedAt, r.UserId, r.CardId, r.Action)).ToList();
        var untracked = cards.Where(c => trackingSince == null || c.CreatedAt < trackingSince.Value).ToList();
        activity.AddRange(untracked.Select(c => new Activity(c.CreatedAt, null, c.Id, WorldHistoryService.Create)));

        var summary = Summarize(activity);
        var now = DateTime.UtcNow;

        // 谁写得最多（只算有记录的修改）
        var byUser = revisions.Where(r => r.UserId.HasValue).GroupBy(r => r.UserId!.Value).ToList();
        var userIds = byUser.Select(g => g.Key).ToList();
        var users = await context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, Avatar = u.Profile != null ? u.Profile.Avatar : null }).ToDictionaryAsync(u => u.Id);
        var contributors = byUser.Select(g => new
        {
            userId = g.Key,
            username = users.GetValueOrDefault(g.Key)?.Username,
            avatar = users.GetValueOrDefault(g.Key)?.Avatar,
            isOwner = g.Key == project.OwnerId,
            edits = g.Count(),
            creates = g.Count(r => r.Action == WorldHistoryService.Create),
            activeDays = g.Select(r => Day(r.CreatedAt)).Distinct().Count(),
            lastAt = g.Max(r => r.CreatedAt),
        }).OrderByDescending(x => x.edits).ToList();

        var alive = cardInfo.ToDictionary(c => c.Id);
        var topEdited = revisions.Where(r => r.Action == WorldHistoryService.Update).GroupBy(r => r.CardId)
            .Select(g => new { id = g.Key, title = alive.GetValueOrDefault(g.Key)?.Title ?? g.OrderByDescending(r => r.CreatedAt).First().Title, exists = alive.ContainsKey(g.Key), count = g.Count() })
            .OrderByDescending(x => x.count).Take(8).ToList();

        // 词条树：类型 → 子类型 → 条目
        var tree = cardInfo.GroupBy(c => c.Type).Select(g => new
        {
            type = g.Key,
            count = g.Count(),
            words = g.Sum(c => c.Words),
            subTypes = g.Where(c => !string.IsNullOrWhiteSpace(c.SubType)).GroupBy(c => c.SubType!.Trim())
                .Select(s => new { name = s.Key, count = s.Count() }).OrderByDescending(s => s.count).ToList(),
            cards = g.OrderByDescending(c => c.Words).Select(c => new { c.Id, c.Title, c.SubType, c.Words }).ToList(),
        }).OrderByDescending(t => t.count).ToList();

        return Ok(new
        {
            name = project.Name,
            createdAt = project.CreatedAt,
            // 世界建立到今天第几天（北京时间，建立当天算第 1 天）
            days = (Day(now) - Day(project.CreatedAt)).Days + 1,
            trackingSince,
            totals = new
            {
                cards = cards.Count,
                words = cardInfo.Sum(c => c.Words),
                novelWords = novels.Sum(n => n.WordCount),
                novels = novels.Count,
                relations,
                timelineEvents = cardInfo.Sum(c => c.Events),
                images = cardInfo.Sum(c => c.Images),
                edits = revisions.Count,
                creates = revisions.Count(r => r.Action == WorldHistoryService.Create) + untracked.Count,
                updates = revisions.Count(r => r.Action == WorldHistoryService.Update),
                deletes = revisions.Count(r => r.Action == WorldHistoryService.Delete),
                restores = revisions.Count(r => r.Action == WorldHistoryService.Restore),
                summary.ActiveDays, summary.CurrentStreak, summary.LongestStreak, summary.Last7, summary.Last30,
                lastActiveAt = activity.Count > 0 ? activity.Max(a => a.At) : (DateTime?)null,
            },
            tree,
            heatmap = summary.Heatmap,
            hours = summary.Hours,
            weekdays = summary.Weekdays,
            contributors,
            topEdited,
            longest = cardInfo.OrderByDescending(c => c.Words).Take(5).Select(c => new { c.Id, c.Title, c.Words }),
        });
    }

    /// <summary>我自己的创作档案：所有我参与的世界里我做的修改 + 我建的世界</summary>
    [HttpGet("stats/me")]
    public async Task<IActionResult> Me()
    {
        var me = CurrentUserId;
        var worlds = await context.WorldProjects.AsNoTracking().MemberOf(me)
            .Select(p => new { p.Id, p.Name, p.OwnerId, p.CreatedAt }).ToListAsync();
        var worldIds = worlds.Select(w => w.Id).ToList();
        var owned = worlds.Where(w => w.OwnerId == me).Select(w => w.Id).ToList();

        var revisions = await context.WorldCardRevisions.AsNoTracking()
            .Where(r => r.UserId == me && r.Action != WorldHistoryService.Baseline && worldIds.Contains(r.ProjectId))
            .Select(r => new { r.CreatedAt, r.CardId, r.Action, r.ProjectId }).ToListAsync();
        // 自己世界里、开始记录之前建的条目，也算自己的
        var trackingByWorld = await context.WorldCardRevisions.AsNoTracking().Where(r => owned.Contains(r.ProjectId))
            .GroupBy(r => r.ProjectId).Select(g => new { g.Key, Since = g.Min(r => r.CreatedAt) }).ToDictionaryAsync(x => x.Key, x => x.Since);
        var ownedCards = await context.WorldCards.AsNoTracking().Where(c => owned.Contains(c.ProjectId))
            .Select(c => new { c.Id, c.ProjectId, c.CreatedAt, c.Description, c.Layout }).ToListAsync();

        var activity = revisions.Select(r => new Activity(r.CreatedAt, me, r.CardId, r.Action)).ToList();
        activity.AddRange(ownedCards.Where(c => !trackingByWorld.TryGetValue(c.ProjectId, out var since) || c.CreatedAt < since)
            .Select(c => new Activity(c.CreatedAt, me, c.Id, WorldHistoryService.Create)));
        // 建世界那天也算创作
        activity.AddRange(worlds.Where(w => w.OwnerId == me).Select(w => new Activity(w.CreatedAt, me, Guid.Empty, "world")));

        var summary = Summarize(activity);
        var firstAt = activity.Count > 0 ? activity.Min(a => a.At) : (DateTime?)null;
        var perWorld = revisions.GroupBy(r => r.ProjectId).ToDictionary(g => g.Key, g => g.Count());
        return Ok(new
        {
            firstAt,
            // 从第一次创作到今天第几天
            days = firstAt is DateTime f ? (Day(DateTime.UtcNow) - Day(f)).Days + 1 : 0,
            totals = new
            {
                worlds = owned.Count,
                joined = worlds.Count - owned.Count,
                cards = ownedCards.Count,
                words = ownedCards.Sum(c => CountWords($"{c.Description} {string.Join(" ", WorldLayout.Blocks(c.Layout).Where(b => b.Kind == "text").Select(b => $"{b.Title} {b.Text}"))}")),
                edits = revisions.Count,
                creates = activity.Count(a => a.Action == WorldHistoryService.Create),
                summary.ActiveDays, summary.CurrentStreak, summary.LongestStreak, summary.Last7, summary.Last30,
            },
            heatmap = summary.Heatmap,
            hours = summary.Hours,
            weekdays = summary.Weekdays,
            worlds = worlds.Select(w => new { w.Id, w.Name, isOwner = w.OwnerId == me, edits = perWorld.GetValueOrDefault(w.Id) })
                .OrderByDescending(w => w.edits).ToList(),
        });
    }

    // ---------------- 计算 ----------------

    private sealed record Summary(int ActiveDays, int CurrentStreak, int LongestStreak, int Last7, int Last30,
        List<object> Heatmap, int[] Hours, int[] Weekdays);

    private static Summary Summarize(List<Activity> activity)
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
            var local = a.At.Add(Beijing);
            hours[local.Hour]++;
            weekdays[((int)local.DayOfWeek + 6) % 7]++; // 周一 = 0
        }
        return new Summary(
            days.Count, current, longest,
            perDay.Where(p => p.Key > today.AddDays(-7)).Sum(p => p.Value),
            perDay.Where(p => p.Key > today.AddDays(-30)).Sum(p => p.Value),
            // 活跃日历：全部有活动的日子（前端按「最近一年 / 某一年」切换）
            perDay.OrderBy(p => p.Key)
                .Select(p => (object)new { d = p.Key.ToString("yyyy-MM-dd"), n = p.Value }).ToList(),
            hours, weekdays);
    }

    /// <summary>北京时间的日期</summary>
    private static DateTime Day(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).Add(Beijing).Date;

    /// <summary>字数：去掉引用标记和空白后的字符数（中文一个字算一个）</summary>
    private static int CountWords(string? text) =>
        string.IsNullOrEmpty(text) ? 0 : Spaces.Replace(CardRef.Replace(text, ""), "").Length;

    private static List<JsonNode> ArrayOf(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try { return (JsonNode.Parse(json) as JsonArray)?.Where(n => n != null).Select(n => n!).ToList() ?? new(); }
        catch { return new(); }
    }

    private static string Str(JsonNode node, string key)
    {
        if (node is not JsonObject o) return node is JsonValue ? node.ToString() : "";
        // 前端存的是 camelCase，旧数据可能是 PascalCase
        var v = o[key] ?? o[char.ToUpperInvariant(key[0]) + key[1..]];
        return v is JsonValue ? v.ToString() : "";
    }
}
