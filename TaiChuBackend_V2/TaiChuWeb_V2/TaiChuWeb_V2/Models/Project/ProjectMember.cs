using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;

namespace TaiChuWeb_V2.Models.Project
{
    public class ProjectMember
    {
        // 联合主键之一：项目 ID
        [Required]
        public string ProjectId { get; set; } = string.Empty;
        public virtual Project Project { get; set; } = null!;

        // 联合主键之二：用户 ID
        [Required]
        public string UserId { get; set; } = string.Empty;

        /// <summary>
        /// 绑定的身份角色 ID 列表（以 JSON 数组格式持久化存入数据库）
        /// 示例：["role_viewer", "role_task_executor", "role_reporter"]
        /// </summary>
        [MaxLength(1000)]
        public string RoleIdsJson { get; set; } = "[]";

        /// <summary>
        /// 内存层面的角色 ID 列表（不映射至数据库字段，方便业务代码直接操作 List）
        /// </summary>
        [NotMapped]
        public List<string> RoleIds
        {
            get => string.IsNullOrWhiteSpace(RoleIdsJson)
                ? new List<string>()
                : JsonSerializer.Deserialize<List<string>>(RoleIdsJson) ?? new List<string>();
            set => RoleIdsJson = JsonSerializer.Serialize(value ?? new List<string>());
        }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}