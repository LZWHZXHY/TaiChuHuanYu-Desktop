using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.ChaiCommunity
{
    [Table("StickmanAttributes")]
    public class StickmanAttribute
    {
        [Key]
        public Guid Id { get; set; }

        public Guid CharacterId { get; set; }

        /// <summary>
        /// 属性名，用户自定义（如：性别、武器、必杀技）
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// 属性值
        /// </summary>
        public string? Value { get; set; }
        [MaxLength(10)]
        public string Type { get; set; } = "short";
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