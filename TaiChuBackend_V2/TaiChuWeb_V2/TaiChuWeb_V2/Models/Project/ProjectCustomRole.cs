using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Project
{
    [Table("ProjectCustomRoles")]
    public class ProjectCustomRole
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        /// <summary>
        /// 归属的项目 Id（为 null 表示系统默认预设身份模板，如全站预置的“观察者”、“开发执行”）
        /// </summary>
        public string? ProjectId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty; // 身份名称，如："主创策划"、"原画执行"

        [MaxLength(20)]
        public string ColorCode { get; set; } = "#1a1a1a"; // 身份标签颜色

        public bool IsSystemDefault { get; set; } = false; // 是否为不可删除的系统基准身份

        /// <summary>
        /// 绑定的权限 Code 列表（以 JSON 数组形式存储，如 ["task:move_self", "report:submit"]）
        /// </summary>
        [Column(TypeName = "text")]
        public string PermissionsJson { get; set; } = "[]";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}