using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Activity;

public class Record
{
    [Key]
    public int Id { get; set; }

    public int MemberId { get; set; }

    public int Day { get; set; } // 第几天

    public bool IsCompleted { get; set; }

    public bool IsLate { get; set; } // 是否为补卡

    [MaxLength(1000)]
    public string? Text { get; set; } // 心得

    [MaxLength(500)]
    public string? Image { get; set; } // 图片URL

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    [ForeignKey(nameof(MemberId))]
    public virtual Member Member { get; set; } = null!;
}