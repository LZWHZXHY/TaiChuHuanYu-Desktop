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

        // 1. 获取待审核修订列表
        [HttpGet("pending")]
        [Authorize]
        public async Task<IActionResult> GetPendingRevisions()
        {
            // 1. 获取待审核修订
            var revisions = await _context.WikiArticleRevisions
                .Include(r => r.Category)
                .Where(r => r.Status == 0)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync();

            // 2. 提取文章信息
            var articleIds = revisions.Select(r => r.ArticleId).Distinct().ToList();
            var articles = await _context.WikiArticles
                .Where(a => articleIds.Contains(a.Id))
                .Select(a => new { a.Id, a.CreatorId })
                .ToListAsync();

            // 3. 关键修改：将所有字符串 ID 转换为 Guid，并过滤掉无效值
            var allUserIds = revisions.Select(r => r.ContributorId)
                .Union(articles.Select(a => a.CreatorId))
                .Where(id => !string.IsNullOrEmpty(id))
                .Select(id => Guid.TryParse(id, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .Distinct()
                .ToList(); // 此时是 List<Guid>

            // 4. 类型匹配查询：allUserIds (Guid) vs u.Id (Guid)
            var users = await _context.Users
                .Where(u => allUserIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.Username);

            // 5. 组装结果
            var result = revisions.Select(r => {
                var article = articles.FirstOrDefault(a => a.Id == r.ArticleId);

                // 解析 Guid 以便从字典中查找
                Guid.TryParse(r.ContributorId, out var cGuid);
                Guid.TryParse(article?.CreatorId ?? "", out var crGuid);

                return new
                {
                    r.Id,
                    r.ArticleId,
                    r.Title,
                    r.EditSummary,
                    r.Status,
                    r.CreatedAt,
                    r.Content,
                    CategoryName = r.Category?.Name ?? "未分类",
                    ContributorName = users.ContainsKey(cGuid) ? users[cGuid] : "匿名",
                    CreatorName = (article != null && users.ContainsKey(crGuid)) ? users[crGuid] : "未知"
                };
            });

            return Ok(result);
        }

        // 2. 清理下架记录
        [HttpDelete("articles/cleanup")]
        public async Task<IActionResult> CleanupDeletedArticles()
        {
            var deletedArticles = await _context.WikiArticles
                .Where(a => a.IsDeleted == true)
                .ToListAsync();

            if (deletedArticles.Count == 0) return Ok(new { message = "无待清理记录" });

            _context.WikiArticles.RemoveRange(deletedArticles);
            await _context.SaveChangesAsync();
            return Ok(new { message = $"成功清除 {deletedArticles.Count} 条记录" });
        }

        // 3. 全量治理（已去重）
        [HttpGet("articles/manage")]
        public async Task<IActionResult> GetAllArticlesForManagement()
        {
            var allArticles = await _context.WikiArticles
                .OrderByDescending(a => a.UpdatedAt)
                .ToListAsync();

            var cleanArticles = allArticles
                .Where(a => !string.IsNullOrEmpty(a.Title))
                .GroupBy(a => a.SourceNoteId ?? a.Id)
                .Select(g => g.OrderByDescending(a => a.UpdatedAt).First())
                .OrderByDescending(a => a.UpdatedAt)
                .ToList();

            return Ok(cleanArticles);
        }

        // 4. 处理审核申请
        [HttpPost("revisions/{revisionId}/handle")]
        [Authorize]
        public async Task<IActionResult> HandleRevision(int revisionId, [FromBody] ReviewRequestDto request)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var rev = await _context.WikiArticleRevisions.FirstOrDefaultAsync(r => r.Id == revisionId);
            if (rev == null) return NotFound();

            rev.Status = request.Approved ? 1 : 2;
            rev.ReviewRemarks = request.Remarks;
            rev.ReviewedAt = DateTime.UtcNow;
            rev.ReviewerId = currentUserId;

            if (request.Approved)
            {
                var article = await _context.WikiArticles.FindAsync(rev.ArticleId);
                if (article != null) article.CurrentRevisionId = rev.Id;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "操作成功" });
        }

        // 5. 下架/恢复切换
        [HttpPost("articles/{articleId}/toggle-archive")]
        public async Task<IActionResult> ToggleArchive(string articleId)
        {
            var article = await _context.WikiArticles.FindAsync(articleId);
            if (article == null) return NotFound();

            article.IsDeleted = !article.IsDeleted;
            article.DeletedAt = article.IsDeleted ? DateTime.UtcNow : null;

            await _context.SaveChangesAsync();
            return Ok(new { status = article.IsDeleted ? "已下架" : "已恢复" });
        }

        // 6. 版本回退
        [HttpPost("articles/{articleId}/rollback/{targetRevisionId}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Rollback(string articleId, int targetRevisionId)
        {
            var article = await _context.WikiArticles.FindAsync(articleId);
            var oldRevision = await _context.WikiArticleRevisions.FindAsync(targetRevisionId);

            if (article == null || oldRevision == null) return NotFound();

            var rollbackRevision = new WikiArticleRevision
            {
                ArticleId = article.Id,
                Title = oldRevision.Title,
                Content = oldRevision.Content,
                ContributorId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value,
                Status = 1,
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