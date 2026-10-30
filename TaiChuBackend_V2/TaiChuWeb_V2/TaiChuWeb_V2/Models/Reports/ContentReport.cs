using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Reports
{
    /// <summary>
    /// 举报：用户觉得某个内容 / 某个人有问题，交给管理员处理。
    /// TargetType：post 帖子 / blog 博客 / artwork 画作 / comment 评论 / history 岁月史书 / history-comment 岁月史书评论 /
    /// work 柴圈作品 / character OC / user 用户 / other 其他。
    /// 状态：pending 待处理 / resolved 已处理（确实有问题，已经处理）/ dismissed 不处理（没问题）。
    /// 表由 ContentReportSchema 启动时创建，不参与 EF 迁移。
    /// </summary>
    [Table("ContentReports")]
    public class ContentReport
    {
        public static readonly string[] TargetTypes = { "post", "blog", "artwork", "comment", "history", "history-comment", "work", "character", "user", "world", "novel", "other" };
        public static readonly string[] Reasons = { "spam", "abuse", "porn", "privacy", "copyright", "illegal", "other" };

        [Key] public long Id { get; set; }
        public Guid ReporterId { get; set; }
        [MaxLength(24)] public string TargetType { get; set; } = "other";
        [MaxLength(64)] public string TargetId { get; set; } = "";
        /// <summary>举报时内容的标题 / 摘要（内容后来被删了也知道举报的是什么）</summary>
        [MaxLength(200)] public string? TargetTitle { get; set; }
        /// <summary>站内地址，管理员点了直接打开</summary>
        [MaxLength(300)] public string? TargetUrl { get; set; }
        /// <summary>spam 广告刷屏 / abuse 人身攻击 / porn 色情低俗 / privacy 泄露隐私 / copyright 侵权搬运 / illegal 违法 / other 其他</summary>
        [MaxLength(16)] public string Reason { get; set; } = "other";
        [MaxLength(500)] public string? Detail { get; set; }
        [MaxLength(16)] public string Status { get; set; } = "pending";
        public Guid? HandledBy { get; set; }
        public DateTime? HandledAt { get; set; }
        /// <summary>处理说明（会告诉举报人）</summary>
        [MaxLength(300)] public string? HandleNote { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
