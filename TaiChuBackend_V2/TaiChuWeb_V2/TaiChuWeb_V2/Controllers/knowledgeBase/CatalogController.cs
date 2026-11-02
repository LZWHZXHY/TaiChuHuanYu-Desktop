using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Personalization;

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
public class CatalogController(AppDbContext db, SponsorPerkService perks) : ControllerBase
{
    /// <param name="includeDevlog">开发进度只给管理员和赞助会员（DevProgressAccess），其他人知识库里不出现这一类</param>
    public static IQueryable<CatalogItem> Query(AppDbContext db, string type, bool includeSettings = false, bool includeDevlog = true)
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
            // 联动活动（草稿不算）、开发进度、必吃榜（收录且显示的）、公告（发布中的，含已过期的历史公告）、版本更新（公开的）、使用手册（已发布的）
            // 后四种的 Summary 先放全文（搜索时整篇都能搜到），返回前再截成摘要
            ["joint"] = db.JointActivities.Where(a => a.Status != "draft")
                .Select(a => new CatalogItem { Id = a.Id.ToString(), Type = "joint", Title = a.Title, Summary = a.Description,
                    Author = a.OrganizerName, Tags = "", CoverUrl = a.CoverUrl, UpdatedAt = a.UpdatedAt ?? a.CreatedAt }),
            ["devlog"] = db.DevProjects
                .Select(p => new CatalogItem { Id = p.Id.ToString(), Type = "devlog", Title = p.Name, Summary = p.Description ?? "",
                    Author = "太初官方", Tags = p.Category, CoverUrl = null, UpdatedAt = p.UpdatedAt }),
            ["musteat"] = db.MustEatItems.Where(m => m.IsVisible && m.Status == "approved")
                .Select(m => new CatalogItem { Id = m.Id.ToString(), Type = "musteat", Title = m.Title, Summary = m.Description ?? "",
                    Author = m.Creator ?? "", Tags = m.Category, CoverUrl = m.CoverImage, UpdatedAt = m.CreatedAt }),
            ["news"] = db.News.Where(n => n.IsPublished)
                .Select(n => new CatalogItem { Id = n.Id, Type = "news", Title = n.Title, Summary = n.Content ?? "",
                    Author = "太初官方", Tags = n.Type, CoverUrl = n.ImageUrl, UpdatedAt = n.CreatedAt }),
            ["release"] = db.Releases.Where(r => r.IsPublished)
                .Select(r => new CatalogItem { Id = r.Id.ToString(), Type = "release", Title = "v" + r.Version + " " + r.Title, Summary = r.Content ?? "",
                    Author = "太初官方", Tags = "", CoverUrl = null, UpdatedAt = r.ReleasedAt }),
            ["manual"] = db.ManualArticles.Where(a => a.IsPublished)
                .Select(a => new CatalogItem { Id = a.Id.ToString(), Type = "manual", Title = a.Title, Summary = a.Content,
                    Author = "太初官方", Tags = a.Category != null ? a.Category.Name : "", CoverUrl = null, UpdatedAt = a.UpdatedAt }),
            ["document"] = db.FinalNodes.Where(n => n.Status == 0)
                .Select(n => new CatalogItem { Id = n.Id, Type = "document", Title = n.Title, Summary = n.Excerpt ?? "",
                    Author = n.CreatorName, Tags = n.Tags, CoverUrl = n.CoverImage, UpdatedAt = n.UpdatedAt })
        };
        if (!includeDevlog) sources.Remove("devlog");
        if (type != "all") return sources[type];
        // 「全部」默认不混入世界观设定条目（一个世界就有上百条，会把其他资料挤掉）；搜索时才一起找
        var keys = sources.Keys.Where(k => k != "setting" || includeSettings);
        return keys.Select(k => sources[k]).Aggregate((left, right) => left.Concat(right));
    }

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] string type = "all", [FromQuery] string? search = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 24, [FromQuery] int? categoryId = null)
    {
        if (type is not ("all" or "blog" or "post" or "artwork" or "world" or "character" or "wiki" or "document" or "novel" or "work" or "setting"
            or "joint" or "devlog" or "musteat" or "news" or "release" or "manual"))
            return BadRequest(new { message = "不支持的资料类型" });
        if (search?.Length > 100) return BadRequest(new { message = "搜索词不能超过 100 字" });
        pageSize = Math.Clamp(pageSize, 1, 50);
        page = Math.Clamp(page, 1, int.MaxValue / pageSize);
        // 开发进度：没有权限的人这一类是空的（locked = true，前端提示「赞助会员专享」）
        var devlogOk = type is "all" or "devlog" && (await DevProgressAccess.CheckAsync(db, perks, User)).CanView;
        if (type == "devlog" && !devlogOk)
            return Ok(new { items = Array.Empty<CatalogItem>(), total = 0, page, pageSize, locked = true });
        var query = Query(db, type, includeSettings: !string.IsNullOrWhiteSpace(search), includeDevlog: devlogOk).AsNoTracking();
        // 保留原百科选择父分类时包含直属子分类的浏览范围。
        if (categoryId.HasValue) query = query.Where(i => i.Type == "wiki" && db.WikiArticles.Any(a => a.Id == i.Id &&
            (a.CategoryId == categoryId || db.WikiCategories.Any(c => c.Id == a.CategoryId && c.ParentId == categoryId))));
        var terms = KnowledgeSearch.Terms(search);
        // 匹配规则收在 KnowledgeSearch.FilterAsync 里，关系图谱的搜索也用它，两边搜出来的内容一致
        query = await KnowledgeSearch.FilterAsync(db, query, terms);
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
        // 公告、版本更新、手册、必吃榜、联动活动的摘要是全文：去掉 Markdown 记号，截成一小段（搜索时截关键词附近那段）
        foreach (var item in items.Where(i => i.Type is "news" or "release" or "manual" or "musteat" or "joint"))
        {
            var plain = PlainMarkdown(item.Summary);
            item.Summary = (firstTerm != null ? Snip(plain, firstTerm, 30, 110) : null) ?? (plain.Length > 140 ? plain[..140] + "…" : plain);
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

    /// <summary>Markdown / 纯文本 → 一行纯文字（图片去掉，链接留文字）</summary>
    private static string PlainMarkdown(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var t = System.Text.RegularExpressions.Regex.Replace(text, @"!\[[^\]]*\]\([^)]*\)", "");
        t = System.Text.RegularExpressions.Regex.Replace(t, @"\[([^\]]*)\]\([^)]*\)", "$1");
        t = System.Text.RegularExpressions.Regex.Replace(t, @"(^|\n)\s*(#{1,6}|[-*+>]|\d+\.)\s+", "$1");
        t = System.Text.RegularExpressions.Regex.Replace(t, @"[*_`~]{1,3}", "");
        return System.Text.RegularExpressions.Regex.Replace(t, @"\s+", " ").Trim();
    }

    public sealed class TextDetail
    {
        public string Type { get; set; } = "";
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        /// <summary>markdown（使用手册）或 text（其余：原样换行）</summary>
        public string Format { get; set; } = "text";
        public string Content { get; set; } = "";
        public string? Meta { get; set; }
        public string? CoverUrl { get; set; }
        public DateTime UpdatedAt { get; set; }
        /// <summary>站内原页面</summary>
        public string? Link { get; set; }
        public string? LinkText { get; set; }
        /// <summary>站外链接（必吃榜条目的官网 / 商店页）</summary>
        public string? ExternalUrl { get; set; }
    }

    /// <summary>
    /// 知识库里直接阅读公告、版本更新、使用手册、必吃榜条目的全文（只给公开的，和列表的可见规则一样）。
    /// 先只按 ID 把原始字段读出来，标题拼接、链接等在内存里做：原来在 SQL 里拼字符串（"v" + 版本号、" · " + 作者、"/?news=" + ID），
    /// 遇到表的字符集 / 排序规则和连接不一致时 MySQL 会报错（Illegal mix of collations），前端就只能显示「读取失败」。
    /// 找不到（不存在 / 没公开 / ID 不对）返回 404 + code=not_found；查询出错返回 500，前端据此给不同的提示。
    /// </summary>
    [HttpGet("text/{type}/{id}")]
    public async Task<IActionResult> Text(string type, string id)
    {
        id = (id ?? "").Trim();
        TextDetail? detail = null;
        try
        {
            switch (type)
            {
                case "news":
                    var news = await db.News.AsNoTracking().Where(n => n.Id == id && n.IsPublished)
                        .Select(n => new { n.Id, n.Title, n.Content, n.Type, n.ImageUrl, n.CreatedAt }).FirstOrDefaultAsync();
                    if (news != null) detail = new TextDetail
                    {
                        Type = "news", Id = news.Id, Title = news.Title, Content = news.Content ?? "", Meta = news.Type, CoverUrl = news.ImageUrl, UpdatedAt = news.CreatedAt,
                        Link = "/?news=" + Uri.EscapeDataString(news.Id), LinkText = "在首页打开这条公告",
                    };
                    break;
                case "release" when int.TryParse(id, out var rid):
                    var release = await db.Releases.AsNoTracking().Where(r => r.Id == rid && r.IsPublished)
                        .Select(r => new { r.Id, r.Version, r.Title, r.Content, r.ReleasedAt }).FirstOrDefaultAsync();
                    if (release != null) detail = new TextDetail
                    {
                        Type = "release", Id = release.Id.ToString(), Title = "v" + release.Version + (string.IsNullOrEmpty(release.Title) ? "" : " · " + release.Title),
                        Content = release.Content ?? "", UpdatedAt = release.ReleasedAt, Link = "/changelog?release=" + release.Id, LinkText = "在版本更新页查看",
                    };
                    break;
                case "manual" when int.TryParse(id, out var mid):
                    var manual = await db.ManualArticles.AsNoTracking().Where(a => a.Id == mid && a.IsPublished)
                        .Select(a => new { a.Id, a.Title, a.Content, Category = a.Category != null ? a.Category.Name : null, a.UpdatedAt }).FirstOrDefaultAsync();
                    if (manual != null) detail = new TextDetail
                    {
                        Type = "manual", Id = manual.Id.ToString(), Title = manual.Title, Format = "markdown", Content = manual.Content, Meta = manual.Category, UpdatedAt = manual.UpdatedAt,
                    };
                    break;
                case "musteat" when int.TryParse(id, out var eid):
                    var eat = await db.MustEatItems.AsNoTracking().Where(m => m.Id == eid && m.IsVisible && m.Status == "approved")
                        .Select(m => new { m.Id, m.Title, m.Description, m.Category, m.Creator, m.CoverImage, m.CreatedAt, m.Link }).FirstOrDefaultAsync();
                    if (eat != null) detail = new TextDetail
                    {
                        Type = "musteat", Id = eat.Id.ToString(), Title = eat.Title, Content = eat.Description ?? "",
                        Meta = eat.Category + (string.IsNullOrEmpty(eat.Creator) ? "" : " · " + eat.Creator),
                        CoverUrl = eat.CoverImage, UpdatedAt = eat.CreatedAt, Link = "/must-eat", LinkText = "去必吃榜", ExternalUrl = eat.Link,
                    };
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            HttpContext.RequestServices.GetService<ILogger<CatalogController>>()?.LogError(ex, "读取知识库全文失败 {Type} {Id}", type, id);
            return StatusCode(500, new { message = "读取失败，请稍后重试", code = "error" });
        }
        if (detail == null) return NotFound(new { message = "这条资料不存在或已下线", code = "not_found" });
        // 只放行 http(s) 外链
        if (detail.ExternalUrl != null && !(detail.ExternalUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || detail.ExternalUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase)))
            detail.ExternalUrl = null;
        return Ok(detail);
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
