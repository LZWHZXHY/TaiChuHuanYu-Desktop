using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.User;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _context;

        public UserController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

            var user = await _context.Users
                .Include(u => u.Profile)
                .Include(u => u.Stats)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound("用户不存在");

            return Ok(new
            {
                user.Username,
                user.Email,
                user.CreatedAt,

                // --- Profile 数据 ---
                Avatar = user.Profile?.Avatar,
                // 核心修复点：将 ?? 0 改为 ?? "未知"
                Gender = user.Profile?.Gender ?? "未知",
                Address = user.Profile?.Address,
                Bio = user.Profile?.Bio,             // 别忘了加上新字段
                Mood = user.Profile?.Mood,           // 别忘了加上新字段
                SocialLinks = user.Profile?.SocialLinks, // 别忘了加上新字段
                Zodiac = user.Profile?.Zodiac,
                ChineseZodiac = user.Profile?.ChineseZodiac,
                Birthday = user.Profile?.Birthday,
                Age = user.Profile?.Age ?? 0,
                // --- Stats 数据 ---
                Level = user.Stats?.Level ?? 1,
                Experience = user.Stats?.Experience ?? 0,
                Points = user.Stats?.Points ?? 0,
                MaxSignStreak = user.Stats?.MaxSignStreak ?? 0,
                Title = user.Stats?.Title
            });
        }

        [HttpPatch("update-profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

            var user = await _context.Users
                .Include(u => u.Profile)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null) return NotFound("用户不存在");

            if (user.Profile == null)
            {
                user.Profile = new TaiChuWeb_V2.Models.User.UserProfile { UserId = userId };
                _context.UserProfiles.Add(user.Profile);
            }

            // --- 开始按需更新（卸货） ---
            if (!string.IsNullOrEmpty(dto.Avatar)) user.Profile.Avatar = dto.Avatar;
            if (!string.IsNullOrEmpty(dto.Address)) user.Profile.Address = dto.Address;

            // 补全以下新字段的映射
            user.Profile.Gender = dto.Gender;
            user.Profile.Bio = dto.Bio;
            user.Profile.Mood = dto.Mood;
            user.Profile.SocialLinks = dto.SocialLinks;
            user.Profile.Birthday = dto.Birthday;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { message = "个人资料更新成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, $"数据库同步失败: {ex.Message}");
            }
        }

    }
}