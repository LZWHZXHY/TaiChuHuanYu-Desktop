using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Tag
{
    // 2. 通用标签关联表：支持无限扩展
    [Table("tag_assignments")]
    public class TagAssignment
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid TagId { get; set; }

        /// <summary>
        /// 被贴标签的业务实体 ID (比如 NoteId, PostId, VideoId)
        /// </summary>
        [Required]
        public string EntityId { get; set; } = string.Empty; // 用 string 兼容 Guid 和 NanoID

        /// <summary>
        /// 实体类型枚举/字符串 (比如: "Note", "Blog", "Video", "Post")
        /// </summary>
        [Required]
        [MaxLength(50)]
        public string EntityType { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 导航属性
        [ForeignKey(nameof(TagId))]
        public virtual Tag? Tag { get; set; }
    }
}
