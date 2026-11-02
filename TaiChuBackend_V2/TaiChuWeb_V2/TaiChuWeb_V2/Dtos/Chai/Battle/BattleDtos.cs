// BattleDtos.cs

using System.Text.Json.Serialization;

namespace TaiChuWeb_V2.Dtos.Chai.Battle
{
    // ===== 基础 DTO =====

    /// <summary>
    /// 约战参与者 DTO
    /// </summary>
    public class BattleParticipantDto
    {
        public string Id { get; set; } = string.Empty;
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public List<Guid> OcIds { get; set; } = new();      // ⭐ 多个OC
        public List<string> OcNames { get; set; } = new();   // ⭐ 多个OC名称
        public string? TeamName { get; set; }
        public int? TeamNumber { get; set; }
        public string Status { get; set; } = "registered";
        public string? Result { get; set; }
        public DateTime JoinedAt { get; set; }
        public DateTime? SubmittedAt { get; set; }
    }

    /// <summary>
    /// 约战响应 DTO
    /// </summary>
    public class BattleDto
    {
        public string Id { get; set; } = string.Empty;

        public Guid InitiatorId { get; set; }

        public string InitiatorName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverUrl { get; set; }
        public string? BattleType { get; set; }
        public string? Rules { get; set; }
        public string JudgmentType { get; set; } = "vote";
        public string Status { get; set; } = "open";
        public string? Result { get; set; }
        public string? ResultDescription { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? RegistrationDeadline { get; set; }
        public DateTime? SubmissionDeadline { get; set; }
        public DateTime? FinishedAt { get; set; }
        public string? SurveyId { get; set; }
        public string BattleConfigJson { get; set; } = "{}";

        /// <summary>结果种类：win 一方胜 / pyrrhic 惨胜 / draw 平手 / mutual 同归于尽 / nocontest 无结果；还没出结果是空字符串</summary>
        public string Outcome { get; set; } = string.Empty;

        public Dictionary<string, List<Guid>>? OpponentOcIds { get; set; }

        public bool IsPublic { get; set; }

        // ⭐ 所有参与者（支持多人/多OC）
        public List<BattleParticipantDto> Participants { get; set; } = new();
        public List<BattleSubmissionDto> Submissions { get; set; } = new();

        public int ParticipantCount { get; set; }
        public int SubmissionCount { get; set; }
        /// <summary>当前看的人能不能审核作品（超级管理员 / 用户审核）</summary>
        public bool CanReview { get; set; }
    }

    public class BattleSubmissionDto
    {
        public string Id { get; set; } = string.Empty;
        public string ParticipantId { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ContentUrl { get; set; } = string.Empty;
        public string? ContentType { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public BattleParticipantDto? Participant { get; set; }
        /// <summary>审核状态：pending 待审核 / approved 通过 / rejected 打回。没通过时别人看不到链接</summary>
        public string ReviewStatus { get; set; } = "pending";
        /// <summary>打回原因（只给作者本人和审核员看）</summary>
        public string? ReviewNote { get; set; }
    }

    // ===== 请求 DTO =====

    public class CreateBattleRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? CoverUrl { get; set; }
        public string? BattleType { get; set; }
        public string? Rules { get; set; }
        public string? JudgmentType { get; set; } = "vote";

        // ⭐ 发起方：多个OC
        public List<Guid> ChallengerOcIds { get; set; } = new();

        // ⭐ 指定对手（可选）：用户ID → OC列表
        public Dictionary<Guid, List<Guid>>? OpponentOcIds { get; set; }

        // ⭐ 战斗配置JSON（完全自由）
        public string? BattleConfigJson { get; set; }
    }

    public class RegisterBattleRequest
    {
        public List<Guid> OcIds { get; set; } = new();  // ⭐ 报名者携带的OC列表
        public string? Remark { get; set; }
    }

    public class SubmitWorkRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ContentUrl { get; set; } = string.Empty;
        public string? ContentType { get; set; }
    }

    public class InternalResultRequest
    {
        public List<string> WinnerIds { get; set; } = new();
        public string? ResultDescription { get; set; }
        /// <summary>结果种类：win 一方胜 / pyrrhic 惨胜 / draw 平手 / mutual 同归于尽 / nocontest 无结果（不传 = 老前端：WinnerIds 里有 draw 就是平手，否则一方胜）</summary>
        public string? Outcome { get; set; }
    }

    public class BattleListResponse
    {
        public List<BattleDto> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        [JsonIgnore]
        public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
    }
}