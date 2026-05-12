using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.LingMai
{
    [Table("space_members")]
    public class SpaceMember
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid SpaceId { get; set; } // 关联到具体的空间

        [Required]
        public string UserId { get; set; } // 社区注册用户的 ID


        [Required]
        public string Role { get; set; } = "Member";

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
