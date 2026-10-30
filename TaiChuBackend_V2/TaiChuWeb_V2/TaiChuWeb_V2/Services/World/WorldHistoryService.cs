using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.World;
using TaiChuWeb_V2.Services.Notify;

namespace TaiChuWeb_V2.Services.World;

/// <summary>条目某一时刻的完整内容（各字段直接存数据库里的原始 JSON 字符串）</summary>
public sealed class WorldCardSnapshot
{
    public string Title { get; set; } = "";
    public string Type { get; set; } = "";
    public string? SubType { get; set; }
    public string? CoverImage { get; set; }
    public string GalleryImages { get; set; } = "[]";
    public string Aliases { get; set; } = "[]";
    public string Attributes { get; set; } = "[]";
    public string? Description { get; set; }
    public string ContentBlocks { get; set; } = "[]";
    public string TimelineEvents { get; set; } = "[]";
    public string Tags { get; set; } = "[]";
    public string EmbeddedCards { get; set; } = "[]";
    public string Content { get; set; } = "{}";
    /// <summary>区块排列（fix80 之前的记录没有这一项）</summary>
    public string? Layout { get; set; }
    /// <summary>这个条目指向别的条目的关系</summary>
    public List<SnapshotRelation> Out { get; set; } = new();
    /// <summary>别的条目指向这个条目的关系（删除后恢复时一起补回）</summary>
    public List<SnapshotRelation> In { get; set; } = new();
}

public sealed record SnapshotRelation(Guid CardId, string RelationType);

/// <summary>
/// 世界观条目修改记录：新建 / 修改 / 删除时记下操作人和完整内容，可以查看、撤销某次修改，
/// 创建者还可以一键撤销某位协作者一段时间内的全部修改（防止恶意破坏）。
/// </summary>
public class WorldHistoryService(AppDbContext context, IMemoryCache cache, ILogger<WorldHistoryService> logger)
{
    public const string Create = "create", Update = "update", Delete = "delete", Restore = "restore", Baseline = "baseline";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static WorldCardSnapshot Snap(WorldCard card, IEnumerable<WorldRelation> outRelations, IEnumerable<WorldRelation> inRelations) => new()
    {
        Title = card.Title, Type = card.Type, SubType = card.SubType, CoverImage = card.CoverImage, GalleryImages = card.GalleryImages,
        Aliases = card.Aliases, Attributes = card.Attributes, Description = card.Description, ContentBlocks = card.ContentBlocks,
        TimelineEvents = card.TimelineEvents, Tags = card.Tags, EmbeddedCards = card.EmbeddedCards, Content = card.Content,
        Layout = card.Layout,
        Out = outRelations.Select(r => new SnapshotRelation(r.TargetCardId, r.RelationType)).ToList(),
        In = inRelations.Select(r => new SnapshotRelation(r.SourceCardId, r.RelationType)).ToList(),
    };

    public static WorldCardSnapshot? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<WorldCardSnapshot>(json, Json); }
        catch (JsonException) { return null; }
    }

    private async Task<WorldCardSnapshot?> LoadSnapshotAsync(Guid cardId)
    {
        var card = await context.WorldCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cardId);
        if (card == null) return null;
        var outs = await context.WorldRelations.AsNoTracking().Where(r => r.SourceCardId == cardId).ToListAsync();
        var ins = await context.WorldRelations.AsNoTracking().Where(r => r.TargetCardId == cardId).ToListAsync();
        return Snap(card, outs, ins);
    }

    private void Add(Guid projectId, Guid cardId, Guid? userId, string action, WorldCardSnapshot snap, string? note) =>
        context.WorldCardRevisions.Add(new WorldCardRevision
        {
            ProjectId = projectId, CardId = cardId, UserId = userId, Action = action,
            Title = snap.Title.Length > 200 ? snap.Title[..200] : snap.Title,
            Snapshot = JsonSerializer.Serialize(snap, Json), Note = note?.Length > 200 ? note[..200] : note, CreatedAt = DateTime.UtcNow,
        });

    /// <summary>
    /// 改之前调用：这个条目还没有任何记录（开启记录前就存在的老条目）时，先把现在的样子存成「记录起点」，
    /// 这样第一次修改也能撤销。失败不影响正常编辑。
    /// </summary>
    public Task EnsureBaselineAsync(Guid projectId, Guid cardId) => EnsureBaselineCoreAsync(projectId, cardId, null);

    /// <summary>已经把条目（含出入关系）读出来了就直接用，不用再查一遍数据库</summary>
    public Task EnsureBaselineAsync(WorldCard card) =>
        EnsureBaselineCoreAsync(card.ProjectId, card.Id, () => Snap(card, card.OutRelations, card.InRelations));

    // 确认过有记录的条目记在内存里，之后每次保存都不用再查（数据库很远时每次查询都要几百毫秒）
    private static string BaselineKey(Guid cardId) => "world-history-baselined:" + cardId;

    private async Task EnsureBaselineCoreAsync(Guid projectId, Guid cardId, Func<WorldCardSnapshot>? snapshot)
    {
        if (cache.TryGetValue(BaselineKey(cardId), out _)) return;
        try
        {
            if (!await context.WorldCardRevisions.AsNoTracking().AnyAsync(r => r.CardId == cardId))
            {
                var snap = snapshot?.Invoke() ?? await LoadSnapshotAsync(cardId);
                if (snap == null) return;
                Add(projectId, cardId, null, Baseline, snap, "开启修改记录前的内容");
                await context.SaveChangesAsync();
            }
            cache.Set(BaselineKey(cardId), true, TimeSpan.FromHours(6));
        }
        catch (Exception ex) { Discard(); logger.LogWarning(ex, "保存条目修改记录起点失败 card={Card}", cardId); }
    }

    /// <summary>改完以后调用：按数据库里的最新内容记一笔。失败不影响正常编辑。</summary>
    public async Task RecordAsync(Guid projectId, Guid cardId, Guid userId, string action, string? note = null)
    {
        try
        {
            var snap = await LoadSnapshotAsync(cardId);
            if (snap == null) return;
            Add(projectId, cardId, userId, action, snap, note);
            await context.SaveChangesAsync();
            cache.Set(BaselineKey(cardId), true, TimeSpan.FromHours(6));
        }
        catch (Exception ex) { Discard(); logger.LogWarning(ex, "保存条目修改记录失败 card={Card}", cardId); return; }
        // 协作者新建 / 修改条目：通知创建者（同一个人在同一个世界一小时内合并成一条）
        if (action is Create or Update) await NotifyOwnerOfEditsAsync(projectId, userId);
    }

    /// <summary>
    /// 同上，但直接用已经读出来、刚保存过的条目（含出入关系）做快照，省掉 3 次数据库查询；
    /// 知道创建者是谁时也不用再查（创建者自己改的不发通知）。
    /// </summary>
    public async Task RecordAsync(WorldCard card, Guid userId, string action, Guid? ownerId = null, string? worldName = null, string? note = null)
    {
        try
        {
            Add(card.ProjectId, card.Id, userId, action, Snap(card, card.OutRelations, card.InRelations), note);
            await context.SaveChangesAsync();
            cache.Set(BaselineKey(card.Id), true, TimeSpan.FromHours(6));
        }
        catch (Exception ex) { Discard(); logger.LogWarning(ex, "保存条目修改记录失败 card={Card}", card.Id); return; }
        if (action is Create or Update && ownerId != userId) await NotifyOwnerOfEditsAsync(card.ProjectId, userId, ownerId, worldName);
    }

    public const string EditBatchType = "world_edits";

    /// <summary>
    /// 合并通知：一小时内同一位协作者在同一个世界的新建 / 修改合成一条，
    /// 内容按修改记录实时算：「在世界观《X》里新建了 2 个、修改了 5 个条目」。创建者已读后再有修改就开新的一条。
    /// </summary>
    private async Task NotifyOwnerOfEditsAsync(Guid projectId, Guid userId, Guid? knownOwner = null, string? knownName = null)
    {
        if (knownOwner == userId) return;
        try
        {
            var project = knownOwner is Guid o && knownName != null ? new { OwnerId = o, Name = knownName }
                : await context.WorldProjects.AsNoTracking().Where(p => p.Id == projectId)
                    .Select(p => new { p.OwnerId, p.Name }).FirstOrDefaultAsync();
            if (project == null || project.OwnerId == userId) return;
            var target = projectId.ToString();
            var since = DateTime.UtcNow.AddHours(-1);
            var existing = await context.Notifications
                .Where(n => n.UserId == project.OwnerId && n.ActorId == userId && n.Type == EditBatchType && n.TargetId == target && !n.IsRead && n.CreatedAt >= since)
                .OrderByDescending(n => n.CreatedAt).FirstOrDefaultAsync();
            var windowStart = existing?.CreatedAt ?? DateTime.UtcNow.AddSeconds(-5);
            var counts = await context.WorldCardRevisions.AsNoTracking()
                .Where(r => r.ProjectId == projectId && r.UserId == userId && r.CreatedAt >= windowStart && (r.Action == Create || r.Action == Update))
                .GroupBy(r => r.Action).Select(g => new { Action = g.Key, Cards = g.Select(r => r.CardId).Distinct().Count() }).ToListAsync();
            var created = counts.FirstOrDefault(c => c.Action == Create)?.Cards ?? 0;
            var updated = counts.FirstOrDefault(c => c.Action == Update)?.Cards ?? 0;
            var parts = new List<string>();
            if (created > 0) parts.Add($"新建了 {created} 个");
            if (updated > 0) parts.Add($"修改了 {updated} 个");
            if (parts.Count == 0) return;
            var content = $"在世界观《{Clip(project.Name, 40)}》里{string.Join("、", parts)}条目";
            if (existing != null) existing.Content = content;
            else new NotificationService(context).Add(project.OwnerId, userId, EditBatchType, "world", target, content,
                $"/world/project/{projectId}?history=1&user={userId}");
            await context.SaveChangesAsync();
        }
        catch (Exception ex) { Discard(); logger.LogWarning(ex, "发送世界观修改通知失败"); }
    }

    /// <summary>撤销了别人的修改时告诉 Ta（不然 Ta 会以为自己写的东西莫名没了）</summary>
    private async Task NotifyUndoneAsync(Guid projectId, Guid? targetUserId, Guid actorId, string what)
    {
        if (targetUserId is not Guid target || target == actorId) return;
        try
        {
            var name = await context.WorldProjects.AsNoTracking().Where(p => p.Id == projectId).Select(p => p.Name).FirstOrDefaultAsync() ?? "世界观";
            new NotificationService(context).Add(target, actorId, "world_undo", "world", projectId.ToString(),
                $"撤销了你在世界观《{Clip(name, 40)}》里的{what}", $"/world/project/{projectId}?history=1&user={target}");
            await context.SaveChangesAsync();
        }
        catch (Exception ex) { Discard(); logger.LogWarning(ex, "发送撤销通知失败"); }
    }

    /// <summary>删除前调用：先单独保存删除前的完整内容，之后可以原样恢复。记录失败也不拦着删除。</summary>
    public async Task RecordDeleteAsync(WorldCard card, IEnumerable<WorldRelation> outRelations, IEnumerable<WorldRelation> inRelations, Guid userId)
    {
        try
        {
            Add(card.ProjectId, card.Id, userId, Delete, Snap(card, outRelations, inRelations), null);
            // 协作者删条目时告诉创建者一声
            var ownerId = card.Project?.OwnerId;
            if (ownerId.HasValue && ownerId.Value != userId)
                new NotificationService(context).Add(ownerId.Value, userId, "world_edit", "world", card.ProjectId.ToString(),
                    $"删除了世界观《{Clip(card.Project!.Name, 40)}》里的条目《{Clip(card.Title, 40)}》，可在「修改记录」里恢复", $"/world/project/{card.ProjectId}?history=1");
            await context.SaveChangesAsync();
        }
        catch (Exception ex) { Discard(); logger.LogWarning(ex, "保存删除记录失败 card={Card}", card.Id); }
    }

    /// <summary>记录保存失败时，把还没存进去的记录 / 通知丢掉，免得拖累后面的正常保存</summary>
    private void Discard()
    {
        foreach (var entry in context.ChangeTracker.Entries()
                     .Where(e => e.State == EntityState.Added && (e.Entity is WorldCardRevision || e.Entity is TaiChuWeb_V2.Models.Notify.Notification)).ToList())
            entry.State = EntityState.Detached;
    }

    private static string Clip(string text, int max) => text.Length > max ? text[..max] + "…" : text;

    /// <summary>某条记录之前，这个条目的样子；null = 之前不存在（这次是新建）或没有记录</summary>
    private async Task<(bool Existed, WorldCardSnapshot? Snap)> StateBeforeAsync(WorldCardRevision revision)
    {
        if (revision.Action == Create) return (false, null);
        var previous = await context.WorldCardRevisions.AsNoTracking()
            .Where(r => r.CardId == revision.CardId && r.Id < revision.Id).OrderByDescending(r => r.Id).FirstOrDefaultAsync();
        if (revision.Action == Delete) return (true, Parse(revision.Snapshot)); // 删除记录本身存的就是删除前的内容
        if (previous == null) return (true, null); // 没有更早的记录，无法得知之前的样子
        if (previous.Action == Delete) return (false, null);
        return (true, Parse(previous.Snapshot));
    }

    /// <summary>
    /// 撤销某次操作：新建 → 删掉；修改 → 恢复成这次修改之前的样子；删除 → 恢复。
    /// 注意：之后其他人对这个条目的修改也会被一起覆盖（会再记一笔「恢复」，可以再撤销回来）。
    /// </summary>
    public async Task<string> UndoAsync(Guid projectId, long revisionId, Guid userId)
    {
        var revision = await context.WorldCardRevisions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == revisionId && r.ProjectId == projectId)
            ?? throw new WorldOperationException(WorldErrorKind.NotFound, "修改记录不存在");
        if (revision.Action == Baseline) throw new WorldOperationException(WorldErrorKind.Invalid, "这是记录起点，没有可撤销的修改");
        var (existed, snap) = await StateBeforeAsync(revision);
        var actorName = revision.UserId.HasValue
            ? await context.Users.AsNoTracking().Where(u => u.Id == revision.UserId).Select(u => u.Username).FirstOrDefaultAsync() : null;
        var note = $"撤销了{(actorName != null ? $" {actorName} " : "")}的一次{ActionLabel(revision.Action)}";
        var what = $"一次{ActionLabel(revision.Action)}（条目《{Clip(revision.Title, 40)}》）";
        if (!existed)
        {
            await DeleteCardAsync(projectId, revision.CardId, userId, note);
            await NotifyUndoneAsync(projectId, revision.UserId, userId, what);
            return $"已撤销：条目《{revision.Title}》已删除";
        }
        if (snap == null) throw new WorldOperationException(WorldErrorKind.Invalid, "找不到这次修改之前的内容，无法撤销");
        await ApplyAsync(projectId, revision.CardId, snap, userId, note);
        await NotifyUndoneAsync(projectId, revision.UserId, userId, what);
        return $"已撤销：条目《{snap.Title}》已恢复";
    }

    /// <summary>
    /// 一键撤销某人从 since 起的全部修改（只有创建者能用）：他碰过的每个条目都恢复成他第一次修改之前的样子，
    /// 他新建的条目删掉。返回处理的条目数。
    /// </summary>
    public async Task<int> RevertUserAsync(Guid projectId, Guid targetUserId, DateTime since, Guid userId)
    {
        var revisions = await context.WorldCardRevisions.AsNoTracking()
            .Where(r => r.ProjectId == projectId && r.UserId == targetUserId && r.CreatedAt >= since && r.Action != Baseline)
            .OrderBy(r => r.Id).ToListAsync();
        var firstPerCard = revisions.GroupBy(r => r.CardId).Select(g => g.First()).ToList();
        var name = await context.Users.AsNoTracking().Where(u => u.Id == targetUserId).Select(u => u.Username).FirstOrDefaultAsync() ?? "协作者";
        var note = $"一键撤销 {name} 的修改";
        var done = 0;
        foreach (var first in firstPerCard)
        {
            var (existed, snap) = await StateBeforeAsync(first);
            if (!existed) { await DeleteCardAsync(projectId, first.CardId, userId, note); done++; }
            else if (snap != null) { await ApplyAsync(projectId, first.CardId, snap, userId, note); done++; }
        }
        if (done > 0) await NotifyUndoneAsync(projectId, targetUserId, userId, $"{done} 个条目的修改");
        return done;
    }

    public static string ActionLabel(string action) => action switch
    {
        Create => "新建", Update => "修改", Delete => "删除", Restore => "恢复", _ => "修改",
    };

    private async Task DeleteCardAsync(Guid projectId, Guid cardId, Guid userId, string note)
    {
        var card = await context.WorldCards.Include(c => c.Project).Include(c => c.OutRelations).Include(c => c.InRelations)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ProjectId == projectId);
        if (card == null) return;
        var relations = card.OutRelations.Concat(card.InRelations).ToList();
        var affected = relations.SelectMany(r => new[] { r.SourceCardId, r.TargetCardId }).Append(cardId).ToArray();
        Add(projectId, cardId, userId, Delete, Snap(card, card.OutRelations, card.InRelations), note);
        context.WorldRelations.RemoveRange(relations);
        context.WorldCards.Remove(card);
        await context.SaveChangesAsync();
        WorldCache.InvalidateCards(cache, affected);
        WorldCache.InvalidateProject(cache, projectId, card.Project?.OwnerId ?? Guid.Empty);
    }

    /// <summary>把条目改成快照里的样子（条目已被删除就按原 ID 重新建出来），关系也一起恢复</summary>
    private async Task ApplyAsync(Guid projectId, Guid cardId, WorldCardSnapshot snap, Guid userId, string note)
    {
        var card = await context.WorldCards.Include(c => c.OutRelations)
            .FirstOrDefaultAsync(c => c.Id == cardId && c.ProjectId == projectId);
        if (card == null)
        {
            card = new WorldCard { Id = cardId, ProjectId = projectId, CreatedAt = DateTime.UtcNow };
            context.WorldCards.Add(card);
        }
        card.Title = snap.Title; card.Type = snap.Type; card.SubType = snap.SubType; card.CoverImage = snap.CoverImage;
        card.GalleryImages = snap.GalleryImages; card.Aliases = snap.Aliases; card.Attributes = snap.Attributes;
        card.Description = snap.Description; card.ContentBlocks = snap.ContentBlocks; card.TimelineEvents = snap.TimelineEvents;
        card.Tags = snap.Tags; card.EmbeddedCards = snap.EmbeddedCards; card.Content = snap.Content;
        card.Layout = snap.Layout;
        card.AdvanceVersion();

        // 关系：只连还存在的条目
        var related = snap.Out.Select(r => r.CardId).Concat(snap.In.Select(r => r.CardId)).Distinct().ToList();
        var alive = (await context.WorldCards.AsNoTracking().Where(c => c.ProjectId == projectId && related.Contains(c.Id))
            .Select(c => c.Id).ToListAsync()).ToHashSet();
        var affected = new HashSet<Guid> { cardId };
        foreach (var old in card.OutRelations.ToList()) { affected.Add(old.TargetCardId); context.WorldRelations.Remove(old); }
        foreach (var r in snap.Out.Where(r => r.CardId != cardId && alive.Contains(r.CardId)).DistinctBy(r => r.CardId))
        {
            context.WorldRelations.Add(new WorldRelation { Id = Guid.NewGuid(), SourceCardId = cardId, TargetCardId = r.CardId, RelationType = r.RelationType, CreatedAt = DateTime.UtcNow });
            affected.Add(r.CardId);
        }
        var existingIn = (await context.WorldRelations.AsNoTracking().Where(r => r.TargetCardId == cardId).Select(r => r.SourceCardId).ToListAsync()).ToHashSet();
        foreach (var r in snap.In.Where(r => r.CardId != cardId && alive.Contains(r.CardId) && !existingIn.Contains(r.CardId)).DistinctBy(r => r.CardId))
        {
            context.WorldRelations.Add(new WorldRelation { Id = Guid.NewGuid(), SourceCardId = r.CardId, TargetCardId = cardId, RelationType = r.RelationType, CreatedAt = DateTime.UtcNow });
            affected.Add(r.CardId);
        }
        await context.SaveChangesAsync();
        var ownerId = await context.WorldProjects.AsNoTracking().Where(p => p.Id == projectId).Select(p => p.OwnerId).FirstOrDefaultAsync();
        WorldCache.InvalidateCards(cache, affected);
        WorldCache.InvalidateProject(cache, projectId, ownerId);
        await RecordAsync(projectId, cardId, userId, Restore, note);
    }

    // ================= 条目「每一项是谁改的」 =================

    public sealed record FieldBlame(Guid? UserId, DateTime At, int Count, int Editors);
    public sealed record EditorStat(Guid UserId, int Count, DateTime LastAt);
    public sealed record CardBlame(Guid? CreatedBy, DateTime? CreatedAt, int TotalEdits, List<EditorStat> Editors, Dictionary<string, FieldBlame> Fields, bool TrackedFromStart);

    /// <summary>
    /// 把条目的所有修改记录按时间顺序两两对比，算出每一项（标题、简介、标签、每个属性……）
    /// 最后是谁、什么时候改的，一共改过几次、几个人改过；以及整个条目有几个人改过、一共改了几次。
    /// 字段名：title / type / description / aliases / tags / cover / gallery / timeline / relations / blocks / attr:属性名 / field:表单字段名
    /// </summary>
    public async Task<CardBlame> BlameAsync(Guid projectId, Guid cardId)
    {
        var revisions = await context.WorldCardRevisions.AsNoTracking()
            .Where(r => r.ProjectId == projectId && r.CardId == cardId).OrderBy(r => r.Id)
            .Select(r => new { r.UserId, r.Action, r.Snapshot, r.CreatedAt }).ToListAsync();
        var fields = new Dictionary<string, (Guid? User, DateTime At, int Count, HashSet<Guid> Editors)>();
        var editors = new Dictionary<Guid, (int Count, DateTime Last)>();
        WorldCardSnapshot? prev = null;
        Guid? createdBy = null; DateTime? createdAt = null;
        var total = 0;
        var trackedFromStart = revisions.Count > 0 && revisions[0].Action == Create;
        foreach (var r in revisions)
        {
            var snap = Parse(r.Snapshot);
            if (snap == null) continue;
            if (r.Action == Baseline) { prev = snap; continue; }
            if (r.Action == Delete) continue; // 删除记录存的是删除前的内容，不算改动
            var changed = r.Action == Create ? FieldValues(snap).Where(kv => !string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ToList()
                : prev == null ? new List<string>() : ChangedFields(prev, snap);
            if (r.Action == Create) { createdBy = r.UserId; createdAt = r.CreatedAt; }
            if (r.UserId is Guid uid && (changed.Count > 0 || r.Action == Create))
            {
                total++;
                editors[uid] = editors.TryGetValue(uid, out var e) ? (e.Count + 1, r.CreatedAt) : (1, r.CreatedAt);
                foreach (var key in changed)
                {
                    fields.TryGetValue(key, out var f);
                    var set = f.Editors ?? new HashSet<Guid>();
                    set.Add(uid);
                    fields[key] = (uid, r.CreatedAt, f.Count + 1, set);
                }
            }
            prev = snap;
        }
        return new CardBlame(createdBy, createdAt, total,
            editors.Select(e => new EditorStat(e.Key, e.Value.Count, e.Value.Last)).OrderByDescending(e => e.Count).ToList(),
            fields.ToDictionary(f => f.Key, f => new FieldBlame(f.Value.User, f.Value.At, f.Value.Count, f.Value.Editors.Count)),
            trackedFromStart);
    }

    private static List<string> ChangedFields(WorldCardSnapshot a, WorldCardSnapshot b)
    {
        var va = FieldValues(a); var vb = FieldValues(b);
        return va.Keys.Union(vb.Keys).Where(k => (va.GetValueOrDefault(k) ?? "") != (vb.GetValueOrDefault(k) ?? "")).ToList();
    }

    /// <summary>把快照拆成「字段名 → 可比较的文字」</summary>
    private static Dictionary<string, string> FieldValues(WorldCardSnapshot s)
    {
        var d = new Dictionary<string, string>
        {
            ["title"] = s.Title ?? "",
            ["type"] = $"{s.Type}|{s.SubType}",
            ["description"] = s.Description ?? "",
            ["aliases"] = Normalize(s.Aliases),
            ["tags"] = Normalize(s.Tags),
            ["cover"] = s.CoverImage ?? "",
            ["gallery"] = Normalize(s.GalleryImages),
            ["timeline"] = Normalize(s.TimelineEvents),
            ["blocks"] = Normalize(s.ContentBlocks) + "|" + Normalize(s.EmbeddedCards),
            ["relations"] = string.Join(";", s.Out.Select(r => $"{r.CardId}:{r.RelationType}").OrderBy(x => x)),
        };
        // 区块：每一块单独算（block:区块ID），排列顺序算一项（layout）
        d["layout"] = WorldLayout.Arrangement(s.Layout);
        foreach (var block in WorldLayout.Blocks(s.Layout)) d["block:" + block.Id] = block.Raw;
        // 自定义属性：每个属性单独算
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(s.Attributes) ? "[]" : s.Attributes);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
                foreach (var a in doc.RootElement.EnumerateArray())
                {
                    if (a.ValueKind != JsonValueKind.Object) continue;
                    var key = (a.TryGetProperty("key", out var k) || a.TryGetProperty("Key", out k)) && k.ValueKind == JsonValueKind.String ? k.GetString() : null;
                    if (string.IsNullOrWhiteSpace(key)) continue;
                    var value = a.TryGetProperty("value", out var v) || a.TryGetProperty("Value", out v) ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText()) : "";
                    d["attr:" + key.Trim()] = value ?? "";
                }
        }
        catch (JsonException) { }
        // 按条目类型的表单字段（Content 里的键值）
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(s.Content) ? "{}" : s.Content);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
                foreach (var prop in doc.RootElement.EnumerateObject())
                    d["field:" + prop.Name] = prop.Value.ValueKind == JsonValueKind.String ? prop.Value.GetString() ?? "" : prop.Value.GetRawText();
        }
        catch (JsonException) { }
        return d;
    }

    private static string Normalize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || json == "[]" || json == "{}") return "";
        try { using var doc = JsonDocument.Parse(json); return doc.RootElement.GetRawText(); }
        catch (JsonException) { return json; }
    }
}
