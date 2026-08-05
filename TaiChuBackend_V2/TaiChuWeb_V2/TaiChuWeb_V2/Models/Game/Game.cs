using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TaiChuWeb_V2.Models.Survey;

namespace TaiChuWeb_V2.Models.Game
{
    // Game/Game.cs - 游戏主表
    public class Game
    {
        [Key]
        public int Id { get; set; }
        public string Type { get; set; } = "questionnaire"; // questionnaire, simulation...
        public string Icon { get; set; } = "🎮";
        public string Title { get; set; }
        public string Description { get; set; }
        public string Status { get; set; } = "草稿"; // 草稿 | 已发布
        public Guid CreatorId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int ExpCost { get; set; } = 50;      // 创建消耗经验
        public int PlayCount { get; set; } = 0;     // 被游玩次数

        [ForeignKey("CreatorId")]
        public virtual User.User Creator { get; set; }
        public virtual GameQuestionnaire Questionnaire { get; set; }
    }

    // Game/GameQuestionnaire.cs - 问卷配置
    public class GameQuestionnaire
    {
        [Key]
        public int Id { get; set; }
        public int GameId { get; set; }
        public string Scoring { get; set; } = "sum"; // sum | average

        [ForeignKey("GameId")]
        public virtual Game Game { get; set; }
        public virtual ICollection<GameQuestion> Questions { get; set; }
        public virtual ICollection<GameResult> Results { get; set; }
    }

    // Game/GameQuestion.cs - 题目
    public class GameQuestion
    {
        [Key]
        public int Id { get; set; }
        public int QuestionnaireId { get; set; }
        public string Type { get; set; } = "single"; // single | yesno | likert | multiple
        public string Text { get; set; }
        public string Image { get; set; }            // 题目配图
        public int Order { get; set; }

        [ForeignKey("QuestionnaireId")]
        public virtual GameQuestionnaire Questionnaire { get; set; }
        public virtual ICollection<GameOption> Options { get; set; }
    }

    // Game/GameOption.cs - 选项
    public class GameOption
    {
        [Key]
        public int Id { get; set; }
        public int QuestionId { get; set; }
        public string Label { get; set; }
        public int Value { get; set; }               // 分值（允许负数）
        public string Image { get; set; }            // 选项配图
        public int Order { get; set; }

        [ForeignKey("QuestionId")]
        public virtual GameQuestion Question { get; set; }
    }

    // Game/GameResult.cs - 结果
    public class GameResult
    {
        [Key]
        public int Id { get; set; }
        public int QuestionnaireId { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Icon { get; set; }
        public string Image { get; set; }            // 结果配图
        public int Order { get; set; }   // ← 新增

        [ForeignKey("QuestionnaireId")]
        public virtual GameQuestionnaire Questionnaire { get; set; }
    }

    // Game/GameSession.cs - 试玩记录（成绩）
    public class GameSession
    {
        [Key]
        public long Id { get; set; }
        public int GameId { get; set; }
        public Guid UserId { get; set; }
        public int TotalScore { get; set; }
        public int? ResultId { get; set; }
        public string AnswersJson { get; set; }      // 存储每道题得分
        public DateTime CreatedAt { get; set; }

        [ForeignKey("GameId")]
        public virtual Game Game { get; set; }
        [ForeignKey("UserId")]
        public virtual User.User User { get; set; }
        [ForeignKey("ResultId")]
        public virtual GameResult Result { get; set; }
    }
}