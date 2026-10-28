using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Community
{
    /// <summary>官方开发项目（面向社区成员展示进度）。</summary>
    [Table("DevProjects")]
    public class DevProject
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        /// <summary>详细内容（长段落，支持换行）。</summary>
        [Column(TypeName = "longtext")]
        public string? Content { get; set; }

        /// <summary>分类：ai / mod / game / world / ip / other</summary>
        [MaxLength(50)]
        public string Category { get; set; } = "other";

        /// <summary>planning / ongoing / done / paused</summary>
        [MaxLength(20)]
        public string Status { get; set; } = "ongoing";

        /// <summary>进度百分比 0-100</summary>
        public int Progress { get; set; } = 0;

        /// <summary>里程碑 JSON：[{ "title": "...", "done": true }]</summary>
        [Column(TypeName = "json")]
        public string Milestones { get; set; } = "[]";

        /// <summary>项目类型：dev（开发）/ research（研究）</summary>
        [MaxLength(20)]
        public string Type { get; set; } = "dev";

        /// <summary>参考文献 JSON：[{id,title,authors,venue,year,url,note}]</summary>
        [Column(TypeName = "longtext")]
        public string ReferencesJson { get; set; } = "[]";

        /// <summary>实验/调研数据 JSON：[{id,name,value,unit,note}]</summary>
        [Column(TypeName = "longtext")]
        public string DataJson { get; set; } = "[]";

        /// <summary>研究日志 JSON：[{id,date,title,content}]</summary>
        [Column(TypeName = "longtext")]
        public string LogsJson { get; set; } = "[]";

        /// <summary>emoji 图标</summary>
        [MaxLength(10)]
        public string Icon { get; set; } = "🚀";

        public int SortOrder { get; set; } = 0;

        /// <summary>父项目 Id（子项目所属的系列/大项目，可空）。</summary>
        public Guid? ParentId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
