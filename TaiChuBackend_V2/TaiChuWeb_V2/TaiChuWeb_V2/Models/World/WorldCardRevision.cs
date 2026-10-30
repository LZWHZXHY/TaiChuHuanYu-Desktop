using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.World
{
    /// <summary>
    /// 世界观条目的修改记录：谁、什么时候、做了什么，以及当时条目的完整内容（快照）。
    /// Action：create 新建 / update 修改 / delete 删除（快照是删除前的内容）/ restore 恢复或撤销 / baseline 记录起点（开启记录前的原样）。
    /// 数据表 WorldCardRevisions 由 WorldCollaboratorSchema 启动时自动创建，不走 EF 迁移。
    /// </summary>
    [Table("WorldCardRevisions")]
    public class WorldCardRevision
    {
        [Key]
        public long Id { get; set; }

        [Column(TypeName = "char(36)")]
        public Guid ProjectId { get; set; }

        [Column(TypeName = "char(36)")]
        public Guid CardId { get; set; }

        /// <summary>操作人；记录起点（baseline）为空</summary>
        [Column(TypeName = "char(36)")]
        public Guid? UserId { get; set; }

        [MaxLength(16)]
        public string Action { get; set; } = "update";

        /// <summary>当时的条目标题（条目被删、被改名后列表里也能看懂）</summary>
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        /// <summary>条目完整内容 JSON（WorldCardSnapshot）</summary>
        [Column(TypeName = "longtext")]
        public string Snapshot { get; set; } = "{}";

        /// <summary>补充说明，比如「撤销了 xxx 的修改」「添加关联」</summary>
        [MaxLength(200)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
