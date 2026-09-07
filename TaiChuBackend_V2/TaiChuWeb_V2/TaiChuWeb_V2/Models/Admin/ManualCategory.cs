using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Admin
{
    [Table("manual_categories")]
    public class ManualCategory
    {
        [Key]
        [Column("id")]
        public int Id { get; set; }

        /// <summary>
        /// 分类名称（如：快速上手、核心功能、常见问题）
        /// </summary>
        [Required]
        [MaxLength(64)]
        [Column("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// 分类排序权重，越小越靠前
        /// </summary>
        [Column("sort_order")]
        public int SortOrder { get; set; } = 0;

        [Column("created_at")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 该分类关联的所有手册文章
        /// </summary>
        public List<ManualArticle> Articles { get; set; } = [];
    }
}