using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.User;
using TaiChuWeb_V2.Models.User;
using Microsoft.EntityFrameworkCore;

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


    // 把它加到你现有的 UserController 里
    [HttpGet("search")]
    public async Task<IActionResult> SearchGlobalUsers([FromQuery] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return Ok(new List<object>());

        var users = await _context.Users
            .Where(u => u.Username.Contains(keyword))
            .Take(10)
            .Select(u => new
            {
                id = u.Id,
                username = u.Username,
                email = u.Email
            })
            .ToListAsync();

        return Ok(users);
    }







    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

        // 🌟 联查 User、Profile 和 Stats
        var user = await _context.Users
            .Include(u => u.Profile)
            .Include(u => u.Stats)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound("用户不存在");

        // 🌟 核心修复：直接通过 Projects 表的 OwnerId 统计当前用户作为所有者且未封存（Status != 3）的活跃项目数
        // 彻底解决 CS1061 报错，不再依赖 ProjectMember.RoleId
        var activeProjectCount = await _context.Projects
            .CountAsync(p => p.OwnerId == userId && p.Status != 3);

        return Ok(new
        {
            user.Username,
            user.Email,
            user.CreatedAt,

            // --- Profile 数据 ---
            Avatar = user.Profile?.Avatar,
            Gender = user.Profile?.Gender ?? "未知",
            Address = user.Profile?.Address,
            PhoneNumber = user.Profile?.PhoneNumber,
            Bio = user.Profile?.Bio,
            Mood = user.Profile?.Mood,
            SocialLinks = user.Profile?.SocialLinks,
            ExtraConfig = user.Profile?.ExtraConfig,

            Zodiac = user.Profile?.Zodiac,
            ChineseZodiac = user.Profile?.ChineseZodiac,
            Birthday = user.Profile?.Birthday,
            Age = user.Profile?.Age ?? 0,

            // --- Stats 数据 ---
            Level = user.Stats?.Level ?? 0,
            Experience = user.Stats?.Experience ?? 0,
            MaxSignStreak = user.Stats?.MaxSignStreak ?? 0,
            Title = user.Stats?.Title,

            // 🌟 灵脉编织额度载荷看板（无缝输送给前端）
            ProjectQuota = new
            {
                ActiveCount = activeProjectCount,                           // 已使用的活跃项目数
                MaxCount = user.Stats?.MaxProjectCount ?? 10,               // 基于 UserStats 表的上限额度（保底 10 个）
                AvailableCount = (user.Stats?.MaxProjectCount ?? 10) - activeProjectCount, // 剩余可用名额
                IsFull = activeProjectCount >= (user.Stats?.MaxProjectCount ?? 10)         // 额度是否已满
            }
        });
    }

    [HttpPatch("update-profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
        {
            profile = new UserProfile { UserId = userId };
            _context.UserProfiles.Add(profile);
        }

        // --- 覆盖式更新字段 ---
        profile.Avatar = dto.Avatar ?? profile.Avatar;
        profile.Address = dto.Address ?? profile.Address;
        profile.PhoneNumber = dto.PhoneNumber ?? profile.PhoneNumber;
        profile.Gender = dto.Gender ?? profile.Gender;
        profile.Bio = dto.Bio ?? profile.Bio;
        profile.Mood = dto.Mood ?? profile.Mood;
        profile.SocialLinks = dto.SocialLinks ?? profile.SocialLinks;
        profile.Birthday = dto.Birthday ?? profile.Birthday;
        profile.ExtraConfig = dto.ExtraConfig ?? profile.ExtraConfig;

        try
        {
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "寰宇档案已重塑",
                data = new
                {
                    profile.Zodiac,
                    profile.ChineseZodiac,
                    profile.Age
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "数据库同步失败", detail = ex.Message });
        }
    }
}