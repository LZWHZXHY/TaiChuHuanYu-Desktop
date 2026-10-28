using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Activity;

/// <summary>参赛作品（作品赛 / 绘画赛用）。</summary>
[Table("ActivityEntries")]
public class ActivityEntry
{
    [Key]
    public int Id { get; set; }

    public int ActivityId { get; set; }

    public Guid UserId { get; set; }

    [MaxLength(50)]
    public string AuthorName { get; set; } = string.Empty;

    [Required, MaxLength(150)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? CoverUrl { get; set; }

    [MaxLength(500)]
    public string? Link { get; set; }

    /// <summary>上传的文件（图片 / 文档 / 压缩包）地址</summary>
    [MaxLength(500)]
    public string? FileUrl { get; set; }

    /// <summary>原始文件名（用于显示）</summary>
    [MaxLength(200)]
    public string? FileName { get; set; }

    [Column(TypeName = "longtext")]
    public string? Description { get; set; }

    /// <summary>票数（冗余计数，投票时同步）</summary>
    public int Votes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>投票记录（同一活动每人一票）。</summary>
[Table("ActivityVotes")]
public class ActivityVote
{
    [Key]
    public int Id { get; set; }

    public int ActivityId { get; set; }

    public int EntryId { get; set; }

    public Guid UserId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>对阵（对战赛用）。</summary>
[Table("ActivityMatches")]
public class ActivityMatch
{
    [Key]
    public int Id { get; set; }

    public int ActivityId { get; set; }

    /// <summary>第几轮（1 = 第一轮）</summary>
    public int Round { get; set; }

    /// <summary>同轮内的序号</summary>
    public int Slot { get; set; }

    [MaxLength(50)]
    public string? SideA { get; set; }

    [MaxLength(50)]
    public string? SideB { get; set; }

    public int? ScoreA { get; set; }

    public int? ScoreB { get; set; }

    [MaxLength(50)]
    public string? Winner { get; set; }

    /// <summary>pending / live / done</summary>
    [MaxLength(20)]
    public string Status { get; set; } = "pending";
}
