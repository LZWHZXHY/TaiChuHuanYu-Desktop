using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Interact
{
    /// <summary>用户关注关系。</summary>
    [Table("Follows")]
    public class Follow
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>关注者。</summary>
        public Guid FollowerId { get; set; }

        /// <summary>被关注者。</summary>
        public Guid FolloweeId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
