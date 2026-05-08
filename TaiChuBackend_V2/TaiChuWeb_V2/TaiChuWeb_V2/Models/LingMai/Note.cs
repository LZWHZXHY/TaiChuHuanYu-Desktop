// TaiChuWeb_V2/Models/LingMai/Note.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.LingMai
{
    [Table("notes")]
    public class Note
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid SpaceId { get; set; }

        // 🌟 扁平化设计：只保留一级分类 FolderId（允许为 null，不设父子树）
        public Guid? FolderId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = NoteTypes.Note; // note 或 thought

        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public string SortOrder { get; set; } = "0";

        [Required]
        public bool ShowInSidebar { get; set; } = false;

        [Required]
        public bool IsPublic { get; set; } = false;

        [Required]
        public int Status { get; set; } = 0; // 0=正常, 2=下架

        [MaxLength(255)]
        public string? BannedReason { get; set; }

        public int? TargetId { get; set; }

        public int Resonance { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // 导航属性
        [NotMapped]
        public virtual ICollection<Block> Blocks { get; set; } = new List<Block>();
    }
}