using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Quest
{
    /// <summary>
    /// 我的世界（MC）服务器的「世界任务」：全服一起完成一个目标（比如收集 10000 块木头），
    /// 完成后公布新 mod / 新内容，给贡献者发奖励，并自动开启下一个任务。
    /// 表由 ServerQuestSchema 启动时创建，不参与 EF 迁移。
    /// </summary>
    [Table("ServerQuests")]
    public class ServerQuest
    {
        public const string Draft = "draft", Active = "active", Completed = "completed";
        /// <summary>count = 数量目标（收集 10000 块木头）；goal = 一次性目标（做出一台刷石机，做到就算完成）</summary>
        public const string KindCount = "count", KindGoal = "goal";
        /// <summary>main = 主线（同时只有一个在进行）；side = 支线（可以和主线、其他支线同时进行）</summary>
        public const string LineMain = "main", LineSide = "side";

        [Key]
        public int Id { get; set; }
        [MaxLength(100)]
        public string Title { get; set; } = "";
        /// <summary>count = 数量目标；goal = 一次性目标（Target 固定为 1，管理员确认一份完成证明就完成）</summary>
        [MaxLength(16)]
        public string Kind { get; set; } = KindCount;
        [NotMapped]
        public bool IsGoal => Kind == KindGoal;
        /// <summary>主线 / 支线</summary>
        [MaxLength(16)]
        public string Line { get; set; } = LineMain;
        [NotMapped]
        public bool IsSide => Line == LineSide;
        /// <summary>前置任务：这个任务完成后自动开启本任务（「完成 A 解锁支线 B」「完成 A 开启下一章 C」）。
        /// 为空时：主线按排队顺序接在上一个主线后面；支线要管理员手动开始</summary>
        public int? PrereqId { get; set; }
        public string? Description { get; set; }
        /// <summary>进度单位：块木头、只末影龙、座建筑……</summary>
        [MaxLength(20)]
        public string Unit { get; set; } = "";
        public long Target { get; set; }
        public long Progress { get; set; }
        /// <summary>draft = 排队中（还没开始）；active = 进行中（同时只有一个）；completed = 已完成</summary>
        [MaxLength(16)]
        public string Status { get; set; } = Draft;
        /// <summary>任务链顺序：完成一个后自动开启排在最前面的 draft</summary>
        public int SortOrder { get; set; }
        /// <summary>完成前公开的预告（「完成后解锁：一个神秘的科技 mod」）</summary>
        [MaxLength(500)]
        public string? RewardTeaser { get; set; }
        /// <summary>完成后公布的内容（新 mod、新玩法……），完成前只有管理员看得到</summary>
        public string? RewardText { get; set; }
        /// <summary>每位贡献者得到的经验</summary>
        public int RewardExp { get; set; }
        /// <summary>每位贡献者得到的土豆</summary>
        public int RewardPotato { get; set; }
        [MaxLength(500)]
        public string? CoverImage { get; set; }
        public DateTime? StartedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>一笔贡献：玩家自己提交（等管理员审核），或者管理员直接记录（直接通过）</summary>
    [Table("ServerQuestContributions")]
    public class ServerQuestContribution
    {
        public const string Pending = "pending", Approved = "approved", Rejected = "rejected";

        [Key]
        public long Id { get; set; }
        public int QuestId { get; set; }
        /// <summary>网站账号（管理员记录给游戏里的玩家时可以为空）</summary>
        public Guid? UserId { get; set; }
        /// <summary>游戏里的名字（方便管理员去服务器里复核）</summary>
        [MaxLength(50)]
        public string? PlayerName { get; set; }
        public long Amount { get; set; }
        [MaxLength(1000)]
        public string? Note { get; set; }
        /// <summary>截图（JSON 数组）</summary>
        public string Images { get; set; } = "[]";
        /// <summary>玩家在论坛「我的世界 · 任务报告」里发的报告帖（PublishedNote.Id）</summary>
        public Guid? PostId { get; set; }
        [MaxLength(16)]
        public string Status { get; set; } = Pending;
        /// <summary>player = 玩家提交；admin = 管理员记录</summary>
        [MaxLength(16)]
        public string Source { get; set; } = "player";
        public Guid? ReviewerId { get; set; }
        [MaxLength(300)]
        public string? ReviewNote { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }
    }
}
