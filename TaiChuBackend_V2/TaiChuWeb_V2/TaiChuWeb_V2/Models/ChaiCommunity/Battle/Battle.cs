using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaiChuWeb_V2.Models.ChaiCommunity;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Models.ChaiCommunity.Battle
{
    [Table("Battles")]
    public class Battle
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        // ===== 基本信息 =====
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;


        // ============================================================
        // 新增：指定对手信息（在发起时设定，独立于实际参与者）
        // ============================================================

        /// <summary>
        /// 是否为公开约战（true=任何人可报名，false=仅限指定对手）
        /// </summary>
        public bool IsPublic { get; set; } = true;

        /// <summary>
        /// 指定对手的OC列表（JSON格式：{ "userId": ["ocId1", "ocId2"] }）
        /// 仅在 IsPublic = false 时有效
        /// </summary>
        [Column(TypeName = "json")]
        public string? OpponentOcIds { get; set; }



        public string? Content { get; set; }

        [MaxLength(500)]
        public string? CoverUrl { get; set; }

        [MaxLength(100)]
        public string? BattleType { get; set; }  // 用户自定义，如 "2v2v2"

        public string? Rules { get; set; }       // 详细规则

        [MaxLength(20)]
        public string JudgmentType { get; set; } = "vote";

        // ===== ⭐ 战斗配置（完全自由，JSON存储） =====
        [Column(TypeName = "json")]
        public string BattleConfigJson { get; set; } = "{}";

        // ===== 状态 =====
        [MaxLength(20)]
        public string Status { get; set; } = "open";  // open / ongoing / judging / finished / cancelled

        [MaxLength(20)]
        public string? Result { get; set; }

        public string? ResultDescription { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RegistrationDeadline { get; set; }
        public DateTime? SubmissionDeadline { get; set; }
        public DateTime? FinishedAt { get; set; }

        [MaxLength(36)]
        public string? SurveyId { get; set; }

        // ===== ⭐ 导航属性：所有参与者（支持多人） =====
        public virtual ICollection<BattleParticipant> Participants { get; set; } = new List<BattleParticipant>();

        public virtual ICollection<BattleSubmission> Submissions { get; set; } = new List<BattleSubmission>();
    }

    /// <summary>
    /// 约战参与者（一个用户可携带多个OC）
    /// </summary>
    [Table("BattleParticipants")]
    public class BattleParticipant
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();


        // BattleParticipant 类中添加：
        public virtual ICollection<BattleSubmission>? Submissions { get; set; }


        public string BattleId { get; set; } = string.Empty;

        public Guid UserId { get; set; }

        [MaxLength(100)]
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// ⭐ 该参与者携带的OC列表（JSON数组，支持多个OC）
        /// </summary>
        [Column(TypeName = "json")]
        public string OcIdsJson { get; set; } = "[]";

        /// <summary>
        /// 该参与者携带的OC名称列表（冗余显示）
        /// </summary>
        public string OcNamesJson { get; set; } = "[]";

        /// <summary>
        /// 队伍名称（可选，多人组队时使用）
        /// </summary>
        [MaxLength(50)]
        public string? TeamName { get; set; }

        /// <summary>
        /// 队伍编号（用于组队）
        /// </summary>
        public int? TeamNumber { get; set; }

        [MaxLength(20)]
        public string Status { get; set; } = "registered";  // registered / submitted / eliminated / finished

        [MaxLength(10)]
        public string? Result { get; set; }  // win / lose / draw

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
        public DateTime? SubmittedAt { get; set; }

        // ===== 导航 =====
        [ForeignKey(nameof(BattleId))]
        public virtual Battle? Battle { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual User.User? User { get; set; }
    }






    [Table("BattleSubmissions")]
    public class BattleSubmission
    {
        [Key]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        public string BattleId { get; set; } = string.Empty;
        public string ParticipantId { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Required]
        [MaxLength(1000)]
        public string ContentUrl { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? ContentType { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(BattleId))]
        public virtual Battle? Battle { get; set; }

        [ForeignKey(nameof(ParticipantId))]
        public virtual BattleParticipant? Participant { get; set; }
    }
}