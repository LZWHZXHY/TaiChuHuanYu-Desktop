using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Community
{
    /// <summary>爱发电同步配置（单行，Id=1）。</summary>
    [Table("AfdianConfigs")]
    public class AfdianConfig
    {
        [Key]
        public int Id { get; set; } = 1;

        [MaxLength(100)]
        public string UserId { get; set; } = string.Empty;

        /// <summary>AES 加密的 API token</summary>
        [MaxLength(2000)]
        public string EncryptedToken { get; set; } = string.Empty;

        /// <summary>是否启用自动同步</summary>
        public bool Enabled { get; set; }

        /// <summary>唯一的同步管理员（绑定的 app 账号 Guid），绑定后只有该账号可操作</summary>
        public Guid? OwnerUserId { get; set; }

        public DateTime? LastSyncAt { get; set; }

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
