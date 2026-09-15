using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.FinalNodes
{
    /// <summary>
    /// 1. 核心节点表 (瘦表：专为列表、检索、关系网渲染优化)
    /// </summary>
    [Table("final_nodes")]
    [Index(nameof(SpaceId), nameof(Type), nameof(CreatedAt), Name = "IX_FinalNodes_Space_Type_Time")]
    public class UniversalNode
    {
        [Key]
        [MaxLength(36)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public Guid? SpaceId { get; set; }

        [Required]
        [MaxLength(36)]
        public string CreatorId { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string CreatorName { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        // 核心鉴别器： "wiki", "blog", "character", "location", "post" 等
        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Excerpt { get; set; }

        [Column(TypeName = "longtext")]
        public string? CoverImage { get; set; }

        // 泛化分类树 ID
        public int? CategoryId { get; set; }

        [Column(TypeName = "json")]
        public string Tags { get; set; } = "[]";

        // 用于容纳原 WorldCard 中的 Aliases, Attributes 等特有元数据
        [Column(TypeName = "json")]
        public string ExtraData { get; set; } = "{}";

        public int Status { get; set; } = 0; // 0: 正常, 1: 草稿, 2: 下架, 3: 待审核
        public int ViewCount { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // 导航属性：一对一关联胖表内容
        public virtual UniversalNodeContent? Content { get; set; }
    }

    /// <summary>
    /// 2. 节点内容表 (胖表：物理隔离沉重的 JSON Block 数据)
    /// </summary>
    [Table("final_node_contents")]
    public class UniversalNodeContent
    {
        [Key]
        [MaxLength(36)]
        public string NodeId { get; set; } = string.Empty;

        // 统一原 Note.BlocksData 和 WorldCard.ContentBlocks
        [Required]
        [Column(TypeName = "longtext")]
        public string BlocksData { get; set; } = "[]";

        // 预留 HTML 缓存字段，加速首屏渲染
        [Column(TypeName = "longtext")]
        public string? HtmlCache { get; set; }

        public DateTime LastSavedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(NodeId))]
        public virtual UniversalNode Node { get; set; } = null!;
    }

    /// <summary>
    /// 3. 统一关系表 (图谱的边：替代 NoteLink 和 WorldRelation)
    /// </summary>
    [Table("final_relations")]
    [Index(nameof(SourceNodeId), nameof(RelationType))]
    [Index(nameof(TargetNodeId), nameof(RelationType))]
    public class UniversalRelation
    {
        [Key]
        [MaxLength(36)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(36)]
        public string SourceNodeId { get; set; } = string.Empty;

        [Required]
        [MaxLength(36)]
        public string TargetNodeId { get; set; } = string.Empty;

        // 关系类型：如 "reference"(引用), "parent"(属于), "born_in"(出生于)
        [Required]
        [MaxLength(50)]
        public string RelationType { get; set; } = string.Empty;

        // 关系元数据：如携带好感度、距离等动态数值
        [Column(TypeName = "json")]
        public string ExtraData { get; set; } = "{}";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(SourceNodeId))]
        public virtual UniversalNode? SourceNode { get; set; }

        [ForeignKey(nameof(TargetNodeId))]
        public virtual UniversalNode? TargetNode { get; set; }
    }
}