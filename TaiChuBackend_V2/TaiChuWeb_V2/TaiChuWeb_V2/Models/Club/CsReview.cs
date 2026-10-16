using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Club
{
    [Table("CsReviews")]
    public class CsReview
    {
        [Key]
        public long Id { get; set; }

        public long OrderId { get; set; }              // 关联订单
        [MaxLength(30)]
        public string OrderNo { get; set; } = "";

        public Guid FromUserId { get; set; }           // 老板
        public Guid CustomerServiceId { get; set; }    // 被评客服（关联 CustomerServices.Id）

        // 评分维度（1-5）
        public int ResponseScore { get; set; }         // 响应速度
        public int AttitudeScore { get; set; }         // 服务态度
        public int ProfessionalScore { get; set; }     // 专业度
        public int OverallScore { get; set; }          // 综合

        [MaxLength(500)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
