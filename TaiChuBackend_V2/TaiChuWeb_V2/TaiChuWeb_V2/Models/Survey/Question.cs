using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Survey
{
    /// <summary>
    /// 题目表
    /// </summary>
    public class Question
    {
        [Key]
        public int Id { get; set; }

        public int SurveyId { get; set; }

        /// <summary>
        /// 题型：1=单选, 2=多选, 3=填空, 4=评分, 5=排序, 6=矩阵
        /// </summary>
        public int QuestionType { get; set; }

        [Required]
        [MaxLength(500)]
        public string Title { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Description { get; set; }

        public bool IsRequired { get; set; } = true;

        public int SortOrder { get; set; } = 0;

        /// <summary>
        /// 扩展配置（JSON格式）
        /// 评分题：{ "maxScore": 5, "minScore": 1 }
        /// 矩阵题：{ "rows": ["行1", "行2"], "cols": ["列1", "列2"] }
        /// </summary>
        [Column(TypeName = "json")]
        public string? Config { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ===== 导航属性 =====
        [ForeignKey(nameof(SurveyId))]
        public virtual Survey? Survey { get; set; }

        public virtual ICollection<QuestionOption> Options { get; set; } = new List<QuestionOption>();
        public virtual ICollection<Answer> Answers { get; set; } = new List<Answer>();
    }
}