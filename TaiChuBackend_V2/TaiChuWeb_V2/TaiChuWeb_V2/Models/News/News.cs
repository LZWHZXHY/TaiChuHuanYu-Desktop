using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.News
{
    [Table("News")]
    public class News
    {
        [Key]
        [Column(TypeName = "varchar(36)")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty; // 标题

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = "公告"; // 类型：比如 "公告", "更新", "活动"

        [MaxLength(1000)]
        public string? ImageUrl { get; set; } // 封面配图 (可选)

        public string? Content { get; set; } // 动态正文 (留给以后做详情页用)

        public bool IsPublished { get; set; } = true; // 是否已发布 (控制前端是否可见，可做草稿箱)

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // 发布/创建时间
    }
}
