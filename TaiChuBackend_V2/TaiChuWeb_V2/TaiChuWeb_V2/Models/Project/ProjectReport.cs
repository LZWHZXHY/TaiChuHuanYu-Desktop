using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Project
{
    [Table("ProjectReports")]
    public class ProjectReport
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string ProjectId { get; set; } = string.Empty;
        public virtual Project Project { get; set; } = null!;

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string AuthorName { get; set; } = string.Empty;

        /// <summary>
        /// 汇报类型：Daily (日报) / Weekly (周报) / Monthly (月报)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = "Weekly";

        /// <summary>
        /// 涉及业务模块 / 任务意图
        /// </summary>
        [MaxLength(100)]
        public string Module { get; set; } = "常规推进";

        /// <summary>
        /// 投入工时 (h)
        /// </summary>
        public decimal Hours { get; set; } = 0m;

        /// <summary>
        /// 主要产出与进展
        /// </summary>
        [Required]
        [Column(TypeName = "text")]
        public string Summary { get; set; } = string.Empty;

        /// <summary>
        /// 阻碍卡点 / 需要协助的问题
        /// </summary>
        [Column(TypeName = "text")]
        public string? Blockers { get; set; }

        /// <summary>
        /// 下一阶段排期 / 计划
        /// </summary>
        [MaxLength(500)]
        public string? NextPlan { get; set; }

        /// <summary>
        /// 存证截图 URL 数组 (以 JSON 字符串存储)
        /// </summary>
        [Column(TypeName = "text")]
        public string ImagesJson { get; set; } = "[]";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}