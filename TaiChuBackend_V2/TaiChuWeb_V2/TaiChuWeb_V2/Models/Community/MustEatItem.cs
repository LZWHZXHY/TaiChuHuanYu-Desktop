using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Community
{
    /// <summary>必吃榜：资源推荐 / 分享（游戏、动漫、小说、视频等）。</summary>
    [Table("MustEatItems")]
    public class MustEatItem
    {
        [Key]
        public int Id { get; set; }

        /// <summary>分类：游戏 / 动漫 / 小说 / 视频 / 其他</summary>
        [Required]
        [MaxLength(20)]
        public string Category { get; set; } = "其他";

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>封面图 URL（可空）</summary>
        [MaxLength(500)]
        public string? CoverImage { get; set; }

        /// <summary>推荐理由 / 简介</summary>
        [MaxLength(1000)]
        public string? Description { get; set; }

        /// <summary>创作团队 / 作者</summary>
        [MaxLength(200)]
        public string? Creator { get; set; }

        /// <summary>资源链接（网页/网盘等）</summary>
        [MaxLength(1000)]
        public string? Link { get; set; }

        /// <summary>来源 / 出处（可空）</summary>
        [MaxLength(100)]
        public string? Source { get; set; }

        /// <summary>排序（支持小数，越小越前）</summary>
        public double SortOrder { get; set; }

        public bool IsVisible { get; set; } = true;

        /// <summary>审核状态：pending（待审核）/ approved（已通过）/ rejected（已驳回）</summary>
        [MaxLength(20)]
        public string Status { get; set; } = "approved";

        /// <summary>提交者（用户投稿时记录）</summary>
        public Guid? SubmittedByUserId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
