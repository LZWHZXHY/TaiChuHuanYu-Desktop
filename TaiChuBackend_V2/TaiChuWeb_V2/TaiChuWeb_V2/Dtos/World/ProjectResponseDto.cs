namespace TaiChuWeb_V2.Dtos.World
{
    public class ProjectResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImage { get; set; }
        public string? GraphPositions { get; set; }
        public bool IsPublic { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int CardCount { get; set; }
        public string? OwnerName { get; set; }
        public Guid OwnerId { get; set; }   // 🆕 添加这个字段
        /// <summary>协作者人数（不含创建者）</summary>
        public int CollaboratorCount { get; set; }
        /// <summary>当前用户在这个世界里的身份：owner / editor / viewer；路人为 null</summary>
        public string? MyRole { get; set; }
    }
}
