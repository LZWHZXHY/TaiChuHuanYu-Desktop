using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.CommunityHistory
{
    /// <summary>
    /// 岁月史书里的一段：群聊名场面、截图、生活里的事、谁的成果进步……
    /// 谁都能上传，马上就能看到；自己只能删自己的；管理员能下架、恢复、编辑任何一段（记下谁、什么时候改的）。
    /// 表由 CommunityHistorySchema 启动时创建，不参与 EF 迁移。
    /// </summary>
    [Table("CommunityHistoryEntries")]
    public class CommunityHistoryEntry
    {
        public static readonly string[] Kinds = { "funny", "chat", "life", "progress", "event", "other" };
        public static readonly string[] Precisions = { "day", "month", "year" };

        [Key] public long Id { get; set; }
        /// <summary>上传的人</summary>
        public Guid UserId { get; set; }
        /// <summary>funny 趣事 / chat 群聊截图 / life 生活 / progress 成果进步 / event 社区大事 / other 其他</summary>
        [MaxLength(16)] public string Kind { get; set; } = "funny";
        [MaxLength(100)] public string Title { get; set; } = "";
        public string? Content { get; set; }
        /// <summary>图片 / 截图（JSON 数组）</summary>
        public string Images { get; set; } = "[]";
        /// <summary>这件事发生在哪天（可以是过去的事）</summary>
        public DateTime HappenedOn { get; set; }
        /// <summary>day 记得哪天 / month 只记得哪个月 / year 只记得哪年</summary>
        [MaxLength(8)] public string DatePrecision { get; set; } = "day";
        /// <summary>被管理员下架（上传的人自己还能看到）</summary>
        public bool Hidden { get; set; }
        [MaxLength(200)] public string? HiddenReason { get; set; }
        public Guid? HiddenBy { get; set; }
        public DateTime? HiddenAt { get; set; }
        /// <summary>管理员最后一次编辑</summary>
        public Guid? EditedBy { get; set; }
        public DateTime? EditedAt { get; set; }
        /// <summary>上传时间</summary>
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>岁月史书的评论：登录用户都能评论、回复某条评论；自己删自己的，管理员能删任何一条</summary>
    [Table("CommunityHistoryComments")]
    public class CommunityHistoryComment
    {
        [Key] public long Id { get; set; }
        public long EntryId { get; set; }
        public Guid UserId { get; set; }
        [MaxLength(500)] public string Content { get; set; } = "";
        /// <summary>回复的是哪条评论（可以没有）</summary>
        public long? ReplyToId { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>岁月史书的表情回应：😂 笑死 / 🥹 泪目 / 👍 牛 / ❤️ 抱抱，每人每种一次</summary>
    [Table("CommunityHistoryReactions")]
    public class CommunityHistoryReaction
    {
        public static readonly string[] Kinds = { "lol", "touched", "awesome", "hug" };

        public long EntryId { get; set; }
        public Guid UserId { get; set; }
        [MaxLength(16)] public string Kind { get; set; } = "lol";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
