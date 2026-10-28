using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.DTOs.Activity;

// 创建活动请求
public class CreateActivityDto
{
    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    [Required]
    public int TypeId { get; set; }  // 改为 int

    [MaxLength(500)]
    public string? Cover { get; set; }

    [Range(1, 365)]
    public int Days { get; set; } = 30;

    /// <summary>活动形态：general / match（对战赛）/ contest（作品赛）</summary>
    [MaxLength(20)]
    public string? Kind { get; set; }

    public DateTime? SignupDeadline { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }

    /// <summary>比赛规则 / 主题说明</summary>
    public string? Rules { get; set; }
}

// 更新活动请求
public class UpdateActivityDto
{
    [MaxLength(100)]
    public string? Title { get; set; }

    [MaxLength(500)]
    public string? Description { get; set; }

    [Range(1, int.MaxValue)]
    public int? TypeId { get; set; }  // 可空 int

    [MaxLength(50)]
    public string? Status { get; set; }

    [MaxLength(500)]
    public string? Cover { get; set; }

    [Range(1, 365)]
    public int? Days { get; set; }

    [MaxLength(20)]
    public string? Kind { get; set; }

    public DateTime? SignupDeadline { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public string? Rules { get; set; }

    /// <summary>是否清空时间字段</summary>
    public bool ClearDates { get; set; }
}

// 活动响应
public class ActivityResponseDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Cover { get; set; }
    public int Days { get; set; }
    public int Participants { get; set; }
    public int CompletedRate { get; set; }
    public string Owner { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Cycle => $"{Days}天";

    public string Kind { get; set; } = "general";
    public DateTime? SignupDeadline { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }
    public string? Rules { get; set; }
}

// 活动列表查询参数
public class ActivityQueryParams
{
    public string? Status { get; set; } // 全部, 招募中, 进行中, 已结束
    public string? Keyword { get; set; } // 搜索关键词
    public string? Type { get; set; } // 活动类型
}