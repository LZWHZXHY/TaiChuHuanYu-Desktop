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
        public Guid OperatorUserId { get; set; }   // 主打手（保留兼容，= OperatorUserIdsJson 的第一个）

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

        /// <summary>用户点击"我已完成付款"的时间（null = 未标记）</summary>
        public DateTime? UserPaidAt { get; set; }

        public DateTime? PaidConfirmedAt { get; set; }

        // ===== 客服归属 =====
        /// <summary>订单来源：cs（客服拉单）/ self（自己找的）</summary>
        [MaxLength(10)]
        public string Source { get; set; } = "self";

        /// <summary>归属客服（客服拉单时为指定ID；自己找的时为系统随机分配）</summary>
        public Guid? CustomerServiceId { get; set; }

        /// <summary>客服确认收款时间</summary>
        public DateTime? CsConfirmedAt { get; set; }

        /// <summary>客服确认收款操作人</summary>
        public Guid? CsConfirmedBy { get; set; }

        [MaxLength(500)]
        public string? CsRemark { get; set; }

        // ===== ⭐ 多打手 + 定价拆分（新增） =====
        /// <summary>
        /// 所有打手的用户 ID 列表（JSON 数组）
        /// 护航单固定 2 个；陪玩单 1-2 个
        /// </summary>
        [Column(TypeName = "json")]
        public string OperatorUserIdsJson { get; set; } = "[]";

        /// <summary>基础价（订单类型定义价，不含打手增幅）</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal BasePrice { get; set; } = 0;

        /// <summary>打手增幅总额（陪玩单按等级算；护航单为 0）</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal OperatorSurcharge { get; set; } = 0;

        /// <summary>整单抽成率（多人取平均）</summary>
        [Column(TypeName = "decimal(4,3)")]
        public decimal CommissionRate { get; set; } = 0.20m;

        /// <summary>平台分成金额（= Price × CommissionRate）</summary>
        [Column(TypeName = "decimal(10,2)")]
        public decimal PlatformShare { get; set; } = 0;

        /// <summary>结算截图 URL（老板可选上传，上传后享纠纷处理保障）</summary>
        [MaxLength(500)]
        public string? SettlementProofUrl { get; set; }

        /// <summary>老板是否提交了结算截图</summary>
        public bool SettlementProofUploaded { get; set; } = false;

        /// <summary>炸单次数（撤离失败次数，结单时老板填写）</summary>
        public int FailedCount { get; set; } = 0;


    }
}