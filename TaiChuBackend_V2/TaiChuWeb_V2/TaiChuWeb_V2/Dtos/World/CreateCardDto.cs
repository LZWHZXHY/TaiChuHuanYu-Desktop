using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Dtos.World
{
    public class CreateCardDto
    {
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

    }

    public class AttributeDto
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }

    public class ContentBlockDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public Guid CardId { get; set; }
        public string CardType { get; set; } = string.Empty;
        public int Order { get; set; }
    }

    public class TimelineEventDto
    {
        public string Date { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
    }
}
