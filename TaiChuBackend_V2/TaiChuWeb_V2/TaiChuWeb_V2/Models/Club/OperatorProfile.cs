using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UserEntity = TaiChuWeb_V2.Models.User.User;

namespace TaiChuWeb_V2.Models.Club
{
    /// <summary>
    /// 打手档案（扩展 User，仅申请成为打手的用户才有此记录）
    /// </summary>
    [Table("OperatorProfiles")]
    public class OperatorProfile
    {
        [Key]
        public Guid UserId { get; set; }

        // ===== 通用基础信息 =====
        [MaxLength(50)]
        public string Nickname { get; set; } = "";

        [MaxLength(20)]
        public string ContactType { get; set; } = "";

        [MaxLength(100)]
        public string ContactValue { get; set; } = "";

        [MaxLength(50)]
        public string OnlineTime { get; set; } = "";

        [MaxLength(2000)]
        public string? Intro { get; set; }

        // ===== 通用状态 =====
        [MaxLength(10)]
        public string AuditStatus { get; set; } = "pending";
        // pending / reviewing / approved / rejected / banned

        [MaxLength(500)]
        public string? AuditNote { get; set; }

        public Guid? ReviewerId { get; set; }
        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }

        // ===== 通用信誉（跨游戏） =====
        public int Reputation { get; set; } = 100;   // 打手信誉分
        public int TotalOrders { get; set; } = 0;    // 累计完成订单（所有游戏）

        // ⭐ 新增：评价统计（每次评价后重算）
        public int ReviewCount { get; set; } = 0;
        public double AvgSkill { get; set; } = 0;
        public double AvgAttitude { get; set; } = 0;
        public double AvgPunctual { get; set; } = 0;
        public double AvgOverall { get; set; } = 0;

        // ⭐ 新增：复购统计
        public int UniqueCustomers { get; set; } = 0;
        public int RepeatCustomers { get; set; } = 0;

        // ⭐ 新增：订单统计
        public int CompletedOrders { get; set; } = 0;
        public int CancelledOrders { get; set; } = 0;


        // ===== 钱包 =====
      /// <summary>可用余额</summary>
       [Column(TypeName = "decimal(12,2)")]
       public decimal Balance { get; set; } = 0;

       /// <summary>冻结余额（订单完成后的冻结期）</summary>
      [Column(TypeName = "decimal(12,2)")]
       public decimal FrozenBalance { get; set; } = 0;

        [ForeignKey("UserId")]
        public virtual UserEntity User { get; set; } = null!;

        public virtual ICollection<OperatorGameSkill> GameSkills { get; set; } = new List<OperatorGameSkill>();
    }
}