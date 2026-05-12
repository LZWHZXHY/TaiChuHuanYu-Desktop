using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Models.Project
{
    public class ProjectCategory
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string ProjectId { get; set; }
        public Project Project { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } // 例如："程序", "剧本", "UI/UX"

        [MaxLength(20)]
        public string ColorCode { get; set; } // 例如：#007bff (用于复刻 HacknPlan 的彩色条)

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
