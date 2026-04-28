using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers
{
    [Authorize] // 只有登录用户可以访问
    [Route("api/[controller]")]
    [ApiController]
    public class SignController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SignController(AppDbContext context)
        {
            _context = context;
        }


        [HttpGet("month-data")]
        public async Task<IActionResult> GetMonthData(int year, int month)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            var userId = Guid.Parse(userIdClaim);

          
            var logs = await _context.UserSignLogs
                .Where(l => l.UserId == userId && l.SignDate.Year == year && l.SignDate.Month == month)
                .Select(l => new { l.SignDate, l.Type })
                .ToListAsync();


            var result = logs.ToDictionary(
                l => l.SignDate.ToString("yyyy-MM-dd"),
                l => l.Type
            );

            return Ok(result);
        }


        [HttpPost("do-sign")]
        public async Task<IActionResult> DoSign()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            var userId = Guid.Parse(userIdClaim);

            // 获取服务器当前日期（不含时分秒）
            var today = DateTime.UtcNow.Date;
            var yesterday = today.AddDays(-1);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. 获取用户统计数据（签到的核心依赖）
                var stats = await _context.UserStats.FirstOrDefaultAsync(s => s.UserId == userId);
                if (stats == null) return NotFound("未找到用户统计信息");

                // 2. 判定是否今日已签到
                // 直接利用 stats 表里的 LastSignDate 判定，比查日志表快得多
                if (stats.LastSignDate.HasValue && stats.LastSignDate.Value.Date == today)
                {
                    return BadRequest(new { message = "今日已经签过到了，太勤奋了道友！" });
                }

                // 3. 计算连续签到逻辑
                if (stats.LastSignDate.HasValue && stats.LastSignDate.Value.Date == yesterday)
                {
                    // 昨天签了，连签 +1
                    stats.CurrentSignStreak += 1;
                }
                else
                {
                    // 昨天没签（断签了），重置为 1
                    stats.CurrentSignStreak = 1;
                }

                // 4. 更新历史最高纪录
                if (stats.CurrentSignStreak > stats.MaxSignStreak)
                {
                    stats.MaxSignStreak = stats.CurrentSignStreak;
                }

                // 5. 奖励发放与状态更新
                stats.Points += 10;
                stats.Experience += 5;
                stats.LastSignDate = today; // 记录本次签到日期

                // 6. 插入签到日志（用于日历展示）
                var signLog = new UserSignLog
                {
                    UserId = userId,
                    SignDate = today,
                    Type = 1,
                    CreatedAt = DateTime.UtcNow
                };
                _context.UserSignLogs.Add(signLog);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    message = "签到成功",
                    currentStreak = stats.CurrentSignStreak,
                    maxStreak = stats.MaxSignStreak,
                    pointsAdded = 10
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "签到失败", detail = ex.Message });
            }
        }
    }
}