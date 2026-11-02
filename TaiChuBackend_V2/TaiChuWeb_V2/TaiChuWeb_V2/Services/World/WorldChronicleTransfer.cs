using System.Text.Json;
using System.Text.RegularExpressions;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.World;

namespace TaiChuWeb_V2.Services.World;

/// <summary>
/// 把备份文件里的编年史变成要写进新世界（或现有世界）的时间线、事件、历法：
/// 时间线和事件全部换成新 Id，事件所在的线、支线从哪条线分出、在哪件事分叉 / 汇合跟着换成新 Id；
/// 只认这次恢复的条目（别的世界的条目不会被连上）；长度、格式和平时编辑时一样检查，不合格的字段丢掉而不是整个失败。
/// </summary>
public static class WorldChronicleTransfer
{
    public sealed record Result(List<WorldTimeline> Timelines, List<WorldTimelineEvent> Events, string? Calendar);

    private static readonly string[] Statuses = { WorldTimeline.StatusCanon, WorldTimeline.StatusAlt, WorldTimeline.StatusWhatIf, WorldTimeline.StatusDraft };
    private static readonly string[] Kinds = { WorldTimelineEvent.KindEvent, WorldTimelineEvent.KindPeriod, WorldTimelineEvent.KindSkip, WorldTimelineEvent.KindLost };
    private const long MaxYear = 1_000_000_000_000;

    /// <param name="cards">这次恢复的条目（新 Id），事件和时间线只能关联它们</param>
    /// <param name="sortBase">恢复到现有世界时，新线排在已有的线后面</param>
    public static Result Build(Guid projectId, WorldChronicleTransferDto dto, HashSet<Guid> cards, Guid userId, int sortBase)
    {
        var now = DateTime.UtcNow;
        var lineIds = new Dictionary<string, Guid>();
        var lines = new List<(ChronicleTimelineTransferDto Dto, WorldTimeline Line)>();
        foreach (var t in (dto.Timelines ?? new()).Take(200))
        {
            if (t == null || string.IsNullOrWhiteSpace(t.Id) || lineIds.ContainsKey(t.Id)) continue;
            var name = (t.Name ?? "").Trim();
            var line = new WorldTimeline
            {
                Id = Guid.NewGuid(), ProjectId = projectId,
                Name = name.Length == 0 ? "未命名时间线" : Clip(name, 80)!,
                Description = Clip(t.Description, 1000),
                Color = CleanColor(t.Color),
                Status = Statuses.Contains(t.Status) ? t.Status! : WorldTimeline.StatusCanon,
                CardId = CardOf(t.CardId, cards),
                Hidden = t.Hidden,
                CreatedAt = now, UpdatedAt = now,
            };
            lineIds[t.Id] = line.Id;
            lines.Add((t, line));
        }

        var eventIds = new Dictionary<string, Guid>();
        var events = new List<WorldTimelineEvent>();
        foreach (var e in (dto.Events ?? new()).Take(5000))
        {
            if (e == null || string.IsNullOrWhiteSpace(e.Id) || eventIds.ContainsKey(e.Id)
                || e.TimelineId == null || !lineIds.TryGetValue(e.TimelineId, out var lineId)) continue;
            var kind = Kinds.Contains(e.Kind) ? e.Kind! : WorldTimelineEvent.KindEvent;
            var ranged = kind != WorldTimelineEvent.KindEvent; // 只有时期 / 跳跃 / 失落的历史才有结束时间
            var title = Regex.Replace(e.Title ?? "", @"\s*[\r\n]+\s*", " ").Trim();
            var year = YearOf(e.Year);
            var month = year == null ? null : MonthOf(e.Month);
            var endYear = ranged ? YearOf(e.EndYear) : null;
            var endMonth = endYear == null ? null : MonthOf(e.EndMonth);
            var ev = new WorldTimelineEvent
            {
                Id = Guid.NewGuid(), ProjectId = projectId, TimelineId = lineId, Kind = kind,
                Title = title.Length == 0 ? "（无标题）" : Clip(title, 500)!,
                Description = string.IsNullOrWhiteSpace(e.Description) ? null : e.Description.Length > 50_000 ? e.Description[..50_000] : e.Description,
                EraId = Clip(e.EraId, 40), Year = year, Month = month, Day = month == null ? null : DayOf(e.Day),
                EndEraId = ranged ? Clip(e.EndEraId, 40) : null, EndYear = endYear, EndMonth = endMonth, EndDay = endMonth == null ? null : DayOf(e.EndDay),
                DateText = Clip(e.DateText, 100),
                Color = CleanColor(e.Color),
                Images = JsonSerializer.Serialize((e.Images ?? new()).Where(u => !string.IsNullOrWhiteSpace(u) && u.Length <= 500).Distinct().Take(20).ToList()),
                Tags = JsonSerializer.Serialize((e.Tags ?? new()).Select(t => (t ?? "").Trim()).Where(t => t.Length is > 0 and <= 30).Distinct().Take(20).ToList()),
                CardIds = JsonSerializer.Serialize((e.CardIds ?? new()).Select(id => CardOf(id, cards)).OfType<Guid>().Distinct().Take(50).Select(g => g.ToString()).ToList()),
                LocationCardId = CardOf(e.LocationCardId, cards),
                Hidden = e.Hidden,
                SortOrder = e.SortOrder,
                CreatedBy = userId, CreatedAt = now, UpdatedAt = now,
            };
            eventIds[e.Id] = ev.Id;
            events.Add(ev);
        }

        // 支线：父线换成新 Id；绕成圈的（被改过的文件才会有）断开
        var parents = new Dictionary<Guid, Guid?>();
        foreach (var (t, line) in lines)
        {
            line.ParentId = t.ParentId != null && lineIds.TryGetValue(t.ParentId, out var p) && p != line.Id ? p : null;
            parents[line.Id] = line.ParentId;
        }
        foreach (var (_, line) in lines)
        {
            if (line.ParentId is not Guid parent) continue;
            var seen = new HashSet<Guid>();
            for (Guid? cur = parent; cur is Guid c && seen.Add(c); cur = parents.GetValueOrDefault(c))
                if (c == line.Id) { line.ParentId = null; parents[line.Id] = null; break; }
        }
        foreach (var (t, line) in lines)
        {
            line.ForkEventId = t.ForkEventId != null && eventIds.TryGetValue(t.ForkEventId, out var f) ? f : null;
            line.MergeEventId = t.MergeEventId != null && eventIds.TryGetValue(t.MergeEventId, out var m) ? m : null;
        }
        // 排序：保持原来的先后，排在 sortBase 后面
        var index = 0;
        foreach (var (_, line) in lines.OrderBy(x => x.Dto.SortOrder)) line.SortOrder = sortBase + ++index;

        // 历法：必须是 JSON 对象，最多 64KB（和保存历法时一样）
        string? calendar = null;
        if (dto.Calendar is JsonElement cal && cal.ValueKind == JsonValueKind.Object)
        {
            var raw = cal.GetRawText();
            if (raw.Length <= 64 * 1024) calendar = raw;
        }
        return new Result(lines.Select(x => x.Line).ToList(), events, calendar);
    }

    private static Guid? CardOf(string? id, HashSet<Guid> cards) => Guid.TryParse(id, out var g) && cards.Contains(g) ? g : null;
    private static long? YearOf(long? y) => y is long v && Math.Abs(v) <= MaxYear ? v : null;
    private static int? MonthOf(int? m) => m is >= 1 and <= 1000 ? m : null;
    private static int? DayOf(int? d) => d is >= 1 and <= 10000 ? d : null;

    private static string? CleanColor(string? color) =>
        !string.IsNullOrWhiteSpace(color) && Regex.IsMatch(color.Trim(), "^#[0-9a-fA-F]{3,8}$") ? color.Trim() : null;

    private static string? Clip(string? s, int max)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        var t = s.Trim();
        return t.Length > max ? t[..max] : t;
    }
}
