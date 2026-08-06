namespace TaiChuWeb_V2.Dtos.Chai.stickman
{
    // ============================================================
    // 基础 DTO
    // ============================================================

    /// <summary>
    /// 首页联合活动简略 DTO（只包含列表展示所需字段）
    /// </summary>
    public class JointActivityHomeDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? CoverUrl { get; set; }
        public string Type { get; set; } = "joint";
        public string Status { get; set; } = "open";
        public int ParticipantCount { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string OrganizerName { get; set; } = string.Empty;
    }

    /// <summary>
    /// 图库图片 DTO
    /// </summary>
    public class StickmanImageDto
    {
        public Guid Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Alt { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// 自定义属性 DTO
    /// </summary>
    public class StickmanAttributeDto
    {
        public Guid Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
        public int SortOrder { get; set; }
        public string Type { get; set; } = "short";
    }

    /// <summary>
    /// OC 角色响应 DTO（列表/详情用）
    /// </summary>
    public class StickmanCharacterDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverUrl { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public Guid AuthorId { get; set; }
        public int Views { get; set; }
        public string Status { get; set; } = "draft";
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsBattleEnabled { get; set; } = true;

        // ⭐ 新增：约战战绩统计
        public int BattleWins { get; set; }
        public int BattleLosses { get; set; }
        public int BattleDraws { get; set; }

        public List<StickmanAttributeDto>? Attributes { get; set; }
        public List<StickmanImageDto>? Images { get; set; }
    }

    // ============================================================
    // 创建请求 DTO
    // ============================================================

    public class CreateStickmanAttributeRequest
    {
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
        public int SortOrder { get; set; }
        public string Type { get; set; } = "short";
    }

    public class CreateStickmanImageRequest
    {
        public string Url { get; set; } = string.Empty;
        public string? Alt { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// OC 角色简略 DTO（用于列表页，不包含 Attributes 和 Images）
    /// </summary>
    public class StickmanBriefDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? CoverUrl { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public Guid AuthorId { get; set; }
        public string Status { get; set; } = "draft";
        public bool IsBattleEnabled { get; set; }
        public DateTime CreatedAt { get; set; }

        // ⭐ 新增：约战战绩统计（列表页可显示）
        public int BattleWins { get; set; }
        public int BattleLosses { get; set; }
        public int BattleDraws { get; set; }
    }


    /// <summary>
    /// 创建 OC 角色请求
    /// </summary>
    public class CreateStickmanRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? CoverUrl { get; set; }
        public string Status { get; set; } = "draft";
        public bool IsBattleEnabled { get; set; } = true;
        public List<CreateStickmanAttributeRequest>? Attributes { get; set; }
        public List<CreateStickmanImageRequest>? Images { get; set; }
    }

    // ============================================================
    // 更新请求 DTO
    // ============================================================

    public class UpdateStickmanAttributeRequest
    {
        public Guid? Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
        public int SortOrder { get; set; }
        public string Type { get; set; } = "short";
    }

    public class UpdateStickmanImageRequest
    {
        public Guid? Id { get; set; }
        public string Url { get; set; } = string.Empty;
        public string? Alt { get; set; }
        public int SortOrder { get; set; }
    }

    /// <summary>
    /// 更新 OC 角色请求
    /// </summary>
    public class UpdateStickmanRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? CoverUrl { get; set; }
        public string? Status { get; set; }
        public bool? IsBattleEnabled { get; set; } = true;
        public List<UpdateStickmanAttributeRequest>? Attributes { get; set; }
        public List<UpdateStickmanImageRequest>? Images { get; set; }
    }

    // ============================================================
    // 列表响应
    // ============================================================

    /// <summary>
    /// OC 角色列表响应（带分页）
    /// </summary>
    public class StickmanListResponse
    {
        public List<StickmanBriefDto> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
    }
}