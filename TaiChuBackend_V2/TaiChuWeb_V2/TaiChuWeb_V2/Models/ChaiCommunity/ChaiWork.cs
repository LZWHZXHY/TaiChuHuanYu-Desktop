using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.ChaiCommunity
{
    /// <summary>
    /// 柴圈作品（动画/图片）：火柴人社区的核心内容，支持关联 OC 角色、约战、联合活动。
    /// </summary>
    [Table("ChaiWorks")]
    public class ChaiWork
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        /// <summary>视频或图片地址（COS CDN URL）。</summary>
        [Required]
        [MaxLength(500)]
        public string MediaUrl { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? CoverUrl { get; set; }

        /// <summary>video / image</summary>
        [Required]
        [MaxLength(20)]
        public string MediaType { get; set; } = "video";

        public Guid AuthorId { get; set; }

        [Required]
        [MaxLength(100)]
        public string AuthorName { get; set; } = string.Empty;

        /// <summary>draft / published</summary>
        [Required]
        [MaxLength(20)]
        public string Status { get; set; } = "draft";

        public int Views { get; set; }

        /// <summary>标签 JSON 数组。</summary>
        [Column(TypeName = "json")]
        public string Tags { get; set; } = "[]";

        /// <summary>关联的 OC 角色 ID 列表（JSON 数组）。</summary>
        [Column(TypeName = "json")]
        public string RelatedCharacterIds { get; set; } = "[]";

        public Guid? RelatedBattleId { get; set; }
        public Guid? RelatedJointId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}
