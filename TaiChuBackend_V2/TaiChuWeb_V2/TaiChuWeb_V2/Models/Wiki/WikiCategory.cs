using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Wiki
{
    [Table("wiki_categories")]
    public class WikiCategory
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        // 父分类Id，用于支持多级分类目录
        public int? ParentId { get; set; }

        // 排序权重
        public int SortOrder { get; set; } = 0;
    }
}