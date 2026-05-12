using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Models.Project
{
    public class ProjectApplication
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string ProjectId { get; set; }
        public Project Project { get; set; }

        [Required]
        public string UserId { get; set; } // 申请人的 ID

        [MaxLength(500)]
        public string Message { get; set; } // 申请留言，例如："我是做 3D 动作设计的，对你们的项目很感兴趣！"

        // 审批状态：0 = 待审批 (Pending), 1 = 已同意 (Approved), 2 = 已拒绝 (Rejected)
        public int Status { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; } // 处理时间
    }
}
