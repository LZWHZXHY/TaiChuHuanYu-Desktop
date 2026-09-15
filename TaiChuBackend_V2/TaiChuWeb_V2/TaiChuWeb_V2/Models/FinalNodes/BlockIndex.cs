// TaiChuWeb_V2/Models/FinalNodes/BlockIndex.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TaiChuWeb_V2.Models.FinalNodes
{
    /// <summary>
    /// 块索引表：把 UniversalNodeContent.BlocksData 里的块投影出来，用于搜索、反查、复习
    /// </summary>
    [Table("block_index")]
    [Index(nameof(NodeId), nameof(SortOrder), Name = "IX_BlockIndex_Node_Sort")]
    [Index(nameof(BlockType), Name = "IX_BlockIndex_Type")]
    [Index(nameof(ParentBlockId), Name = "IX_BlockIndex_Parent")]
    public class BlockIndex
    {
        [Key]
        [MaxLength(64)]
        public string Id { get; set; } = string.Empty;   // 与 Tiptap 节点 attrs.id 一致（Nanoid 21）

        [Required]
        [MaxLength(36)]
        public string NodeId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string BlockType { get; set; } = "paragraph";

        [MaxLength(64)]
        public string? ParentBlockId { get; set; }

        public int SortOrder { get; set; }

        [Column(TypeName = "longtext")]
        public string? TextContent { get; set; }

        [Column(TypeName = "longtext")]
        public string? Latex { get; set; }

        [MaxLength(64)]
        public string? AssetId { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}