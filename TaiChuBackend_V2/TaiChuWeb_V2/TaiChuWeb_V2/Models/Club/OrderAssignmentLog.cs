using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

[Table("OrderAssignmentLogs")]
public class OrderAssignmentLog
{
    [Key]
    public long Id { get; set; }

    [MaxLength(30)]
    public string OrderNo { get; set; } = "";

    /// <summary>assign_cs / assign_op / accept / reassign / cancel</summary>
    [MaxLength(20)]
    public string Action { get; set; } = "";

    /// <summary>操作人（客服ID 或 系统=null）</summary>
    public Guid? ActorId { get; set; }

    /// <summary>目标人（被指派的打手 / 被换的客服）</summary>
    public Guid? TargetId { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}