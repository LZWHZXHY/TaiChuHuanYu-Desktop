using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Project
{
    [Table("ProjectPermissionDefinitions")]
    public class ProjectPermissionDefinition
    {
        /// <summary>
        /// 权限唯一标识符（如: task:create, task:move_self, report:submit, doc:pin）
        /// </summary>
        [Key]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty; // 如："提交汇报"

        [MaxLength(50)]
        public string Module { get; set; } = "Task"; // 归属模块：Task / Report / Document / Member / Setting

        [MaxLength(200)]
        public string? Description { get; set; }
    }
}