using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UserEntity = TaiChuWeb_V2.Models.User.User;

namespace TaiChuWeb_V2.Models.Club
{
    [Table("OperatorGameSkills")]
    public class OperatorGameSkill
    {
        [Key]
        public long Id { get; set; }

        public Guid UserId { get; set; }

        /// <summary>游戏代码：delta / apex / valorant / csgo / lol ...</summary>
        [MaxLength(20)]
        public string GameCode { get; set; } = "";

        /// <summary>游戏内显示名（冗余存储，方便展示，比如"三角洲行动"）</summary>
        [MaxLength(50)]
        public string GameName { get; set; } = "";

        // ===== 该游戏的申请信息 =====
        /// <summary>该游戏的指标（JSON），比如 {"kd":"4-5","matches":"1000+"}</summary>
        [Column(TypeName = "json")]
        public string MetricsJson { get; set; } = "{}";

        // ===== 该游戏的审核状态 =====
        [MaxLength(10)]
        public string AuditStatus { get; set; } = "pending";
        // pending / reviewing / approved / rejected / banned

        [MaxLength(500)]
        public string? AuditNote { get; set; }

        /// <summary>打手编号（该游戏专用，比如 OP-DELTA-0001）</summary>
        [MaxLength(30)]
        public string? Code { get; set; }

        // ===== 该游戏的等级 =====
        /// <summary>L1~L5</summary>
        [MaxLength(2)]
        public string? OperatorLevel { get; set; }

        // ===== 该游戏的复查 =====
        [MaxLength(20)]
        public string RecheckStatus { get; set; } = "none";
        // none / rechecking / flagged / demoted / suspended

        public DateTime? LastReviewAt { get; set; }
        public DateTime? NextRecheckAt { get; set; }

        [MaxLength(500)]
        public string? RecheckNote { get; set; }

        public int DemoteCount { get; set; } = 0;

        // ===== 该游戏的数据积累 =====
        public int OrdersInGame { get; set; } = 0;   // 该游戏完成的订单数

        // ⭐ 新增：订单拆分统计
        public int CompletedOrdersInGame { get; set; } = 0;
        public int FailedOrdersInGame { get; set; } = 0;

        public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ReviewedAt { get; set; }

        [ForeignKey("UserId")]
        public virtual UserEntity User { get; set; } = null!;
    }
}