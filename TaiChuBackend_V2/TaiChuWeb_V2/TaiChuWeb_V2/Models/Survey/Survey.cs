using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using UserModel = TaiChuWeb_V2.Models.User.User;   // 添加这一行
using TaiChuWeb_V2.Models.User;  // 保留这行，因为 ICollection<User> 可能需要

namespace TaiChuWeb_V2.Models.Survey
{
    /// <summary>
    /// 问卷主表
    /// </summary>
    public class Survey
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [MaxLength(500)]
        public string? CoverImage { get; set; }

        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }

        /// <summary>
        /// 状态：0=草稿, 1=发布中, 2=已结束, 3=已关闭
        /// </summary>
        public int Status { get; set; } = 0;

        /// <summary>
        /// 是否公开结果
        /// </summary>
        public bool IsPublic { get; set; } = true;

        /// <summary>
        /// 是否允许匿名提交
        /// </summary>
        public bool AllowAnonymous { get; set; } = false;

        /// <summary>
        /// 每人最大提交次数（默认1次）
        /// </summary>
        public int MaxSubmissions { get; set; } = 1;

        /// <summary>
        /// 总提交数（冗余字段，提高查询效率）
        /// </summary>
        public int TotalSubmissions { get; set; } = 0;

        public Guid CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }

        // ===== 导航属性 =====
        [ForeignKey(nameof(CreatedBy))]
        public virtual UserModel? Creator { get; set; }  // User → UserModel

        public virtual ICollection<Question> Questions { get; set; } = new List<Question>();
        public virtual ICollection<SurveySubmission> Submissions { get; set; } = new List<SurveySubmission>();
    }
}