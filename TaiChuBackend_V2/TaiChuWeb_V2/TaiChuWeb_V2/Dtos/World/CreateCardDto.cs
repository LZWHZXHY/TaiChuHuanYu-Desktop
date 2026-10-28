using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Dtos.World
{
    public class CreateCardDto
    {

        public List<string>? GalleryImages { get; set; }


        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = string.Empty;

        [MaxLength(50)]
        public string? SubType { get; set; }

        public List<string> Aliases { get; set; } = new();

        public List<AttributeDto> Attributes { get; set; } = new();

        public string? Description { get; set; }

        public List<ContentBlockDto> ContentBlocks { get; set; } = new();

        public List<TimelineEventDto> TimelineEvents { get; set; } = new();

        public List<string> Tags { get; set; } = new();

        public List<Guid> EmbeddedCards { get; set; } = new();

        public string? Content { get; set; }  // 兼容旧数据

        public string? CoverImage { get; set; }

        // 省略时保留既有关系；传空数组时清空出向关系。
        public List<CreateRelationDto>? Relations { get; set; }

    }

    public class AttributeDto
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string Type { get; set; } = "short";   // 🆕 添加
    }

    public class ContentBlockDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Guid CardId { get; set; }
        public string CardType { get; set; } = string.Empty;
        public int Order { get; set; }
        public string? ContextLabel { get; set; }
    }

    public class TimelineEventDto
    {
        public string Date { get; set; } = string.Empty;
        public string? Era { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    /// <summary>项目级时间线条目（聚合所有卡片的时间线事件）。</summary>
    public class TimelineItemDto
    {
        public Guid CardId { get; set; }
        public string CardTitle { get; set; } = string.Empty;
        public string CardType { get; set; } = string.Empty;
        public string Date { get; set; } = string.Empty;
        public string? Era { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
    }

    /// <summary>全局搜索卡片结果。</summary>
    public class WorldSearchResultDto
    {
        public Guid CardId { get; set; }
        public string CardTitle { get; set; } = string.Empty;
        public string CardType { get; set; } = string.Empty;
        public Guid ProjectId { get; set; }
        public string ProjectName { get; set; } = string.Empty;
    }
}
