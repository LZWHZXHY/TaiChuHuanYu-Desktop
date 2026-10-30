using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.World;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World;

/// <summary>
/// 世界编年史：多条时间线（主线 / 支线 / 视角）、分叉与汇合、时期、时间跳跃、失落的历史、自定义历法。
/// 事件可以关联条目、地点、标签、图片。读：能看这个世界的人（访客看不到隐藏的线和事件）；写：创建者和编写者。
/// </summary>
[Route("api/world/projects/{projectId:guid}/chronicle")]
public class WorldChronicleController(AppDbContext context) : WorldControllerBase
{
    private static readonly string[] Statuses = { WorldTimeline.StatusCanon, WorldTimeline.StatusAlt, WorldTimeline.StatusWhatIf, WorldTimeline.StatusDraft };
    private static readonly string[] Kinds = { WorldTimelineEvent.KindEvent, WorldTimelineEvent.KindPeriod, WorldTimelineEvent.KindSkip, WorldTimelineEvent.KindLost };
    private const long MaxYear = 1_000_000_000_000;

    public sealed class TimelineDto
    {
        public string Name { get; set; } = "";
        public string? Description { get; set; }
        public string? Color { get; set; }
        public string? Status { get; set; }
        public Guid? ParentId { get; set; }
        public Guid? ForkEventId { get; set; }
        public Guid? MergeEventId { get; set; }
        public Guid? CardId { get; set; }
        public int? SortOrder { get; set; }
        public bool Hidden { get; set; }
    }

    public sealed class EventDto
    {
        public Guid TimelineId { get; set; }
        public string? Kind { get; set; }
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
        public List<string>? Images { get; set; }
        public List<string>? Tags { get; set; }
        public List<Guid>? CardIds { get; set; }
        public Guid? LocationCardId { get; set; }
        public bool Hidden { get; set; }
        public int? SortOrder { get; set; }
    }

    public sealed class ReorderDto { public List<Guid> Ids { get; set; } = new(); }

    // ================= 读 =================

    /// <summary>整本编年史：历法、所有时间线、所有事件</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(Guid projectId)
    {
        var me = ReaderUserId;
        await WorldAccess.RequireReadableProjectAsync(context, projectId, me);
        var member = await IsMemberAsync(projectId, me);
        var canEdit = me != Guid.Empty && await WorldAccess.CanEditProjectAsync(context, projectId, me);

        var calendar = await context.WorldCalendars.AsNoTracking().Where(c => c.ProjectId == projectId).Select(c => c.Json).FirstOrDefaultAsync();
        var timelines = await context.WorldTimelines.AsNoTracking().Where(t => t.ProjectId == projectId)
            .OrderBy(t => t.SortOrder).ThenBy(t => t.CreatedAt).ToListAsync();
        var events = await context.WorldTimelineEvents.AsNoTracking().Where(e => e.ProjectId == projectId).ToListAsync();
        if (!member)
        {
            var hiddenLines = timelines.Where(t => t.Hidden).Select(t => t.Id).ToHashSet();
            timelines = timelines.Where(t => !t.Hidden).ToList();
            events = events.Where(e => !e.Hidden && !hiddenLines.Contains(e.TimelineId)).ToList();
        }
        return Ok(new
        {
            calendar = ParseObject(calendar),
            timelines = timelines.Select(TimelineView),
            events = events.Select(EventView),
            canEdit,
        });
    }

    /// <summary>提到某个条目的事件（关联条目、发生地点、标题或描述里 @ 了它），条目页「编年史中的事件」用</summary>
    [HttpGet("by-card/{cardId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> ByCard(Guid projectId, Guid cardId)
    {
        var me = ReaderUserId;
        await WorldAccess.RequireReadableProjectAsync(context, projectId, me);
        var member = await IsMemberAsync(projectId, me);
        var id = cardId.ToString();
        var events = await context.WorldTimelineEvents.AsNoTracking()
            .Where(e => e.ProjectId == projectId && (e.LocationCardId == cardId || e.CardIds.Contains(id)
                || e.Title.Contains(id) || (e.Description != null && e.Description.Contains(id))))
            .ToListAsync();
        // 时间线本身关联了这个条目（人物生平）也算
        var timelines = await context.WorldTimelines.AsNoTracking().Where(t => t.ProjectId == projectId).ToListAsync();
        var lineOfCard = timelines.Where(t => t.CardId == cardId).Select(t => t.Id).ToHashSet();
        if (!member)
        {
            var hiddenLines = timelines.Where(t => t.Hidden).Select(t => t.Id).ToHashSet();
            events = events.Where(e => !e.Hidden && !hiddenLines.Contains(e.TimelineId)).ToList();
            lineOfCard.RemoveWhere(hiddenLines.Contains);
        }
        var calendar = events.Count == 0 && lineOfCard.Count == 0 ? null
            : await context.WorldCalendars.AsNoTracking().Where(c => c.ProjectId == projectId).Select(c => c.Json).FirstOrDefaultAsync();
        return Ok(new
        {
            calendar = ParseObject(calendar),
            events = events.Select(EventView),
            timelines = timelines.Where(t => member || !t.Hidden).Select(t => new { t.Id, t.Name, t.Color, t.Status }),
            ownLines = lineOfCard,
        });
    }

    // ================= 历法 =================

    [HttpPut("calendar")]
    [Authorize]
    public async Task<IActionResult> SaveCalendar(Guid projectId, [FromBody] JsonElement body)
    {
        await RequireEditAsync(projectId);
        var json = body.GetRawText();
        if (json.Length > 64 * 1024) return BadRequest(new { message = "历法设置太长了" });
        if (body.ValueKind != JsonValueKind.Object) return BadRequest(new { message = "历法格式不正确" });
        var row = await context.WorldCalendars.FirstOrDefaultAsync(c => c.ProjectId == projectId);
        if (row == null) context.WorldCalendars.Add(new WorldCalendar { ProjectId = projectId, Json = json });
        else { row.Json = json; row.UpdatedAt = DateTime.UtcNow; }
        await context.SaveChangesAsync();
        return Ok(new { calendar = ParseObject(json) });
    }

    // ================= 时间线 =================

    [HttpPost("timelines")]
    [Authorize]
    public async Task<IActionResult> CreateTimeline(Guid projectId, [FromBody] TimelineDto dto)
    {
        await RequireEditAsync(projectId);
        if (await context.WorldTimelines.CountAsync(t => t.ProjectId == projectId) >= 200) return BadRequest(new { message = "时间线太多了（最多 200 条）" });
        var line = new WorldTimeline { ProjectId = projectId };
        var error = await ApplyTimelineAsync(projectId, line, dto);
        if (error != null) return BadRequest(new { message = error });
        if (dto.SortOrder == null) line.SortOrder = (await context.WorldTimelines.Where(t => t.ProjectId == projectId).MaxAsync(t => (int?)t.SortOrder) ?? 0) + 1;
        context.WorldTimelines.Add(line);
        await context.SaveChangesAsync();
        return Ok(TimelineView(line));
    }

    [HttpPut("timelines/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateTimeline(Guid projectId, Guid id, [FromBody] TimelineDto dto)
    {
        await RequireEditAsync(projectId);
        var line = await context.WorldTimelines.FirstOrDefaultAsync(t => t.Id == id && t.ProjectId == projectId);
        if (line == null) return NotFound(new { message = "时间线不存在" });
        var error = await ApplyTimelineAsync(projectId, line, dto);
        if (error != null) return BadRequest(new { message = error });
        line.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Ok(TimelineView(line));
    }

    /// <summary>删除时间线：它的事件一起删；从它分出去的支线变成独立的线</summary>
    [HttpDelete("timelines/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteTimeline(Guid projectId, Guid id)
    {
        await RequireEditAsync(projectId);
        var line = await context.WorldTimelines.FirstOrDefaultAsync(t => t.Id == id && t.ProjectId == projectId);
        if (line == null) return NotFound(new { message = "时间线不存在" });
        var eventIds = await context.WorldTimelineEvents.Where(e => e.TimelineId == id).Select(e => e.Id).ToListAsync();
        foreach (var other in await context.WorldTimelines.Where(t => t.ProjectId == projectId && t.Id != id).ToListAsync())
        {
            if (other.ParentId == id) { other.ParentId = null; other.ForkEventId = null; }
            if (other.ForkEventId is Guid f && eventIds.Contains(f)) other.ForkEventId = null;
            if (other.MergeEventId is Guid m && eventIds.Contains(m)) other.MergeEventId = null;
        }
        await context.WorldTimelineEvents.Where(e => e.TimelineId == id).ExecuteDeleteAsync();
        context.WorldTimelines.Remove(line);
        await context.SaveChangesAsync();
        return NoContent();
    }

    [HttpPost("timelines/reorder")]
    [Authorize]
    public async Task<IActionResult> ReorderTimelines(Guid projectId, [FromBody] ReorderDto dto)
    {
        await RequireEditAsync(projectId);
        var ids = dto.Ids.Distinct().ToList();
        foreach (var t in await context.WorldTimelines.Where(t => t.ProjectId == projectId && ids.Contains(t.Id)).ToListAsync())
            t.SortOrder = ids.IndexOf(t.Id) + 1;
        await context.SaveChangesAsync();
        return Ok();
    }

    // ================= 事件 =================

    [HttpPost("events")]
    [Authorize]
    public async Task<IActionResult> CreateEvent(Guid projectId, [FromBody] EventDto dto)
    {
        await RequireEditAsync(projectId);
        if (await context.WorldTimelineEvents.CountAsync(e => e.ProjectId == projectId) >= 5000) return BadRequest(new { message = "事件太多了（每个世界最多 5000 个）" });
        var ev = new WorldTimelineEvent { ProjectId = projectId, CreatedBy = CurrentUserId };
        var error = await ApplyEventAsync(projectId, ev, dto);
        if (error != null) return BadRequest(new { message = error });
        context.WorldTimelineEvents.Add(ev);
        await context.SaveChangesAsync();
        return Ok(EventView(ev));
    }

    [HttpPut("events/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateEvent(Guid projectId, Guid id, [FromBody] EventDto dto)
    {
        await RequireEditAsync(projectId);
        var ev = await context.WorldTimelineEvents.FirstOrDefaultAsync(e => e.Id == id && e.ProjectId == projectId);
        if (ev == null) return NotFound(new { message = "事件不存在" });
        var error = await ApplyEventAsync(projectId, ev, dto);
        if (error != null) return BadRequest(new { message = error });
        ev.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Ok(EventView(ev));
    }

    /// <summary>删除事件：以它为分叉点 / 汇合点的支线解开（支线本身保留）</summary>
    [HttpDelete("events/{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteEvent(Guid projectId, Guid id)
    {
        await RequireEditAsync(projectId);
        var ev = await context.WorldTimelineEvents.FirstOrDefaultAsync(e => e.Id == id && e.ProjectId == projectId);
        if (ev == null) return NotFound(new { message = "事件不存在" });
        foreach (var t in await context.WorldTimelines.Where(t => t.ProjectId == projectId && (t.ForkEventId == id || t.MergeEventId == id)).ToListAsync())
        {
            if (t.ForkEventId == id) t.ForkEventId = null;
            if (t.MergeEventId == id) t.MergeEventId = null;
        }
        context.WorldTimelineEvents.Remove(ev);
        await context.SaveChangesAsync();
        return NoContent();
    }

    // ================= 工具 =================

    private async Task<bool> IsMemberAsync(Guid projectId, Guid me) =>
        me != Guid.Empty && await context.WorldProjects.AsNoTracking().MemberOf(me).AnyAsync(p => p.Id == projectId);

    private async Task RequireEditAsync(Guid projectId)
    {
        if (!await WorldAccess.CanEditProjectAsync(context, projectId, CurrentUserId))
            throw new WorldOperationException(WorldErrorKind.Forbidden, "只有创建者和编写者能修改编年史");
    }

    private async Task<string?> ApplyTimelineAsync(Guid projectId, WorldTimeline line, TimelineDto dto)
    {
        var name = (dto.Name ?? "").Trim();
        if (name.Length is 0 or > 80) return "时间线名称要填，最多 80 个字";
        if ((dto.Description ?? "").Length > 1000) return "说明最多 1000 个字";
        line.Name = name;
        line.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description.Trim();
        line.Color = CleanColor(dto.Color);
        line.Status = Statuses.Contains(dto.Status) ? dto.Status! : WorldTimeline.StatusCanon;
        line.Hidden = dto.Hidden;
        if (dto.SortOrder is int order) line.SortOrder = order;

        // 分叉：父线要在这个世界里，不能是自己，也不能绕成圈
        if (dto.ParentId is Guid parentId)
        {
            if (parentId == line.Id) return "不能从自己分出支线";
            var parents = await context.WorldTimelines.AsNoTracking().Where(t => t.ProjectId == projectId)
                .ToDictionaryAsync(t => t.Id, t => t.ParentId);
            if (!parents.ContainsKey(parentId)) return "分叉的那条时间线不存在";
            var seen = new HashSet<Guid>();
            for (Guid? cur = parentId; cur is Guid c && seen.Add(c); cur = parents.GetValueOrDefault(c))
                if (c == line.Id) return "支线不能绕成圈";
        }
        line.ParentId = dto.ParentId;
        line.ForkEventId = await EventInProjectAsync(projectId, dto.ForkEventId);
        line.MergeEventId = await EventInProjectAsync(projectId, dto.MergeEventId);
        line.CardId = await CardInProjectAsync(projectId, dto.CardId);
        return null;
    }

    private async Task<string?> ApplyEventAsync(Guid projectId, WorldTimelineEvent ev, EventDto dto)
    {
        if (!await context.WorldTimelines.AnyAsync(t => t.Id == dto.TimelineId && t.ProjectId == projectId)) return "时间线不存在";
        // 标题可以带引用 {CARD:id}，换行合成空格；引用不算字数
        var title = System.Text.RegularExpressions.Regex.Replace(dto.Title ?? "", @"\s*[\r\n]+\s*", " ").Trim();
        var plainLength = System.Text.RegularExpressions.Regex.Replace(title, @"!?\{CARD:[^}]+\}", "某").Length;
        if (plainLength == 0 || title.Length > 500) return "事件名称要填，最多 200 个字（引用最多 8 个）";
        if (plainLength > 200) return "事件名称最多 200 个字";
        if ((dto.Description ?? "").Length > 50_000) return "描述太长了";
        if ((dto.DateText ?? "").Length > 100) return "时间说明最多 100 个字";
        if (dto.Year is long y && Math.Abs(y) > MaxYear || dto.EndYear is long ey && Math.Abs(ey) > MaxYear) return "年份太大了";
        if (dto.Month is < 1 or > 1000 || dto.EndMonth is < 1 or > 1000) return "月份不对";
        if (dto.Day is < 1 or > 10000 || dto.EndDay is < 1 or > 10000) return "日期不对";

        ev.TimelineId = dto.TimelineId;
        ev.Kind = Kinds.Contains(dto.Kind) ? dto.Kind! : WorldTimelineEvent.KindEvent;
        ev.Title = title;
        ev.Description = string.IsNullOrWhiteSpace(dto.Description) ? null : dto.Description;
        ev.EraId = Clip(dto.EraId, 40); ev.Year = dto.Year; ev.Month = dto.Year == null ? null : dto.Month; ev.Day = dto.Month == null ? null : dto.Day;
        // 只有时期 / 跳跃 / 失落的历史才有结束时间
        var ranged = ev.Kind != WorldTimelineEvent.KindEvent;
        ev.EndEraId = ranged ? Clip(dto.EndEraId, 40) : null;
        ev.EndYear = ranged ? dto.EndYear : null;
        ev.EndMonth = ranged && dto.EndYear != null ? dto.EndMonth : null;
        ev.EndDay = ranged && dto.EndMonth != null ? dto.EndDay : null;
        ev.DateText = string.IsNullOrWhiteSpace(dto.DateText) ? null : dto.DateText.Trim();
        ev.Color = CleanColor(dto.Color);
        ev.Images = JsonSerializer.Serialize((dto.Images ?? new()).Where(u => !string.IsNullOrWhiteSpace(u) && u.Length <= 500).Distinct().Take(20).ToList());
        ev.Tags = JsonSerializer.Serialize((dto.Tags ?? new()).Select(t => (t ?? "").Trim()).Where(t => t.Length is > 0 and <= 30).Distinct().Take(20).ToList());
        // 条目只留这个世界里存在的
        var wanted = (dto.CardIds ?? new()).Distinct().Take(50).ToList();
        var existing = wanted.Count == 0 ? new List<Guid>() : await context.WorldCards.AsNoTracking()
            .Where(c => c.ProjectId == projectId && wanted.Contains(c.Id)).Select(c => c.Id).ToListAsync();
        ev.CardIds = JsonSerializer.Serialize(wanted.Where(existing.Contains).Select(g => g.ToString()).ToList());
        ev.LocationCardId = await CardInProjectAsync(projectId, dto.LocationCardId);
        ev.Hidden = dto.Hidden;
        if (dto.SortOrder is int order) ev.SortOrder = order;
        return null;
    }

    private async Task<Guid?> EventInProjectAsync(Guid projectId, Guid? id) =>
        id is Guid g && await context.WorldTimelineEvents.AnyAsync(e => e.Id == g && e.ProjectId == projectId) ? g : null;

    private async Task<Guid?> CardInProjectAsync(Guid projectId, Guid? id) =>
        id is Guid g && await context.WorldCards.AnyAsync(c => c.Id == g && c.ProjectId == projectId) ? g : null;

    private static string? CleanColor(string? color) =>
        !string.IsNullOrWhiteSpace(color) && System.Text.RegularExpressions.Regex.IsMatch(color.Trim(), "^#[0-9a-fA-F]{3,8}$") ? color.Trim() : null;

    private static string? Clip(string? s, int max) => string.IsNullOrWhiteSpace(s) ? null : s.Trim().Length > max ? s.Trim()[..max] : s.Trim();

    private static object TimelineView(WorldTimeline t) => new
    {
        t.Id, t.Name, t.Description, t.Color, t.Status, t.ParentId, t.ForkEventId, t.MergeEventId, t.CardId, t.SortOrder, t.Hidden,
    };

    private static object EventView(WorldTimelineEvent e) => new
    {
        e.Id, e.TimelineId, e.Kind, e.Title, e.Description, e.EraId, e.Year, e.Month, e.Day, e.EndEraId, e.EndYear, e.EndMonth, e.EndDay,
        e.DateText, e.Color, Images = ReadList(e.Images), Tags = ReadList(e.Tags), CardIds = ReadList(e.CardIds),
        e.LocationCardId, e.Hidden, e.SortOrder, e.UpdatedAt,
    };

    private static List<string> ReadList(string? json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? new(); }
        catch (JsonException) { return new(); }
    }

    private static JsonNode? ParseObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonNode.Parse(json); }
        catch (JsonException) { return null; }
    }
}
