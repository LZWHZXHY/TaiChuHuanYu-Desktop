using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Threading.Tasks;
using TaiChuWeb_V2.DbContext; // 引入你的 AppDbContext 命名空间
using TaiChuWeb_V2.Models.Artwork;

namespace TaiChuWeb_V2.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/product")]
    // [Authorize(Roles = "SuperAdmin,Trade_Manage")] // 记得解除注释，启用太初权限屏障
    public class AdminProductController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminProductController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 获取画廊大盘数据 (带分页、搜索与状态过滤)
        /// </summary>
        [HttpGet("gallery")]
        public async Task<IActionResult> GetGalleryWorks(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 15,
            [FromQuery] string? search = null,
            [FromQuery] string? status = null)
        {
            // 1. 构造基础查询，Include 关联的用户和图片表
            var query = _context.Artworks
                .Include(a => a.Uploader)
                .Include(a => a.Images)
                .AsQueryable();

            // 2. 检索逻辑：匹配 ID、标题或作者名
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(a =>
                    a.Id.ToString() == searchLower ||
                    a.Title.ToLower().Contains(searchLower) ||
                    (a.Uploader.Username != null && a.Uploader.Username.ToLower().Contains(searchLower)));
            }

            // 3. 状态过滤 (依赖于 Artwork.cs 中新增的 Status 字段)
            if (!string.IsNullOrWhiteSpace(status))
            {
                query = query.Where(a => a.Status == status);
            }

            // 4. 分页与统计
            var totalCount = await query.CountAsync();
            var artworks = await query
                .OrderByDescending(a => a.UploadAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 5. 映射到前端 GalleryDto 所需的数据结构
            var items = artworks.Select(a => new
            {
                id = $"GAL-{a.Id.ToString().PadLeft(6, '0')}", // 伪装成带前缀的追踪码，契合太初 UI
                title = a.Title,
                authorId = a.UploaderId.ToString(),
                authorName = a.Uploader?.Username ?? "未知造物主",
                coverUrl = a.Images.FirstOrDefault(i => i.IsCover)?.ImageUrl
                           ?? a.Images.FirstOrDefault()?.ImageUrl, // 提取封面图
                views = a.ViewCount,
                likes = a.LikesCount,
                favorites = a.FavoritesCount,
                status = a.Status,            // 依赖新增的 Status 字段
                isFeatured = a.IsFeatured,    // 依赖新增的 IsFeatured 字段
                createdAt = a.UploadAt
            });

            return Ok(new { items, totalCount });
        }
        /// <summary>
        /// 获取博客大盘数据 (Type = "blog")
        /// </summary>
        [HttpGet("blog")]
        public async Task<IActionResult> GetBlogPosts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 15,
            [FromQuery] string? search = null)
        {
            // 基础查询：限定为博客类型
            var query = _context.PublishedNotes
                .Where(n => n.Type == "blog")
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(n => n.Title != null && n.Title.Contains(search) || n.AuthorName != null && n.AuthorName.Contains(search));
            }

            var totalCount = await query.CountAsync();
            var posts = await query
                .OrderByDescending(n => n.PublishedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 映射到前端所需结构
            var items = posts.Select(p => new
            {
                id = p.Id.ToString(), // 使用 GUID
                title = p.Title ?? "无题",
                authorName = p.AuthorName ?? "佚名",
                excerpt = p.Excerpt,
                resonance = p.Resonance, // 博客的阅读/共鸣数
                publishedAt = p.PublishedAt
            });

            return Ok(new { items, totalCount });
        }

        /// <summary>
        /// 博客干涉 (修改标题/共鸣数)
        /// </summary>
        [HttpPut("blog/{id}/governance")]
        public async Task<IActionResult> UpdateBlogGovernance(Guid id, [FromBody] BlogGovernanceDto dto)
        {
            var note = await _context.PublishedNotes.FindAsync(id);
            if (note == null || note.Type != "blog") return NotFound(new { message = "未捕捉到该博客实体" });

            note.Title = dto.Title;
            note.Resonance = dto.Resonance;

            await _context.SaveChangesAsync();
            return Ok(new { message = "博客数据已修正" });
        }

        /// <summary>
        /// 彻底删除博客
        /// </summary>
        [HttpDelete("blog/{id}")]
        public async Task<IActionResult> DeleteBlog(Guid id)
        {
            var note = await _context.PublishedNotes.FindAsync(id);
            if (note == null || note.Type != "blog") return NotFound(new { message = "未捕捉到该博客实体" });

            _context.PublishedNotes.Remove(note);
            await _context.SaveChangesAsync();
            return Ok(new { message = "博客已彻底移除" });
        }
        /// <summary>
        /// 深层干涉：更新画廊作品特征与流转状态
        /// </summary>
        [HttpPut("gallery/{id}/governance")]
        public async Task<IActionResult> UpdateGalleryGovernance(string id, [FromBody] GalleryGovernanceDto dto)
        {
            // 前端传来的 ID 可能是 "GAL-000012"，需要提取真实数字 ID
            var rawIdStr = id.Replace("GAL-", "");
            if (!int.TryParse(rawIdStr, out int realId))
            {
                return BadRequest(new { message = "实体追踪码格式异常" });
            }

            var artwork = await _context.Artworks.FindAsync(realId);
            if (artwork == null)
            {
                return NotFound(new { message = "未捕捉到该画廊实体" });
            }

            // 覆写流量特征与状态
            artwork.ViewCount = dto.Views;
            artwork.LikesCount = dto.Likes;
            artwork.Status = dto.Status;
            artwork.IsFeatured = dto.IsFeatured;

            // 联动：如果状态为驳回，你可以选择同步修改 IsApproved 为 false
            artwork.IsApproved = dto.Status == "published";

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "指令已广播至太初域" });
            }
            catch (DbUpdateException)
            {
                return StatusCode(500, new { message = "矩阵数据同步失败，请重试" });
            }
        }


        /// <summary>
        /// 极度危险：彻底从太初矩阵中抹除画廊实体
        /// </summary>
        [HttpDelete("gallery/{id}")]
        public async Task<IActionResult> DeleteGalleryWork(string id)
        {
            // 剥离前端伪装的前缀 "GAL-"
            var rawIdStr = id.Replace("GAL-", "");
            if (!int.TryParse(rawIdStr, out int realId))
            {
                return BadRequest(new { message = "实体追踪码格式异常" });
            }

            var artwork = await _context.Artworks.FindAsync(realId);
            if (artwork == null)
            {
                return NotFound(new { message = "未捕捉到该画廊实体" });
            }

            // 彻底移除
            _context.Artworks.Remove(artwork);

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "画廊实体已从矩阵中彻底抹除" });
            }
            catch (DbUpdateException)
            {
                return StatusCode(500, new { message = "矩阵数据同步失败，请重试" });
            }
        }


    }
    // ✅ 必须在这个 namespace 内部！
    public class BlogGovernanceDto
    {
        public string Title { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public int ReadCount { get; set; } // 你之前可能写的是这个

        // ✅ 必须添加下面这一行，解决 CS1061 错误
        public int Resonance { get; set; }
    }
    /// <summary>
    /// 数据传输对象：接收前端的干涉表单
    /// </summary>
    public class GalleryGovernanceDto
    {
        public int Views { get; set; }
        public int Likes { get; set; }
        public bool IsFeatured { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}