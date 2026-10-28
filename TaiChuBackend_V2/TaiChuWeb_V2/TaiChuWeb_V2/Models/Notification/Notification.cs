using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Notify
{
    /// <summary>站内通知：别人评论/回复/点赞了你的内容时，给你一条通知。</summary>
    [Table("Notifications")]
    public class Notification
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>接收者（站内用户）</summary>
        public Guid UserId { get; set; }

        /// <summary>操作者（谁触发的）</summary>
        public Guid? ActorId { get; set; }

        /// <summary>类型：comment（评论）/ reply（回复）/ like（点赞）/ quote（引用）</summary>
        [MaxLength(30)]
        public string Type { get; set; } = "comment";

        /// <summary>目标类型：artwork / note / chai / musteat / comment ...</summary>
        [MaxLength(30)]
        public string TargetType { get; set; } = "";

        /// <summary>目标 ID</summary>
        [MaxLength(64)]
        public string TargetId { get; set; } = "";

        /// <summary>通知文案（如「评论了你的作品」）</summary>
        [MaxLength(200)]
        public string Content { get; set; } = "";

        /// <summary>点击跳转的站内路由（hash 路由）</summary>
        [MaxLength(500)]
        public string? Link { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
