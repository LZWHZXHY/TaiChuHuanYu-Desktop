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

        public Dictionary<string, List<Guid>>? OpponentOcIds { get; set; }

        public bool IsPublic { get; set; }

        // ⭐ 所有参与者（支持多人/多OC）
        public List<BattleParticipantDto> Participants { get; set; } = new();
        public List<BattleSubmissionDto> Submissions { get; set; } = new();

        public int ParticipantCount { get; set; }
        public int SubmissionCount { get; set; }
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