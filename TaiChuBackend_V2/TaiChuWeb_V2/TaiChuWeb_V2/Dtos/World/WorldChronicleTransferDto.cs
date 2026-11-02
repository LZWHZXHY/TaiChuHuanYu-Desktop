using System.Text.Json;

namespace TaiChuWeb_V2.Dtos.World;

/// <summary>
/// 导出 / 备份文件里的编年史（时间线、事件、历法），导入、恢复、复制世界时一起还原。
/// 时间线和事件的 Id 是原世界里的旧 Id，只用来对上「事件在哪条线上、从哪条线哪件事分叉」，服务器会全部换成新 Id；
/// 条目 Id 前端已经换成了这次恢复的新条目 Id。字符串全部可空：缺字段时由服务器补默认值，不让整个恢复因为一个空名字失败。
/// </summary>
public class WorldChronicleTransferDto
{
    /// <summary>历法（整个 JSON 对象）；没有就用默认历法</summary>
    public JsonElement? Calendar { get; set; }
    public List<ChronicleTimelineTransferDto>? Timelines { get; set; }
    public List<ChronicleEventTransferDto>? Events { get; set; }
}

public class ChronicleTimelineTransferDto
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Color { get; set; }
    public string? Status { get; set; }
    public string? ParentId { get; set; }
    public string? ForkEventId { get; set; }
    public string? MergeEventId { get; set; }
    public string? CardId { get; set; }
    public int SortOrder { get; set; }
    public bool Hidden { get; set; }
}

public class ChronicleEventTransferDto
{
    public string? Id { get; set; }
    public string? TimelineId { get; set; }
    public string? Kind { get; set; }
    public string? Title { get; set; }
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
    public List<string>? CardIds { get; set; }
    public string? LocationCardId { get; set; }
    public bool Hidden { get; set; }
    public int SortOrder { get; set; }
}

/// <summary>提交恢复：条目之外再带上编年史（老页面 / 老备份不带也照常恢复条目）</summary>
public class CommitWorldRestoreWithChronicleDto : CommitWorldRestoreDto
{
    public WorldChronicleTransferDto? Chronicle { get; set; }
}
