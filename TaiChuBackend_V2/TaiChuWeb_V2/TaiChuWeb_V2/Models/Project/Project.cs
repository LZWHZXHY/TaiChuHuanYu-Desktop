using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Models.Project
{
    public class Project
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; }

        [MaxLength(500)]
        public string Description { get; set; }

        // 🌟 新增 1：可见性
        // false = 绝对私有（别人搜不到）
        // true = 公开展示（别人能看到简介和公开文档，但不能改任务）
        public bool IsPublic { get; set; } = false;

        // 🌟 新增 2：准入策略
        // 0 = 仅限邀请 (Invite Only) - 默认最安全
        // 1 = 允许申请 (Require Approval) - 别人可以点“申请加入”，管理员审批
        // 2 = 自由加入 (Open) - 任何看到的人点一下就进来了（适合开源/纯公开打卡项目）
        public int JoinPolicy { get; set; } = 0;
        // 🌟 时间维度：均为可选，赋予项目弹性
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int Status { get; set; } = 0;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 导航属性
        public ICollection<ProjectMember> Members { get; set; }
        public ICollection<ProjectTask> Tasks { get; set; }
        public ICollection<ProjectDocument> Documents { get; set; }
        public ICollection<ProjectCategory> Categories { get; set; }
        // 关联下面的申请表
        public ICollection<ProjectApplication> Applications { get; set; }
    }
}
