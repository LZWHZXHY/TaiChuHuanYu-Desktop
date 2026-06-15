// TaiChuWeb_V2/Models/LingMai/Note.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.LingMai
{
    [Table("notes")]
    public class Note
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        public string AuthorId { get; set; } = string.Empty;


        // 协作属性：任务执行者（可选，用于看板指派）
        public string? AssigneeId { get; set; }

        [Required]
        public Guid SpaceId { get; set; }

        // 🌟 扁平化设计：只保留一级分类 FolderId（允许为 null，不设父子树）
        public Guid? FolderId { get; set; }

        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = NoteTypes.Note; // note 或 thought


        [Column(TypeName = "json")]
        public string? BlocksData { get; set; }


        [Required]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;
        [MaxLength(500)]
        public string? Tags { get; set; }

        // 🌟 新增：用于存储 Wiki 属性、角色数值等动态键值对 (Frontmatter)
        // 前端传过来的 [{key: "Category", value: "Core Rules"}] 会被序列化成 JSON 存在这里
        [Column(TypeName = "json")]
        public string? ExtraData { get; set; }

        [Required]
        public string SortOrder { get; set; } = "0";


        public bool IsPrivate { get; set; } = false;

        

        [Required]
        public bool ShowInSidebar { get; set; } = false;

        [Required]
        public bool IsPublic { get; set; } = false;

        [Required]
        public int Status { get; set; } = 0; // 0=正常, 2=下架

        [MaxLength(255)]
        public string? BannedReason { get; set; }

        public int? TargetId { get; set; }

        public int Resonance { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        // 导航属性
        [NotMapped]
        public virtual ICollection<Block> Blocks { get; set; } = new List<Block>();
    }


    // 在 Note.cs 逻辑中建议的枚举定义
    public enum NoteStatus
    {
        Active = 0,    // 活跃：在侧边栏显示，正常编辑
        Banned = 2,    // 下架：违规处理
        Archived = 3   // 归档：从侧边栏消失，但保留发布快照，可随时找回
    }
}