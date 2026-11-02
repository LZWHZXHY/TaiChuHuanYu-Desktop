using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.World
{
    /// <summary>
    /// 世界编年史里的一条时间线（主线、各国史、人物生平……）。
    /// 支线：ParentId = 从哪条线分出来，ForkEventId = 在哪个事件分叉；MergeEventId = 在哪个事件（任意一条线上）汇合回去。
    /// 表由 WorldCollaboratorSchema 启动时创建，不参与 EF 迁移。
    /// </summary>
    [Table("WorldTimelines")]
    public class WorldTimeline
    {
        public const string StatusCanon = "canon", StatusAlt = "alt", StatusWhatIf = "whatif", StatusDraft = "draft";

        [Key] public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProjectId { get; set; }
        [MaxLength(80)] public string Name { get; set; } = "";
        [MaxLength(1000)] public string? Description { get; set; }
        [MaxLength(16)] public string? Color { get; set; }
        /// <summary>canon 正史 / alt 支线 / whatif 假如 / draft 废案</summary>
        [MaxLength(16)] public string Status { get; set; } = StatusCanon;
        public Guid? ParentId { get; set; }
        public Guid? ForkEventId { get; set; }
        public Guid? MergeEventId { get; set; }
        /// <summary>这条线是关于哪个条目的（人物生平、国家史……）</summary>
        public Guid? CardId { get; set; }
        public int SortOrder { get; set; }
        /// <summary>对访客隐藏</summary>
        public bool Hidden { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 编年史里的一件事。Kind：event 事件（一个时间点）/ period 时期（一段时间）/ skip 时间跳跃（轴上折叠）/ lost 失落的历史（标成未知的一段）。
    /// 时间按世界自己的历法存：纪元 + 年 + 月 + 日（都可以不填）；DateText 是模糊写法（「第三纪元末」），给人看的。
    /// </summary>
    [Table("WorldTimelineEvents")]
    public class WorldTimelineEvent
    {
        public const string KindEvent = "event", KindPeriod = "period", KindSkip = "skip", KindLost = "lost";

        [Key] public Guid Id { get; set; } = Guid.NewGuid();
        public Guid ProjectId { get; set; }
        public Guid TimelineId { get; set; }
        [MaxLength(16)] public string Kind { get; set; } = KindEvent;
        /// <summary>标题，可以带 {CARD:id} 引用（「{CARD:某人} 写下了 {CARD:某书}」）</summary>
        [MaxLength(500)] public string Title { get; set; } = "";
        public string? Description { get; set; }
        [MaxLength(40)] public string? EraId { get; set; }
        public long? Year { get; set; }
        public int? Month { get; set; }
        public int? Day { get; set; }
        [MaxLength(40)] public string? EndEraId { get; set; }
        public long? EndYear { get; set; }
        public int? EndMonth { get; set; }
        public int? EndDay { get; set; }
        [MaxLength(100)] public string? DateText { get; set; }
        [MaxLength(16)] public string? Color { get; set; }
        /// <summary>图片（JSON 数组）</summary>
        public string Images { get; set; } = "[]";
        /// <summary>标签（JSON 数组）</summary>
        public string Tags { get; set; } = "[]";
        /// <summary>相关条目（JSON 数组）</summary>
        public string CardIds { get; set; } = "[]";
        /// <summary>发生地点（一个地点条目）</summary>
        public Guid? LocationCardId { get; set; }
        public bool Hidden { get; set; }
        /// <summary>同一时间的先后（时间相同或没写时间时用）</summary>
        public int SortOrder { get; set; }
        public Guid? CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>世界的历法（纪元、月份……），整个存成 JSON，前端负责换算</summary>
    [Table("WorldCalendars")]
    public class WorldCalendar
    {
        [Key] public Guid ProjectId { get; set; }
        public string Json { get; set; } = "{}";
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// 编年史的修改记录：时间线 / 事件 / 历法每次新建、修改、删除、撤销都记一条，
    /// 存下改之前和改之后的完整内容（JSON），这样能看出改了什么，也能一键撤销。
    /// TargetType：timeline 时间线 / event 事件 / calendar 历法（TargetId 就是世界 Id）。
    /// Action：create 新建 / update 修改 / delete 删除 / restore 撤销或恢复。
    /// 表 WorldChronicleRevisions 由 WorldCollaboratorSchema 启动时创建，不参与 EF 迁移。
    /// </summary>
    [Table("WorldChronicleRevisions")]
    public class WorldChronicleRevision
    {
        [Key] public long Id { get; set; }
        [Column(TypeName = "char(36)")] public Guid ProjectId { get; set; }
        [MaxLength(16)] public string TargetType { get; set; } = "event";
        [Column(TypeName = "char(36)")] public Guid TargetId { get; set; }
        /// <summary>操作人</summary>
        [Column(TypeName = "char(36)")] public Guid? UserId { get; set; }
        [MaxLength(16)] public string Action { get; set; } = "update";
        /// <summary>当时的名称（时间线名 / 事件名，可能带 {CARD:id} 引用），被删、被改名后列表里也看得懂</summary>
        [MaxLength(200)] public string Title { get; set; } = "";
        /// <summary>改之前的完整内容；新建时为空</summary>
        [Column(TypeName = "longtext")] public string? BeforeJson { get; set; }
        /// <summary>改之后的完整内容；删除时为空</summary>
        [Column(TypeName = "longtext")] public string? AfterJson { get; set; }
        /// <summary>这次改了哪些字段（英文字段名，逗号分隔），列表里直接显示，不用每条都去对比</summary>
        [MaxLength(500)] public string? ChangedFields { get; set; }
        /// <summary>补充说明，比如「撤销了 xxx 的一次修改」</summary>
        [MaxLength(200)] public string? Note { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
