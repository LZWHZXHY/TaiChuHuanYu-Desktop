using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Club
{
    [Table("ClubGameFields")]
    public class ClubGameField
    {
        [Key]
        public long Id { get; set; }

        [MaxLength(20)]
        public string GameCode { get; set; } = "";

        [MaxLength(50)]
        public string Key { get; set; } = "";

        [MaxLength(50)]
        public string Label { get; set; } = "";

        [MaxLength(20)]
        public string Type { get; set; } = "select";

        [Column(TypeName = "json")]
        public string? OptionsJson { get; set; }

        public bool Required { get; set; } = true;

        [MaxLength(200)]
        public string? Placeholder { get; set; }

        public int SortOrder { get; set; } = 0;

        // ⭐ 新增：是否作为雷达图评分维度
        public bool IsScoreDimension { get; set; } = false;

        // ⭐ 新增：值 → 分数的映射规则（JSON 字符串）
        //   select 类型：{"0-1":20, "1-2":40, "2-3":70}
        //   number 类型：{"min":0, "max":1500, "reverse":false}
        [Column(TypeName = "json")]
        public string? ScoreMapJson { get; set; }

        [ForeignKey("GameCode")]
        public virtual ClubGame Game { get; set; } = null!;
    }
}