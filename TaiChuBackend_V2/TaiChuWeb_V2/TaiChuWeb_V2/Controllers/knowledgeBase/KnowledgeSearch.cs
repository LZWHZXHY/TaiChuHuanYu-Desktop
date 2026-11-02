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
        // 开发进度的详细内容（列表里只放了简介）
        hits.AddRange((await db.DevProjects.AsNoTracking().Where(p => p.Content != null && p.Content.Contains(term))
            .Select(p => p.Id).Take(BodyHitLimit).ToListAsync()).Select(id => id.ToString()));
        return hits.Select(h => h.ToLowerInvariant()).Distinct().ToList();
    }

    /// <summary>
    /// 知识库搜索的匹配规则（列表 catalog 和关系图谱搜索共用这一处，保证同一个关键词两边搜出来一样）：
    /// · 关键词用空格分开（Terms），每个词都要出现（多个词是「并且」），不分大小写；
    /// · 一个词出现在这些地方之一就算：标题、简介（含旧数据的 Unicode 转义写法）、标签、作者名、画廊作品的标签、正文（BodyHitsAsync）。
    /// </summary>
    public static async Task<IQueryable<CatalogItem>> FilterAsync(AppDbContext db, IQueryable<CatalogItem> query, List<string> terms)
    {
        foreach (var term in terms)
        {
            // 旧博客摘要将中文保存为 JSON 的 Unicode 转义，同时兼容原文与转义检索。
            var encoded = Encoded(term);
            // 正文里出现也算：博客 / 帖子 / 百科的正文、小说章节、图谱资料正文、世界观条目的属性 / 别名 / 标签 / 时间线
            var bodyHits = await BodyHitsAsync(db, term, encoded);
            query = query.Where(i => i.Title.ToLower().Contains(term) || i.Summary.ToLower().Contains(term) || i.Summary.ToLower().Contains(encoded) || i.Tags.ToLower().Contains(term) || i.Author.ToLower().Contains(term) || (i.Type == "artwork" && db.TagAssignments.Any(t => t.EntityType == "Artwork" && t.EntityId == i.Id && t.Tag != null && t.Tag.Name.ToLower().Contains(term))) || bodyHits.Contains(i.Id));
        }
        return query;
    }

    /// <summary>
    /// 列表条目（类型, ID）→ 关系图谱节点 ID（和 GraphController.GetPublicGraph 生成节点的规则一致）。
    /// 图谱里没有这一类节点（设定条目、联动、开发进度、必吃榜、公告、版本更新、使用手册）时返回 null。
    /// </summary>
    public static string? GraphNodeId(string type, string id) => type switch
    {
        "blog" or "post" or "artwork" or "world" or "wiki" or "novel" or "work" => $"{type}:{id}",
        "character" => $"oc:{id}",
        "document" => id,
        _ => null,
    };

}
