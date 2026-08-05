using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.ChaiCommunity
{
    [Table("StickmanImages")]
    public class StickmanImage
    {
        [Key]
        public Guid Id { get; set; }

        public Guid CharacterId { get; set; }

        /// <summary>
        /// COS 图片完整 URL
        /// </summary>
        [Required]
        [MaxLength(500)]
        public string Url { get; set; } = string.Empty;

        /// <summary>
        /// 图片描述（可选）
        /// </summary>
        [MaxLength(200)]
        public string? Alt { get; set; }

        /// <summary>
        /// 显示排序
        /// </summary>
        public int SortOrder { get; set; }

        public DateTime CreatedAt { get; set; }

        // ========== 导航属性 ==========

        [ForeignKey(nameof(CharacterId))]
        public virtual StickmanCharacter? Character { get; set; }
    }
}