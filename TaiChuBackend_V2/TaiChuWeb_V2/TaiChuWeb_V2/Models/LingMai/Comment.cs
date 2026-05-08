// TaiChuWeb_V2/Models/LingMai/Comment.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.LingMai
{
    [Table("comments")]
    public class Comment
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        // 🌟 多态关联：支持对博客、随笔、简语的评论，或对艺术作品的评论
        public Guid? NoteId { get; set; }
        public int? ArtworkId { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty; // 评论者 ID

        [Required]
        [MaxLength(1000)]
        public string Content { get; set; } = string.Empty; // 评论正文

        // 🌟 盖楼/回复功能
        public Guid? ParentId { get; set; }

        [ForeignKey(nameof(ParentId))]
        public virtual Comment? Parent { get; set; }

        public virtual ICollection<Comment> Replies { get; set; } = new List<Comment>();

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}