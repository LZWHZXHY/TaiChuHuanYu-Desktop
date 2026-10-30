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
        /// <summary>区块排列 JSON（访客拿到的是去掉隐藏区块后的版本）</summary>
        public string? Layout { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public List<RelationDto> OutRelations { get; set; } = new();
        public List<RelationDto> InRelations { get; set; } = new();

        public List<string> GalleryImages { get; set; } = new();
    }

}
