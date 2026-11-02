using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.World;
using TaiChuWeb_V2.Services.Notify;

namespace TaiChuWeb_V2.Services.World;

/// <summary>删除时间线 / 事件时被「解开」的别的线：原来从哪条线分出、在哪件事分叉、在哪件事汇合（恢复时接回去）</summary>
public sealed class ChronicleLinkSnap
{
    public Guid TimelineId { get; set; }
    public Guid? ParentId { get; set; }
    public Guid? ForkEventId { get; set; }
    public Guid? MergeEventId { get; set; }
}

/// <summary>时间线某一时刻的完整内容</summary>
public sealed class ChronicleLineSnap
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string Status { get; set; } = WorldTimeline.StatusCanon;
    public Guid? ParentId { get; set; }
    public Guid? ForkEventId { get; set; }
    public Guid? MergeEventId { get; set; }
    public Guid? CardId { get; set; }
    public int SortOrder { get; set; }
    public bool Hidden { get; set; }
    /// <summary>只有删除记录才有：删掉时线上的全部事件（撤销删除时一起恢复）</summary>
    public List<ChronicleEventSnap>? Events { get; set; }
    /// <summary>只有删除记录才有：被解开的别的线</summary>
    public List<ChronicleLinkSnap>? Links { get; set; }
}

/// <summary>事件某一时刻的完整内容</summary>
public sealed class ChronicleEventSnap
{
    public Guid Id { get; set; }
    public Guid TimelineId { get; set; }
    public string Kind { get; set; } = WorldTimelineEvent.KindEvent;
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? EraId { get; set; }
    public long? Year { get; set; }
    public int? Month { get; set; }
    public int? Day { get; set; }
    public string? EndEraId { get; set; }
    public long? EndYear { get; set; }
    public int? EndMonth { get; set; }
    public int? EndDay { get; set; }
    public string? DateText { get; set; }
    public string? Color { get; set; }
    public List<string> Images { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public List<string> CardIds { get; set; } = new();
    public Guid? LocationCardId { get; set; }
    public bool Hidden { get; set; }
    public int SortOrder { get; set; }
    public Guid? CreatedBy { get; set; }
    /// <summary>只有删除记录才有：以它为分叉点 / 汇合点、删除时被解开的线</summary>
    public List<ChronicleLinkSnap>? Links { get; set; }
}

/// <summary>
/// 编年史修改记录：时间线、事件、历法的新建 / 修改 / 删除都记一笔（谁、什么时候、改了哪些字段、改前改后的完整内容），
/// 可以撤销某一次（修改 → 恢复旧内容；新建 → 删掉；删除 → 原样恢复，连同线上的事件和分叉汇合）。撤销本身也记一笔，可以再撤销回来。
/// 权限和条目修改记录一样：创建者和协作者能看，创建者和编写者能撤销（由控制器检查）。
/// 不走依赖注入，控制器里直接 new（和 NotificationService 一样），免得漏注册。
/// </summary>
public class WorldChronicleHistoryService(AppDbContext context, ILogger logger)
{
    public const string TargetTimeline = "timeline", TargetEvent = "event", TargetCalendar = "calendar";
    public const string Create = "create", Update = "update", Delete = "delete", Restore = "restore";

    // 空字段不写进 JSON，记录小一些；对比时「没有」和「空」一样
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull };
    private static readonly string[] Statuses = { WorldTimeline.StatusCanon, WorldTimeline.StatusAlt, WorldTimeline.StatusWhatIf, WorldTimeline.StatusDraft };
    private static readonly string[] Kinds = { WorldTimelineEvent.KindEvent, WorldTimelineEvent.KindPeriod, WorldTimelineEvent.KindSkip, WorldTimelineEvent.KindLost };
    // 对比「改了哪些字段」时不算的：Id 不会变；事件列表 / 解开的线只是删除记录的附带内容
    private static readonly HashSet<string> NotFields = new() { "id", "events", "links", "createdBy" };
    private static readonly Regex CardRef = new(@"!?\{CARD:([^}]+)\}");

    // ================= 快照 =================

    public static ChronicleLineSnap Snap(WorldTimeline t) => new()
    {
        Id = t.Id, Name = t.Name, Description = t.Description, Color = t.Color, Status = t.Status, ParentId = t.ParentId,
        ForkEventId = t.ForkEventId, MergeEventId = t.MergeEventId, CardId = t.CardId, SortOrder = t.SortOrder, Hidden = t.Hidden,
    };

    public static ChronicleEventSnap Snap(WorldTimelineEvent e) => new()
    {
        Id = e.Id, TimelineId = e.TimelineId, Kind = e.Kind, Title = e.Title, Description = e.Description,
        EraId = e.EraId, Year = e.Year, Month = e.Month, Day = e.Day, EndEraId = e.EndEraId, EndYear = e.EndYear, EndMonth = e.EndMonth, EndDay = e.EndDay,
        DateText = e.DateText, Color = e.Color, Images = ReadList(e.Images), Tags = ReadList(e.Tags), CardIds = ReadList(e.CardIds),
        LocationCardId = e.LocationCardId, Hidden = e.Hidden, SortOrder = e.SortOrder, CreatedBy = e.CreatedBy,
    };

    public static string ToJson(object snap) => JsonSerializer.Serialize(snap, snap.GetType(), Json);

    private static T? Parse<T>(string? json) where T : class
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<T>(json, Json); }
        catch (JsonException) { return null; }
    }

    private static List<string> ReadList(string? json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? new(); }
        catch (JsonException) { return new(); }
    }

    /// <summary>两份内容（JSON 对象）里不一样的顶层字段名；历法就是 eras / months / yearUnit……</summary>
    public static List<string> ChangedFields(string? beforeJson, string? afterJson)
    {
        if (beforeJson == null || afterJson == null) return new();
        try
        {
            if (JsonNode.Parse(beforeJson) is not JsonObject a || JsonNode.Parse(afterJson) is not JsonObject b) return new();
            return a.Select(kv => kv.Key).Union(b.Select(kv => kv.Key))
                .Where(k => !NotFields.Contains(k) && !JsonNode.DeepEquals(a[k], b[k])).ToList();
        }
        catch (JsonException) { return new(); }
    }

    // ================= 记录 =================

    /// <summary>
    /// 改完以后调用，记一笔。beforeJson / afterJson 是改前 / 改后的完整内容（新建没有 before，删除没有 after）。
    /// 修改了但其实什么都没变（原样保存）就不记。记录失败不影响正常编辑。
    /// </summary>
    public async Task RecordAsync(Guid projectId, string targetType, Guid targetId, Guid? userId, string action, string title,
        string? beforeJson, string? afterJson, string? note = null)
    {
        try
        {
            var fields = ChangedFields(beforeJson, afterJson);
            if (action == Update && fields.Count == 0) return;
            context.WorldChronicleRevisions.Add(new WorldChronicleRevision
            {
                ProjectId = projectId, TargetType = targetType, TargetId = targetId, UserId = userId, Action = action,
                Title = Clip(title ?? "", 200, ""), BeforeJson = beforeJson, AfterJson = afterJson,
                ChangedFields = fields.Count == 0 ? null : Clip(string.Join(",", fields), 500, ""),
                Note = note == null ? null : Clip(note, 200, ""), CreatedAt = DateTime.UtcNow,
            });
            await context.SaveChangesAsync();
        }
        catch (Exception ex) { Discard(); logger.LogWarning(ex, "保存编年史修改记录失败 {Type} {Id}", targetType, targetId); }
    }

    /// <summary>记录保存失败时，把还没存进去的记录 / 通知丢掉，免得拖累后面的正常保存</summary>
    private void Discard()
    {
        foreach (var entry in context.ChangeTracker.Entries()
                     .Where(e => e.State == EntityState.Added && (e.Entity is WorldChronicleRevision || e.Entity is TaiChuWeb_V2.Models.Notify.Notification)).ToList())
            entry.State = EntityState.Detached;
    }

    // ================= 删除（控制器和撤销共用，删之前把要恢复的东西全存下来） =================

    /// <summary>删除时间线：它的事件一起删；从它分出去的支线变成独立的线。返回删除前的完整内容（含事件、被解开的线）。</summary>
    public async Task<ChronicleLineSnap> DeleteTimelineCoreAsync(Guid projectId, WorldTimeline line)
    {
        var lineId = line.Id;
        var events = await context.WorldTimelineEvents.Where(e => e.ProjectId == projectId && e.TimelineId == lineId).ToListAsync();
        var eventIds = events.Select(e => e.Id).ToHashSet();
        var snap = Snap(line);
        snap.Events = events.OrderBy(e => e.SortOrder).ThenBy(e => e.CreatedAt).Select(e => Snap(e)).ToList();
        snap.Links = new();
        foreach (var other in await context.WorldTimelines.Where(t => t.ProjectId == projectId && t.Id != lineId).ToListAsync())
        {
            var link = new ChronicleLinkSnap { TimelineId = other.Id };
            var touched = false;
            if (other.ParentId == lineId)
            {
                link.ParentId = other.ParentId; link.ForkEventId = other.ForkEventId;
                other.ParentId = null; other.ForkEventId = null; touched = true;
            }
            if (other.ForkEventId is Guid f && eventIds.Contains(f)) { link.ForkEventId = f; other.ForkEventId = null; touched = true; }
            if (other.MergeEventId is Guid m && eventIds.Contains(m)) { link.MergeEventId = m; other.MergeEventId = null; touched = true; }
            if (touched) snap.Links.Add(link);
        }
        context.WorldTimelineEvents.RemoveRange(events);
        context.WorldTimelines.Remove(line);
        await context.SaveChangesAsync();
        return snap;
    }

    /// <summary>删除事件：以它为分叉点 / 汇合点的支线解开（支线本身保留）。返回删除前的完整内容。</summary>
    public async Task<ChronicleEventSnap> DeleteEventCoreAsync(Guid projectId, WorldTimelineEvent ev)
    {
        var id = ev.Id;
        var snap = Snap(ev);
        snap.Links = new();
        foreach (var t in await context.WorldTimelines.Where(t => t.ProjectId == projectId && (t.ForkEventId == id || t.MergeEventId == id)).ToListAsync())
        {
            snap.Links.Add(new ChronicleLinkSnap { TimelineId = t.Id, ForkEventId = t.ForkEventId == id ? id : null, MergeEventId = t.MergeEventId == id ? id : null });
            if (t.ForkEventId == id) t.ForkEventId = null;
            if (t.MergeEventId == id) t.MergeEventId = null;
        }
        context.WorldTimelineEvents.Remove(ev);
        await context.SaveChangesAsync();
        return snap;
    }

    // ================= 撤销 =================

    /// <summary>
    /// 撤销某一次操作：把对象改回「这次操作之前」的样子（之前不存在 → 删掉；之前存在 → 恢复成当时的内容，已删除的按原 Id 建回来）。
    /// 注意：之后别人对同一个对象的修改也会被一起覆盖（会再记一笔「撤销」，可以再撤销回来）。
    /// </summary>
    public async Task<string> UndoAsync(Guid projectId, long revisionId, Guid userId)
    {
        var rev = await context.WorldChronicleRevisions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == revisionId && r.ProjectId == projectId)
            ?? throw new WorldOperationException(WorldErrorKind.NotFound, "修改记录不存在");
        var actorName = rev.UserId.HasValue
            ? await context.Users.AsNoTracking().Where(u => u.Id == rev.UserId).Select(u => u.Username).FirstOrDefaultAsync() : null;
        var note = $"撤销了{(actorName != null ? $" {actorName} " : "")}的一次{ActionLabel(rev.Action)}";
        var message = rev.TargetType switch
        {
            TargetCalendar => await UndoCalendarAsync(projectId, rev, userId, note),
            TargetTimeline => await UndoTimelineAsync(projectId, rev, userId, note),
            TargetEvent => await UndoEventAsync(projectId, rev, userId, note),
            _ => throw new WorldOperationException(WorldErrorKind.Invalid, "这条修改记录没法撤销"),
        };
        var what = rev.TargetType == TargetCalendar ? "历法" : $"{TargetLabel(rev.TargetType)}《{Clip(await DisplayTitleAsync(projectId, rev.Title), 40)}》";
        await NotifyUndoneAsync(projectId, rev.UserId, userId, $"编年史里的一次{ActionLabel(rev.Action)}（{what}）");
        return message;
    }

    private async Task<string> UndoCalendarAsync(Guid projectId, WorldChronicleRevision rev, Guid userId, string note)
    {
        var row = await context.WorldCalendars.FirstOrDefaultAsync(c => c.ProjectId == projectId);
        var current = row?.Json;
        var target = rev.BeforeJson; // 空 = 这次之前还没设置过历法（用默认的公元历）
        if (target == null && row == null) throw new WorldOperationException(WorldErrorKind.Invalid, "历法已经是默认设置了，不用撤销");
        if (target == null) context.WorldCalendars.Remove(row!);
        else if (row == null) context.WorldCalendars.Add(new WorldCalendar { ProjectId = projectId, Json = target });
        else { row.Json = target; row.UpdatedAt = DateTime.UtcNow; }
        await context.SaveChangesAsync();
        await RecordAsync(projectId, TargetCalendar, projectId, userId, Restore, "历法", current, target, note);
        return target == null ? "已撤销：历法恢复成了默认设置" : "已撤销：历法恢复到了这次修改之前的样子";
    }

    private async Task<string> UndoTimelineAsync(Guid projectId, WorldChronicleRevision rev, Guid userId, string note)
    {
        var before = Parse<ChronicleLineSnap>(rev.BeforeJson);
        var line = await context.WorldTimelines.FirstOrDefaultAsync(t => t.Id == rev.TargetId && t.ProjectId == projectId);
        if (before == null)
        {
            // 这次是新建：撤销 = 删掉（删之前存下完整内容，还能再恢复）
            if (line == null) throw new WorldOperationException(WorldErrorKind.Invalid, "这条时间线已经不在了，不用撤销");
            var name = line.Name;
            var removed = await DeleteTimelineCoreAsync(projectId, line);
            await RecordAsync(projectId, TargetTimeline, rev.TargetId, userId, Restore, name, ToJson(removed), null, note);
            return $"已撤销：时间线《{name}》已删除{(removed.Events?.Count > 0 ? $"（连同上面的 {removed.Events!.Count} 件事）" : "")}，还能在修改记录里恢复";
        }
        var current = line == null ? null : ToJson(Snap(line));
        var (restoredLine, restoredEvents) = await ApplyTimelineAsync(projectId, rev.TargetId, before, line);
        await RecordAsync(projectId, TargetTimeline, rev.TargetId, userId, Restore, restoredLine.Name, current, ToJson(Snap(restoredLine)), note);
        return current == null
            ? $"已恢复：时间线《{restoredLine.Name}》{(restoredEvents > 0 ? $"和上面的 {restoredEvents} 件事" : "")}"
            : $"已撤销：时间线《{restoredLine.Name}》恢复到了这次修改之前的样子";
    }

    private async Task<string> UndoEventAsync(Guid projectId, WorldChronicleRevision rev, Guid userId, string note)
    {
        var before = Parse<ChronicleEventSnap>(rev.BeforeJson);
        var ev = await context.WorldTimelineEvents.FirstOrDefaultAsync(e => e.Id == rev.TargetId && e.ProjectId == projectId);
        if (before == null)
        {
            if (ev == null) throw new WorldOperationException(WorldErrorKind.Invalid, "这件事已经不在了，不用撤销");
            var title = ev.Title;
            var removed = await DeleteEventCoreAsync(projectId, ev);
            await RecordAsync(projectId, TargetEvent, rev.TargetId, userId, Restore, title, ToJson(removed), null, note);
            return $"已撤销：事件《{await DisplayTitleAsync(projectId, title)}》已删除，还能在修改记录里恢复";
        }
        if (!await context.WorldTimelines.AnyAsync(t => t.Id == before.TimelineId && t.ProjectId == projectId))
            throw new WorldOperationException(WorldErrorKind.Invalid, "这件事所在的时间线已经删掉了，请先在修改记录里恢复那条时间线");
        var current = ev == null ? null : ToJson(Snap(ev));
        var cardIds = await CardIdsAsync(projectId);
        if (ev == null)
        {
            ev = new WorldTimelineEvent { Id = rev.TargetId, ProjectId = projectId, CreatedBy = before.CreatedBy, CreatedAt = DateTime.UtcNow };
            context.WorldTimelineEvents.Add(ev);
        }
        CopyEvent(before, ev, cardIds);
        // 删除时被解开的分叉 / 汇合：那条线现在还空着的话接回来
        foreach (var link in before.Links ?? new())
        {
            var t = await context.WorldTimelines.FirstOrDefaultAsync(x => x.Id == link.TimelineId && x.ProjectId == projectId);
            if (t == null) continue;
            if (link.ForkEventId == ev.Id && t.ForkEventId == null) t.ForkEventId = ev.Id;
            if (link.MergeEventId == ev.Id && t.MergeEventId == null) t.MergeEventId = ev.Id;
        }
        await context.SaveChangesAsync();
        await RecordAsync(projectId, TargetEvent, ev.Id, userId, Restore, ev.Title, current, ToJson(Snap(ev)), note);
        var shown = await DisplayTitleAsync(projectId, ev.Title);
        return current == null ? $"已恢复：事件《{shown}》" : $"已撤销：事件《{shown}》恢复到了这次修改之前的样子";
    }

    /// <summary>把时间线改成快照里的样子（已删除就按原 Id 建回来，连同删除时线上的事件），删除时解开的支线也接回去</summary>
    private async Task<(WorldTimeline Line, int Events)> ApplyTimelineAsync(Guid projectId, Guid id, ChronicleLineSnap snap, WorldTimeline? line)
    {
        var lines = await context.WorldTimelines.Where(t => t.ProjectId == projectId).ToListAsync();
        var parents = lines.ToDictionary(t => t.Id, t => t.ParentId);
        var eventIds = (await context.WorldTimelineEvents.AsNoTracking().Where(e => e.ProjectId == projectId).Select(e => e.Id).ToListAsync()).ToHashSet();
        var cardIds = await CardIdsAsync(projectId);
        if (line == null)
        {
            line = new WorldTimeline { Id = id, ProjectId = projectId, CreatedAt = DateTime.UtcNow, SortOrder = snap.SortOrder };
            context.WorldTimelines.Add(line);
        }
        parents[id] = null;

        // 删除时存下的事件：还不在的按原 Id 重新建出来
        var restored = 0;
        foreach (var e in snap.Events ?? new())
        {
            if (eventIds.Contains(e.Id)) continue;
            var ev = new WorldTimelineEvent { Id = e.Id, ProjectId = projectId, CreatedBy = e.CreatedBy, CreatedAt = DateTime.UtcNow };
            CopyEvent(e, ev, cardIds);
            ev.TimelineId = id;
            context.WorldTimelineEvents.Add(ev);
            eventIds.Add(e.Id);
            restored++;
        }

        line.Name = string.IsNullOrWhiteSpace(snap.Name) ? "未命名时间线" : Clip(snap.Name.Trim(), 80, "");
        line.Description = snap.Description;
        line.Color = snap.Color;
        line.Status = Statuses.Contains(snap.Status) ? snap.Status : WorldTimeline.StatusCanon;
        line.Hidden = snap.Hidden;
        // 排序不动：还在的线保留现在的位置（之后可能又调过顺序），建回来的线在上面已经用了原来的位置
        line.CardId = snap.CardId is Guid c && cardIds.Contains(c) ? c : null;
        // 分叉：父线还在、不会绕成圈才接上；分叉点 / 汇合点的事件还在才接上
        line.ParentId = snap.ParentId is Guid p && p != id && parents.ContainsKey(p) && !WouldCycle(parents, id, p) ? p : null;
        parents[id] = line.ParentId;
        line.ForkEventId = snap.ForkEventId is Guid f && eventIds.Contains(f) ? f : null;
        line.MergeEventId = snap.MergeEventId is Guid m && eventIds.Contains(m) ? m : null;
        line.UpdatedAt = DateTime.UtcNow;

        // 删除时被解开的别的线：现在还空着的话重新接回来
        foreach (var link in snap.Links ?? new())
        {
            var other = lines.FirstOrDefault(t => t.Id == link.TimelineId);
            if (other == null || other.Id == id) continue;
            if (link.ParentId is Guid lp && other.ParentId == null && parents.ContainsKey(lp) && !WouldCycle(parents, other.Id, lp))
            {
                other.ParentId = lp;
                parents[other.Id] = lp;
            }
            if (link.ForkEventId is Guid lf && other.ForkEventId == null && eventIds.Contains(lf)) other.ForkEventId = lf;
            if (link.MergeEventId is Guid lm && other.MergeEventId == null && eventIds.Contains(lm)) other.MergeEventId = lm;
            other.UpdatedAt = DateTime.UtcNow;
        }
        await context.SaveChangesAsync();
        return (line, restored);
    }

    /// <summary>把快照里的事件内容写到实体上（关联条目只留这个世界里还存在的）</summary>
    private static void CopyEvent(ChronicleEventSnap s, WorldTimelineEvent ev, HashSet<Guid> cardIds)
    {
        ev.TimelineId = s.TimelineId;
        ev.Kind = Kinds.Contains(s.Kind) ? s.Kind : WorldTimelineEvent.KindEvent;
        ev.Title = Clip(s.Title ?? "", 500, "");
        ev.Description = s.Description;
        ev.EraId = s.EraId; ev.Year = s.Year; ev.Month = s.Month; ev.Day = s.Day;
        ev.EndEraId = s.EndEraId; ev.EndYear = s.EndYear; ev.EndMonth = s.EndMonth; ev.EndDay = s.EndDay;
        ev.DateText = s.DateText;
        ev.Color = s.Color;
        ev.Images = JsonSerializer.Serialize(s.Images ?? new());
        ev.Tags = JsonSerializer.Serialize(s.Tags ?? new());
        ev.CardIds = JsonSerializer.Serialize((s.CardIds ?? new()).Where(x => Guid.TryParse(x, out var g) && cardIds.Contains(g)).Distinct().ToList());
        ev.LocationCardId = s.LocationCardId is Guid l && cardIds.Contains(l) ? l : null;
        ev.Hidden = s.Hidden;
        ev.SortOrder = s.SortOrder;
        ev.UpdatedAt = DateTime.UtcNow;
    }

    private async Task<HashSet<Guid>> CardIdsAsync(Guid projectId) =>
        (await context.WorldCards.AsNoTracking().Where(c => c.ProjectId == projectId).Select(c => c.Id).ToListAsync()).ToHashSet();

    private static bool WouldCycle(Dictionary<Guid, Guid?> parents, Guid child, Guid parent)
    {
        var seen = new HashSet<Guid>();
        for (Guid? cur = parent; cur is Guid c && seen.Add(c); cur = parents.GetValueOrDefault(c))
            if (c == child) return true;
        return false;
    }

    // ================= 通知 =================

    private static string Link(Guid projectId) => $"/world/project/{projectId}?view=timeline&chronicleHistory=1";

    /// <summary>协作者删了一整条时间线时告诉创建者一声（线上可能有很多事），可以在修改记录里恢复</summary>
    public async Task NotifyOwnerOfDeleteAsync(Guid projectId, Guid userId, string what)
    {
        try
        {
            var project = await context.WorldProjects.AsNoTracking().Where(p => p.Id == projectId)
                .Select(p => new { p.OwnerId, p.Name }).FirstOrDefaultAsync();
            if (project == null || project.OwnerId == userId) return;
            new NotificationService(context).Add(project.OwnerId, userId, "world_edit", "world", projectId.ToString(),
                $"删除了世界观《{Clip(project.Name, 40)}》编年史里的{what}，可在编年史的「修改记录」里恢复", Link(projectId));
            await context.SaveChangesAsync();
        }
        catch (Exception ex) { Discard(); logger.LogWarning(ex, "发送编年史删除通知失败"); }
    }

    /// <summary>撤销了别人的修改时告诉 Ta（不然 Ta 会以为自己写的东西莫名没了）</summary>
    private async Task NotifyUndoneAsync(Guid projectId, Guid? targetUserId, Guid actorId, string what)
    {
        if (targetUserId is not Guid target || target == actorId) return;
        try
        {
            var name = await context.WorldProjects.AsNoTracking().Where(p => p.Id == projectId).Select(p => p.Name).FirstOrDefaultAsync() ?? "世界观";
            new NotificationService(context).Add(target, actorId, "world_undo", "world", projectId.ToString(),
                $"撤销了你在世界观《{Clip(name, 40)}》{what}", Link(projectId));
            await context.SaveChangesAsync();
        }
        catch (Exception ex) { Discard(); logger.LogWarning(ex, "发送编年史撤销通知失败"); }
    }

    // ================= 文字 =================

    public static string ActionLabel(string action) => action switch
    {
        Create => "新建", Update => "修改", Delete => "删除", Restore => "撤销 / 恢复", _ => "修改",
    };

    public static string TargetLabel(string type) => type switch
    {
        TargetTimeline => "时间线", TargetEvent => "事件", TargetCalendar => "历法", _ => "内容",
    };

    /// <summary>事件名里的 {CARD:id} 换成条目名，提示和通知里给人看</summary>
    public async Task<string> DisplayTitleAsync(Guid projectId, string title)
    {
        var ids = CardRef.Matches(title ?? "").Select(m => Guid.TryParse(m.Groups[1].Value, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty).Distinct().ToList();
        var names = ids.Count == 0 ? new Dictionary<Guid, string>() : await context.WorldCards.AsNoTracking()
            .Where(c => c.ProjectId == projectId && ids.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Title);
        return CardRef.Replace(title ?? "", m => Guid.TryParse(m.Groups[1].Value, out var g) && names.TryGetValue(g, out var n) ? n : "已删除的条目");
    }

    private static string Clip(string text, int max, string tail = "…") => text.Length > max ? text[..max] + tail : text;
}
