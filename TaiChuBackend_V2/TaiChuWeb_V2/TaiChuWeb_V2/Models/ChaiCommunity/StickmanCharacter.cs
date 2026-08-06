using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaiChuWeb_V2.Models.User;  // ← User 在这个命名空间下

namespace TaiChuWeb_V2.Models.ChaiCommunity
{
    [Table("StickmanCharacters")]
    public class StickmanCharacter
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>
        /// 角色标题
        /// </summary>
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;



        public int BattleWins { get; set; }
        public int BattleLosses { get; set; }
        public int BattleDraws { get; set; }




        /// <summary>
        /// 一句话简介
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// 封面图 COS URL
        /// </summary>
        [MaxLength(500)]
        public string? CoverUrl { get; set; }

        /// <summary>
        /// 创建者用户 ID
        /// </summary>
        public Guid AuthorId { get; set; }

        /// <summary>
        /// 创建者用户名（冗余，方便展示）
        /// </summary>
        [Required]
        [MaxLength(100)]
        public string AuthorName { get; set; } = string.Empty;

        /// <summary>
        /// 浏览量
        /// </summary>
        public int Views { get; set; }

        /// <summary>
        /// 状态：draft / published / archived
        /// </summary>
        [MaxLength(20)]
        public string Status { get; set; } = "draft";


        // ✨ 新增：是否允许参与约战
        public bool IsBattleEnabled { get; set; } = true;   // 默认开启


        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // ========== 导航属性 ==========

        /// <summary>
        /// 作者（关联 Users 表）
        /// </summary>
        [ForeignKey(nameof(AuthorId))]
        public virtual TaiChuWeb_V2.Models.User.User? Author { get; set; }

        /// <summary>
        /// 自定义属性列表
        /// </summary>
        public virtual ICollection<StickmanAttribute>? Attributes { get; set; }

        /// <summary>
        /// 图库图片列表
        /// </summary>
        public virtual ICollection<StickmanImage>? Images { get; set; }



    }
}