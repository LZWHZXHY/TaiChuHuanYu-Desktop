namespace TaiChuWeb_V2.Dtos.World
{
    public class CardSummaryDto
    {
        public Guid Id { get; set; }
        public Guid ProjectId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string? CoverImage { get; set; }  // 仅用于列表缩略图
        public DateTime UpdatedAt { get; set; }
        public int OutRelationCount { get; set; }
        public int InRelationCount { get; set; }
    }
}
