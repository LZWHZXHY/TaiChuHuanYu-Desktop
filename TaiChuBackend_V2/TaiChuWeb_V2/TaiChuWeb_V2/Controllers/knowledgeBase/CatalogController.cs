using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers.knowledgeBase;

public sealed class CatalogItem
{
    public string Id { get; set; } = "";
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public string Author { get; set; } = "";
    public string Tags { get; set; } = "";
    public string? CoverUrl { get; set; }
    public DateTime UpdatedAt { get; set; }
}

// 仅聚合公开来源，不建立副本或另一套发布状态。
[ApiController]
[Route("api/knowledge-base/catalog")]
public class CatalogController(AppDbContext db) : ControllerBase
{
    public static IQueryable<CatalogItem> Query(AppDbContext db, string type, bool includeSettings = false)
    {
        var sources = new Dictionary<string, IQueryable<CatalogItem>>
        {
            ["blog"] = db.PublishedNotes.Where(n => (n.Type == "blog" || n.Type == "note") &&
                (n.OriginalNoteId == null || !db.Notes.Any(s => s.Id.ToString() == EF.Functions.Collate(n.OriginalNoteId.ToString(), "utf8mb4_general_ci")) || db.Notes.Any(s => s.Id.ToString() == EF.Functions.Collate(n.OriginalNoteId.ToString(), "utf8mb4_general_ci") && s.IsPublic && s.Status != 2)))
                .Select(n => new CatalogItem { Id = n.Id.ToString(), Type = "blog", Title = n.Title ?? "无标题文章", Summary = n.Excerpt ?? "",
                    Author = n.AuthorName ?? "", Tags = n.Tags ?? "", CoverUrl = null, UpdatedAt = n.PublishedAt }),
            ["post"] = db.PublishedNotes.Where(n => n.Type == "post" &&
                (n.OriginalNoteId == null || !db.Notes.Any(s => s.Id.ToString() == EF.Functions.Collate(n.OriginalNoteId.ToString(), "utf8mb4_general_ci")) || db.Notes.Any(s => s.Id.ToString() == EF.Functions.Collate(n.OriginalNoteId.ToString(), "utf8mb4_general_ci") && s.IsPublic && s.Status != 2)))
                .Select(n => new CatalogItem { Id = n.Id.ToString(), Type = "post", Title = n.Title ?? "短帖", Summary = n.Excerpt ?? "",
                    Author = n.AuthorName ?? "", Tags = n.Tags ?? "", CoverUrl = null, UpdatedAt = n.PublishedAt }),
            ["artwork"] = db.Artworks.Where(a => a.IsApproved && (a.Status == "published" || a.Status == "" || a.Status == null))
                .Select(a => new CatalogItem { Id = a.Id.ToString(), Type = "artwork", Title = a.Title, Summary = a.Description ?? "",
                    Author = a.Uploader.Username ?? "", Tags = "", CoverUrl = a.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault(), UpdatedAt = a.UploadAt }),
            ["world"] = db.WorldProjects.Where(p => p.IsPublic)
                .Select(p => new CatalogItem { Id = p.Id.ToString(), Type = "world", Title = p.Name, Summary = p.Description ?? "",
                    Author = p.Owner != null ? p.Owner.Username ?? "" : "", Tags = "", CoverUrl = p.CoverImage, UpdatedAt = p.UpdatedAt }),
            ["character"] = db.StickmanCharacters.Where(c => c.Status == "published")
                .Select(c => new CatalogItem { Id = c.Id.ToString(), Type = "character", Title = c.Title, Summary = c.Description ?? "",
                    Author = c.AuthorName, Tags = "", CoverUrl = c.CoverUrl, UpdatedAt = c.UpdatedAt ?? c.CreatedAt }),
            ["wiki"] = db.WikiArticles.Where(a => !a.IsDeleted && a.CurrentRevisionId != null)
                .Select(a => new CatalogItem { Id = a.Id, Type = "wiki", Title = a.Title, Summary = a.Excerpt ?? "", Author = "",
                    Tags = a.Tags ?? "", CoverUrl = null, UpdatedAt = a.UpdatedAt }),
            // 世界观小说（公开的）
            ["novel"] = db.WorldNovels.Where(n => n.IsPublic)
                .Select(n => new CatalogItem { Id = n.Id.ToString(), Type = "novel", Title = n.Title, Summary = n.Description ?? "",
                    Author = n.AuthorName, Tags = n.Tags ?? "", CoverUrl = n.CoverUrl, UpdatedAt = n.UpdatedAt }),
            // 柴圈作品（已发布的视频 / 图片作品）
            ["work"] = db.ChaiWorks.Where(w => w.Status == "published")
                .Select(w => new CatalogItem { Id = w.Id.ToString(), Type = "work", Title = w.Title, Summary = w.Description ?? "",
                    Author = w.AuthorName, Tags = "", CoverUrl = w.CoverUrl, UpdatedAt = w.UpdatedAt ?? w.CreatedAt }),
            // 公开世界观里的设定条目（人物、地点、势力……）。Id = 世界ID:条目ID，前端据此跳到条目页
            ["setting"] = db.WorldCards.Where(c => c.Project != null && c.Project.IsPublic)
                .Select(c => new CatalogItem { Id = c.ProjectId.ToString() + ":" + c.Id.ToString(), Type = "setting", Title = c.Title, Summary = c.Description ?? "",
                    Author = c.Project!.Owner != null ? c.Project.Owner.Username ?? "" : "", Tags = "", CoverUrl = c.CoverImage, UpdatedAt = c.UpdatedAt }),
            ["document"] = db.FinalNodes.Where(n => n.Status == 0)
                .Select(n => new CatalogItem { Id = n.Id, Type = "document", Title = n.Title, Summary = n.Excerpt ?? "",
                    Author = n.CreatorName, Tags = n.Tags, CoverUrl = n.CoverImage, UpdatedAt = n.UpdatedAt })
        };
        if (type != "all") return sources[type];
        // 「全部」默认不混入世界观设定条目（一个世界就有上百条，会把其他资料挤掉）；搜索时才一起找
        var keys = sources.Keys.Where(k => k != "setting" || includeSettings);
        return keys.Select(k => sources[k]).Aggregate((left, right) => left.Concat(right));
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string type = "all", [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 24, [FromQuery] int? categoryId = null)
    {
        if (type is not ("all" or "blog" or "post" or "artwork" or "world" or "character" or "wiki" or "document" or "novel" or "work" or "setting"))
            return BadRequest(new { message = "不支持的资料类型" });
        if (search?.Length > 100) return BadRequest(new { message = "搜索词不能超过 100 字" });
        pageSize = Math.Clamp(pageSize, 1, 50);
        page = Math.Clamp(page, 1, int.MaxValue / pageSize);
        var query = Query(db, type, includeSettings: !string.IsNullOrWhiteSpace(search)).AsNoTracking();
        // 保留原百科选择父分类时包含直属子分类的浏览范围。
        if (categoryId.HasValue) query = query.Where(i => i.Type == "wiki" && db.WikiArticles.Any(a => a.Id == i.Id &&
            (a.CategoryId == categoryId || db.WikiCategories.Any(c => c.Id == a.CategoryId && c.ParentId == categoryId))));
        var terms = (search ?? "").Trim().ToLower().Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(10).ToList();
        foreach (var term in terms)
        {
            // 旧博客摘要将中文保存为 JSON 的 Unicode 转义，同时兼容原文与转义检索。
            var encoded = JsonSerializer.Serialize(term)[1..^1].ToLowerInvariant();
            // 正文里出现也算：博客 / 帖子 / 百科的正文、小说章节、图谱资料正文、世界观条目的属性 / 别名 / 标签 / 时间线
            var bodyHits = await KnowledgeSearch.BodyHitsAsync(db, term, encoded);
            query = query.Where(i => i.Title.ToLower().Contains(term) || i.Summary.ToLower().Contains(term) || i.Summary.ToLower().Contains(encoded) || i.Tags.ToLower().Contains(term) || i.Author.ToLower().Contains(term) || (i.Type == "artwork" && db.TagAssignments.Any(t => t.EntityType == "Artwork" && t.EntityId == i.Id && t.Tag != null && t.Tag.Name.ToLower().Contains(term))) || bodyHits.Contains(i.Id));
        }
        var total = await query.CountAsync();
        // 搜索时标题里有关键词的排前面，其余按更新时间
        var firstTerm = terms.FirstOrDefault();
        var ordered = firstTerm == null
            ? query.OrderByDescending(i => i.UpdatedAt)
            : query.OrderByDescending(i => i.Title.ToLower().Contains(firstTerm)).ThenByDescending(i => i.UpdatedAt);
        var items = await ordered.ThenBy(i => i.Type).ThenBy(i => i.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var artworkIds = items.Where(i => i.Type == "artwork").Select(i => i.Id).ToArray();
        var tags = await db.TagAssignments.Where(t => t.EntityType == "Artwork" && Enumerable.Contains(artworkIds, t.EntityId) && t.Tag != null).Select(t => new { t.EntityId, t.Tag!.Name }).ToListAsync();
        foreach (var item in items.Where(i => i.Type == "artwork")) item.Tags = string.Join(",", tags.Where(t => t.EntityId == item.Id).Select(t => t.Name));
        foreach (var item in items.Where(i => i.Type == "blog" && i.Summary.TrimStart().StartsWith('{')))
        {
            try
            {
                using var document = JsonDocument.Parse(item.Summary);
                if (document.RootElement.TryGetProperty("text", out var text) && text.ValueKind == JsonValueKind.String) item.Summary = text.GetString() ?? "";
                if (document.RootElement.TryGetProperty("coverUrl", out var cover) && cover.ValueKind == JsonValueKind.String) item.CoverUrl = cover.GetString();
            }
            catch (JsonException) { /* 历史纯文本保留原值。 */ }
        }
        // 世界观条目的封面是 JSON 数组，取第一张；小说标签是 JSON 数组，转成逗号分隔
        foreach (var item in items)
        {
            if (item.Type == "setting") item.Summary = System.Text.RegularExpressions.Regex.Replace(item.Summary, @"\{CARD:[^}]+\}", "").Trim();
            if (item.Type == "setting" && item.CoverUrl?.TrimStart().StartsWith('[') == true)
            {
                try
                {
                    using var document = JsonDocument.Parse(item.CoverUrl);
                    item.CoverUrl = document.RootElement.ValueKind == JsonValueKind.Array && document.RootElement.GetArrayLength() > 0
                        && document.RootElement[0].ValueKind == JsonValueKind.String ? document.RootElement[0].GetString() : null;
                }
                catch (JsonException) { item.CoverUrl = null; }
            }
            if (item.Tags.TrimStart().StartsWith('['))
            {
                try
                {
                    using var document = JsonDocument.Parse(item.Tags);
                    item.Tags = document.RootElement.ValueKind == JsonValueKind.Array
                        ? string.Join(",", document.RootElement.EnumerateArray().Where(t => t.ValueKind == JsonValueKind.String).Select(t => t.GetString()))
                        : "";
                }
                catch (JsonException) { }
            }
        }
        // 短帖的配图存在 ExtraData.cardCover 里
        var postIds = items.Where(i => i.Type == "post" && Guid.TryParse(i.Id, out _)).Select(i => Guid.Parse(i.Id)).ToArray();
        if (postIds.Length > 0)
        {
            var extras = await db.PublishedNotes.AsNoTracking().Where(n => Enumerable.Contains(postIds, n.Id)).Select(n => new { n.Id, n.ExtraData }).ToListAsync();
            foreach (var item in items.Where(i => i.Type == "post"))
            {
                var extra = extras.FirstOrDefault(e => e.Id.ToString() == item.Id)?.ExtraData;
                if (string.IsNullOrWhiteSpace(extra)) continue;
                try
                {
                    using var document = JsonDocument.Parse(extra);
                    if (document.RootElement.ValueKind == JsonValueKind.Object && document.RootElement.TryGetProperty("cardCover", out var cover) && cover.ValueKind == JsonValueKind.String)
                        item.CoverUrl = cover.GetString();
                }
                catch (JsonException) { }
            }
        }
        // 关键词只出现在正文里的，把摘要换成正文里包含关键词的那一小段，让人知道为什么搜到它
        if (firstTerm != null) await FillSnippetsAsync(items, firstTerm);
        return Ok(new { items, total, page, pageSize });
    }

    private async Task FillSnippetsAsync(List<CatalogItem> items, string term)
    {
        var need = items.Where(i => !i.Title.Contains(term, StringComparison.OrdinalIgnoreCase)
            && !i.Summary.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        if (need.Count == 0) return;
        var blockOwners = need.Where(i => i.Type is "blog" or "post" or "wiki").Select(i => i.Id).ToList();
        var blockTexts = await db.PublishedBlocks.AsNoTracking()
            .Where(b => blockOwners.Contains(b.OwnerId) && b.Data != null)
            .OrderBy(b => b.SortOrder).Select(b => new { b.OwnerId, b.Data }).ToListAsync();
        var docIds = need.Where(i => i.Type == "document").Select(i => i.Id).ToList();
        var docTexts = await db.FinalNodeContents.AsNoTracking()
            .Where(c => docIds.Contains(c.NodeId)).Select(c => new { c.NodeId, c.BlocksData }).ToListAsync();
        var novelIds = need.Where(i => i.Type == "novel" && Guid.TryParse(i.Id, out _)).Select(i => Guid.Parse(i.Id)).ToList();
        var chapters = await db.WorldNovelChapters.AsNoTracking()
            .Where(c => novelIds.Contains(c.NovelId) && c.Content.Contains(term))
            .OrderBy(c => c.Number).Select(c => new { c.NovelId, c.Number, c.Title, c.Content }).ToListAsync();
        var cardIds = need.Where(i => i.Type == "setting").Select(i => i.Id.Split(':').LastOrDefault())
            .Where(id => Guid.TryParse(id, out _)).Select(id => Guid.Parse(id!)).ToList();
        var cards = await db.WorldCards.AsNoTracking().Where(c => cardIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Attributes, c.Aliases, c.Tags, c.TimelineEvents, c.Layout }).ToListAsync();

        foreach (var item in need)
        {
            string? snippet = null;
            switch (item.Type)
            {
                case "blog" or "post" or "wiki":
                    snippet = Snip(string.Join(" ", blockTexts.Where(b => string.Equals(b.OwnerId, item.Id, StringComparison.OrdinalIgnoreCase)).Select(b => PlainText(b.Data))), term);
                    break;
                case "document":
                    snippet = Snip(string.Join(" ", docTexts.Where(d => d.NodeId == item.Id).Select(d => PlainText(d.BlocksData))), term);
                    break;
                case "novel":
                    var chapter = chapters.FirstOrDefault(c => c.NovelId.ToString().Equals(item.Id, StringComparison.OrdinalIgnoreCase));
                    if (chapter != null) snippet = $"第 {chapter.Number} 章「{chapter.Title}」：{Snip(chapter.Content, term)}";
                    break;
                case "setting":
                    var card = cards.FirstOrDefault(c => item.Id.EndsWith(c.Id.ToString(), StringComparison.OrdinalIgnoreCase));
                    if (card != null) snippet = Snip(string.Join(" ", new[] { card.Aliases, card.Attributes, card.Tags, VisibleTimeline(card.TimelineEvents) }.Select(PlainText)
                        .Append(TaiChuWeb_V2.Services.World.WorldLayout.VisibleText(card.Layout))), term);
                    break;
            }
            if (!string.IsNullOrWhiteSpace(snippet)) item.Summary = snippet;
        }
    }

    /// <summary>时间线里去掉「对访客隐藏」的事件（知识库是公开的）</summary>
    private static string VisibleTimeline(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return "";
        try
        {
            var events = JsonSerializer.Deserialize<List<TaiChuWeb_V2.Dtos.World.TimelineEventDto>>(json) ?? new();
            return string.Join(" ", events.Where(e => !e.Hidden).Select(e => $"{e.Date} {e.Era} {e.Title} {e.Description}"));
        }
        catch (JsonException) { return ""; }
    }

    /// <summary>把 JSON（Tiptap 文档、属性列表……）里的文字拼成纯文本；不是 JSON 就原样返回</summary>
    private static string PlainText(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var trimmed = raw.TrimStart();
        if (!trimmed.StartsWith('{') && !trimmed.StartsWith('[') && !trimmed.StartsWith('"')) return raw;
        try
        {
            using var doc = JsonDocument.Parse(raw);
            var parts = new List<string>();
            void Walk(JsonElement e, int depth)
            {
                if (depth > 40) return;
                switch (e.ValueKind)
                {
                    case JsonValueKind.String:
                        var text = e.GetString();
                        // 嵌套的 JSON 字符串（旧数据把积木块存成字符串数组）
                        if (text != null && (text.StartsWith('{') || text.StartsWith('[')) && depth < 3) parts.Add(PlainText(text));
                        else if (!string.IsNullOrWhiteSpace(text) && !text.StartsWith("http", StringComparison.OrdinalIgnoreCase) && text.Length < 5000) parts.Add(text);
                        break;
                    case JsonValueKind.Object:
                        foreach (var prop in e.EnumerateObject())
                            if (prop.Name is not ("type" or "id" or "src" or "href" or "marks" or "cardId" or "cardType")) Walk(prop.Value, depth + 1);
                        break;
                    case JsonValueKind.Array:
                        foreach (var child in e.EnumerateArray()) Walk(child, depth + 1);
                        break;
                }
            }
            Walk(doc.RootElement, 0);
            return string.Join(" ", parts);
        }
        catch (JsonException) { return raw; }
    }

    private static string? Snip(string text, string term, int before = 30, int after = 70)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ");
        var at = text.IndexOf(term, StringComparison.OrdinalIgnoreCase);
        if (at < 0) return null;
        var start = Math.Max(0, at - before);
        var end = Math.Min(text.Length, at + term.Length + after);
        return (start > 0 ? "…" : "") + text[start..end].Trim() + (end < text.Length ? "…" : "");
    }
}
