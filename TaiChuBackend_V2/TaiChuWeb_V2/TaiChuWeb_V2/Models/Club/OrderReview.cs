using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Club
{
    /// <summary>
    /// 订单评价表（双向：老板 ↔ 打手）
    /// </summary>
    [Table("OrderReviews")]
    public class OrderReview
    {
        [Key]
        public long Id { get; set; }

        public long OrderId { get; set; }

        public Guid FromUserId { get; set; }   // 评价人
        public Guid ToUserId { get; set; }     // 被评价人

        /// <summary>true=老板评打手，false=打手评老板</summary>
        public bool FromCustomer { get; set; }

        // ===== 多维评分（1-5 星） =====
        public int SkillScore { get; set; }      // 技术
        public int AttitudeScore { get; set; }   // 态度
        public int PunctualScore { get; set; }   // 准时
        public int OverallScore { get; set; }    // 综合

        [MaxLength(500)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}