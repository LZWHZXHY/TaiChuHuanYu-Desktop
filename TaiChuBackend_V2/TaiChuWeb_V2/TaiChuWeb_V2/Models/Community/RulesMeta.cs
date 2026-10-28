using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Community
{
    /// <summary>社区规则文档的版本信息（单例，Id 固定为 1）。</summary>
    [Table("RulesMeta")]
    public class RulesMeta
    {
        [Key]
        public int Id { get; set; } = 1;

        /// <summary>规则文档版本，例如 1.0.1</summary>
        [MaxLength(20)]
        public string Version { get; set; } = "1.0.0";

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}
