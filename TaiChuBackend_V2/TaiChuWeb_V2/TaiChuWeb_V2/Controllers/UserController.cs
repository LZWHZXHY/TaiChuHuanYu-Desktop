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

            // --- Stats 数据 (已移除 Points) ---
            Level = user.Stats?.Level ?? 0,
            Experience = user.Stats?.Experience ?? 0, // 现在的“修为”总额
                                                      // Points = ... 👈 这一行已经被彻底抹除
            MaxSignStreak = user.Stats?.MaxSignStreak ?? 0,
            Title = user.Stats?.Title
        });
    }

    [HttpPatch("update-profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

        // 优化：直接查 Profile 效率更高
        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

        // 如果用户主表存在但 Profile 还没创建（虽然注册时你应该已经初始化了，但这里做个保险）
        if (profile == null)
        {
            profile = new UserProfile { UserId = userId };
            _context.UserProfiles.Add(profile);
        }

        // --- 开始更新字段 ---
        // 注意：如果你希望用户能把某个字段改为空，就不能用 string.IsNullOrEmpty 判断
        // 这里采用覆盖式更新，或者你可以根据业务需求判断是否为 null
        profile.Avatar = dto.Avatar ?? profile.Avatar;
        profile.Address = dto.Address ?? profile.Address;
        profile.PhoneNumber = dto.PhoneNumber ?? profile.PhoneNumber; // 新增
        profile.Gender = dto.Gender ?? profile.Gender;
        profile.Bio = dto.Bio ?? profile.Bio;
        profile.Mood = dto.Mood ?? profile.Mood;
        profile.SocialLinks = dto.SocialLinks ?? profile.SocialLinks;
        profile.Birthday = dto.Birthday ?? profile.Birthday;
        profile.ExtraConfig = dto.ExtraConfig ?? profile.ExtraConfig; // 新增

        try
        {
            await _context.SaveChangesAsync();

            // 建议返回更新后的数据，方便前端刷新状态
            return Ok(new
            {
                message = "寰宇档案已重塑",
                // 返回计算后的新数据，前端不用刷新页面就能看到星座/年龄变化
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