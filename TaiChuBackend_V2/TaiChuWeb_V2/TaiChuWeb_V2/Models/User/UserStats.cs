using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.User
{
    public class UserStats
    {
        [Key]
        public Guid UserId { get; set; }

        // 标记为 NotMapped，告诉 EF Core 数据库里没有这一列
        [NotMapped]
        public int Level => CalculateLevel(Experience);

        
        public static int CalculateLevel(long exp)
        {
            if (exp <= 0) return 0;
            int level = (int)Math.Sqrt(exp / 100.0);
            return level;
        }

        public long GetNextLevelExp(int currentLevel)
        {
            return (long)(100 * Math.Pow(currentLevel + 1, 2));
        }

        public long Experience { get; set; } = 0;

        public int Reputation { get; set; } = 100; // 信誉分，初始100。用于违规扣分或高质量奖励。
        public string? Title { get; set; } // 头衔
        public int CurrentSignStreak { get; set; } = 0; // 当前连续签到天数
        public int MaxSignStreak { get; set; } = 0;     // 历史最高连续签到天数
        public DateTime? LastSignDate { get; set; }     // 上次签到日期

        public int ArtworksCount { get; set; } = 0;    // 发布的作品总数
        public int LikesReceived { get; set; } = 0;    // 获得的获赞总数
        public int FollowingCount { get; set; } = 0;   // 关注人数
        public int FollowersCount { get; set; } = 0;   // 粉丝人数


        [ForeignKey("UserId")] // 明确外键关系
        public virtual User User { get; set; } = null!;
    }
}
