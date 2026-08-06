namespace TaiChuWeb_V2.Dtos.Chai.Joint
{
    // ============================================================
    // 基础 DTO
    // ============================================================

    /// <summary>
    /// 参与者 DTO
    /// </summary>
    public class JointParticipantDto
    {
        public Guid Id { get; set; }
        public Guid UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Status { get; set; } = "pending";
        public string? Remark { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>
    /// 联合活动响应 DTO
    /// </summary>
    public class JointActivityDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Requirements { get; set; }
        public string? Contact { get; set; }
        public string Type { get; set; } = "joint";
        public string Status { get; set; } = "open";
        public bool AuditRequired { get; set; }
        public string? CoverUrl { get; set; }
        public Guid OrganizerId { get; set; }
        public string OrganizerName { get; set; } = string.Empty;
        public int ParticipantCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // ===== 新增字段 =====
        /// <summary>
        /// 来源类型：user / official
        /// </summary>
        public string OrganizerType { get; set; } = "user";

        /// <summary>
        /// 审核状态：pending / approved / rejected（仅用户自建联合）
        /// </summary>
        public string? ApprovalStatus { get; set; }

        public List<JointParticipantDto>? Participants { get; set; }
    }

    // ============================================================
    // 创建请求 DTO
    // ============================================================

    /// <summary>
    /// 创建联合活动请求
    /// </summary>
    public class CreateJointRequest
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string? Requirements { get; set; }
        public string? Contact { get; set; }
        public string Type { get; set; } = "joint";
        public string Status { get; set; } = "open";
        public bool AuditRequired { get; set; } = true;
        public string? CoverUrl { get; set; }

        // ===== 新增字段 =====
        /// <summary>
        /// 来源类型：user / official（默认 user）
        /// </summary>
        public string OrganizerType { get; set; } = "user";
    }

    // ============================================================
    // 更新请求 DTO
    // ============================================================

    /// <summary>
    /// 更新联合活动请求
    /// </summary>
    public class UpdateJointRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Requirements { get; set; }
        public string? Contact { get; set; }
        public string? Type { get; set; }
        public string? Status { get; set; }
        public bool? AuditRequired { get; set; }
        public string? CoverUrl { get; set; }

        // 注意：OrganizerType 和 ApprovalStatus 不允许通过更新接口修改
        // 它们由系统管理：OrganizerType 在创建时设定，ApprovalStatus 通过审核接口变更
    }

    // ============================================================
    // 列表响应 DTO
    // ============================================================

    /// <summary>
    /// 联合活动列表响应（带分页）
    /// </summary>
    public class JointListResponse
    {
        public List<JointActivityDto> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
    }

    // ============================================================
    // 操作请求 DTO
    // ============================================================

    /// <summary>
    /// 报名请求
    /// </summary>
    public class JoinRequest
    {
        public string? Remark { get; set; }
    }

    /// <summary>
    /// 审核参与者请求
    /// </summary>
    public class AuditRequest
    {
        public Guid UserId { get; set; }
        public string Status { get; set; } = "approved";
    }

    /// <summary>
    /// 踢出参与者请求
    /// </summary>
    public class KickRequest
    {
        public Guid UserId { get; set; }
    }

    // ============================================================
    // 新增：审批联合活动请求 DTO
    // ============================================================

    /// <summary>
    /// 审批用户自建联合请求
    /// </summary>
    public class ApproveRequest
    {
        /// <summary>
        /// 审批状态：approved / rejected
        /// </summary>
        public string Status { get; set; } = "approved";
    }
}