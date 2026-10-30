using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UserEntity = TaiChuWeb_V2.Models.User.User;

namespace TaiChuWeb_V2.Models.World
{
    /// <summary>
    /// 世界观协作者：创建者把别人拉进来一起编写世界观。
    /// Role = "editor"（可编写条目、关系、图谱布局）或 "viewer"（只读，可以看私有世界）。
    /// 数据表 WorldCollaborators 由 WorldCollaboratorSchema 在启动时自动创建，不走 EF 迁移。
    /// </summary>
    [Table("WorldCollaborators")]
    public class WorldCollaborator
    {
        [Column(TypeName = "char(36)")]
        public Guid ProjectId { get; set; }

        [Column(TypeName = "char(36)")]
        public Guid UserId { get; set; }

        [MaxLength(16)]
        public string Role { get; set; } = "editor";

        [Column(TypeName = "char(36)")]
        public Guid? AddedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(ProjectId))]
        public virtual WorldProject? Project { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual UserEntity? User { get; set; }
    }
}
