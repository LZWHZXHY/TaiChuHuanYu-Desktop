using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Release
{
    /// <summary>社区网站的版本发布记录（Changelog）。</summary>
    [Table("Releases")]
    public class Release
    {
        [Key]
        public int Id { get; set; }

        /// <summary>版本号，例如 7.0.0</summary>
        [Required]
        [MaxLength(30)]
        public string Version { get; set; } = string.Empty;

        /// <summary>版本标题，例如「接入 DSH」</summary>
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>更新内容（纯文本/Markdown，保留换行）</summary>
        public string? Content { get; set; }

        /// <summary>发布日期</summary>
        public DateTime ReleasedAt { get; set; } = DateTime.UtcNow;

        public bool IsPublished { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
