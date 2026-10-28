using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Community
{
    /// <summary>社区规则（层级：分类 → 条款 → 子条款）。</summary>
    [Table("CommunityRules")]
    public class CommunityRule
    {
        [Key]
        public int Id { get; set; }

        /// <summary>父规则 ID（NULL 表示顶层分类）</summary>
        public int? ParentId { get; set; }

        /// <summary>条款编号，例如 1.1 / 2.1.1；顶层分类为空</summary>
        [MaxLength(20)]
        public string? Code { get; set; }

        /// <summary>标题（顶层分类名，例如「通用规则」；条款可留空）</summary>
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>规则内容 / 分类描述</summary>
        [MaxLength(2000)]
        public string? Content { get; set; }

        /// <summary>处理方式</summary>
        [MaxLength(500)]
        public string? Penalty { get; set; }

        public int SortOrder { get; set; }

        public bool IsActive { get; set; } = true;

        [MaxLength(20)]
        public string Version { get; set; } = "1.0";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
