// TaiChuWeb_V2/Models/FinalNodes/BlockLink.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TaiChuWeb_V2.Models.FinalNodes
{
    /// <summary>
    /// 块级引用：Block -> Block
    /// </summary>
    [Table("block_links")]
    [Index(nameof(SourceBlockId), Name = "IX_BlockLinks_Source")]
    [Index(nameof(TargetBlockId), Name = "IX_BlockLinks_Target")]
    public class BlockLink
    {
        [Key]
        [MaxLength(36)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(64)]
        public string SourceBlockId { get; set; } = string.Empty;

        [Required]
        [MaxLength(64)]
        public string TargetBlockId { get; set; } = string.Empty;

        // reference / prerequisite / derived_from / example_of / contradicts
        [MaxLength(50)]
        public string RelationType { get; set; } = "reference";

        [Column(TypeName = "longtext")]
        public string? Excerpt { get; set; }

        [Column(TypeName = "json")]
        public string ExtraData { get; set; } = "{}";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}