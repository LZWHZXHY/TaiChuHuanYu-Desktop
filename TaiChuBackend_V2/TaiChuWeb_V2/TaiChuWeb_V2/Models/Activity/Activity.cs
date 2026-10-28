using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UserModel = TaiChuWeb_V2.Models.User.User; // 别名

namespace TaiChuWeb_V2.Models.Activity;

public class Activity
{
    [Key]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Title { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    // 将原来的 string Type 改为外键
    public int TypeId { get; set; }

    [ForeignKey(nameof(TypeId))]
    public virtual ActivityType Type { get; set; } = null!;

    [MaxLength(50)]
    public string Status { get; set; } = "招募中";

    [MaxLength(500)]
    public string? Cover { get; set; }

    [Required]
    public int Days { get; set; } = 30;

    /// <summary>活动形态：general（普通）/ match（对战赛）/ contest（作品赛）</summary>
    [MaxLength(20)]
    public string Kind { get; set; } = "general";

    public DateTime? SignupDeadline { get; set; }
    public DateTime? StartAt { get; set; }
    public DateTime? EndAt { get; set; }

    /// <summary>比赛规则 / 主题说明（长文本）</summary>
    [Column(TypeName = "longtext")]
    public string? Rules { get; set; }

    public Guid OwnerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(OwnerId))]
    public virtual UserModel Owner { get; set; } = null!; // 使用别名

    public virtual ICollection<Member> Members { get; set; } = new List<Member>();
    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
}