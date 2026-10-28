using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.User
{
    /// <summary>用户自带的 AI 配置（BYOK）。API Key 加密存储。</summary>
    [Table("UserAiConfigs")]
    public class UserAiConfig
    {
        [Key]
        [Column(TypeName = "char(36)")]
        public Guid UserId { get; set; }

        [MaxLength(500)]
        public string BaseUrl { get; set; } = "https://api.deepseek.com";

        [MaxLength(4000)]
        public string EncryptedApiKey { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Model { get; set; } = "deepseek-chat";

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
