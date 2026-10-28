using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.LingMai;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Services.Notify;

namespace TaiChuWeb_V2.Services.LingMai;

public sealed class NoteCommentRequestException(int status, string message) : Exception(message)
{
    public int Status { get; } = status;
}

// 博客与帖子的评论区：与画廊评论同源（同一张 comments 表），
// 只是把评论锚定到发布内容的源草稿 Note（NoteId），而不是 Artwork。
public class NoteCommentService(AppDbContext context)
{
    // 将公开阅读时拿到的 PublishedNote.Id 解析回源草稿 Note.Id。
    // 博客 / 帖子一定由草稿发布而来（OriginalNoteId 必填），只有它们才开放评论。
    private async Task<Guid> ResolveTargetNoteAsync(Guid publishedId)
    {
        var published = await context.PublishedNotes.AsNoTracking()
            .FirstOrDefaultAsync(pn => pn.Id == publishedId);
        if (published == null || published.OriginalNoteId == null)
            throw new NoteCommentRequestException(404, "内容不存在或尚未公开");

        var note = await context.Notes.AsNoTracking()
            .FirstOrDefaultAsync(n => n.Id == published.OriginalNoteId);
        if (note == null || !note.IsPublic || note.Status == 2)
            throw new NoteCommentRequestException(404, "内容不存在或尚未公开");

        return note.Id;
    }

    public IQueryable<NoteCommentDto> Comments(Guid noteId, Guid? viewer)
    {
        var userId = viewer.HasValue ? viewer.Value.ToString() : "";
        return from c in context.Comments
               where c.NoteId == noteId
               join u in context.Users on EF.Functions.Collate(c.UserId, "utf8mb4_general_ci") equals EF.Functions.Collate(u.Id.ToString(), "utf8mb4_general_ci") into authors
               from u in authors.DefaultIfEmpty()
               join p in context.Comments.Where(parent => parent.NoteId == noteId) on EF.Functions.Collate(c.ParentId.ToString(), "utf8mb4_general_ci") equals EF.Functions.Collate(p.Id.ToString(), "utf8mb4_general_ci") into parents
               from p in parents.DefaultIfEmpty()
               join author in context.Users on EF.Functions.Collate(p != null ? p.UserId : "", "utf8mb4_general_ci") equals EF.Functions.Collate(author.Id.ToString(), "utf8mb4_general_ci") into replyAuthors
               from author in replyAuthors.DefaultIfEmpty()
               select new NoteCommentDto
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

    public async Task<object> GetCommentsAsync(Guid publishedId, Guid? viewer, int offset, int limit)
    {
        var noteId = await ResolveTargetNoteAsync(publishedId);
        offset = Math.Max(0, offset);
        limit = Math.Clamp(limit, 1, 50);
        var query = Comments(noteId, viewer);
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).Skip(offset).Take(limit).ToListAsync();
        return new { items, total, hasMore = offset + items.Count < total };
    }

    public async Task<object> AddCommentAsync(Guid publishedId, Guid userId, CreateNoteCommentDto dto)
    {
        var content = dto.Content?.Trim() ?? "";
        if (content.Length is < 1 or > 1000) throw new NoteCommentRequestException(400, "评论需为 1 至 1000 个字符");
        var noteId = await ResolveTargetNoteAsync(publishedId);
        if (dto.ParentId.HasValue && !await context.Comments.AnyAsync(c => c.Id == dto.ParentId && c.NoteId == noteId && c.Content != ""))
            throw new NoteCommentRequestException(400, "回复的评论不存在、已删除或不属于当前内容");

        var comment = new Comment { Id = Guid.NewGuid(), NoteId = noteId, UserId = userId.ToString(), Content = content, ParentId = dto.ParentId };
        context.Comments.Add(comment);

        // 站内通知：回复 → 通知被回复者；评论 → 通知内容作者
        var notifier = new NotificationService(context);
        if (dto.ParentId.HasValue)
        {
            var parentUserId = await context.Comments.Where(c => c.Id == dto.ParentId).Select(c => c.UserId).FirstOrDefaultAsync();
            if (Guid.TryParse(parentUserId, out var parentId))
                notifier.Add(parentId, userId, "reply", "note", publishedId.ToString(), "回复了你的评论", $"/stream?post={publishedId}");
        }
        else
        {
            var ownerId = await context.Notes.Where(n => n.Id == noteId).Select(n => n.AuthorId).FirstOrDefaultAsync();
            if (Guid.TryParse(ownerId, out var authorId))
                notifier.Add(authorId, userId, "comment", "note", publishedId.ToString(), "评论了你的帖子", $"/stream?post={publishedId}");
        }

        await context.SaveChangesAsync();
        var count = await context.Comments.CountAsync(c => c.NoteId == noteId && c.Content != "");
        return new { id = comment.Id, commentsCount = count };
    }

    public async Task<object> DeleteCommentAsync(Guid publishedId, Guid commentId, Guid userId)
    {
        var noteId = await ResolveTargetNoteAsync(publishedId);
        var comment = await context.Comments.FirstOrDefaultAsync(c => c.Id == commentId && c.NoteId == noteId);
        if (comment == null) throw new NoteCommentRequestException(404, "评论不存在");
        if (comment.UserId != userId.ToString()) throw new NoteCommentRequestException(403, "只能删除自己的评论");
        // 保留回复关系，仅清空正文，避免删除自己评论时连带删除他人回复。
        comment.Content = "";
        await context.SaveChangesAsync();
        var count = await context.Comments.CountAsync(c => c.NoteId == noteId && c.Content != "");
        return new { commentsCount = count };
    }
}
