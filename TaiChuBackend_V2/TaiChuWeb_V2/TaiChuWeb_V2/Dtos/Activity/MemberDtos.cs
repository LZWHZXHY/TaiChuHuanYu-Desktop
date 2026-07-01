namespace TaiChuWeb_V2.DTOs.Activity;

// 成员信息（含打卡记录）
public class MemberDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime JoinedAt { get; set; }
    public List<RecordDto> Records { get; set; } = new();
}

// 单天打卡记录
public class RecordDto
{
    public int Day { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsLate { get; set; }
    public string? Text { get; set; }
    public string? Image { get; set; }
}

// 加入/退出响应
public class JoinResponseDto
{
    public bool IsJoined { get; set; }
    public int MembersCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

// 打卡统计数据
public class StatsResponseDto
{
    public int TotalDays { get; set; }
    public int ElapsedDays { get; set; } // 已进行天数
    public int CompletionRate { get; set; } // 打卡率
    public int ConsecutiveDays { get; set; } // 连续打卡
    public int Rank { get; set; } // 排名
}