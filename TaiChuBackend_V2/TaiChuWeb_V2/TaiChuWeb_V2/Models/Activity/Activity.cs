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

    public Guid OwnerId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey(nameof(OwnerId))]
    public virtual UserModel Owner { get; set; } = null!; // 使用别名

    public virtual ICollection<Member> Members { get; set; } = new List<Member>();
    public virtual ICollection<Post> Posts { get; set; } = new List<Post>();
}