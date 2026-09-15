// TaiChuWeb_V2/Models/FinalNodes/ReviewCard.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TaiChuWeb_V2.Models.FinalNodes
{
    /// <summary>
    /// FSRS 复习卡片：一张卡指向一个 Block
    /// </summary>
    [Table("review_cards")]
    [Index(nameof(BlockId), Name = "IX_ReviewCards_Block")]
    [Index(nameof(Due), Name = "IX_ReviewCards_Due")]
    public class ReviewCard
    {
        [Key]
        [MaxLength(36)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(64)]
        public string BlockId { get; set; } = string.Empty;

        [Required]
        [MaxLength(36)]
        public string OwnerId { get; set; } = string.Empty;

        // 0=New 1=Learning 2=Review 3=Relearning
        public int State { get; set; } = 0;

        public double Stability { get; set; }
        public double Difficulty { get; set; }

        public DateTime Due { get; set; } = DateTime.UtcNow;
        public DateTime? LastReview { get; set; }

        public int Reps { get; set; }
        public int Lapses { get; set; }

        public int ElapsedDays { get; set; }
        public int ScheduledDays { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}