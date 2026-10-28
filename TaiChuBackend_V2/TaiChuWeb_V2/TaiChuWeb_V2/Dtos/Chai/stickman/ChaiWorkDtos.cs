namespace TaiChuWeb_V2.Dtos.Chai.stickman
{
    /// <summary>作品简略 DTO（列表用）。</summary>
    public class ChaiWorkBriefDto
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? CoverUrl { get; set; }
        public string MediaType { get; set; } = "video";
        public string AuthorName { get; set; } = string.Empty;
        public Guid AuthorId { get; set; }
        public string Status { get; set; } = "draft";
        public int Views { get; set; }
        public int LikeCount { get; set; }
        public int FavoriteCount { get; set; }
        public int CommentCount { get; set; }
        /// <summary>标签（列表也返回，供前端分类筛选）</summary>
        public List<string> Tags { get; set; } = new();
        public string? TagsText { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    /// <summary>作品完整 DTO（详情用）。</summary>
    public class ChaiWorkDto : ChaiWorkBriefDto
    {
        public string? Description { get; set; }
        public string MediaUrl { get; set; } = string.Empty;
        public List<Guid> RelatedCharacterIds { get; set; } = new();
        public Guid? RelatedBattleId { get; set; }
        public Guid? RelatedJointId { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    /// <summary>创建作品请求。</summary>
    public class CreateChaiWorkRequest
    {
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string MediaUrl { get; set; } = string.Empty;
        public string? CoverUrl { get; set; }
        public string MediaType { get; set; } = "video";
        public string Status { get; set; } = "draft";
        public List<string>? Tags { get; set; }
        public List<Guid>? RelatedCharacterIds { get; set; }
        public Guid? RelatedBattleId { get; set; }
        public Guid? RelatedJointId { get; set; }
    }

    /// <summary>更新作品请求。</summary>
    public class UpdateChaiWorkRequest
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? MediaUrl { get; set; }
        public string? CoverUrl { get; set; }
        public string? MediaType { get; set; }
        public string? Status { get; set; }
        public List<string>? Tags { get; set; }
        public List<Guid>? RelatedCharacterIds { get; set; }
        public Guid? RelatedBattleId { get; set; }
        public Guid? RelatedJointId { get; set; }
    }

    public class ChaiWorkListResponse
    {
        public List<ChaiWorkBriefDto> Items { get; set; } = new();
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }
}
