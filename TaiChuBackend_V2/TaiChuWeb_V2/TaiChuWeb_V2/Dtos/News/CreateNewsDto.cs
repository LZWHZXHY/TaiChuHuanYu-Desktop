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

        /// <summary>persistent（常驻）/ scheduled（定时）</summary>
        public string NoticeKind { get; set; } = "persistent";

        /// <summary>normal（普通）/ important（重要）/ urgent（紧急）</summary>
        public string Importance { get; set; } = "normal";

        /// <summary>定时公告截止时间</summary>
        public DateTime? ExpireAt { get; set; }
    }
}