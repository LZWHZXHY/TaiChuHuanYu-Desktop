using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UserEntity = TaiChuWeb_V2.Models.User.User;

namespace TaiChuWeb_V2.Models.Club
{
    /// <summary>客服账号</summary>
    [Table("CustomerServices")]
    public class CustomerService
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>关联系统用户（客服也是登录用户）</summary>
        public Guid UserId { get; set; }

        [MaxLength(50)]
        public string Name { get; set; } = "";

        /// <summary>对外展示的客服微信</summary>
        [MaxLength(100)]
        public string Wechat { get; set; } = "";

        [MaxLength(200)]
        public string? Avatar { get; set; }

        /// <summary>online / busy / offline</summary>
        [MaxLength(10)]
        public string Status { get; set; } = "offline";

        /// <summary>当前在手订单数（用于"载单 3/10"展示）</summary>
        public int CurrentLoad { get; set; } = 0;

        /// <summary>最大同时跟进订单数（超出不再自动分配）</summary>
        public int Capacity { get; set; } = 10;

        /// <summary>是否参与自动分配（休息中的客服置 false）</summary>
        public bool IsAssignable { get; set; } = true;

        public int SortOrder { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        // ===== ⭐ 审核状态 =====
        /// <summary>pending / approved / rejected / banned</summary>
        [MaxLength(10)]
        public string AuditStatus { get; set; } = "pending";

        public DateTime? AppliedAt { get; set; }
        public DateTime? ReviewedAt { get; set; }

        [MaxLength(500)]
        public string? AuditNote { get; set; }

        // ===== ⭐ 申请信息 =====
        [MaxLength(2000)]
        public string? Intro { get; set; }

        [MaxLength(50)]
        public string? OnlineTime { get; set; }

        // ===== ⭐ 评价统计 =====
        public int ReviewCount { get; set; } = 0;
        public double AvgResponse { get; set; } = 0;
        public double AvgAttitude { get; set; } = 0;
        public double AvgProfessional { get; set; } = 0;
        public double AvgOverall { get; set; } = 0;


        // ===== ⭐ 钱包 =====
        /// <summary>可用余额</summary>
        [Column(TypeName = "decimal(12,2)")]
        public decimal Balance { get; set; } = 0;

        /// <summary>冻结余额（订单完成后的冻结期）</summary>
        [Column(TypeName = "decimal(12,2)")]
        public decimal FrozenBalance { get; set; } = 0;

        /// <summary>累计收益</summary>
        [Column(TypeName = "decimal(12,2)")]
        public decimal TotalIncome { get; set; } = 0;


        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public virtual UserEntity User { get; set; } = null!;
    }
}