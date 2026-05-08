// TaiChuWeb_V2/Models/LingMai/NoteLink.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.LingMai
{
    [Table("note_links")]
    public class NoteLink
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid SpaceId { get; set; }

        [Required]
        public Guid SourceNoteId { get; set; }

        [Required]
        public Guid TargetNoteId { get; set; }

        [MaxLength(500)]
        public string? Excerpt { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // --- 🌟 导航属性：只关联草稿区，解决级联与 VS 报错 ---

        [ForeignKey(nameof(SourceNoteId))]
        public virtual Note? SourceNote { get; set; }

        [ForeignKey(nameof(TargetNoteId))]
        public virtual Note? TargetNote { get; set; } // 👈 补回这里，CS1061 报错立刻消失
    }
}