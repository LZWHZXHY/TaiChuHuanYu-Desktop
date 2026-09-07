using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Admin;

[Table("manual_articles")]
public class ManualArticle
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// 所属分类 ID
    /// </summary>
    [Column("category_id")]
    public int CategoryId { get; set; }

    /// <summary>
    /// 路由别名，用于前台 URL 访问（例如：quick-start），需唯一
    /// </summary>
    [Required]
    [MaxLength(64)]
    [Column("slug")]
    public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// 文档标题
    /// </summary>
    [Required]
    [MaxLength(128)]
    [Column("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Markdown 纯文本正文，对应 MySQL 的 mediumtext
    /// </summary>
    [Required]
    [Column("content", TypeName = "mediumtext")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// 同分类下的排序权重，越小越靠前
    /// </summary>
    [Column("sort_order")]
    public int SortOrder { get; set; } = 0;

    /// <summary>
    /// 是否公开：true = 已发布，false = 草稿
    /// </summary>
    [Column("is_published")]
    public bool IsPublished { get; set; } = true;

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [Column("updated_at")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 外键关联所属分类
    /// </summary>
    [ForeignKey(nameof(CategoryId))]
    public ManualCategory? Category { get; set; }
}