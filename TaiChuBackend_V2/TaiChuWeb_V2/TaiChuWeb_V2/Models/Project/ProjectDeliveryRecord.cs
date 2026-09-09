using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Project
{
    [Table("project_delivery_records")]
    public class ProjectDeliveryRecord
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string ProjectId { get; set; } = string.Empty;

        // 🌟 1. 绑定灵魂：不可变的底层用户ID
        [Required]
        public Guid MemberId { get; set; }

        // 🌟 2. 历史快照：记录发生时该用户的名字，防改名抵赖
        [Required]
        [MaxLength(100)]
        public string RecordedName { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string TaskName { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string TimingStatus { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string QualityStatus { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string CommStatus { get; set; } = string.Empty;

        public int ReworkCount { get; set; } = 0;

        [MaxLength(1000)]
        public string? AdminNote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ProjectId")]
        public virtual Project? Project { get; set; }

        [ForeignKey("MemberId")]
        public virtual User.User? Member { get; set; }
    }
}