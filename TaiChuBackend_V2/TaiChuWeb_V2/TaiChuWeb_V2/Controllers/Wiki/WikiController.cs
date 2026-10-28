using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Wiki;
using TaiChuWeb_V2.Models.Wiki;
using TaiChuWeb_V2.Services.Wiki;

namespace TaiChuWeb_V2.Controllers.Wiki;

[ApiController, Route("api/wiki")]
public class WikiController(AppDbContext db) : ControllerBase
{
    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
    private Task<bool> KnownUser() => Guid.TryParse(UserId, out var id) ? db.Users.AnyAsync(u => u.Id == id) : Task.FromResult(false);
    public static bool ValidContent(string? content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 2_000_000) return false;
        try { using var json = JsonDocument.Parse(content); return json.RootElement.ValueKind == JsonValueKind.Object && json.RootElement.TryGetProperty("type", out var type) && type.GetString() == "doc" && json.RootElement.TryGetProperty("content", out var nodes) && nodes.ValueKind == JsonValueKind.Array; }
        catch (Exception e) when (e is JsonException or InvalidOperationException) { return false; }
    }

    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories() => Ok(await db.WikiCategories.OrderBy(c => c.SortOrder).ThenBy(c => c.Id).ToListAsync());

    [HttpGet("articles")]
    public async Task<IActionResult> GetAllArticles() => Ok(await db.WikiArticles.Where(a => !a.IsDeleted && a.CurrentRevisionId != null)
        .Select(a => new { a.Id, a.Title, a.Excerpt, a.CategoryId, a.ViewCount, a.UpdatedAt }).ToListAsync());

    [Authorize, HttpPost("apply-category")]
    public async Task<IActionResult> ApplyCategory([FromBody] CategoryRequestDto request)
    {
        if (!await KnownUser()) return Unauthorized();
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100) return BadRequest(new { message = "分类名称需为 1–100 字" });
        db.WikiCategoryRequests.Add(new WikiCategoryRequest { RequesterId = UserId, CategoryName = request.Name.Trim(), ParentId = request.ParentId, SortOrder = request.SortOrder, Reason = request.Reason, Status = 0, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); return Ok(new { message = "分类申请已提交" });
    }

    // 兼容原来的 NoteId 发布，同时允许直接创建公共知识，不强制先创建笔记。
    [Authorize, HttpPost("publish")]
    public async Task<IActionResult> PublishFromNote([FromBody] WikiPublishDto request)
    {
        if (!await KnownUser()) return Unauthorized();
        var category = await db.WikiCategories.FindAsync(request.CategoryId);
        if (category == null) return BadRequest(new { message = "请选择有效分类" });
        var title = request.Title?.Trim() ?? "";
        var content = request.Content ?? "";
        if (!string.IsNullOrWhiteSpace(request.NoteId))
        {
            if (!Guid.TryParse(request.NoteId, out var noteId)) return BadRequest(new { message = "笔记标识无效" });
            var note = await db.Notes.FindAsync(noteId);
            if (note == null || note.Status == 2) return NotFound();
            if (!await db.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == UserId)) return Forbid();
            if (await db.WikiArticles.AnyAsync(a => a.SourceNoteId == request.NoteId && !a.IsDeleted)) return Conflict(new { message = "该笔记已有词条，请编辑现有词条" });
            title = note.Title;
            var blocks = await db.Blocks.Where(b => b.OwnerId == request.NoteId && b.OwnerType == note.Type).OrderBy(b => b.SortOrder).ToListAsync();
            try {
                content = JsonSerializer.Serialize(new { type = "doc", content = blocks.Select(b => {
                    var data = JsonSerializer.Deserialize<Dictionary<string, object>>(b.Data) ?? new();
                    data["type"] = b.Type; return data;
                }).ToArray() });
            } catch (JsonException) { return BadRequest(new { message = "笔记内容格式异常，请先修复原稿" }); }
        }
        if (string.IsNullOrWhiteSpace(title) || title.Length > 200 || !ValidContent(content) || request.Excerpt?.Length > 150 ||
            request.Tags.Count > 10 || request.Tags.Any(t => t == null || t.Length > 50) || string.Join(",", request.Tags).Length > 200)
            return BadRequest(new { message = "请检查标题、正文、摘要和标签长度" });
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () => {
            db.ChangeTracker.Clear();
            using var transaction = await db.Database.BeginTransactionAsync();
            var article = new WikiArticle { Title = title, CreatorId = UserId, CategoryId = category.Id, Excerpt = request.Excerpt?.Trim(), Tags = string.Join(",", request.Tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct()), SourceNoteId = string.IsNullOrWhiteSpace(request.NoteId) ? null : request.NoteId, IsFromNote = !string.IsNullOrWhiteSpace(request.NoteId) };
            db.WikiArticles.Add(article); await db.SaveChangesAsync();
            var revision = new WikiArticleRevision { ArticleId = article.Id, CategoryId = category.Id, Title = title, Content = content, ContributorId = UserId, EditSummary = "创建词条", Status = category.OwnershipType == 1 && category.OwnerId == UserId ? 1 : 0 };
            db.WikiArticleRevisions.Add(revision); await db.SaveChangesAsync();
            if (revision.Status == 1) { article.CurrentRevisionId = revision.Id; await db.SaveChangesAsync(); }
            await transaction.CommitAsync();
            return Ok(new { articleId = article.Id, status = revision.Status, message = revision.Status == 1 ? "发布成功" : "词条已提交，等待审核后进入知识库" });
        });
    }

    [Authorize, HttpPost("update")]
    public async Task<IActionResult> UpdateFromNote([FromBody] WikiUpdateDto request)
    {
        if (!await KnownUser()) return Unauthorized();
        if (!ValidContent(request.Content) || request.Summary?.Length > 255) return BadRequest(new { message = "正文格式或修改说明无效" });
        var article = await db.WikiArticles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == request.ArticleId && !a.IsDeleted && a.CurrentRevisionId != null);
        if (article == null) return NotFound();
        if (request.BaseRevisionId != article.CurrentRevisionId) return Conflict(new { message = "词条已有新版本，当前编辑内容已保留，请对照最新版本后重新提交" });
        if (!await db.WikiCategories.AnyAsync(c => c.Id == article.CategoryId)) return BadRequest(new { message = "词条分类已失效" });
        var revision = new WikiArticleRevision { ArticleId = article.Id, Title = article.Title, CategoryId = article.CategoryId, Content = request.Content, ContributorId = UserId, EditSummary = request.Summary?.Trim() ?? "协作修改", PreviousRevisionId = article.CurrentRevisionId, Status = 0 };
        db.WikiArticleRevisions.Add(revision); await db.SaveChangesAsync();
        return Ok(new { revisionId = revision.Id, message = "修订已提交，审核通过后公开生效" });
    }

    [HttpGet("article/{id}")]
    public async Task<IActionResult> GetArticleDetail(string id)
    {
        var article = await db.WikiArticles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted);
        if (article == null) return NotFound();
        if (article.CurrentRevisionId == null) return Ok(new { status = "Pending", message = "词条正在审核中" });
        var revision = await db.WikiArticleRevisions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == article.CurrentRevisionId && r.ArticleId == id);
        if (revision == null) return NotFound();
        var revisions = db.WikiArticleRevisions.Where(r => r.ArticleId == id && (r.Status == 1 || r.Id == article.CurrentRevisionId));
        return Ok(new { id, title = revision.Title, content = revision.Content, tags = article.Tags, categoryId = article.CategoryId, publishedAt = revision.CreatedAt, authorId = article.CreatorId,
            contributors = await revisions.Select(r => r.ContributorId).Distinct().ToListAsync(), revisionCount = await revisions.CountAsync(), currentRevisionId = article.CurrentRevisionId });
    }

    [HttpGet("article/{id}/history")]
    public async Task<IActionResult> History(string id, int page = 1)
    {
        var article = await db.WikiArticles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted && a.CurrentRevisionId != null);
        if (article == null) return NotFound();
        var query = db.WikiArticleRevisions.Where(r => r.ArticleId == id && (r.Status == 1 || r.Id == article.CurrentRevisionId));
        var total = await query.CountAsync(); page = Math.Clamp(page, 1, Math.Max(1, (int)Math.Ceiling(total / 20d)));
        var rows = await query.OrderByDescending(r => r.Id).Skip((page - 1) * 20).Take(20).Select(r => new { r.Id, r.PreviousRevisionId, r.ContributorId, r.CreatedAt, r.ReviewedAt, r.EditSummary }).ToListAsync();
        var ids = await query.Select(r => r.ContributorId).Distinct().ToListAsync();
        var userIds = ids.Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty).ToArray();
        var users = await db.Users.AsNoTracking().Where(u => Enumerable.Contains(userIds, u.Id)).Select(u => new { u.Id, u.Username }).ToListAsync();
        string Name(string id) => users.FirstOrDefault(u => u.Id.ToString() == id)?.Username ?? "历史贡献者";
        return Ok(new { total, page, currentRevisionId = article.CurrentRevisionId, contributors = ids.Select(id => new { id, name = Name(id) }), items = rows.Select(r => new { r.Id, r.PreviousRevisionId, r.ContributorId, contributorName = Name(r.ContributorId), r.CreatedAt, r.ReviewedAt, r.EditSummary }) });
    }

    [HttpGet("article/{id}/history/{revisionId:int}")]
    public async Task<IActionResult> Revision(string id, int revisionId)
    {
        var article = await db.WikiArticles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id && !a.IsDeleted && a.CurrentRevisionId != null);
        if (article == null) return NotFound();
        var query = db.WikiArticleRevisions.AsNoTracking().Where(r => r.ArticleId == id && (r.Status == 1 || r.Id == article.CurrentRevisionId));
        var revision = await query.FirstOrDefaultAsync(r => r.Id == revisionId);
        if (revision == null) return NotFound();
        var previous = revision.PreviousRevisionId.HasValue ? await query.FirstOrDefaultAsync(r => r.Id == revision.PreviousRevisionId) : null;
        return Ok(new { revision.Id, revision.Content, revision.PreviousRevisionId, previousContent = previous?.Content, revision.EditSummary, revision.CreatedAt });
    }

    [HttpGet("articles/by-category/{categoryId}")]
    public async Task<IActionResult> GetArticlesByCategory(int categoryId) => Ok(await db.WikiArticles.Where(a => a.CategoryId == categoryId && !a.IsDeleted && a.CurrentRevisionId != null).Select(a => new { a.Id, a.Title, a.Excerpt, a.ViewCount, a.UpdatedAt }).ToListAsync());
}

public class WikiPublishDto
{
    public string NoteId { get; set; } = "";
    public int CategoryId { get; set; }
    public List<string> Tags { get; set; } = new();
    public string? Title { get; set; }
    public string? Excerpt { get; set; }
    public string? Content { get; set; }
}
