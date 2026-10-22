using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Dtos.World
{
    public class UpdateCardDto
    {
        [MaxLength(200)]
        public string? Title { get; set; }

        [MaxLength(50)]
        public string? Type { get; set; }

        [MaxLength(50)]
        public string? SubType { get; set; }

        public List<string>? Aliases { get; set; }

        public List<string>? GalleryImages { get; set; }

        public List<AttributeDto>? Attributes { get; set; }

        public string? Description { get; set; }

        public List<ContentBlockDto>? ContentBlocks { get; set; }

        public List<TimelineEventDto>? TimelineEvents { get; set; }

        public List<string>? Tags { get; set; }

        public List<Guid>? EmbeddedCards { get; set; }

        public string? Content { get; set; }

        public string? CoverImage { get; set; }

    }
}
