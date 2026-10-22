namespace TaiChuWeb_V2.Dtos.World
{
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
