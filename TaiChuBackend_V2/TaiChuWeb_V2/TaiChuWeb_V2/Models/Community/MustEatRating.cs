using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TaiChuWeb_V2.Models.Community
{
    /// <summary>必吃榜条目的用户评分（1-5 星，一人一条）。</summary>
    [Table("MustEatRatings")]
    [Index(nameof(ItemId), nameof(UserId), IsUnique = true)]
    public class MustEatRating
    {
        [Key]
        public int Id { get; set; }

        public int ItemId { get; set; }

        public Guid UserId { get; set; }

        /// <summary>1-5 分</summary>
        public int Score { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
