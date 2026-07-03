namespace TaiChuWeb_V2.Dtos.Artwork
{
    public class ArtworkDetailDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public DateTime UploadAt { get; set; }

        public AuthorDto? Author { get; set; }
        public List<ArtworkImageDto> Images { get; set; } = new();

        // ========== 水印配置 ==========
        public string WatermarkType { get; set; } = "text";

        // 文字水印
        public bool WatermarkEnabled { get; set; } = true;
        public string? WatermarkText { get; set; }
        public string? WatermarkPosition { get; set; }
        public int WatermarkFontSize { get; set; }
        public double WatermarkOpacity { get; set; }
        public string? WatermarkColor { get; set; }
        public int WatermarkRotation { get; set; }

        // 图片水印
        public string? WatermarkImageUrl { get; set; }
        public int WatermarkImageWidth { get; set; }
        public int WatermarkImageHeight { get; set; }
        public double WatermarkImageScale { get; set; }
        public double WatermarkImageOpacity { get; set; }
    }

    public class AuthorDto
    {
        public string Username { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public string? Bio { get; set; }
    }

    public class ArtworkImageDto
    {
        public string Url { get; set; } = string.Empty;
        public string? Caption { get; set; }
    }
}
