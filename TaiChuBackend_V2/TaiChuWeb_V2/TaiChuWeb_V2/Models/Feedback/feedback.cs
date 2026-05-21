using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Feedback
{
    [Table("Feedbacks")]
    public class Feedback
    {
        [Key]
        [Column(TypeName = "varchar(36)")]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string Content { get; set; } = string.Empty; // 反馈内容

        [MaxLength(100)]
        public string? ContactInfo { get; set; } // 联系方式 (选填)

        [MaxLength(36)]
        public string? UserId { get; set; } // 提交人的用户ID

        [MaxLength(1000)]
        public string? ImageUrls { get; set; } // 存放图片/GIF的URL

        // 🌟 新增：是否匿名提交 (默认 false)
        public bool IsAnonymous { get; set; } = false;

        public int Status { get; set; } = 0; // 处理状态: 0=待处理, 1=已解决

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // 提交时间
    }
}