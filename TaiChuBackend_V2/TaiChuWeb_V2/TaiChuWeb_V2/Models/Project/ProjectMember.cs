namespace TaiChuWeb_V2.Models.Project
{
    public class ProjectMember
    {
        // 联合主键 (ProjectId + UserId)
        public string ProjectId { get; set; }
        public Project Project { get; set; }

        public string UserId { get; set; }
        // public User User { get; set; } // 关联你现有的用户表

        // 权限角色：0=管理员, 1=普通开发者, 2=只读观察者
        public int RoleId { get; set; }

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
