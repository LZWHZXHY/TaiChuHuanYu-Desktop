using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Wiki;

namespace TaiChuWeb_V2.Controllers.Wiki
{
    [ApiController]
    [Route("api/wiki/governance")]
    public class WikiReviewController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WikiReviewController(AppDbContext context) => _context = context;

        // ==========================================
        // 1. 内容审核逻辑 (Audit)
        // ==========================================

        [HttpGet("pending")]
        [Authorize]
        public async Task<IActionResult> GetPendingRevisions()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User.IsInRole("Admin");

            var query = _context.WikiArticleRevisions
                .Include(r => r.Category)
                .Where(r => r.Status == 0);

            if (!isAdmin)
            {
                if (string.IsNullOrEmpty(userId)) return Unauthorized();
                query = query.Where(r => r.Category.OwnerId == userId);
            }

            return Ok(await query.Select(r => new {
                r.Id,
                r.Title,
                AuthorId = r.ContributorId,
                r.Content,
                CategoryName = r.Category.Name,
                r.CreatedAt
            }).ToListAsync());
        }

        // 添加到 WikiReviewController.cs
        [HttpGet("articles/manage")]
        //[Authorize(Roles = "Admin")] // 确保只有管理员能看到所有文章
        public async Task<IActionResult> GetAllArticlesForManagement()
        {
            // 注意：如果是全量管理，直接从 DbContext 查询
            var articles = await _context.WikiArticles
                .OrderByDescending(a => a.UpdatedAt)
                .ToListAsync();

            return Ok(articles);
        }


        [HttpPost("revisions/{revisionId}/handle")]
        [Authorize]
        public async Task<IActionResult> HandleRevision(int revisionId, [FromBody] ReviewRequestDto request)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var isAdmin = User.IsInRole("Admin");

            var rev = await _context.WikiArticleRevisions
                .Include(r => r.Category)
                .FirstOrDefaultAsync(r => r.Id == revisionId);

            if (rev == null) return NotFound();
            if (!isAdmin && rev.Category.OwnerId != currentUserId) return Forbid();

            rev.Status = request.Approved ? 1 : 2;
            rev.ReviewRemarks = request.Remarks;
            rev.ReviewedAt = DateTime.UtcNow;
            rev.ReviewerId = currentUserId;

            if (request.Approved)
            {
                var article = await _context.WikiArticles.FindAsync(Guid.Parse(rev.ArticleId));
                if (article != null) article.CurrentRevisionId = rev.Id;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "操作已完成" });
        }

        // ==========================================
        // 2. 存量治理：下架/恢复 (Archiving)
        // ==========================================

        [HttpPost("articles/{articleId}/toggle-archive")]
        //[Authorize(Roles = "Admin")] // 仅管理员可操作
        public async Task<IActionResult> ToggleArchive(string articleId)
        {
            var article = await _context.WikiArticles.FindAsync(articleId);
            if (article == null) return NotFound();

            article.IsDeleted = !article.IsDeleted;
            article.DeletedAt = article.IsDeleted ? DateTime.UtcNow : null;

            await _context.SaveChangesAsync();
            return Ok(new { status = article.IsDeleted ? "已下架" : "已恢复" });
        }

        // ==========================================
        // 3. 版本回溯：后悔药 (Rollback)
        // ==========================================

        [HttpPost("articles/{articleId}/rollback/{targetRevisionId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Rollback(string articleId, int targetRevisionId)
        {
            var article = await _context.WikiArticles.FindAsync(Guid.Parse(articleId));
            var oldRevision = await _context.WikiArticleRevisions.FindAsync(targetRevisionId);

            if (article == null || oldRevision == null) return NotFound();

            // 生成一个新的修订版作为“回退”记录
            var rollbackRevision = new WikiArticleRevision
            {
                ArticleId = article.Id.ToString(),
                Title = oldRevision.Title,
                Content = oldRevision.Content,
                ContributorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                Status = 1, // 直接生效
                EditSummary = $"回退至版本 #{targetRevisionId}",
                CreatedAt = DateTime.UtcNow,
                PreviousRevisionId = article.CurrentRevisionId
            };

            _context.WikiArticleRevisions.Add(rollbackRevision);
            await _context.SaveChangesAsync();

            article.CurrentRevisionId = rollbackRevision.Id;
            await _context.SaveChangesAsync();

            return Ok(new { message = "回退成功" });
        }
    }

    public class ReviewRequestDto
    {
        public bool Approved { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }
}