using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.ChaiCommunity.Joint
{
    [Table("JointParticipants")]
    public class JointParticipant
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// 所属联合活动 ID
        /// </summary>
        public Guid ActivityId { get; set; }

        /// <summary>
        /// 参与者用户 ID
        /// </summary>
        public Guid UserId { get; set; }

        /// <summary>
        /// 参与者用户名（冗余）
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string UserName { get; set; } = string.Empty;

        /// <summary>
        /// 审核状态：pending/approved/rejected
        /// </summary>
        [MaxLength(20)]
        public string Status { get; set; } = "pending";

        /// <summary>
        /// 报名备注
        /// </summary>
        [MaxLength(500)]
        public string? Remark { get; set; }

        public DateTime CreatedAt { get; set; }

        // ========== 导航属性 ==========

        [ForeignKey(nameof(ActivityId))]
        public virtual JointActivity? Activity { get; set; }
    }
}