using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.DTOs.Activity;

// 打卡请求
public class CheckinDto
{
    [Required, Range(1, 365)]
    public int Day { get; set; }

    [MaxLength(1000)]
    public string? Text { get; set; }

    [MaxLength(500)]
    public string? Image { get; set; }
}

// 打卡响应
public class CheckinResponseDto
{
    public int Id { get; set; }
    public int Day { get; set; }
    public bool IsCompleted { get; set; }
    public bool IsLate { get; set; }
    public string? Text { get; set; }
    public string? Image { get; set; }
    public DateTime CreatedAt { get; set; }
}