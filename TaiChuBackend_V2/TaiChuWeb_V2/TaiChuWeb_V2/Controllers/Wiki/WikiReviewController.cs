using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Wiki;

namespace TaiChuWeb_V2.Controllers.Wiki
{
    [ApiController]
    [Route("api/wiki/reviews")]
    public class WikiReviewController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WikiReviewController(AppDbContext context) => _context = context;

        // 1. 获取所有待审核的词条
        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingRevisions(string userId, bool isAdmin)
        {
            // 1. 先定义基础查询（操作的是实体对象，可以进行 Include 和 Where）
            var query = _context.WikiArticleRevisions
                .Include(r => r.Category)
                .Where(r => r.Status == 0);

            // 2. 权限过滤（必须在 Select 投影之前执行）
            if (!isAdmin)
            {
                query = query.Where(r => r.Category.OwnerId == userId);
            }

            // 3. 最后再执行投影（Select）
            var result = await query.Select(r => new {
                r.Id,
                r.Title,
                r.AuthorId,
                r.Content,
                CategoryName = r.Category.Name // 这里取到了分类名
            }).ToListAsync();

            return Ok(result);
        }

        // 2. 处理审核 (通过/驳回)
        [HttpPost("{revisionId}/handle")]
        public async Task<IActionResult> HandleRevision(int revisionId, [FromBody] ReviewRequestDto request)
        {
            var rev = await _context.WikiArticleRevisions
                .Include(r => r.Category)
                .FirstOrDefaultAsync(r => r.Id == revisionId);

            if (rev == null) return NotFound("修订版不存在");

            // 权限校验逻辑
            bool isOwner = rev.Category.OwnerId == request.CurrentUserId;
            if (!request.IsAdmin && !isOwner)
                return Forbid("你没有该分类的审核权限");

            rev.Status = request.Approved ? 1 : 2; // 1通过, 2驳回
            rev.ReviewRemarks = request.Remarks;
            rev.ReviewedAt = DateTime.UtcNow;
            rev.ReviewerId = request.CurrentUserId;

            // 如果审核通过，正式生效
            if (request.Approved)
            {
                var article = await _context.WikiArticles.FindAsync(rev.ArticleId);
                article.CurrentRevisionId = rev.Id;
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = request.Approved ? "词条已发布" : "词条已驳回" });
        }
    }

    public class ReviewRequestDto
    {
        public string CurrentUserId { get; set; } = string.Empty;
        public bool IsAdmin { get; set; }
        public bool Approved { get; set; }
        public string Remarks { get; set; } = string.Empty;
    }
}