using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Wiki
{
    [Table("wiki_article_revisions")]
    public class WikiArticleRevision
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(36)]
        public string ArticleId { get; set; } = string.Empty;

        // 🌟 1. 建立历史链条：指向前一个修订版本
        // 当你要做“版本比对”时，直接查这个字段即可获取上一个版本的快照
        public int? PreviousRevisionId { get; set; }

        [Column(TypeName = "longtext")]
        public string Content { get; set; } = string.Empty;

        // 🌟 2. 语义修正：从 Author 改为 Contributor，明确这是本次修订的贡献者
        [Required]
        [MaxLength(36)]
        public string ContributorId { get; set; } = string.Empty;

        [Required]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public virtual WikiCategory Category { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? EditSummary { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 状态：0 = 待审核, 1 = 已通过, 2 = 已拒绝
        public int Status { get; set; } = 0;

        [MaxLength(36)]
        public string? ReviewerId { get; set; }

        [MaxLength(500)]
        public string? ReviewRemarks { get; set; }

        public DateTime? ReviewedAt { get; set; }
    }
}