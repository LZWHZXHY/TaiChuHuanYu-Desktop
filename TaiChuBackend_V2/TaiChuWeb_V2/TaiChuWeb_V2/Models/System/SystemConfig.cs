using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Models.System
{
    public class SystemConfig
    {
        [Key]
        public string Key { get; set; } = string.Empty;  // 例如 "Game:CreateCostExp"
        public string Value { get; set; } = string.Empty; // 值
        public string Description { get; set; } = string.Empty; // 描述（便于管理）
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string? UpdatedBy { get; set; } // 谁修改的
    }
}
