namespace TaiChuWeb_V2.Dtos.News
{
    /// <summary>
    /// 管理员发布动态时使用的 DTO
    /// </summary>
    public class CreateNewsDto
    {
        public string Title { get; set; } = string.Empty;

        public string Type { get; set; } = "公告";

        public string? ImageUrl { get; set; }

        public string? Content { get; set; }
    }
}