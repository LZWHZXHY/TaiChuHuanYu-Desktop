using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization; // 引入授权
using System.Security.Claims;             // 引入 Claims (用于获取当前登录用户ID)
using System;
using System.Linq;
using System.Threading.Tasks;

// 💡 确保这里是你项目实际的 DbContext、Models 和 Dtos 命名空间
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Feedback;
using TaiChuWeb_V2.Dtos.Feedback;

namespace TaiChuWeb_V2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FeedbackController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FeedbackController(AppDbContext context)
        {
            _context = context;
        }

        // ==============================================
        // 1. 用户提交反馈 (拦截经验值不足的用户)
        // ==============================================
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> SubmitFeedback([FromBody] CreateFeedbackDto dto)
        {
            // 🌟 1. 获取当前登录用户的 ID (string) 并转换为 Guid
            var userIdStr = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userIdGuid))
            {
                return Unauthorized(new { code = 401, message = "请先登录或登录状态异常" });
            }

            // 🌟 2. 去 UserStats 表里查询当前用户的经验值
            var userStats = await _context.UserStats.FirstOrDefaultAsync(us => us.UserId == userIdGuid);

            if (userStats == null)
            {
                return Unauthorized(new { code = 401, message = "用户数据不存在" });
            }

            // 🌟 3. 校验 Experience 字段 (经验值满 300 才可提交)
            if (userStats.Experience < 300)
            {
                return StatusCode(403, new { code = 403, message = "抱歉，只有经验值达到 300 及以上的用户才能发表意见" });
            }

            // 4. 校验反馈内容
            if (string.IsNullOrWhiteSpace(dto.Content))
            {
                return BadRequest(new { code = 400, message = "反馈内容不能为空" });
            }

            // 5. 创建实体并保存
            var feedback = new Feedback
            {
                Content = dto.Content,
                ContactInfo = dto.ContactInfo,
                UserId = userIdStr,
                ImageUrls = dto.Images != null && dto.Images.Any() ? string.Join(",", dto.Images) : null,
                IsAnonymous = dto.IsAnonymous, // 🌟 接收前端传来的匿名开关
                CreatedAt = DateTime.UtcNow
            };

            _context.Feedbacks.Add(feedback);
            await _context.SaveChangesAsync();

            return Ok(new { code = 200, data = feedback.Id, message = "提交成功，感谢您的反馈！" });
        }

        // ==============================================
        // 2. 获取公示反馈 (🌟 前端列表专用，自带物理脱敏功能)
        // ==============================================
        [HttpGet("public")]
        public async Task<IActionResult> GetPublicFeedbacks()
        {
            // 使用 Select 在数据库层面直接进行脱敏过滤，安全且性能极高
            var feedbacks = await _context.Feedbacks
                .OrderByDescending(f => f.CreatedAt)
                .Select(f => new
                {
                    Id = f.Id,
                    Content = f.Content,
                    ImageUrls = f.ImageUrls,
                    Status = f.Status,
                    CreatedAt = f.CreatedAt,
                    IsAnonymous = f.IsAnonymous,

                    // 🚨 核心防御：如果用户选择了匿名，联系方式和用户ID直接抹除变 null
                    ContactInfo = f.IsAnonymous ? null : f.ContactInfo,
                    UserId = f.IsAnonymous ? null : f.UserId
                })
                .ToListAsync();

            return Ok(new { code = 200, data = feedbacks, message = "success" });
        }

        // ==============================================
        // 3. 获取所有反馈 (🚨 管理后台专用，返回所有真实数据)
        // ==============================================
        [HttpGet]
        // [Authorize(Roles = "Admin")] // 💡 建议日后给管理接口加权限限制
        public async Task<IActionResult> GetAllFeedbacks()
        {
            var feedbacks = await _context.Feedbacks
                .OrderByDescending(f => f.CreatedAt)
                .ToListAsync();

            return Ok(new { code = 200, data = feedbacks, message = "success" });
        }

        // ==============================================
        // 4. 更新反馈处理状态 (管理后台使用)
        // ==============================================
        [HttpPatch("{id}/status")]
        // [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateFeedbackStatus(string id, [FromBody] UpdateFeedbackStatusRequest req)
        {
            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback == null)
            {
                return NotFound(new { code = 404, message = "未找到该反馈" });
            }

            feedback.Status = req.Status; // 0 = 待处理, 1 = 已解决
            await _context.SaveChangesAsync();

            return Ok(new { code = 200, data = feedback.Id, message = "状态更新成功" });
        }

        // ==============================================
        // 5. 删除反馈 (管理后台使用)
        // ==============================================
        [HttpDelete("{id}")]
        // [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteFeedback(string id)
        {
            var feedback = await _context.Feedbacks.FindAsync(id);
            if (feedback == null)
            {
                return NotFound(new { code = 404, message = "未找到该反馈" });
            }

            _context.Feedbacks.Remove(feedback);
            await _context.SaveChangesAsync();

            return Ok(new { code = 200, data = id, message = "删除成功" });
        }
    }

    /// <summary>
    /// 用于接收反馈状态局部更新的请求体
    /// </summary>
    public class UpdateFeedbackStatusRequest
    {
        public int Status { get; set; }
    }
}