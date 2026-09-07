using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers.Users;

[ApiController]
[Route("api/manual")] // 显式匹配前台路径 /api/manual
public class ManualController(AppDbContext context) : ControllerBase
{
    /// <summary>
    /// 获取前台公开手册目录树
    /// GET: /api/manual/menu
    /// </summary>
    [HttpGet("menu")]
    public async Task<IActionResult> GetMenu()
    {
        var categories = await context.ManualCategories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .Select(c => new
            {
                c.Id,
                c.Name,
                Articles = c.Articles
                    .Where(a => a.IsPublished) // 仅展示已发布的文章
                    .OrderBy(a => a.SortOrder)
                    .Select(a => new
                    {
                        a.Id,
                        a.Slug,
                        a.Title
                    })
                    .ToList()
            })
            .Where(c => c.Articles.Count != 0) // 过滤无公开文章的空分类
            .ToListAsync();

        return Ok(new { code = 200, data = categories });
    }

    /// <summary>
    /// 根据 slug 读取单篇文章的正文内容
    /// GET: /api/manual/article/{slug}
    /// </summary>
    [HttpGet("article/{slug}")]
    public async Task<IActionResult> GetArticleBySlug(string slug)
    {
        var article = await context.ManualArticles
            .AsNoTracking()
            .Where(a => a.Slug == slug && a.IsPublished)
            .Select(a => new
            {
                a.Id,
                a.Slug,
                a.Title,
                a.Content,
                a.UpdatedAt
            })
            .FirstOrDefaultAsync();

        if (article == null)
        {
            return NotFound(new { code = 404, message = "未找到该手册章节" });
        }

        return Ok(new { code = 200, data = article });
    }
}