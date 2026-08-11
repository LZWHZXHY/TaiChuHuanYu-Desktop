using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.User
{
    /// <summary>
    /// 配额扩容记录（用于审计和用户查询）
    /// </summary>
    [Table("QuotaUpgradeRecords")]
    public class QuotaUpgradeRecord
    {
        [Key]
        public long Id { get; set; }

        [Column(TypeName = "char(36)")]
        public Guid UserId { get; set; }

        /// <summary>
        /// 扩容类型：WorldCount / WorldCardCapacity
        /// </summary>
        [MaxLength(20)]
        public string UpgradeType { get; set; } = string.Empty;

        /// <summary>
        /// 扩容增加的数量
        /// </summary>
        public int Amount { get; set; }

        /// <summary>
        /// 消耗的经验值
        /// </summary>
        public int CostExp { get; set; }

        /// <summary>
        /// 扩容前的值
        /// </summary>
        public int PreviousValue { get; set; }

        /// <summary>
        /// 扩容后的值
        /// </summary>
        public int NewValue { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("UserId")]
        public virtual User? User { get; set; }
    }
}