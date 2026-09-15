using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Club
{
    [Table("ClubOrderTypes")]
    public class ClubOrderType
    {
        [Key]
        public long Id { get; set; }

        [MaxLength(20)]
        public string GameCode { get; set; } = "";

        [MaxLength(30)]
        public string Code { get; set; } = "";

        [MaxLength(50)]
        public string Name { get; set; } = "";

        [MaxLength(500)]
        public string? Description { get; set; }

        [Column(TypeName = "json")]
        public string? ParamsSchemaJson { get; set; }

        /// <summary>价格映射 {"2588W":388,"4588W":688}</summary>
        [Column(TypeName = "json")]
        public string? PricingJson { get; set; }

        /// <summary>该订单类型特有的规则（纯文本，每行一条）</summary>
        [Column(TypeName = "text")]
        public string? SpecificRulesText { get; set; }

        [MaxLength(20)]
        public string PricingMode { get; set; } = "fixed";

        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; } = 0;

        // ⭐ 新增：时效窗口
        /// <summary>上架开始时间（null = 立即生效）</summary>
        public DateTime? StartAt { get; set; }

        /// <summary>下架时间（null = 永久有效）</summary>
        public DateTime? EndAt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("GameCode")]
        public virtual ClubGame Game { get; set; } = null!;
    }
}