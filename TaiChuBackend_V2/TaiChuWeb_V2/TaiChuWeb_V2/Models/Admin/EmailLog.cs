using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Admin
{
    [Table("EmailLogs")]
    public class EmailLog
    {
        [Key]
        public int Id { get; set; }

        /// <summary>
        /// 任务类型：update, activity, recall, festival
        /// </summary>
        [Required]
        [MaxLength(20)]
        public string Type { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Subject { get; set; } = string.Empty;

        /// <summary>
        /// 成功触达的人数
        /// </summary>
        public int TargetCount { get; set; }

        /// <summary>
        /// 发送状态：success, failed
        /// </summary>
        [MaxLength(20)]
        public string Status { get; set; } = "success";

        public DateTime SentAt { get; set; } = DateTime.UtcNow;
    }
}