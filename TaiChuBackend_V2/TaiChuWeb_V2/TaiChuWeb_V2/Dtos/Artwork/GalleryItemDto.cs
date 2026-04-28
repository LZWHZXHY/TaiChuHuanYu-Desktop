namespace TaiChuWeb_V2.Dtos.Artwork
{
    public class ArtworkItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? CoverImageUrl { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string? AuthorAvatar { get; set; }
        public DateTime UploadAt { get; set; }
        public int ImageCount { get; set; }

        // --- 新增统计数值，用于瀑布流卡片展示 ---
        public int LikesCount { get; set; }
        public int CommentsCount { get; set; }
        public int ViewCount { get; set; }
    }
}