using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers.knowledgeBase;

/// <summary>知识库全文检索的公共部分：列表搜索和关系图谱搜索共用</summary>
public static class KnowledgeSearch
{
    /// <summary>把搜索框内容拆成关键词（空格分开，最多 10 个，转小写）</summary>
    public static List<string> Terms(string? search) =>
        (search ?? "").Trim().ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(10).ToList();

    /// <summary>旧数据把中文存成 JSON 的 Unicode 转义，检索时两种写法都要找</summary>
    public static string Encoded(string term) => JsonSerializer.Serialize(term)[1..^1].ToLowerInvariant();

    public const int BodyHitLimit = 2000;

    /// <summary>正文里包含关键词的内容 ID（和 CatalogItem.Id 同一格式）</summary>
    public static async Task<List<string>> BodyHitsAsync(AppDbContext db, string term, string encoded)
    {
        var hits = new List<string>();
        // 博客、帖子、百科的正文都在 PublishedBlocks，OwnerId 就是发布 ID / 词条 ID
        hits.AddRange(await db.PublishedBlocks.AsNoTracking()
            .Where(b => b.Data != null && (b.Data.Contains(term) || b.Data.Contains(encoded)))
            .Select(b => b.OwnerId).Distinct().Take(BodyHitLimit).ToListAsync());
        hits.AddRange((await db.WorldNovelChapters.AsNoTracking().Where(c => c.Content.Contains(term))
            .Select(c => c.NovelId).Distinct().Take(BodyHitLimit).ToListAsync()).Select(id => id.ToString()));
        hits.AddRange(await db.FinalNodeContents.AsNoTracking()
            .Where(c => c.BlocksData.Contains(term) || c.BlocksData.Contains(encoded))
            .Select(c => c.NodeId).Distinct().Take(BodyHitLimit).ToListAsync());
        hits.AddRange((await db.WorldCards.AsNoTracking()
            .Where(c => c.Project != null && c.Project.IsPublic && (c.Attributes.Contains(term) || c.Attributes.Contains(encoded)
                || c.Aliases.Contains(term) || c.Aliases.Contains(encoded) || c.Tags.Contains(term) || c.Tags.Contains(encoded)
                || c.TimelineEvents.Contains(term) || c.TimelineEvents.Contains(encoded)
                || (c.Layout != null && (c.Layout.Contains(term) || c.Layout.Contains(encoded)))))
            .Select(c => new { c.ProjectId, c.Id }).Take(BodyHitLimit).ToListAsync()).Select(c => $"{c.ProjectId}:{c.Id}"));
        return hits.Select(h => h.ToLowerInvariant()).Distinct().ToList();
    }

}
