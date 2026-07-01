using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Models.Activity;

public class ActivityType
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(50)]
    public string Name { get; set; } = string.Empty; // 显示名称

    [MaxLength(50)]
    public string? Icon { get; set; } // 图标（可选）

    public int SortOrder { get; set; } = 0; // 排序

    public bool IsActive { get; set; } = true; // 是否启用

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}