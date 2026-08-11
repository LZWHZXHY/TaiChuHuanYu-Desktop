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

        public int UsedNotes { get; set; } = 0;
        public int UsedSpaces { get; set; } = 0;
        public int MaxNotes { get; set; } = 100; // 最大笔记数，默认100，可通过升级或购买扩展
        public int MaxSpaces { get; set; } = 1; // 最大空间数，默认1，可通过升级或购买扩展

        public int UsedProjectCount { get; set; } = 0;

        public int MaxProjectCount { get; set; } = 10; // 最大项目数，默认10，可通过升级或购买扩展


        // ===== 🆕 世界观配额 =====
        /// <summary>
        /// 已使用的世界观数量（创建世界观项目时 +1，删除时 -1）
        /// </summary>
        public int UsedWorldCount { get; set; } = 0;

        /// <summary>
        /// 可创建的世界观总数（默认 3）
        /// </summary>
        public int MaxWorldCount { get; set; } = 3;

        /// <summary>
        /// 每个世界观的最大词条数（默认 100）
        /// </summary>
        public int MaxCardsPerWorld { get; set; } = 100;

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;


       


    }


    public class UserExpLog
    {
        [Key]  // ✅ 明确主键
        public long Id { get; set; }

        public Guid UserId { get; set; }  // 谁变了

        public int Change { get; set; }   // 变化量（+50 或 -50）

        [MaxLength(200)]  // ✅ 建议加上长度限制
        public string Reason { get; set; } // 为什么变

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // 什么时候变的

        [ForeignKey("UserId")]
        public virtual User User { get; set; } // ✅ 建议加上导航属性
    }
}
