using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Admin
{
    [Table("EmailTemplates")]
    public class EmailTemplate
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 模板类型：recall (召回), festival (节庆)
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// 触发条件参数。
        /// 召回时：7, 30, 90 (天数)
        /// 节庆时：birthday, 或具体的日期如 "10-01"
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string ConditionValue { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string Content { get; set; } = string.Empty;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}