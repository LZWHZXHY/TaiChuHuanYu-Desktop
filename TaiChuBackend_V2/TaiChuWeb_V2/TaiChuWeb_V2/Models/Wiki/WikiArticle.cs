using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Wiki
{
    [Table("wiki_articles")]
    public class WikiArticle
    {
        [Key]
        [MaxLength(36)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public int CategoryId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        // 当前生效的修订版本Id (指向 wiki_article_revisions 表)
        public int? CurrentRevisionId { get; set; }

        public int ViewCount { get; set; } = 0;

        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}