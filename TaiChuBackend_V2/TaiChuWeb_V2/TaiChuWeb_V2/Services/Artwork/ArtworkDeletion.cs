using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Cos;
using TaiChuWeb_V2.Services.Notify;

namespace TaiChuWeb_V2.Services.Artworks;

/// <summary>
/// 彻底删除一件画廊作品（作者自己删，或管理员删）。删除后无法恢复：
/// · 作品、图片记录、评论（含回复）、点赞 / 收藏、标签、关于这件作品的通知一起删除；
/// · 还没处理的举报自动标记为「已处理：作品已删除」；
/// · 灵脉原稿保留（只是解除和作品的关联、改回不公开），作者以后还能重新发布；
/// · 服务器上传的图片文件（COS 的 artwork/ 目录下、没有别的作品在用的）一并删除；从灵脉原稿发布的图片还在原稿里用着，不删。
/// 年度最佳里提名过这件作品的条目会自动显示为「作品已删除」（那边按编号查不到就不显示作品信息）。
/// </summary>
public static class ArtworkDeletion
{
    public sealed record Result(string Title, Guid UploaderId);

    public static async Task<Result?> PurgeAsync(AppDbContext db, CosService cos, ILogger logger, int id, Guid operatorId, bool byAdmin)
    {
        var artwork = await db.Artworks.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (artwork == null) return null;
        var target = id.ToString();
        var imageUrls = await db.ArtworkImages.AsNoTracking().Where(i => i.ArtworkId == id).Select(i => i.ImageUrl).ToListAsync();

        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync();

            // 灵脉原稿：解除关联（外键是 Restrict，不解开删不掉作品），改回不公开
            var notes = await db.Notes.Where(n => n.TargetId == id || (artwork.OriginalNoteId != null && n.Id == artwork.OriginalNoteId)).ToListAsync();
            foreach (var n in notes)
            {
                if (n.TargetId == id) n.TargetId = null;
                if (n.Id == artwork.OriginalNoteId) n.IsPublic = false;
                n.UpdatedAt = DateTime.UtcNow;
            }
            await db.SaveChangesAsync();

            // 评论：先断开回复关系（回复之间是 Restrict 外键），再整批删除
            await db.Comments.Where(c => c.ArtworkId == id && c.ParentId != null).ExecuteUpdateAsync(s => s.SetProperty(c => c.ParentId, (Guid?)null));
            await db.Comments.Where(c => c.ArtworkId == id).ExecuteDeleteAsync();
            await db.UserInteractions.Where(i => i.TargetType == "Artwork" && i.TargetId == target).ExecuteDeleteAsync();
            await db.TagAssignments.Where(t => t.EntityType == "Artwork" && t.EntityId == target).ExecuteDeleteAsync();
            await db.Notifications.Where(n => n.TargetType == "artwork" && n.TargetId == target).ExecuteDeleteAsync();
            await db.ContentReports.Where(r => r.TargetType == "artwork" && r.TargetId == target && r.Status == "pending")
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, "resolved").SetProperty(r => r.HandleNote, "作品已被删除")
                    .SetProperty(r => r.HandledBy, (Guid?)operatorId).SetProperty(r => r.HandledAt, (DateTime?)DateTime.UtcNow));
            await db.ArtworkImages.Where(i => i.ArtworkId == id).ExecuteDeleteAsync();
            await db.Artworks.Where(a => a.Id == id).ExecuteDeleteAsync();

            // 管理员删了别人的作品：告诉作者一声
            if (byAdmin && artwork.UploaderId != operatorId)
            {
                new NotificationService(db).Add(artwork.UploaderId, null, "system", "system", "", $"你的画廊作品《{artwork.Title}》已被管理员删除", null);
                await db.SaveChangesAsync();
            }
            await tx.CommitAsync();
        });

        // 图片文件：只删服务器自己上传到 artwork/ 目录、且没有其他作品在用的（失败只记日志，不影响删除结果）
        var prefix = cos.ObjectUrl("artwork/");
        var mine = imageUrls.Where(u => u.StartsWith(prefix, StringComparison.Ordinal)).Distinct().ToList();
        if (mine.Count > 0)
        {
            var stillUsed = await db.ArtworkImages.AsNoTracking().Where(i => mine.Contains(i.ImageUrl)).Select(i => i.ImageUrl).ToListAsync();
            foreach (var url in mine.Except(stillUsed))
            {
                var key = url[(prefix.Length - "artwork/".Length)..];
                try { await cos.DeleteAsync(key); }
                catch (Exception ex) { logger.LogWarning(ex, "删除作品图片文件失败，保留待核查：{Key}", key); }
            }
        }
        return new Result(artwork.Title, artwork.UploaderId);
    }
}
