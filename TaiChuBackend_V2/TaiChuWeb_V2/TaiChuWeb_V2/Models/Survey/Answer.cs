using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Survey
{
    /// <summary>
    /// 答案表（灵活存储不同题型的答案）
    /// </summary>
    public class Answer
    {
        [Key]
        public int Id { get; set; }

        public int SubmissionId { get; set; }
        public int QuestionId { get; set; }

        /// <summary>
        /// 填空题答案 / 评分题值 / 其他文本答案
        /// </summary>
        public string? AnswerText { get; set; }

        /// <summary>
        /// 选择题选中的选项ID列表（JSON数组）
        /// </summary>
        [Column(TypeName = "json")]
        public string? SelectedOptionIds { get; set; }

        /// <summary>
        /// 排序题结果（JSON数组，按顺序存放选项ID）
        /// </summary>
        [Column(TypeName = "json")]
        public string? SortResult { get; set; }

        /// <summary>
        /// 矩阵题结果（JSON对象）
        /// </summary>
        [Column(TypeName = "json")]
        public string? MatrixResult { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ===== 导航属性 =====
        [ForeignKey(nameof(SubmissionId))]
        public virtual SurveySubmission? Submission { get; set; }

        [ForeignKey(nameof(QuestionId))]
        public virtual Question? Question { get; set; }
    }
}