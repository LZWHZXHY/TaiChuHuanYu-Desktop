using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.World;
using static TaiChuWeb_V2.Services.World.WorldStatsCalc;

namespace TaiChuWeb_V2.Controllers.World;

/// <summary>
/// 世界观创作统计：词条树、字数、创作天数、连续创作、活跃日历、修改次数、谁写得最多、创作里程碑……
/// 创建者和协作者看完整统计；公开世界的访客（包括没登录的）看公开的那部分（mode = "public"）：
/// 只算访客能看到的内容，不显示是谁写的、不显示修改记录相关的东西。
/// 修改次数来自修改记录（WorldCardRevisions），开始记录之前建的条目按建立时间算一次「新建」。
/// 日期按北京时间分天。算法在 WorldStatsCalc，和后台里程碑检测共用。
/// </summary>
[Route("api/world")]
[Authorize]
public class WorldStatsController(AppDbContext context, IMemoryCache cache) : WorldControllerBase
{
    /// <summary>访客版统计缓存多久：访客多了不用每次都把整个世界算一遍</summary>
    private static readonly TimeSpan PublicCacheTime = TimeSpan.FromMinutes(10);
    private static string PublicCacheKey(Guid projectId) => $"world_stats_public_{projectId}";

    /// <summary>某个世界的创作统计（成员看完整的，公开世界的访客看公开的那部分）</summary>
    [HttpGet("projects/{projectId:guid}/stats")]
    [AllowAnonymous]
    public async Task<IActionResult> Project(Guid projectId)
    {
        var me = ReaderUserId;
        var project = await context.WorldProjects.AsNoTracking().Where(p => p.Id == projectId)
            .Select(p => new
            {
                p.Id, p.Name, p.OwnerId, p.CreatedAt, p.IsPublic,
                Member = me != Guid.Empty && (p.OwnerId == me || p.Collaborators.Any(c => c.UserId == me)),
            }).FirstOrDefaultAsync();
        // 私有世界对外人一律说「看不了」，不区分存不存在
        if (project == null || (!project.Member && !project.IsPublic))
            return StatusCode(403, new { message = "只有世界的创建者和协作者能看创作统计" });

        if (!project.Member)
        {
            // 访客：同一个世界所有访客看到的都一样，缓存 10 分钟
            // （每次都先查一下是不是还公开，所以取消公开后缓存不会漏出去）
            if (!cache.TryGetValue(PublicCacheKey(projectId), out object? cached) || cached == null)
            {
                cached = await PublicStatsAsync(projectId, project.Name, project.CreatedAt);
                cache.Set(PublicCacheKey(projectId), cached, PublicCacheTime);
            }
            return Ok(cached);
        }

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
            var m = Measure(c.Description, c.Attributes, c.TimelineEvents, c.Layout, c.GalleryImages, c.CoverImage, visitor: false);
            return new { c.Id, c.Title, Type = string.IsNullOrWhiteSpace(c.Type) ? "未分类" : c.Type, c.SubType, m.Words, m.Events, m.Images };
        }).ToList();

        // 活动：修改记录 +（开始记录之前建的条目按建立时间算一次新建）
        var activity = BuildActivity(
            revisions.Select(r => new WorldStatsCalc.Activity(r.CreatedAt, r.UserId, r.CardId, r.Action)),
            cards.Select(c => (c.Id, c.CreatedAt)), out var trackingSince, out var untracked);

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

        var words = cardInfo.Sum(c => (long)c.Words);
        var novelWords = novels.Sum(n => (long)n.WordCount);
        // 里程碑墙：成员看全部（达成的点亮，没达成的显示还差多少）
        var milestones = WorldMilestoneCatalog.ForPage(
            new WorldMilestoneCatalog.Metrics(cards.Count, words, novelWords, summary.LongestStreak, summary.CurrentStreak, summary.ActiveDays, project.CreatedAt),
            await WorldMilestoneStore.ReadAsync(context, projectId), member: true);

        return Ok(new
        {
            mode = "member",
            name = project.Name,
            createdAt = project.CreatedAt,
            // 世界建立到今天第几天（北京时间，建立当天算第 1 天）
            days = (Day(now) - Day(project.CreatedAt)).Days + 1,
            trackingSince,
            totals = new
            {
                cards = cards.Count,
                words,
                novelWords,
                novels = novels.Count,
                relations,
                timelineEvents = cardInfo.Sum(c => c.Events),
                images = cardInfo.Sum(c => c.Images),
                edits = revisions.Count,
                creates = revisions.Count(r => r.Action == WorldHistoryService.Create) + untracked,
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
            milestones,
        });
    }

    /// <summary>
    /// 访客版统计：词条数、设定字数（对访客隐藏的区块 / 时间线事件不算）、公开小说的字数、建立第几天、
    /// 创作天数、活跃日历（只有日期和次数）、最近更新时间、已达成的里程碑。
    /// 不给：各人贡献、修改次数、改得最多的词条、几点写、连续创作这些和修改记录、具体人有关的东西。
    /// </summary>
    private async Task<object> PublicStatsAsync(Guid projectId, string name, DateTime createdAt)
    {
        var cards = await context.WorldCards.AsNoTracking().Where(c => c.ProjectId == projectId)
            .Select(c => new { c.Id, c.Description, c.Attributes, c.TimelineEvents, c.Layout, c.CreatedAt })
            .ToListAsync();
        // 只要时间、条目和动作，不取是谁改的
        var revisions = await context.WorldCardRevisions.AsNoTracking()
            .Where(r => r.ProjectId == projectId && r.Action != WorldHistoryService.Baseline)
            .Select(r => new { r.CreatedAt, r.CardId, r.Action }).ToListAsync();
        var novels = await context.WorldNovels.AsNoTracking().Where(n => n.ProjectId == projectId && n.IsPublic)
            .Select(n => new { n.WordCount }).ToListAsync();

        var words = cards.Sum(c => (long)Measure(c.Description, c.Attributes, c.TimelineEvents, c.Layout, null, null, visitor: true).Words);
        var novelWords = novels.Sum(n => (long)n.WordCount);
        var activity = BuildActivity(
            revisions.Select(r => new WorldStatsCalc.Activity(r.CreatedAt, null, r.CardId, r.Action)),
            cards.Select(c => (c.Id, c.CreatedAt)), out _, out _);
        var summary = Summarize(activity);
        var now = DateTime.UtcNow;

        // 里程碑：只给已达成的；字数类的按访客能看到的字数再核一遍（见 WorldMilestoneCatalog.ForPage）
        var milestones = WorldMilestoneCatalog.ForPage(
            new WorldMilestoneCatalog.Metrics(cards.Count, words, novelWords, summary.LongestStreak, summary.CurrentStreak, summary.ActiveDays, createdAt),
            await WorldMilestoneStore.ReadAsync(context, projectId), member: false);

        return new
        {
            mode = "public",
            name,
            createdAt,
            days = (Day(now) - Day(createdAt)).Days + 1,
            // 这份统计是什么时候算的（有缓存，最多晚 10 分钟）
            generatedAt = now,
            totals = new
            {
                cards = cards.Count,
                words,
                novelWords,
                novels = novels.Count,
                summary.ActiveDays,
                lastActiveAt = activity.Count > 0 ? activity.Max(a => a.At) : (DateTime?)null,
            },
            heatmap = summary.Heatmap,
            milestones,
        };
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

        var activity = revisions.Select(r => new WorldStatsCalc.Activity(r.CreatedAt, me, r.CardId, r.Action)).ToList();
        activity.AddRange(ownedCards.Where(c => !trackingByWorld.TryGetValue(c.ProjectId, out var since) || c.CreatedAt < since)
            .Select(c => new WorldStatsCalc.Activity(c.CreatedAt, me, c.Id, WorldHistoryService.Create)));
        // 建世界那天也算创作
        activity.AddRange(worlds.Where(w => w.OwnerId == me).Select(w => new WorldStatsCalc.Activity(w.CreatedAt, me, Guid.Empty, "world")));

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
}
