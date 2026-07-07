using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.World
{
    /// <summary>
    /// 卡片类型定义（系统级，非用户数据）
    /// </summary>
    [Table("CardTypes")]
    public class CardType
    {
        [Key]
        [MaxLength(50)]
        public string Id { get; set; } = string.Empty;  // 如 'character', 'location'

        [Required]
        [MaxLength(50)]
        public string Label { get; set; } = string.Empty;  // 显示名称：'角色', '地点'

        [MaxLength(10)]
        public string? Icon { get; set; }  // 图标：'🧙', '📍'

        [MaxLength(200)]
        public string? Description { get; set; }

        public int SortOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public bool IsSystem { get; set; } = true;  // 系统预设的不可删除

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }
}