using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Wiki;
using TaiChuWeb_V2.Models.Wiki;

namespace TaiChuWeb_V2.Controllers.Wiki
{
    [ApiController]
    [Route("api/[controller]")] // 基础路由: api/wiki
    public class WikiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WikiController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("categories")] // 完整路由: GET api/wiki/categories
        public async Task<IActionResult> GetCategories()
        {
            var categories = await _context.WikiCategories
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Id)
                .ToListAsync();

            return Ok(categories);
        }


        // 🌟 添加这个方法来处理 GET api/wiki/articles 请求
        [HttpGet("articles")]
        public async Task<IActionResult> GetAllArticles()
        {
            var articles = await _context.WikiArticles
                .Where(a => !a.IsDeleted && a.CurrentRevisionId != null)
                .Select(a => new {
                    a.Id,
                    a.Title,
                    a.Excerpt,
                    a.CategoryId,
                    a.ViewCount,
                    a.UpdatedAt
                })
                .ToListAsync();

            return Ok(articles);
        }



        [HttpPost("apply-category")] // 完整路由: POST api/wiki/apply-category
        public async Task<IActionResult> ApplyCategory([FromBody] CategoryRequestDto request)
        {
            var requestItem = new WikiCategoryRequest
            {
                RequesterId = "current-user-id", // 实际建议从 User.Claims 中获取
                CategoryName = request.Name,
                ParentId = request.ParentId,
                Reason = request.Reason,
                Status = 0 // 0: 待审
            };

            _context.WikiCategoryRequests.Add(requestItem);
            await _context.SaveChangesAsync();
            return Ok(new { message = "申请已提交，等待管理员审阅。" });
        }

        // ==========================================
        // 🌟 修正版：由笔记“折射/发布”为百科词条 (适配修订版本系统)
        // ==========================================
        [HttpPost("publish")]
        public async Task<IActionResult> PublishFromNote([FromBody] WikiPublishDto request)
        {
            // 1. 查找源笔记
            var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == Guid.Parse(request.NoteId));
            if (note == null) return NotFound("未找到源笔记");

            // 2. 🌟 获取分类信息以判定审核策略
            var category = await _context.WikiCategories.FindAsync(request.CategoryId);
            if (category == null) return BadRequest("分类不存在");

            // 3. 拼装内容 (逻辑不变)
            var blocks = await _context.Blocks
                .Where(b => b.OwnerId == request.NoteId && b.OwnerType == "note")
                .ToListAsync();

            var fullContent = string.Join("\n", blocks
                .OrderBy(b => int.Parse(b.SortOrder))
                .Select(b => {
                    try { using var doc = JsonDocument.Parse(b.Data); return doc.RootElement.TryGetProperty("text", out var text) ? text.GetString() : b.Data; }
                    catch { return b.Data; }
                }));

            // 4. 创建外壳
            var wikiArticle = new WikiArticle { /* ... 保持原有赋值 ... */ };
            _context.WikiArticles.Add(wikiArticle);
            await _context.SaveChangesAsync();

            // 5. 🌟 动态判定审核状态 (核心修改)
            // 如果 OwnershipType == 1 (私有空间)，自动通过(1)；否则需管理员审核(0)
            int initialStatus = (category.OwnershipType == 1) ? 1 : 0;

            var revision = new WikiArticleRevision
            {
                ArticleId = wikiArticle.Id,
                Content = fullContent,
                AuthorId = note.AuthorId,
                CategoryId = request.CategoryId,
                Title = note.Title,
                Status = initialStatus, // 动态赋予状态
                EditSummary = initialStatus == 1 ? "折射发布成功" : "已提交，等待管理员审核"
            };

            _context.WikiArticleRevisions.Add(revision);
            await _context.SaveChangesAsync();

            // 6. 🌟 仅在自动通过时更新指针
            if (initialStatus == 1)
            {
                wikiArticle.CurrentRevisionId = revision.Id;
                await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                articleId = wikiArticle.Id,
                status = initialStatus,
                message = initialStatus == 1 ? "发布成功" : "发布请求已提交，待管理员审核"
            });
        }

        // ==========================================
        // 🌟 修正版：供 WikiDetail.vue 感应读取完整词条详情
        // ==========================================
        [HttpGet("article/{id}")]
        public async Task<IActionResult> GetArticleDetail(string id)
        {
            var article = await _context.WikiArticles.FindAsync(id);
            if (article == null) return NotFound();

            // 如果 CurrentRevisionId 存在，说明有正式版
            int targetRevisionId = article.CurrentRevisionId ?? 0;

            // 🌟 如果没有正式版，且你有审核权限，你可以去查该词条对应的最新待审 Revision
            // 这里为了简洁，假设只获取已通过的。如果需要，可以扩展逻辑
            if (targetRevisionId == 0)
                return Ok(new { status = "Pending", message = "词条正在审核中" });

            var revision = await _context.WikiArticleRevisions.FindAsync(targetRevisionId);

            return Ok(new
            {
                id = article.Id,
                title = revision.Title,
                content = revision.Content, // 这里的 Content 就是你要喂给 SpiritPreview 的数据
                tags = article.Tags,
                publishedAt = revision.CreatedAt
            });
        }


        [HttpGet("articles/by-category/{categoryId}")]
        public async Task<IActionResult> GetArticlesByCategory(int categoryId)
        {
            var articles = await _context.WikiArticles
                .Where(a => a.CategoryId == categoryId && !a.IsDeleted && a.CurrentRevisionId != null)
                .Select(a => new {
                    a.Id,
                    a.Title,
                    a.Excerpt, // 你的数据库里应该有摘要字段，没有的话可以截取 Revision.Content
                    a.ViewCount,
                    a.UpdatedAt
                })
                .ToListAsync();

            return Ok(articles);
        }
    }

    // ==========================================
    // 🌟 新增：接收发布词条的 DTO 传输模型
    // ==========================================
    public class WikiPublishDto
    {
        public string NoteId { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public List<string> Tags { get; set; } = new List<string>();
    }
}