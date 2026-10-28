using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.World;

/// <summary>世界观配套小说。</summary>
[Table("WorldNovels")]
public class WorldNovel
{
    [Key]
    [Column(TypeName = "char(36)")]
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>所属世界观（可空 = 独立小说）</summary>
    [Column(TypeName = "char(36)")]
    public Guid? ProjectId { get; set; }

    [Column(TypeName = "char(36)")]
    public Guid AuthorId { get; set; }

    [MaxLength(50)]
    public string AuthorName { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string? Description { get; set; }

    [MaxLength(500)]
    public string? CoverUrl { get; set; }

    /// <summary>ongoing（连载中）/ completed（已完结）/ paused（暂停）</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "ongoing";

    /// <summary>标签 JSON 数组</summary>
    [Column(TypeName = "longtext")]
    public string Tags { get; set; } = "[]";

    public bool IsPublic { get; set; } = false;

    public int Views { get; set; }

    public int WordCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>小说章节。</summary>
[Table("WorldNovelChapters")]
public class WorldNovelChapter
{
    [Key]
    [Column(TypeName = "char(36)")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Column(TypeName = "char(36)")]
    public Guid NovelId { get; set; }

    /// <summary>章节序号（1 起，可重排）</summary>
    public int Number { get; set; }

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    /// <summary>正文（Markdown）</summary>
    [Column(TypeName = "longtext")]
    public string Content { get; set; } = string.Empty;

    public int WordCount { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
