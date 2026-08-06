using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.ChaiCommunity.Joint
{
    [Table("JointActivities")]
    public class JointActivity
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// 联合活动标题
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// 活动描述
        /// </summary>
        [Required]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 参与要求
        /// </summary>
        public string? Requirements { get; set; }

        /// <summary>
        /// 群聊号/联系方式
        /// </summary>
        [MaxLength(200)]
        public string? Contact { get; set; }

        /// <summary>
        /// 封面图 URL
        /// </summary>
        [MaxLength(500)]
        public string? CoverUrl { get; set; }

        /// <summary>
        /// 活动类型：joint/relay/project/free/other
        /// </summary>
        [MaxLength(20)]
        public string Type { get; set; } = "joint";

        /// <summary>
        /// 活动状态：open/closed/ended/banned/abandoned
        /// </summary>
        [MaxLength(20)]
        public string Status { get; set; } = "open";

        /// <summary>
        /// 是否需要审核
        /// </summary>
        public bool AuditRequired { get; set; } = true;

        /// <summary>
        /// 举办者用户 ID
        /// </summary>
        public Guid OrganizerId { get; set; }

        /// <summary>
        /// 来源类型：user / official
        /// </summary>
        [MaxLength(20)]
        public string OrganizerType { get; set; } = "user";

        /// <summary>
        /// 审核状态（仅用户自建联合）：pending / approved / rejected
        /// </summary>
        [MaxLength(20)]
        public string ApprovalStatus { get; set; } = "pending";


        /// <summary>
        /// 举办者用户名（冗余）
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string OrganizerName { get; set; } = string.Empty;

        /// <summary>
        /// 当前参与人数
        /// </summary>
        public int ParticipantCount { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // ========== 导航属性 ==========

        /// <summary>
        /// 举办者（关联 Users 表）
        /// </summary>
        [ForeignKey(nameof(OrganizerId))]
        public virtual TaiChuWeb_V2.Models.User.User? Organizer { get; set; }

        /// <summary>
        /// 参与者列表
        /// </summary>
        public virtual ICollection<JointParticipant>? Participants { get; set; }
    }
}