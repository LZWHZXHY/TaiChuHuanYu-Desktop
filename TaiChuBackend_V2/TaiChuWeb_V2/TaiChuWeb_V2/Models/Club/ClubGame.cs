using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Club
{
    /// <summary>
    /// 陪玩游戏注册表（后台可增删改，不用改代码）
    /// </summary>
    [Table("ClubGames")]
    public class ClubGame
    {
        [Key]
        [MaxLength(20)]
        public string Code { get; set; } = "";        // delta / apex / valorant / csgo

        [MaxLength(50)]
        public string Name { get; set; } = "";        // 三角洲行动

        [MaxLength(200)]
        public string? Description { get; set; }

        /// <summary>是否启用（下架用，不删数据）</summary>
        public bool IsActive { get; set; } = true;

        public int SortOrder { get; set; } = 0;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual ICollection<ClubGameField> Fields { get; set; } = new List<ClubGameField>();

        [Column(TypeName = "text")]
        public string? CommonRulesText { get; set; }


        /// <summary>
        /// 该游戏启用的"系统评分维度" code 列表（JSON 数组）
        /// 例：["reputation", "experience", "completionRate"]
        /// </summary>
        [Column(TypeName = "json")]
        public string? RadarSystemDimsJson { get; set; }
    }
}