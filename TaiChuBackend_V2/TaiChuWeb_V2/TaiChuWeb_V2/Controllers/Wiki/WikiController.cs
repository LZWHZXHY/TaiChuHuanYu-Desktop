using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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

        [HttpPost("publish")]
        [Authorize] // 🌟 必须加，确保只有登录用户能操作
        public async Task<IActionResult> PublishFromNote([FromBody] WikiPublishDto request)
        {
            // 1. 获取当前发布者 (贡献者) 的 ID
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
                return Unauthorized("身份验证失败，请重新登录");

            // 2. 查找源笔记
            var note = await _context.Notes
                .FirstOrDefaultAsync(n => n.Id == Guid.Parse(request.NoteId));
            if (note == null) return NotFound("未找到源笔记");

            // 3. 获取分类信息
            var category = await _context.WikiCategories.FindAsync(request.CategoryId);
            if (category == null) return BadRequest("分类不存在");

            // 4. 拼装内容
            var blocks = await _context.Blocks
                .Where(b => b.OwnerId == request.NoteId && b.OwnerType == "note")
                .OrderBy(b => b.SortOrder) // 确保按顺序拼装
                .ToListAsync();

            var fullContent = string.Join("\n", blocks.Select(b => {
                try
                {
                    using var doc = JsonDocument.Parse(b.Data);
                    return doc.RootElement.TryGetProperty("text", out var text) ? text.GetString() : b.Data;
                }
                catch { return b.Data; }
            }));

            // 5. 创建 WikiArticle (外壳)
            var wikiArticle = new WikiArticle
            {
                Title = note.Title,
                CreatedAt = DateTime.UtcNow,
                // 如果你的数据库有 OriginalAuthorId 字段，记得加上这一行
                // OriginalAuthorId = note.AuthorId 
            };
            _context.WikiArticles.Add(wikiArticle);
            await _context.SaveChangesAsync();

            // 6. 判定审核状态
            int initialStatus = (category.OwnershipType == 1) ? 1 : 0;

            // 7. 创建修订记录 (Revision)
            var revision = new WikiArticleRevision
            {
                ArticleId = wikiArticle.Id.ToString(), // 确保类型匹配
                Content = fullContent,
                ContributorId = currentUserId,       // 🌟 记录当前发布者
                CategoryId = request.CategoryId,
                Title = note.Title,
                Status = initialStatus,
                EditSummary = initialStatus == 1 ? "折射发布成功" : "已提交，等待管理员审核",
                CreatedAt = DateTime.UtcNow,
                PreviousRevisionId = null            // 初始发布，无前置版本
            };

            _context.WikiArticleRevisions.Add(revision);
            await _context.SaveChangesAsync();

            // 8. 如果自动通过，更新文章的当前修订指针
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

        [HttpPost("update")] 
        [Authorize]
        public async Task<IActionResult> UpdateFromNote([FromBody] WikiUpdateDto request)
        {
            // 1. 解析当前登录用户
            var currentUserId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId)) return Unauthorized("未登录");

            if (!Guid.TryParse(currentUserId, out Guid userGuid))
            {
                return BadRequest(new { message = "用户ID格式不合法" });
            }

            // 2. 校验用户在数据库里是否真实存在（防止幽灵 Token）
            var userExists = await _context.Users.AnyAsync(u => u.Id == userGuid);
            if (!userExists)
            {
                return StatusCode(401, new { message = "您的用户状态在当前的太初世界中不存在，请重新登录" });
            }

            // 3. 🌟 核心优化：使用 AsNoTracking() 读取母表！
            // 这样 EF Core 就不会追踪 article 的任何状态，后面无论如何都不会自动触发主表的 UPDATE 语句
            var article = await _context.WikiArticles
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == request.ArticleId);

            if (article == null) return NotFound("该词条在百科宇宙中不存在");

            // 4. 校验分类是否存在
            var categoryExists = await _context.WikiCategories.AnyAsync(c => c.Id == article.CategoryId);
            if (!categoryExists)
            {
                return BadRequest(new { message = "该词条绑定的分类已不存在，请先修正分类" });
            }

            // 5. 显式乐观锁判断（由我们自己的业务逻辑打回，而不是让 EF Core 抛异常崩溃）
            if (request.BaseRevisionId != article.CurrentRevisionId)
            {
                return StatusCode(409, new { message = "⚠️ 提交失败：词条已被其他修士更新，请重新获取最新内容后再提交修订" });
            }

            // 6. 组装待审核的修订版本记录
            var newRevision = new WikiArticleRevision
            {
                ArticleId = request.ArticleId,
                CategoryId = article.CategoryId,
                Title = article.Title,
                Content = request.Content,        // 前端传过来的标准 JSON 字符串
                EditSummary = request.Summary ?? "协作修改",
                ContributorId = currentUserId,    // 关联当前真实的 string 类型用户 ID
                CreatedAt = DateTime.UtcNow,
                Status = 0                        // 🌟 设为整数 0（代表待审核 Pending 状态）
            };

            // 7. 🌟 纯粹的 INSERT 操作
            // 仅向修订记录表添加数据。由于前面 article 加了 AsNoTracking()，
            // 这里 SaveChanges 时，EF Core 只会发一条 INSERT 语句，绝对不可能触发母表的并发冲突异常！
            _context.WikiArticleRevisions.Add(newRevision);
            await _context.SaveChangesAsync();

            return Ok(new { message = "修订提交成功，已放入太初天平等待管理员审核！" });
        }



        [HttpGet("article/{id}")]
    public async Task<IActionResult> GetArticleDetail(string id)
    {
        var article = await _context.WikiArticles.FindAsync(id);
        if (article == null) return NotFound("词条不存在");

        // 如果 CurrentRevisionId 存在，说明有正式版
        int targetRevisionId = article.CurrentRevisionId ?? 0;

        if (targetRevisionId == 0)
            return Ok(new { status = "Pending", message = "词条正在审核中" });

        // 1. 获取正式版内容
        var revision = await _context.WikiArticleRevisions.FindAsync(targetRevisionId);
        if (revision == null) return NotFound("修订记录丢失");

        // 2. 🌟 核心：查询该词条所有参与过的贡献者 (去重)
        var contributors = await _context.WikiArticleRevisions
            .Where(r => r.ArticleId == id && r.Status == 1) // 只统计已通过的贡献
            .Select(r => r.ContributorId)
            .Distinct()
            .ToListAsync();

        return Ok(new
        {
            id = article.Id,
            title = revision.Title,
            content = revision.Content, 
            tags = article.Tags,
            publishedAt = revision.CreatedAt,
            authorId = article.CreatorId, // 确保 WikiArticle 模型中已包含 CreatorId 属性
            contributors = contributors,  // 现在这里就是干净的 List<string> 数据了
            currentRevisionId = article.CurrentRevisionId
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