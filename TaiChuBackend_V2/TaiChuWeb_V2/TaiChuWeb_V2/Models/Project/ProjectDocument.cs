
using System.ComponentModel.DataAnnotations;
namespace TaiChuWeb_V2.Models.Project
{
    public class ProjectDocument
    {
        public string ProjectId { get; set; }
        public Project Project { get; set; }

        [Required]
        public string NoteId { get; set; } // 关联你现有的“灵脉碎片 (Note)”表的主键

        // 你甚至可以在这里加一个字段：
        // public string SnapshotId { get; set; } // 如果你想锁定某个历史版本发布到项目

        public DateTime PinnedAt { get; set; } = DateTime.UtcNow;

        public string PinnedByUserId { get; set; } // 是谁把这篇笔记发布到项目的
    }
}
