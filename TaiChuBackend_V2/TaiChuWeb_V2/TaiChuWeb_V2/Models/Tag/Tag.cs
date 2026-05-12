using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Tag
{
    // 1. 标签定义表：存储全局唯一的标签
    [Table("tags")]
    public class Tag
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();


        [Required]
        public Guid SpaceId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        // 规范化名称，比如用于 URL，或防止大小写重复（"C#" -> "c#"）
        [Required]
        [MaxLength(50)]
        public string NormalizedName { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
