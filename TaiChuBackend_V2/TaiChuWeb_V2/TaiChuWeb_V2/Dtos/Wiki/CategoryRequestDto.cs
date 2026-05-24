namespace TaiChuWeb_V2.Dtos.Wiki
{
    public class CategoryRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public int SortOrder { get; set; }
        public string? OwnerId { get; set; }
        public int OwnershipType { get; set; }

        public string Reason { get; set; } = string.Empty;
    }
}
