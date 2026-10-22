using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Dtos.Artwork;

public class ArtworkStatsDto
{
    public int LikesCount { get; set; }
    public int FavoritesCount { get; set; }
    public int CommentsCount { get; set; }
    public bool IsLiked { get; set; }
    public bool IsFavorited { get; set; }

    public void SetStats(ArtworkStatsDto stats)
    {
        LikesCount = stats.LikesCount;
        FavoritesCount = stats.FavoritesCount;
        CommentsCount = stats.CommentsCount;
        IsLiked = stats.IsLiked;
        IsFavorited = stats.IsFavorited;
    }
}

public class ArtworkStatsResult : ArtworkStatsDto
{
    public int Id { get; set; }
}

public class CreateArtworkCommentDto
{
    [Required, StringLength(1000)]
    public string Content { get; set; } = "";
    public Guid? ParentId { get; set; }
}

public class ArtworkCommentDto
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = "";
    public string AuthorName { get; set; } = "";
    public string? Avatar { get; set; }
    public string Content { get; set; } = "";
    public Guid? ParentId { get; set; }
    public string? ReplyToAuthor { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public bool CanDelete { get; set; }
}
