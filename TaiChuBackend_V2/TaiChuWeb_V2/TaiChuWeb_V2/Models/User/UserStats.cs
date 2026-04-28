using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Models.User
{
    public class UserStats
    {
        [Key]
        public Guid UserId { get; set; }

        public int Level { get; set; } = 1;
        public long Experience { get; set; } = 0;
        public decimal Points { get; set; } = 0; // 积分
        public string? Title { get; set; } // 头衔
        public int CurrentSignStreak { get; set; } = 0; // 当前连续签到天数
        public int MaxSignStreak { get; set; } = 0;     // 历史最高连续签到天数
        public DateTime? LastSignDate { get; set; }     // 上次签到日期

        public virtual User User { get; set; } = null!;
    }
}
