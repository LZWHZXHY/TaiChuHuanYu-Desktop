using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UserModel = TaiChuWeb_V2.Models.User.User;   // 添加这一行

namespace TaiChuWeb_V2.Models.Survey
{
    /// <summary>
    /// 问卷提交记录
    /// </summary>
    public class SurveySubmission
    {
        [Key]
        public int Id { get; set; }

        public int SurveyId { get; set; }

        /// <summary>
        /// 提交用户ID（匿名时为NULL）
        /// </summary>
        public Guid? UserId { get; set; }

        /// <summary>
        /// 匿名提交标识（IP或设备指纹）
        /// </summary>
        [MaxLength(200)]
        public string? SubmitterIdentifier { get; set; }

        public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// 完成耗时（秒）
        /// </summary>
        public int? CompletedTime { get; set; }

        public bool IsValid { get; set; } = true;

        // ===== 导航属性 =====
        [ForeignKey(nameof(SurveyId))]
        public virtual Survey? Survey { get; set; }

        [ForeignKey(nameof(UserId))]
        public virtual UserModel? User { get; set; }  // User → UserModel

        public virtual ICollection<Answer> Answers { get; set; } = new List<Answer>();
    }
}