using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TaiChuWeb_V2.Models.World;

// 保留已完成记录，响应丢失或重新选择同一文件时仍能识别，不能按超时删除。
[Index(nameof(OwnerId), nameof(SourceProjectId))]
[Index(nameof(State), nameof(UpdatedAt))]
public class WorldRestore
{
    [Key, Column(TypeName = "char(36)")] public Guid Id { get; set; }
    [Column(TypeName = "char(36)")] public Guid OwnerId { get; set; }
    [Column(TypeName = "char(36)")] public Guid SourceProjectId { get; set; }
    [Column(TypeName = "char(36)")] public Guid TargetProjectId { get; set; }
    [MaxLength(16)] public string State { get; set; } = "pending";
    [Column(TypeName = "longtext")] public string Plan { get; set; } = "{}";
    [Column(TypeName = "longtext")] public string Uploaded { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
