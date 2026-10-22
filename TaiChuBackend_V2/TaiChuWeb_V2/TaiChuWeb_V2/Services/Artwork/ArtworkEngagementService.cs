using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Artwork;
using TaiChuWeb_V2.Models.Interact;
using TaiChuWeb_V2.Models.LingMai;

namespace TaiChuWeb_V2.Services.Artworks;

public sealed class ArtworkRequestException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

public class ArtworkEngagementService(AppDbContext context)
{
    // 记录是统计的依据，避免旧的“收藏增加点赞数”错误继续影响列表和详情。
    public IQueryable<ArtworkStatsResult> Stats(Guid? userId) => context.Artworks.Select(a => new ArtworkStatsResult
    {
        Id = a.Id,
        LikesCount = context.UserInteractions.Where(i => i.TargetType == "Artwork" && i.TargetId == a.Id.ToString() && i.ActionType == "Like").Select(i => i.UserId).Distinct().Count(),
        FavoritesCount = context.UserInteractions.Where(i => i.TargetType == "Artwork" && i.TargetId == a.Id.ToString() && i.ActionType == "Favorite").Select(i => i.UserId).Distinct().Count(),
        CommentsCount = context.Comments.Count(c => c.ArtworkId == a.Id && c.Content != ""),
        IsLiked = userId.HasValue && context.UserInteractions.Any(i => i.TargetType == "Artwork" && i.TargetId == a.Id.ToString() && i.ActionType == "Like" && i.UserId == userId),
        IsFavorited = userId.HasValue && context.UserInteractions.Any(i => i.TargetType == "Artwork" && i.TargetId == a.Id.ToString() && i.ActionType == "Favorite" && i.UserId == userId)
    });

    public async Task<object> SetInteractionAsync(int id, Guid userId, string action, bool? desired)
    {
        if (action is not ("Like" or "Favorite")) throw new ArtworkRequestException(400, "不支持的互动类型");
        return await WriteAsync(id, userId, async () =>
        {
            var target = id.ToString();
            var existing = await context.UserInteractions.Where(i => i.UserId == userId && i.TargetType == "Artwork" && i.TargetId == target && i.ActionType == action).ToListAsync();
            var active = desired ?? existing.Count == 0;
            if (!active) context.UserInteractions.RemoveRange(existing);
            else if (existing.Count == 0) context.UserInteractions.Add(new UserInteraction { UserId = userId, TargetId = target, TargetType = "Artwork", ActionType = action });
            else if (existing.Count > 1) context.UserInteractions.RemoveRange(existing.Skip(1));
            return active;
        }, (active, stats) => new { isActive = active, newCount = action == "Like" ? stats.LikesCount : stats.FavoritesCount, stats });
    }

    public IQueryable<ArtworkCommentDto> Comments(int id, Guid? viewer)
    {
        var userId = viewer.HasValue ? viewer.Value.ToString() : "";
        return from c in context.Comments
               where c.ArtworkId == id
               join u in context.Users on EF.Functions.Collate(c.UserId, "utf8mb4_general_ci") equals EF.Functions.Collate(u.Id.ToString(), "utf8mb4_general_ci") into authors
               from u in authors.DefaultIfEmpty()
               join p in context.Comments.Where(parent => parent.ArtworkId == id) on EF.Functions.Collate(c.ParentId.ToString(), "utf8mb4_general_ci") equals EF.Functions.Collate(p.Id.ToString(), "utf8mb4_general_ci") into parents
               from p in parents.DefaultIfEmpty()
               join author in context.Users on EF.Functions.Collate(p != null ? p.UserId : "", "utf8mb4_general_ci") equals EF.Functions.Collate(author.Id.ToString(), "utf8mb4_general_ci") into replyAuthors
               from author in replyAuthors.DefaultIfEmpty()
               select new ArtworkCommentDto
               {
                   Id = c.Id, UserId = c.Content == "" ? "" : c.UserId,
                   AuthorName = c.Content == "" ? "已删除评论" : u != null ? u.Username : "已注销用户",
                   Avatar = c.Content != "" && u != null && u.Profile != null ? u.Profile!.Avatar : null,
                   Content = c.Content, ParentId = c.ParentId,
                   ReplyToAuthor = p == null ? null : p.Content == "" ? "已删除评论" : author != null ? author.Username : "已注销用户",
                   CreatedAt = c.CreatedAt, IsDeleted = c.Content == "",
                   CanDelete = c.Content != "" && c.UserId == userId
               };
    }

    public async Task<object> GetCommentsAsync(int id, Guid? viewer, int offset, int limit)
    {
        if (!await context.Artworks.AnyAsync(a => a.Id == id && a.IsApproved && (a.Status == "published" || a.Status == "" || a.Status == null))) throw new ArtworkRequestException(404, "作品不存在或尚未公开");
        offset = Math.Max(0, offset);
        limit = Math.Clamp(limit, 1, 50);
        var query = Comments(id, viewer);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).Skip(offset).Take(limit).ToListAsync();
        return new { items, total, hasMore = offset + items.Count < total };
    }

    public async Task<object> AddCommentAsync(int id, Guid userId, CreateArtworkCommentDto dto)
    {
        var content = dto.Content?.Trim() ?? "";
        if (content.Length is < 1 or > 1000) throw new ArtworkRequestException(400, "评论需为 1 至 1000 个字符");
        var commentId = Guid.NewGuid();
        return await WriteAsync(id, userId, async () =>
        {
            if (await context.Comments.AnyAsync(c => c.Id == commentId)) return commentId;
            if (dto.ParentId.HasValue && !await context.Comments.AnyAsync(c => c.Id == dto.ParentId && c.ArtworkId == id && c.Content != ""))
                throw new ArtworkRequestException(400, "回复的评论不存在、已删除或不属于当前作品");
            var comment = new Comment { Id = commentId, ArtworkId = id, UserId = userId.ToString(), Content = content, ParentId = dto.ParentId };
            context.Comments.Add(comment);
            return comment.Id;
        }, (commentId, stats) => new { id = commentId, stats });
    }

    public Task<object> DeleteCommentAsync(int id, Guid commentId, Guid userId) => WriteAsync(id, userId, async () =>
    {
        var comment = await context.Comments.FirstOrDefaultAsync(c => c.Id == commentId && c.ArtworkId == id);
        if (comment == null) throw new ArtworkRequestException(404, "评论不存在");
        if (comment.UserId != userId.ToString()) throw new ArtworkRequestException(403, "只能删除自己的评论");
        // 保留回复关系，避免删除自己的评论时连带删除其他人的回复。
        comment.Content = "";
        return true;
    }, (_, stats) => new { stats });

    private Task<object> WriteAsync<T>(int id, Guid userId, Func<Task<T>> change, Func<T, ArtworkStatsResult, object> result)
        => context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            using var transaction = await context.Database.BeginTransactionAsync();
            // 先取得作品行的写锁，同一作品的记录与计数变更在数据库事务中串行完成。
            var found = await context.Artworks.Where(a => a.Id == id && a.IsApproved && (a.Status == "published" || a.Status == "" || a.Status == null))
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.LikesCount, a => a.LikesCount));
            if (found == 0) throw new ArtworkRequestException(404, "作品不存在或尚未公开");
            var value = await change();
            await context.SaveChangesAsync();
            var stats = await Stats(userId).SingleAsync(a => a.Id == id);
            await context.Artworks.Where(a => a.Id == id).ExecuteUpdateAsync(s => s
                .SetProperty(a => a.LikesCount, stats.LikesCount)
                .SetProperty(a => a.FavoritesCount, stats.FavoritesCount)
                .SetProperty(a => a.CommentsCount, stats.CommentsCount));
            await transaction.CommitAsync();
            return result(value, stats);
        });
}
