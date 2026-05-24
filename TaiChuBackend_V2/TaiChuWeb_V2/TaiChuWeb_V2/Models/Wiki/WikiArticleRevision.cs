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


        [Column(TypeName = "longtext")]
        public string Content { get; set; } = string.Empty;


        [Required]
        [MaxLength(36)]
        public string AuthorId { get; set; } = string.Empty;

        [Required]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public virtual WikiCategory Category { get; set; } = null!;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        // 本次修改的摘要说明（如：“修正了错别字”、“补充了历史背景”）
        [MaxLength(255)]
        public string? EditSummary { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 状态：0 = 待审核 (Pending), 1 = 已通过 (Approved), 2 = 已拒绝 (Rejected)
        public int Status { get; set; } = 0;

        [MaxLength(36)]
        public string? ReviewerId { get; set; }

        [MaxLength(500)]
        public string? ReviewRemarks { get; set; }

        public DateTime? ReviewedAt { get; set; }
    }
}