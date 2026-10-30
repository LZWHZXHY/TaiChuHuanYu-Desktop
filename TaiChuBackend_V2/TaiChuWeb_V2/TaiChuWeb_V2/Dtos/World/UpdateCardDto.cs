using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Dtos.World
{
    public class UpdateCardDto
    {
        // 可选以兼容既有调用；编辑器始终发送读取时的版本。
        public DateTime? ExpectedUpdatedAt { get; set; }

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

        /// <summary>区块排列 JSON；省略时不改，传空字符串表示恢复默认排列</summary>
        public string? Layout { get; set; }

        public string? CoverImage { get; set; }

        // 省略时保留既有关系；传空数组时清空出向关系。
        public List<CreateRelationDto>? Relations { get; set; }

    }
}
