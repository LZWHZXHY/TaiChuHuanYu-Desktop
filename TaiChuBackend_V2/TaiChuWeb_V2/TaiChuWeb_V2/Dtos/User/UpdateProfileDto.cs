namespace TaiChuWeb_V2.Dtos.User
{
    public class UpdateProfileDto
    {
        public string? Avatar { get; set; }

        public string? Gender { get; set; }

        // 对应新模型中 [MaxLength(1000)] 的 Bio
        public string? Bio { get; set; }

        public string? Mood { get; set; }

        public string? Address { get; set; }

        // 建议添加，因为模型里有这个字段
        public string? PhoneNumber { get; set; }

        public DateTime? Birthday { get; set; }

        // 接收前端传来的 JSON 字符串
        public string? SocialLinks { get; set; }

        // 新增：对应模型中的 ExtraConfig (JSON 配置)
        // 即使前端暂时不传，加上它也能保证后续扩展主题色、隐私开关等功能
        public string? ExtraConfig { get; set; }
    }
}