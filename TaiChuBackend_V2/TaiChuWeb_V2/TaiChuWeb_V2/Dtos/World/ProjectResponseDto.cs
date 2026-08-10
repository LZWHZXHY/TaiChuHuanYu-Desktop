namespace TaiChuWeb_V2.Dtos.World
{
    public class ProjectResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsPublic { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int CardCount { get; set; }
        public string? OwnerName { get; set; }
        public Guid OwnerId { get; set; }   // 🆕 添加这个字段
    }
}
