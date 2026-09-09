using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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

        // 🌟 动态分类外键 (允许为空，表示未分类)
        public string? CategoryId { get; set; }
        public ProjectCategory? Category { get; set; }

        public int Priority { get; set; } = 1; // 0=低缓, 1=常规, 2=高优, 3=极度紧急

        public DateTime? StartDate { get; set; }

        // 截止期限
        public DateTime? DueDate { get; set; }

        // 自由指派外键 (指向 User 表的主键 Guid 字符串)
        public string? AssigneeId { get; set; }

        [MaxLength(500)]
        public string? Tags { get; set; }

        public decimal Cost { get; set; } = 0;

        public double SortOrder { get; set; }

        // ===== 🌟 核心量化指标与贡献核算新增 =====

        /// <summary>
        /// 预估耗时（小时，支持小数如 1.5h）
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal EstimatedHours { get; set; } = 0m;

        /// <summary>
        /// 实际耗时（小时，可与工作汇报流水核对）
        /// </summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal ActualHours { get; set; } = 0m;

        /// <summary>
        /// 意图复杂度 / 贡献点数 (Story Points，默认 1 点)
        /// 用于衡量任务工作量权重及成员贡献统计
        /// </summary>
        public int Points { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}