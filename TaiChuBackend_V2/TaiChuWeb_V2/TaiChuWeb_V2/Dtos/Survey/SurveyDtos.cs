using System.Text.Json.Serialization;

namespace TaiChuWeb_V2.Dtos.Survey
{
    // ============================================================
    // 1. 问卷基础 DTO
    // ============================================================

    /// <summary>
    /// 创建问卷请求
    /// </summary>
    public class CreateSurveyRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImage { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsPublic { get; set; } = true;
        public bool AllowAnonymous { get; set; } = false;
        public int MaxSubmissions { get; set; } = 1;
        public List<CreateQuestionDto> Questions { get; set; } = new();
    }

    /// <summary>
    /// 更新问卷请求（支持更新题目）
    /// </summary>
    public class UpdateSurveyRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CoverImage { get; set; }
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public int? Status { get; set; }
        public bool? IsPublic { get; set; }
        public bool? AllowAnonymous { get; set; }
        public int? MaxSubmissions { get; set; }
        public List<UpdateQuestionDto>? Questions { get; set; }
    }

    /// <summary>
    /// 问卷列表项（用于列表页展示）
    /// </summary>
    public class SurveyListItemDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImage { get; set; }
        public int Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public int TotalSubmissions { get; set; }
        public int QuestionCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatorName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 问卷详情（含所有题目和选项）
    /// </summary>
    public class SurveyDetailDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImage { get; set; }
        public int Status { get; set; }
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public bool IsPublic { get; set; }
        public bool AllowAnonymous { get; set; }
        public int MaxSubmissions { get; set; }
        public int TotalSubmissions { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatorName { get; set; } = string.Empty;
        public List<QuestionDetailDto> Questions { get; set; } = new();
    }

    /// <summary>
    /// 问卷填写内容（给用户填写的版本，不含答案数据）
    /// </summary>
    public class SurveyFillDto
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverImage { get; set; }
        public int Status { get; set; }
        public bool IsPublic { get; set; }
        public bool HasSubmitted { get; set; }  // 当前用户是否已提交
        public List<QuestionFillDto> Questions { get; set; } = new();
    }


    // ============================================================
    // 2. 题目相关 DTO
    // ============================================================

    /// <summary>
    /// 创建题目请求（在创建问卷时使用）
    /// </summary>
    public class CreateQuestionDto
    {
        public int QuestionType { get; set; }  // 1=单选 2=多选 3=填空 4=评分 5=排序 6=矩阵
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsRequired { get; set; } = true;
        public int SortOrder { get; set; }
        public string? Config { get; set; }  // JSON 配置
        public List<CreateOptionDto> Options { get; set; } = new();
    }

    /// <summary>
    /// 更新题目（用于编辑问卷）
    /// </summary>
    public class UpdateQuestionDto
    {
        public int Id { get; set; }  // 0 表示新增题目
        public int QuestionType { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsRequired { get; set; } = true;
        public int SortOrder { get; set; }
        public string? Config { get; set; }
        public List<UpdateOptionDto> Options { get; set; } = new();
    }

    /// <summary>
    /// 题目详情（含选项）
    /// </summary>
    public class QuestionDetailDto
    {
        public int Id { get; set; }
        public int QuestionType { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsRequired { get; set; }
        public int SortOrder { get; set; }
        public string? Config { get; set; }
        public List<OptionDto> Options { get; set; } = new();
    }

    /// <summary>
    /// 题目填写视图（含用户已选内容）
    /// </summary>
    public class QuestionFillDto
    {
        public int Id { get; set; }
        public int QuestionType { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public bool IsRequired { get; set; }
        public string? Config { get; set; }
        public List<OptionDto> Options { get; set; } = new();
        // 用户已填的内容
        public string? UserAnswer { get; set; }           // 填空题
        public List<int>? UserSelectedOptionIds { get; set; }  // 选择题
        public List<int>? UserSortResult { get; set; }    // 排序题
        public Dictionary<string, int>? UserMatrixResult { get; set; }  // 矩阵题
    }


    // ============================================================
    // 3. 选项相关 DTO
    // ============================================================

    /// <summary>
    /// 创建选项
    /// </summary>
    public class CreateOptionDto
    {
        public string OptionText { get; set; } = string.Empty;
        public string? OptionValue { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// 更新选项（用于编辑问卷）
    /// </summary>
    public class UpdateOptionDto
    {
        public int Id { get; set; }  // 0 表示新增选项
        public string OptionText { get; set; } = string.Empty;
        public string? OptionValue { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// 选项视图
    /// </summary>
    public class OptionDto
    {
        public int Id { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public string? OptionValue { get; set; }
        public int SortOrder { get; set; }
    }


    // ============================================================
    // 4. 提交/填写相关 DTO
    // ============================================================

    /// <summary>
    /// 提交问卷请求
    /// </summary>
    public class SubmitSurveyRequest
    {
        public List<QuestionAnswerDto> Answers { get; set; } = new();
        public int? CompletedTime { get; set; }  // 完成耗时（秒）
    }

    /// <summary>
    /// 单个题目的答案
    /// </summary>
    public class QuestionAnswerDto
    {
        public int QuestionId { get; set; }

        // 根据题型使用不同字段
        public string? AnswerText { get; set; }          // 填空题 / 评分题
        public List<int>? SelectedOptionIds { get; set; } // 选择题
        public List<int>? SortResult { get; set; }       // 排序题
        public Dictionary<string, int>? MatrixResult { get; set; }  // 矩阵题
    }

    /// <summary>
    /// 提交成功返回
    /// </summary>
    public class SubmitSurveyResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int SubmissionId { get; set; }
        public bool CanViewResult { get; set; }  // 是否可以查看结果
    }


    // ============================================================
    // 5. 统计相关 DTO
    // ============================================================

    /// <summary>
    /// 问卷统计概览
    /// </summary>
    public class SurveyStatsOverviewDto
    {
        public int TotalSubmissions { get; set; }
        public int TotalQuestions { get; set; }
        public double AvgCompletionTime { get; set; }  // 平均完成时间（秒）
        public List<QuestionStatsDto> QuestionStats { get; set; } = new();
    }

    /// <summary>
    /// 单个题目的统计数据
    /// </summary>
    public class QuestionStatsDto
    {
        public int QuestionId { get; set; }
        public string Title { get; set; } = string.Empty;
        public int QuestionType { get; set; }
        public int TotalAnswers { get; set; }
        public int SkipCount { get; set; }  // 跳过人数（非必答题）

        // 选择题统计
        public List<OptionStatsDto>? OptionStats { get; set; }

        // 评分题统计
        public double? AverageScore { get; set; }
        public int? MinScore { get; set; }
        public int? MaxScore { get; set; }
        public Dictionary<int, int>? ScoreDistribution { get; set; }  // 分数分布

        // 填空题统计
        public List<string>? TextAnswers { get; set; }

        // 排序题统计
        public Dictionary<int, double>? AvgRank { get; set; }  // 选项ID → 平均排名

        // 矩阵题统计
        public Dictionary<string, double>? MatrixAverages { get; set; }  // "行_列" → 平均分
    }

    /// <summary>
    /// 选项统计
    /// </summary>
    public class OptionStatsDto
    {
        public int OptionId { get; set; }
        public string OptionText { get; set; } = string.Empty;
        public int Count { get; set; }
        public double Percentage { get; set; }
    }

    /// <summary>
    /// 提交记录列表项
    /// </summary>
    public class SubmissionListItemDto
    {
        public int Id { get; set; }
        public string? UserName { get; set; }
        public string? UserId { get; set; }
        public bool IsAnonymous { get; set; }
        public DateTime SubmittedAt { get; set; }
        public int? CompletedTime { get; set; }
        public bool IsValid { get; set; }
    }


    // ============================================================
    // 6. 通用响应 DTO
    // ============================================================

    /// <summary>
    /// 通用操作响应
    /// </summary>
    public class SurveyResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? Id { get; set; }
        public object? Data { get; set; }
    }
}