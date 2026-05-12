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
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid SpaceId { get; set; }

        public Guid? OriginalNoteId { get; set; }

        public string? Title { get; set; }

        // 🌟 新增：摘要平铺
        // 用于在百科列表页直接显示，不需要去查 PublishedBlocks
        public string? Excerpt { get; set; }

        // 🌟 新增：标签快照 (冗余设计)
        // 存储形式如 "火柴人,动画,设定"。
        // 理由：在广场列表加载时，直接从这一行读取，避免百万级数据下的多表 Join 查询。
        public string? Tags { get; set; }

        // 🌟 新增：作者名称冗余
        // 百科广场显示时直接读取，无需关联 User 表
        public string? AuthorName { get; set; }

        // 🌟 新增：角色/设定扩展数据 (JSON)
        // 存储如 {"power":80, "speed":90} 的数值，用于支撑前端的角色雷达图
        [Column(TypeName = "json")]
        public string? ExtraData { get; set; }

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = "note";

        public DateTime PublishedAt { get; set; } = DateTime.UtcNow;

        public int Resonance { get; set; } = 0;
    }

    [Table("PublishedBlocks")]
    [Index(nameof(OwnerId), nameof(OwnerType), Name = "IX_pub_blocks_Owner")]
    public class PublishedBlock
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(36)]
        public string OwnerId { get; set; } = string.Empty;

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