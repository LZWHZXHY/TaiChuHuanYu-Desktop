using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers.Users
{
    [Authorize] // 必须登录
    [ApiController]
    [Route("api/Users/Settings")] // 路由匹配前端的 '/api/Users/Settings'
    public class UserSettingsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UserSettingsController(AppDbContext context)
        {
            _context = context;
        }

        // GET: api/User/Settings
        [HttpGet]
        public async Task<ActionResult<UserSettingsDto>> GetSettings()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized("无法识别用户身份");
            }

            var settings = await _context.UserSettings
                .FirstOrDefaultAsync(s => s.UserId == userId);

            // 如果用户还没有设置记录，返回默认值
            if (settings == null)
            {
                return Ok(new UserSettingsDto
                {
                    ReceiveUpdateEmail = true,
                    ReceiveActivityEmail = false,
                    WeeklyReport = true
                });
            }

            return Ok(new UserSettingsDto
            {
                ReceiveUpdateEmail = settings.ReceiveUpdateEmail,
                ReceiveActivityEmail = settings.ReceiveActivityEmail,
                WeeklyReport = settings.WeeklyReport
            });
        }

        // PUT: api/User/Settings
        [HttpPut]
        public async Task<IActionResult> UpdateSettings([FromBody] UserSettingsDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdStr) || !Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized("无法识别用户身份");
            }

            // 查出用户以及关联的设置
            var user = await _context.Users
                .Include(u => u.Settings)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return NotFound("未找到该用户");
            }

            // 如果这是老用户，之前没有设置记录，则新建一条
            if (user.Settings == null)
            {
                user.Settings = new UserSettings { UserId = userId };
                
            }

            // 更新偏好
            user.Settings.ReceiveUpdateEmail = dto.ReceiveUpdateEmail;
            user.Settings.ReceiveActivityEmail = dto.ReceiveActivityEmail;
            user.Settings.WeeklyReport = dto.WeeklyReport;

            await _context.SaveChangesAsync();

            return Ok(new { message = "偏好设置已成功保存" });
        }
    }

    // 定义用于前后端传输的 DTO (Data Transfer Object)
    public class UserSettingsDto
    {
        public bool ReceiveUpdateEmail { get; set; }
        public bool ReceiveActivityEmail { get; set; }
        public bool WeeklyReport { get; set; }
    }
}