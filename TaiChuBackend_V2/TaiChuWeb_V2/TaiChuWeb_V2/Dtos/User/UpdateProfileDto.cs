namespace TaiChuWeb_V2.Dtos.User
{
    public class UpdateProfileDto
    {
        public string? Avatar { get; set; }
        public string? Gender { get; set; }
        public string? Bio { get; set; }
        public string? Mood { get; set; }
        public string? Address { get; set; }
        public string? SocialLinks { get; set; } // 接收前端传来的 JSON 字符串
        public DateTime? Birthday { get; set; }
    }
}
