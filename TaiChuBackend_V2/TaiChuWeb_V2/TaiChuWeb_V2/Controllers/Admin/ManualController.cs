using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Admin;

namespace TaiChuWeb_V2.Controllers.Admin;

[ApiController]
[Route("api/admin/manual")]
public class ManualController(AppDbContext context) : ControllerBase
{
    #region DTOs (请求数据契约)

    public record SaveArticleDto(
        int? Id,
        int CategoryId,
        string Slug,
        string Title,
        string Content,
        int SortOrder = 0,
        bool IsPublished = true
    );

    public record SaveCategoryDto(
        int? Id,
        string Name,
        int SortOrder = 0
    );

    #endregion

    #region 文章管理接口

    /// <summary>
    /// 获取后台文章全量列表（包含草稿状态）
    /// GET: /api/admin/manual/articles
    /// </summary>
    [HttpGet("articles")]
    public async Task<IActionResult> GetArticles()
    {
        var articles = await context.ManualArticles
            .AsNoTracking()
            .Include(a => a.Category)
            .OrderBy(a => a.Category != null ? a.Category.SortOrder : 0)
            .ThenBy(a => a.SortOrder)
            .Select(a => new
            {
                a.Id,
                a.CategoryId,
                CategoryName = a.Category != null ? a.Category.Name : "未分类",
                a.Slug,
                a.Title,
                a.Content,
                a.SortOrder,
                a.IsPublished,
                a.CreatedAt,
                a.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { code = 200, data = articles });
    }

    /// <summary>
    /// 新增或保存修改文档（保存即发布/生效）
    /// POST: /api/admin/manual/article
    /// </summary>
    [HttpPost("article")]
    public async Task<IActionResult> SaveArticle([FromBody] SaveArticleDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title) || string.IsNullOrWhiteSpace(dto.Slug))
        {
            return BadRequest(new { code = 400, message = "文档标题和 URL 标识(slug)不能为空" });
        }

        var normalizedSlug = dto.Slug.Trim();

        // 1. 确保指定分类存在，如果不存在则自动挂到首个分类或创建默认分类
        var categoryExists = await context.ManualCategories.AnyAsync(c => c.Id == dto.CategoryId);
        var targetCategoryId = dto.CategoryId;

        if (!categoryExists)
        {
            var defaultCat = await context.ManualCategories.FirstOrDefaultAsync();
            if (defaultCat is null)
            {
                defaultCat = new ManualCategory { Name = "快速上手", SortOrder = 1, CreatedAt = DateTime.UtcNow };
                context.ManualCategories.Add(defaultCat);
                await context.SaveChangesAsync();
            }
            targetCategoryId = defaultCat.Id;
        }

        ManualArticle? article;

        // 2. 更新已有文档
        if (dto.Id is > 0)
        {
            article = await context.ManualArticles.FindAsync(dto.Id.Value);
            if (article is null)
            {
                return NotFound(new { code = 404, message = "要编辑的文档不存在" });
            }

            // Slug 防重（排除自身）
            var isDuplicate = await context.ManualArticles
                .AnyAsync(a => a.Slug == normalizedSlug && a.Id != dto.Id.Value);
            if (isDuplicate)
            {
                return BadRequest(new { code = 400, message = "URL 标识 (slug) 与现有文档冲突，请更换" });
            }

            article.CategoryId = targetCategoryId;
            article.Slug = normalizedSlug;
            article.Title = dto.Title.Trim();
            article.Content = dto.Content ?? string.Empty;
            article.SortOrder = dto.SortOrder;
            article.IsPublished = dto.IsPublished;
            article.UpdatedAt = DateTime.UtcNow;
        }
        // 3. 新增文档
        else
        {
            // Slug 防重
            var isDuplicate = await context.ManualArticles.AnyAsync(a => a.Slug == normalizedSlug);
            if (isDuplicate)
            {
                return BadRequest(new { code = 400, message = "URL 标识 (slug) 已存在，请更换" });
            }

            article = new ManualArticle
            {
                CategoryId = targetCategoryId,
                Slug = normalizedSlug,
                Title = dto.Title.Trim(),
                Content = dto.Content ?? string.Empty,
                SortOrder = dto.SortOrder,
                IsPublished = dto.IsPublished,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            context.ManualArticles.Add(article);
        }

        await context.SaveChangesAsync();
        return Ok(new { code = 200, message = "文档保存成功", data = new { article.Id, article.Slug } });
    }

    /// <summary>
    /// 删除指定文档
    /// DELETE: /api/admin/manual/article/{id}
    /// </summary>
    [HttpDelete("article/{id:int}")]
    public async Task<IActionResult> DeleteArticle(int id)
    {
        var article = await context.ManualArticles.FindAsync(id);
        if (article is null)
        {
            return NotFound(new { code = 404, message = "目标文档不存在" });
        }

        context.ManualArticles.Remove(article);
        await context.SaveChangesAsync();

        return Ok(new { code = 200, message = "删除成功" });
    }

    #endregion

    #region 分类管理接口

    /// <summary>
    /// 获取所有手册分类（供后台下拉框选择使用）
    /// GET: /api/admin/manual/categories
    /// </summary>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories()
    {
        var categories = await context.ManualCategories
            .AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.SortOrder,
                ArticleCount = c.Articles.Count
            })
            .ToListAsync();

        return Ok(new { code = 200, data = categories });
    }

    /// <summary>
    /// 新增或修改分类
    /// POST: /api/admin/manual/category
    /// </summary>
    [HttpPost("category")]
    public async Task<IActionResult> SaveCategory([FromBody] SaveCategoryDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
        {
            return BadRequest(new { code = 400, message = "分类名称不能为空" });
        }

        if (dto.Id is > 0)
        {
            var cat = await context.ManualCategories.FindAsync(dto.Id.Value);
            if (cat is null) return NotFound(new { code = 404, message = "分类不存在" });

            cat.Name = dto.Name.Trim();
            cat.SortOrder = dto.SortOrder;
        }
        else
        {
            var cat = new ManualCategory
            {
                Name = dto.Name.Trim(),
                SortOrder = dto.SortOrder,
                CreatedAt = DateTime.UtcNow
            };
            context.ManualCategories.Add(cat);
        }

        await context.SaveChangesAsync();
        return Ok(new { code = 200, message = "分类保存成功" });
    }

    /// <summary>
    /// 删除分类（级联删除旗下所有文章）
    /// DELETE: /api/admin/manual/category/{id}
    /// </summary>
    [HttpDelete("category/{id:int}")]
    public async Task<IActionResult> DeleteCategory(int id)
    {
        var category = await context.ManualCategories.FindAsync(id);
        if (category is null)
        {
            return NotFound(new { code = 404, message = "目标分类不存在" });
        }

        context.ManualCategories.Remove(category);
        await context.SaveChangesAsync();

        return Ok(new { code = 200, message = "分类及旗下文章已成功删除" });
    }

    #endregion
}