using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Models.Interact
{
    public class UserInteraction
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public Guid UserId { get; set; } // 谁操作的

        [Required]
        public string TargetId { get; set; } = string.Empty; // 目标的 ID (用 string 兼容 Int 或 Guid)

        [Required]
        [MaxLength(20)]
        public string TargetType { get; set; } = string.Empty; // "Artwork", "Post", "Blog"......

        [Required]
        [MaxLength(20)]
        public string ActionType { get; set; } = string.Empty; // "Like", "Favorite", "Report"

        public string? ExtraData { get; set; } // 如果是举报，这里存理由

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
