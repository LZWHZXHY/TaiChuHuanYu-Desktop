using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Club
{
    /// <summary>订单客诉</summary>
    [Table("OrderComplaints")]
    public class OrderComplaint
    {
        [Key]
        public long Id { get; set; }

        public long OrderId { get; set; }

        [MaxLength(30)]
        public string OrderNo { get; set; } = "";

        /// <summary>提交人（老板）</summary>
        public Guid FromUserId { get; set; }

        /// <summary>被投诉打手（可空，空 = 针对整单）</summary>
        public Guid? TargetOperatorId { get; set; }

        /// <summary>
        /// 投诉类型：
        /// attitude = 服务态度差
        /// late = 迟到/缺席
        /// runner = 打手跑路
        /// quality = 服务质量差
        /// harassment = 辱骂/骚扰
        /// other = 其他
        /// </summary>
        [MaxLength(30)]
        public string ComplaintType { get; set; } = "other";

        [MaxLength(2000)]
        public string Description { get; set; } = "";

        /// <summary>证据截图 URL（可选）</summary>
        [MaxLength(500)]
        public string? EvidenceUrl { get; set; }

        /// <summary>pending / resolved / rejected</summary>
        [MaxLength(20)]
        public string Status { get; set; } = "pending";

        /// <summary>处理客服的 UserId</summary>
        public Guid? HandlerId { get; set; }

        /// <summary>处理客服名称（冗余，方便展示）</summary>
        [MaxLength(50)]
        public string? HandlerName { get; set; }

        /// <summary>客服回复内容（给老板看）</summary>
        [MaxLength(2000)]
        public string? HandlerNote { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? HandledAt { get; set; }
    }
}