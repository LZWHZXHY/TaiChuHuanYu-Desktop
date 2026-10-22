using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.LingMai;

namespace TaiChuWeb_V2.Services.Publish;

// 博客和短动态处理器负责重试及事务；这里统一写入发布快照。
internal static class PublishedNoteStore
{
    public static async Task<Guid> SaveAsync(AppDbContext context, Note note, IEnumerable<Block> blocks,
        string userId, string type, string? title, string excerpt, string? extraData)
    {
        var published = await context.PublishedNotes.FirstOrDefaultAsync(p => p.OriginalNoteId == note.Id);
        if (published == null)
        {
            Guid.TryParse(userId, out var authorId);
            var author = await context.Users.FirstOrDefaultAsync(u => u.Id == authorId);
            published = new PublishedNote
            {
                Id = Guid.NewGuid(), OriginalNoteId = note.Id, SpaceId = note.SpaceId,
                Type = type, AuthorName = author?.Username ?? "匿名隐士", Resonance = 0
            };
            context.PublishedNotes.Add(published);
        }

        published.Title = title;
        published.Tags = note.Tags;
        published.Excerpt = excerpt;
        published.ExtraData = extraData;
        published.PublishedAt = DateTime.UtcNow;

        var ownerId = published.Id.ToString();
        var previousBlocks = await context.PublishedBlocks.Where(b => b.OwnerId == ownerId).ToListAsync();
        context.PublishedBlocks.RemoveRange(previousBlocks);
        foreach (var block in blocks)
        {
            Guid.TryParse(block.Id, out var blockId);
            context.PublishedBlocks.Add(new PublishedBlock
            {
                Id = blockId != Guid.Empty ? blockId : Guid.NewGuid(), OwnerId = ownerId,
                OwnerType = type, Type = block.Type, Data = block.Data, SortOrder = block.SortOrder
            });
        }

        note.IsPublic = true;
        note.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return published.Id;
    }
}
