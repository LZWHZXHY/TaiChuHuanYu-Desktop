namespace TaiChuWeb_V2.Dtos.World
{
    public class CreateRelationDto
    {
        public Guid TargetCardId { get; set; }
        public string RelationType { get; set; } = string.Empty;
    }
}
