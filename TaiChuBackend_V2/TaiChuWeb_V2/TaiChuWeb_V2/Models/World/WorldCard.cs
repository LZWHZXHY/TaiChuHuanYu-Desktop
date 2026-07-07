using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.World
{
    [Table("WorldCards")]
    public class WorldCard
    {
        [Key]
        [Column(TypeName = "char(36)")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column(TypeName = "char(36)")]
        public Guid ProjectId { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? SubType { get; set; }

        // JSON 字段
        [Column(TypeName = "json")]
        public string Aliases { get; set; } = "[]";

        [Column(TypeName = "json")]
        public string Attributes { get; set; } = "[]";

        [Column(TypeName = "longtext")]
        public string? Description { get; set; }

        [Column(TypeName = "json")]
        public string ContentBlocks { get; set; } = "[]";

        [Column(TypeName = "json")]
        public string TimelineEvents { get; set; } = "[]";

        [Column(TypeName = "json")]
        public string Tags { get; set; } = "[]";

        [Column(TypeName = "json")]
        public string EmbeddedCards { get; set; } = "[]";

        [Column(TypeName = "longtext")]
        public string Content { get; set; } = "{}";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("ProjectId")]
        public virtual WorldProject? Project { get; set; }

        public virtual ICollection<WorldRelation> OutRelations { get; set; } = new List<WorldRelation>();
        public virtual ICollection<WorldRelation> InRelations { get; set; } = new List<WorldRelation>();
    }
}
