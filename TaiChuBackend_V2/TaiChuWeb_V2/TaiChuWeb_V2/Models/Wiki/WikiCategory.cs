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

        // 🌟 新增：所有者 ID (关联到 User)
        // 如果为 null，视为“社区共有”，由管理员审核；如果不为 null，由该所有者审核
        public string? OwnerId { get; set; }

        // 🌟 新增：所有权模式
        // 0 = 社区共有 (管理员审)，1 = 私有空间 (所有者审)
        public int OwnershipType { get; set; }
        // 排序权重
        public int SortOrder { get; set; } = 0;

        public bool NeedsReview { get; set; } = true;
    }
}