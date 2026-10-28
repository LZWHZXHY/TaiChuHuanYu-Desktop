using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization; // 引入授权（后续加权限用）
using System;
using System.Linq;
using System.Threading.Tasks;

// 💡 注意：请确保这三个命名空间和你实际项目中的一致！
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.News;
using TaiChuWeb_V2.Dtos.News;

namespace TaiChuWeb_V2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class NewsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public NewsController(AppDbContext context)
        {
            _context = context;
        }
        // ==============================================
        // 5. 更新动态 (管理后台用)
        // ==============================================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateNews(string id, [FromBody] CreateNewsDto dto)
        {
            var news = await _context.News.FindAsync(id);
            if (news == null)
            {
                return NotFound(new { code = 404, message = "未找到该动态" });
            }

            // 更新字段
            news.Title = dto.Title;
            news.Type = dto.Type;
            news.ImageUrl = dto.ImageUrl;
            news.Content = dto.Content;
            news.NoticeKind = string.IsNullOrWhiteSpace(dto.NoticeKind) ? news.NoticeKind : dto.NoticeKind;
            news.Importance = string.IsNullOrWhiteSpace(dto.Importance) ? news.Importance : dto.Importance;
            news.ExpireAt = dto.ExpireAt;
            // 也可以选择在这里不更新 CreatedAt，保持原始发布时间

            await _context.SaveChangesAsync();

            return Ok(new { code = 200, data = news.Id, message = "更新成功" });
        }
        // ==============================================
        // 1. 获取所有动态 (管理后台 & 前端展示用)
        // ==============================================
        [HttpGet]
        public async Task<IActionResult> GetAllNews()
        {
            var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
            var canManage = Guid.TryParse(userId, out var id) && await _context.UserPermissions
                .AnyAsync(p => p.UserId == id && p.Permission == TaiChuWeb_V2.Models.User.AdminPermission.SuperAdmin);
            // 按创建时间倒序排列，最新发布的排在最前面
            // 非管理员看不到「已到期的定时公告」
            var now = DateTime.UtcNow;

            // 定时公告到期 → 真正下架（写回 IsPublished=false），
            // 否则管理端会一直显示「公开中」。通常 0 行受影响，开销极小。
            await _context.News
                .Where(n => n.IsPublished && n.NoticeKind == "scheduled" && n.ExpireAt != null && n.ExpireAt <= now)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.IsPublished, false));

            var newsList = await _context.News
                .Where(n => canManage || (n.IsPublished && (n.NoticeKind != "scheduled" || n.ExpireAt == null || n.ExpireAt > now)))
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            // 返回标准的 JSON 格式： { code: 200, data: [...], message: "success" }
            return Ok(new { code = 200, data = newsList, message = "success" });
        }

        // ==============================================
        // 2. 创建新动态 (管理后台用)
        // ==============================================
        [HttpPost]
        // [Authorize(Roles = "Admin")] // 💡 强烈建议以后把这行注释解开，仅限管理员发布
        public async Task<IActionResult> CreateNews([FromBody] CreateNewsDto dto)
        {
            // 基础防呆校验
            if (string.IsNullOrWhiteSpace(dto.Title))
            {
                return BadRequest(new { code = 400, message = "动态标题不能为空" });
            }

            // 创建实体
            var news = new News
            {
                Title = dto.Title,
                Type = dto.Type ?? "公告",
                ImageUrl = dto.ImageUrl,
                Content = dto.Content,
                NoticeKind = string.IsNullOrWhiteSpace(dto.NoticeKind) ? "persistent" : dto.NoticeKind,
                Importance = string.IsNullOrWhiteSpace(dto.Importance) ? "normal" : dto.Importance,
                ExpireAt = dto.ExpireAt,
                IsPublished = true, // 默认直接发布
                CreatedAt = DateTime.UtcNow
            };

            _context.News.Add(news);
            await _context.SaveChangesAsync();

            return Ok(new { code = 200, data = news.Id, message = "发布成功！" });
        }

        // ==============================================
        // 3. 删除动态 (管理后台用)
        // ==============================================
        [HttpDelete("{id}")]
        // [Authorize(Roles = "Admin")] // 💡 仅限管理员删除
        public async Task<IActionResult> DeleteNews(string id)
        {
            var news = await _context.News.FindAsync(id);
            if (news == null)
            {
                return NotFound(new { code = 404, message = "未找到该动态" });
            }

            _context.News.Remove(news);
            await _context.SaveChangesAsync();

            return Ok(new { code = 200, data = id, message = "删除成功" });
        }

        // ==============================================
        // 4. 修改发布状态 (预留：上架/下架草稿箱功能)
        // ==============================================
        [HttpPatch("{id}/publish")]
        // [Authorize(Roles = "Admin")]
        public async Task<IActionResult> TogglePublishStatus(string id, [FromBody] UpdatePublishStatusRequest req)
        {
            var news = await _context.News.FindAsync(id);
            if (news == null)
            {
                return NotFound(new { code = 404, message = "未找到该动态" });
            }

            news.IsPublished = req.IsPublished;
            await _context.SaveChangesAsync();

            return Ok(new { code = 200, data = news.Id, message = "状态更新成功" });
        }
    }

    /// <summary>
    /// 用于接收切换“发布/草稿”状态的请求体
    /// </summary>
    public class UpdatePublishStatusRequest
    {
        public bool IsPublished { get; set; }
    }
}
