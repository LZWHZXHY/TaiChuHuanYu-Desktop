using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UserEntity = TaiChuWeb_V2.Models.User.User;
namespace TaiChuWeb_V2.Models.World
{
    [Table("WorldProjects")]
    public class WorldProject
    {
        [Key]
        [Column(TypeName = "char(36)")]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Column(TypeName = "char(36)")]
        public Guid OwnerId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsPublic { get; set; } = false;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey("OwnerId")]
        public virtual UserEntity? Owner { get; set; }

        public virtual ICollection<WorldCard> Cards { get; set; } = new List<WorldCard>();
    }
}
