using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.World
{
    [Table("WorldRelations")]
    public class WorldRelation
    {
        [Key]
        [Column(TypeName = "char(36)")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column(TypeName = "char(36)")]
        public Guid SourceCardId { get; set; }

        [Column(TypeName = "char(36)")]
        public Guid TargetCardId { get; set; }

        [MaxLength(100)]
        public string RelationType { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("SourceCardId")]
        public virtual WorldCard? SourceCard { get; set; }

        [ForeignKey("TargetCardId")]
        public virtual WorldCard? TargetCard { get; set; }
    }
}
