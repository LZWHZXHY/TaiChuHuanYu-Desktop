using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.LingMai
{
    [Table("note_histories")]
    public class NoteHistory
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public Guid NoteId { get; set; }

        /// <summary>
        /// 存储该版本完整的 Tiptap JSON 树
        /// </summary>
        [Required]
        [Column(TypeName = "longtext")]
        public string ContentJson { get; set; } = string.Empty;

        /// <summary>
        /// 版本备注（如：自动保存、手动快照、发布前备份）
        /// </summary>
        [MaxLength(200)]
        public string? Remark { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // 导航属性（可选，取决于你的 DbContext 配置）
        // [ForeignKey("NoteId")]
        // public virtual Note? Note { get; set; }
    }
}