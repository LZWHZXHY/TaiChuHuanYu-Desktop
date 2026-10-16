using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Club
{
    /// <summary>客服钱包流水</summary>
    [Table("CustomerServiceWalletTransactions")]
    public class CustomerServiceWalletTransaction
    {
        [Key]
        public long Id { get; set; }

        /// <summary>客服关联用户 ID</summary>
        public Guid UserId { get; set; }

        /// <summary>客服档案 ID（CustomerServices.Id）</summary>
        public Guid CustomerServiceId { get; set; }

        /// <summary>income / withdraw / adjust</summary>
        [MaxLength(20)]
        public string Type { get; set; } = "";

        /// <summary>收入为正，支出为负</summary>
        [Column(TypeName = "decimal(12,2)")]
        public decimal Amount { get; set; }

        [MaxLength(30)]
        public string? OrderNo { get; set; }

        /// <summary>pending / completed / rejected</summary>
        [MaxLength(20)]
        public string Status { get; set; } = "completed";

        [MaxLength(500)]
        public string? Remark { get; set; }

        /// <summary>仅 income 使用：是否已解冻</summary>
        public bool IsUnfrozen { get; set; } = false;

        /// <summary>仅 income 使用：冻结到期时间</summary>
        public DateTime? UnfreezeAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? CompletedAt { get; set; }
    }
}