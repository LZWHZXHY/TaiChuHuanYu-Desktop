using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UserModel = TaiChuWeb_V2.Models.User.User; // 别名

namespace TaiChuWeb_V2.Models.Activity;

public class Post
{
    [Key]
    public int Id { get; set; }

    public int ActivityId { get; set; }
    public Guid AuthorId { get; set; }

    [Required, MaxLength(2000)]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    [ForeignKey(nameof(ActivityId))]
    public virtual Activity Activity { get; set; } = null!;

    [ForeignKey(nameof(AuthorId))]
    public virtual UserModel Author { get; set; } = null!;

    public virtual ICollection<Reply> Replies { get; set; } = new List<Reply>();
}