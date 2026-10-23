using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Game
{
    /// <summary>棋类对局记录（五子棋 / 围棋 / 象棋）。</summary>
    [Table("BoardGameRecords")]
    public class BoardGameRecord
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>gobang / go / chess</summary>
        [Required]
        [MaxLength(20)]
        public string GameType { get; set; } = string.Empty;

        public Guid? Player1Id { get; set; }

        [MaxLength(100)]
        public string Player1Name { get; set; } = "先手";

        public Guid? Player2Id { get; set; }

        [MaxLength(100)]
        public string Player2Name { get; set; } = "后手";

        /// <summary>player1 / player2 / draw / undefined</summary>
        [MaxLength(20)]
        public string Winner { get; set; } = "undefined";

        /// <summary>总步数</summary>
        public int MovesCount { get; set; }

        /// <summary>完整棋谱（JSON 落子序列）</summary>
        [Column(TypeName = "longtext")]
        public string? MovesJson { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
