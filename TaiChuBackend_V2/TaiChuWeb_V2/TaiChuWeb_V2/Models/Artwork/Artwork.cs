using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaiChuWeb_V2.Models.User; // 引用你已有的用户命名空间

namespace TaiChuWeb_V2.Models.Artwork
{
    public class Artwork
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        // --- 核心关联：直接指向你的 User 类 ---
        [Required]
        public Guid UploaderId { get; set; }

        [ForeignKey("UploaderId")]
        public virtual User.User Uploader { get; set; } = null!;

        // --- 媒体关联 ---
        public virtual ICollection<ArtworkImage> Images { get; set; } = new List<ArtworkImage>();

        public DateTime UploadAt { get; set; } = DateTime.UtcNow;

        // 既然你有 UserStats，以后甚至可以给上传作品的群成员加经验值/积分
        public bool IsApproved { get; set; } = true;
    }
}