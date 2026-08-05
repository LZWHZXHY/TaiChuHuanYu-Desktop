using TaiChuWeb_V2.Models.Game;

namespace TaiChuWeb_V2.Dtos.Game
{
    // ============================================================
    //  请求 DTO（前端 → 后端）
    // ============================================================

    /// <summary>
    /// 创建游戏请求
    /// </summary>
    public class CreateGameDto
    {
        public string Type { get; set; } = "questionnaire";
        public string Icon { get; set; } = "🎮";
        public string Title { get; set; }
        public string Description { get; set; }
        public string Scoring { get; set; } = "sum";
        public List<QuestionDto> Questions { get; set; }
        public List<ResultDto> Results { get; set; }
    }

    /// <summary>
    /// 更新游戏请求
    /// </summary>
    public class UpdateGameDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Icon { get; set; }
        public string? Status { get; set; }
        public string? Scoring { get; set; }
    }

    /// <summary>
    /// 题目 DTO
    /// </summary>
    public class QuestionDto
    {
        public string Type { get; set; } = "single";
        public string Text { get; set; }
        public string? Image { get; set; }
        public List<OptionDto> Options { get; set; }
    }

    /// <summary>
    /// 选项 DTO
    /// </summary>
    public class OptionDto
    {
        public string Label { get; set; }
        public int Value { get; set; }
        public string? Image { get; set; }
    }

    /// <summary>
    /// 结果 DTO
    /// </summary>
    public class ResultDto
    {
        public int Min { get; set; }
        public int Max { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string? Desc { get; set; }  // ← 添加此行，兼容前端
        public string? Icon { get; set; }
        public string? Image { get; set; }
    }

    // ============================================================
    //  响应 DTO（后端 → 前端）
    // ============================================================

    /// <summary>
    /// 游戏响应 DTO（不包含循环引用）
    /// </summary>
    public class GameResponseDto
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string Icon { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public Guid CreatorId { get; set; }
        public string CreatorName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int ExpCost { get; set; }
        public int PlayCount { get; set; }
        public GameQuestionnaireDto? Questionnaire { get; set; }
    }

    /// <summary>
    /// 游戏列表项 DTO（精简版）
    /// </summary>
    public class GameListItemDto
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string Icon { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public int ExpCost { get; set; }
        public int PlayCount { get; set; }
        public string CreatorName { get; set; }
        public int? QuestionnaireId { get; set; }
    }

    /// <summary>
    /// 问卷配置 DTO
    /// </summary>
    public class GameQuestionnaireDto
    {
        public int Id { get; set; }
        public string Scoring { get; set; }
        public List<GameQuestionDto> Questions { get; set; }
        public List<GameResultDto> Results { get; set; }
    }

    /// <summary>
    /// 游戏题目 DTO
    /// </summary>
    public class GameQuestionDto
    {
        public int Id { get; set; }
        public string Type { get; set; }
        public string Text { get; set; }
        public string? Image { get; set; }
        public int Order { get; set; }
        public List<GameOptionDto> Options { get; set; }
    }

    /// <summary>
    /// 游戏选项 DTO
    /// </summary>
    public class GameOptionDto
    {
        public int Id { get; set; }
        public string Label { get; set; }
        public int Value { get; set; }
        public string? Image { get; set; }
        public int Order { get; set; }
    }

    /// <summary>
    /// 游戏结果 DTO
    /// </summary>
    public class GameResultDto
    {
        public int Id { get; set; }
        public int Min { get; set; }
        public int Max { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string? Icon { get; set; }
        public string? Image { get; set; }
        public int Order { get; set; }
    }
}