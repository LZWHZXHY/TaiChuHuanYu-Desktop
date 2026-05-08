using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TaiChuWeb_V2.Models.LingMai
{
    [Table("PublishedNotes")]
    public class PublishedNote
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid(); // 独立发布表主键

        [Required]
        public Guid SpaceId { get; set; }

        public Guid? OriginalNoteId { get; set; }

        public string? Title { get; set; }

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = "note"; // note, thought, wiki, blog 等

        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

        public int Resonance { get; set; } = 0; // 点赞/共鸣数

        // 🌟 导航属性：如果你仍需要让 PublishedNote 级联其 Blocks，
        // 可以在 DbContext 中通过 HasMany().WithOne() 并指定 HasForeignKey(b => b.OwnerId) 来绑定。
        // 但推荐在查询时直接根据 OwnerId 和 OwnerType 显式拉取。
    }

    [Table("PublishedBlocks")]
    // 🌟 在实体类上配置复合索引
    [Index(nameof(OwnerId), nameof(OwnerType), Name = "IX_pub_blocks_Owner")]
    public class PublishedBlock
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid(); // 显式初始化主键

        /// <summary>
        /// 🌟 多态所有者 ID（例如发布的 NoteId、词条 WikiArticleId 等）
        /// </summary>
        [Required]
        [MaxLength(36)]
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// 🌟 多态所有者类型：如 "note", "wiki", "artwork", "blog"
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string OwnerType { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = "paragraph";

        [Column(TypeName = "longtext")]
        public string? Data { get; set; }

        [Required]
        public int SortOrder { get; set; } = 0;
    }
}