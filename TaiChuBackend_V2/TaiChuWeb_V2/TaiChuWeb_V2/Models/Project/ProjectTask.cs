using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Models.Project
{
    public class ProjectTask
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string ProjectId { get; set; }
        public Project Project { get; set; }
        [MaxLength(2000)]
        public string? Description { get; set; }
        [Required]
        [MaxLength(200)]
        public string Title { get; set; }

        public int Status { get; set; } = 0; // 0=Todo, 1=Doing, 2=Done

        // 🌟 核心改动 1：动态分类外键 (允许为空，表示未分类)
        public string? CategoryId { get; set; }
        public ProjectCategory Category { get; set; }
        public int Priority { get; set; } = 1;

        public DateTime? StartDate { get; set; }

        // 🌟 新增：截止期限
        public DateTime? DueDate { get; set; }
        // 🌟 核心改动 2：自由指派外键
        // 指向你的 User 表，前端可以通过下拉菜单把任务指派给项目里的任何人
        public string? AssigneeId { get; set; }
        [MaxLength(500)]
        public string? Tags { get; set; }
        public decimal Cost { get; set; } = 0;
        public double SortOrder { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
