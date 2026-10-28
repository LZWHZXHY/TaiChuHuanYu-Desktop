using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Community
{
    /// <summary>赞助者（赞助墙展示）。</summary>
    [Table("Sponsors")]
    public class Sponsor
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>爱发电用户 ID（用于同步去重）</summary>
        [MaxLength(100)]
        public string? AfdianUserId { get; set; }

        /// <summary>头像 URL</summary>
        [MaxLength(500)]
        public string? Avatar { get; set; }

        /// <summary>档位：觉醒者 / 超凡者 / 大超凡者 / 君者</summary>
        [MaxLength(30)]
        public string Tier { get; set; } = "觉醒者";

        public double Amount { get; set; }

        [MaxLength(500)]
        public string? Message { get; set; }

        /// <summary>是否匿名（公开列表隐藏名字）</summary>
        public bool IsAnonymous { get; set; }

        /// <summary>是否公开展示</summary>
        public bool IsVisible { get; set; } = true;

        public DateTime SponsoredAt { get; set; } = DateTime.UtcNow;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        /// <summary>关联的财政收入流水 index（删除赞助时一并清理）。</summary>
        public int? FinancialIndex { get; set; }
    }
}
