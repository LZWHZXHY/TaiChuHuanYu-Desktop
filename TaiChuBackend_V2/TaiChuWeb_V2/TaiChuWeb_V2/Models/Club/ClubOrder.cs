using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Club
{
    /// <summary>
    /// 订单主表（所有游戏通用）
    /// 游戏特有参数走 ParamsJson
    /// </summary>
    [Table("ClubOrders")]
    public class ClubOrder
    {
        [Key]
        public long Id { get; set; }

        /// <summary>订单号：ORD-20260912-0001</summary>
        [MaxLength(30)]
        public string OrderNo { get; set; } = "";

        // ===== 三方关系 =====
        public Guid CustomerId { get; set; }       // 老板
        public Guid OperatorUserId { get; set; }   // 打手

        // ===== 游戏 + 订单类型 =====
        [MaxLength(20)]
        public string GameCode { get; set; } = "";

        [MaxLength(30)]
        public string OrderTypeCode { get; set; } = "";

        /// <summary>
        /// 订单特有参数（JSON）
        /// 三角洲：{"保底":"600W","模式":"绝密"}
        /// LOL：  {"起始段位":"黄金","目标段位":"铂金"}
        /// </summary>
        [Column(TypeName = "json")]
        public string ParamsJson { get; set; } = "{}";

        // ===== 通用字段 =====
        [Column(TypeName = "decimal(10,2)")]
        public decimal Price { get; set; }

        /// <summary>状态：pending / accepted / processing / completed / cancelled / refunded / disputed</summary>
        [MaxLength(20)]
        public string Status { get; set; } = "pending";

        [MaxLength(500)]
        public string? Remark { get; set; }

        // ===== 时间轴 =====
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcceptedAt { get; set; }     // 打手接单
        public DateTime? StartedAt { get; set; }      // 开始服务
        public DateTime? CompletedAt { get; set; }    // 完成
        public DateTime? CancelledAt { get; set; }    // 取消

        // ===== 导航（可选） =====
        [ForeignKey("OperatorUserId")]
        public virtual OperatorProfile? Operator { get; set; }
    }
}