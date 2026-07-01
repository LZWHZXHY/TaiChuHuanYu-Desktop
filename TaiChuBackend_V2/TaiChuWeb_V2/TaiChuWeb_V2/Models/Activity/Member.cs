using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UserModel = TaiChuWeb_V2.Models.User.User; // 别名

namespace TaiChuWeb_V2.Models.Activity;

public class Member
{
    [Key]
    public int Id { get; set; }

    public int ActivityId { get; set; }
    public Guid UserId { get; set; }

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // 导航属性
    [ForeignKey(nameof(ActivityId))]
    public virtual Activity Activity { get; set; } = null!;

    [ForeignKey(nameof(UserId))]
    public virtual UserModel User { get; set; } = null!;

    public virtual ICollection<Record> Records { get; set; } = new List<Record>();
}