namespace TaiChuWeb_V2.Dtos.Artwork
{
    public class ArtworkItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImageUrl { get; set; } // 封面图
        public string AuthorName { get; set; } = string.Empty; // 作者昵称
        public string? AuthorAvatar { get; set; } // 作者头像
        public DateTime UploadAt { get; set; }
        public int ImageCount { get; set; } // 该作品包含的图片总数
    }
}
