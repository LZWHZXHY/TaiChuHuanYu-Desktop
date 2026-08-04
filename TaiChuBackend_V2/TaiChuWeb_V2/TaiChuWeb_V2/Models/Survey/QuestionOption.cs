using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Survey
{
    /// <summary>
    /// 选项表（用于选择题、排序题）
    /// </summary>
    public class QuestionOption
    {
        [Key]
        public int Id { get; set; }

        public int QuestionId { get; set; }

        [Required]
        [MaxLength(500)]
        public string OptionText { get; set; } = string.Empty;

        /// <summary>
        /// 选项值（评分题用数字，其他题型可为空）
        /// </summary>
        [MaxLength(50)]
        public string? OptionValue { get; set; }

        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ===== 导航属性 =====
        [ForeignKey(nameof(QuestionId))]
        public virtual Question? Question { get; set; }
    }
}