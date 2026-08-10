namespace TaiChuWeb_V2.Dtos.World
{
    public class CardResponseDto
    {
        public Guid Id { get; set; }

        public string? CoverImage { get; set; }
        public Guid ProjectId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? SubType { get; set; }
        public List<string> Aliases { get; set; } = new();
        public List<AttributeDto> Attributes { get; set; } = new();
        public string? Description { get; set; }
        public List<ContentBlockDto> ContentBlocks { get; set; } = new();
        public List<TimelineEventDto> TimelineEvents { get; set; } = new();
        public List<string> Tags { get; set; } = new();
        public List<Guid> EmbeddedCards { get; set; } = new();
        public string Content { get; set; } = "{}";
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<RelationDto> OutRelations { get; set; } = new();
        public List<RelationDto> InRelations { get; set; } = new();

        public List<string> GalleryImages { get; set; } = new();
    }

    public class RelationDto
    {
        public Guid Id { get; set; }
        public Guid SourceCardId { get; set; }
        public Guid TargetCardId { get; set; }
        public string RelationType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? SourceCardTitle { get; set; }
        public string? TargetCardTitle { get; set; }
        public string? SourceCardType { get; set; }
        public string? TargetCardType { get; set; }
    }
}
