using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore; // 🌟 引入用于配置复合索引

namespace TaiChuWeb_V2.Models.LingMai
{
    [Table("blocks")]
    // 🌟 在实体类上配置复合索引，大幅提升多态查询性能
    [Index(nameof(OwnerId), nameof(OwnerType), Name = "IX_blocks_Owner")]
    public class Block
    {
        [Key]
        [MaxLength(128)] // 使用 NanoID (21位)，比 GUID 更短，前端生成快
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// 🌟 多态指针：可以是 NoteId、WikiArticleId、ArtworkId 等
        /// 统一用 string 存储，既能兼容 Guid，也能兼容未来的其他 ID 类型
        /// </summary>
        [Required]
        [MaxLength(36)]
        public string OwnerId { get; set; } = string.Empty;

        /// <summary>
        /// 🌟 标识来源类型：如 "note", "wiki", "artwork", "blog"
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string OwnerType { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty; // paragraph, heading-1, image, spirit-link

        [Required]
        public string SortOrder { get; set; } = "0";

        [Column(TypeName = "json")]
        public string Data { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}