using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.User
{
    [Table("UserSettings")]
    public class UserSettings
    {
        [Key]
        public Guid UserId { get; set; }

        /// <summary>
        /// 接收系统更新邮件
        /// </summary>
        public bool ReceiveUpdateEmail { get; set; } = true;

        /// <summary>
        /// 接收活动与资讯邮件
        /// </summary>
        public bool ReceiveActivityEmail { get; set; } = false;

        /// <summary>
        /// 接收个人周报推送
        /// </summary>
        public bool WeeklyReport { get; set; } = true;

        [ForeignKey("UserId")]
        public virtual User User { get; set; } = null!;
    }
}