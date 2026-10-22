using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Wiki;
using TaiChuWeb_V2.Services.Wiki;

namespace TaiChuWeb_V2.Controllers.Wiki;

[ApiController, Authorize, Route("api/wiki/governance")]
public class WikiReviewController(AppDbContext db) : ControllerBase
{
    private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

    [HttpGet("pending")]
    public async Task<IActionResult> GetPendingRevisions()
    {
        var global = await WikiAccess.CanManageAsync(db, User);
        var owned = db.WikiCategories.Where(c => c.OwnerId == UserId).Select(c => c.Id);
        var rows = await db.WikiArticleRevisions.Include(r => r.Category).Where(r => r.Status == 0 && (global || owned.Contains(r.CategoryId))).OrderByDescending(r => r.CreatedAt).ToListAsync();
        var articleIds = rows.Select(r => r.ArticleId).ToArray();
        var articles = await db.WikiArticles.Where(a => articleIds.Contains(a.Id)).ToListAsync();
        var ids = rows.Select(r => r.ContributorId).Concat(articles.Select(a => a.CreatorId)).Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty).ToArray();
        var users = await db.Users.Where(u => ids.Contains(u.Id)).ToListAsync();
        string Name(string? id) => users.FirstOrDefault(u => u.Id.ToString() == id)?.Username ?? "历史贡献者";
        return Ok(rows.Select(r => new { r.Id, r.ArticleId, r.Title, r.EditSummary, r.Status, r.CreatedAt, r.Content, r.PreviousRevisionId, CategoryName = r.Category?.Name ?? "未分类", ContributorName = Name(r.ContributorId), CreatorName = Name(articles.FirstOrDefault(a => a.Id == r.ArticleId)?.CreatorId) }));
    }

    [HttpGet("articles/manage")]
    public async Task<IActionResult> GetAllArticlesForManagement()
    {
        if (!await WikiAccess.CanManageAsync(db, User)) return Forbid();
        return Ok(await db.WikiArticles.OrderByDescending(a => a.UpdatedAt).ToListAsync());
    }

    [HttpDelete("articles/cleanup")]
    public async Task<IActionResult> CleanupDeletedArticles()
    {
        if (!await WikiAccess.CanManageAsync(db, User)) return Forbid();
        var deleted = await db.WikiArticles.Where(a => a.IsDeleted).ToListAsync();
        db.WikiArticles.RemoveRange(deleted); await db.SaveChangesAsync();
        return Ok(new { message = $"成功清除 {deleted.Count} 条下架记录" });
    }

    [HttpPost("revisions/{revisionId}/handle")]
    public async Task<IActionResult> HandleRevision(int revisionId, [FromBody] ReviewRequestDto request)
    {
        if (request.Remarks?.Length > 500) return BadRequest(new { message = "审核说明不能超过 500 字" });
        var revision = await db.WikiArticleRevisions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == revisionId);
        if (revision == null) return NotFound();
        if (!await WikiAccess.CanManageAsync(db, User, revision.CategoryId)) return Forbid();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () => {
            using var transaction = await db.Database.BeginTransactionAsync();
            // 在同一事务中锁定待审版本及正文指针，防止重复审核与过期覆盖。
            var handled = await db.WikiArticleRevisions.Where(r => r.Id == revisionId && r.Status == 0)
                .ExecuteUpdateAsync(s => s.SetProperty(r => r.Status, request.Approved ? 1 : 2).SetProperty(r => r.ReviewerId, UserId).SetProperty(r => r.ReviewRemarks, request.Remarks).SetProperty(r => r.ReviewedAt, DateTime.UtcNow));
            if (handled == 0) return Conflict(new { message = "此修订已被处理，请刷新列表" });
            if (request.Approved) {
                var updated = await db.WikiArticles.Where(a => a.Id == revision.ArticleId && !a.IsDeleted && (a.CurrentRevisionId == revision.PreviousRevisionId || a.CurrentRevisionId == revision.Id))
                    .ExecuteUpdateAsync(s => s.SetProperty(a => a.CurrentRevisionId, revisionId).SetProperty(a => a.Title, revision.Title).SetProperty(a => a.UpdatedAt, DateTime.UtcNow));
                if (updated == 0) return Conflict(new { message = "正文已更新或词条已下架，请让贡献者基于最新版本重新提交" });
            }
            await transaction.CommitAsync(); return Ok(new { message = "审核完成" });
        });
    }

    [HttpPost("articles/{articleId}/toggle-archive")]
    public async Task<IActionResult> ToggleArchive(string articleId)
    {
        var article = await db.WikiArticles.FindAsync(articleId);
        if (article == null) return NotFound();
        if (!await WikiAccess.CanManageAsync(db, User, article.CategoryId)) return Forbid();
        article.IsDeleted = !article.IsDeleted; article.DeletedAt = article.IsDeleted ? DateTime.UtcNow : null;
        await db.SaveChangesAsync(); return Ok(new { status = article.IsDeleted ? "已下架" : "已恢复" });
    }

    [HttpPost("articles/{articleId}/rollback/{targetRevisionId}")]
    public async Task<IActionResult> Rollback(string articleId, int targetRevisionId)
    {
        var article = await db.WikiArticles.AsNoTracking().FirstOrDefaultAsync(a => a.Id == articleId && !a.IsDeleted);
        if (article == null) return NotFound();
        if (!await WikiAccess.CanManageAsync(db, User, article.CategoryId)) return Forbid();
        var old = await db.WikiArticleRevisions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == targetRevisionId && r.ArticleId == articleId && (r.Status == 1 || r.Id == article.CurrentRevisionId));
        if (old == null) return NotFound();
        return await db.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () => {
            db.ChangeTracker.Clear(); using var transaction = await db.Database.BeginTransactionAsync();
            var revision = new WikiArticleRevision { ArticleId = articleId, CategoryId = article.CategoryId, Title = old.Title, Content = old.Content, ContributorId = UserId, Status = 1, EditSummary = $"回退至版本 #{targetRevisionId}", PreviousRevisionId = article.CurrentRevisionId, ReviewerId = UserId, ReviewedAt = DateTime.UtcNow };
            db.WikiArticleRevisions.Add(revision); await db.SaveChangesAsync();
            var updated = await db.WikiArticles.Where(a => a.Id == articleId && !a.IsDeleted && a.CurrentRevisionId == article.CurrentRevisionId)
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.CurrentRevisionId, revision.Id).SetProperty(a => a.Title, old.Title).SetProperty(a => a.UpdatedAt, DateTime.UtcNow));
            if (updated == 0) return Conflict(new { message = "词条已更新，请刷新后再回退" });
            await transaction.CommitAsync(); return Ok(new { message = "回退成功，已保留本次修订记录" });
        });
    }
}

public class ReviewRequestDto { public bool Approved { get; set; } public string Remarks { get; set; } = ""; }
