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


            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var stats = await _context.UserStats.FirstOrDefaultAsync(s => s.UserId == userId);
                if (stats == null) return NotFound("未找到用户统计信息");

                var today = DateTime.UtcNow.Date;
                var yesterday = today.AddDays(-1);

                if (stats.LastSignDate.HasValue && stats.LastSignDate.Value.Date == today)
                {
                    return BadRequest(new { message = "今日已经签过到了，太勤奋了道友！" });
                }

                // --- 核心逻辑：计算连签天数 ---
                if (stats.LastSignDate.HasValue && stats.LastSignDate.Value.Date == yesterday)
                {
                    stats.CurrentSignStreak += 1;
                }
                else
                {
                    stats.CurrentSignStreak = 1; // 断签或首次签到，重置为 1
                }

                // --- 核心逻辑：阶梯式经验奖励 ---
                // 第1天: 50, 第2天: 70, 第3天: 100, 第4天: 140, 第5天: 190, 第6天: 250, 第7天及以后: 320
                int experienceBonus = stats.CurrentSignStreak switch
                {
                    1 => 50,
                    2 => 70,
                    3 => 100,
                    4 => 140,
                    5 => 190,
                    6 => 250,
                    _ => 320 // 7天及以上固定最高加成
                };

                // 更新最高纪录
                if (stats.CurrentSignStreak > stats.MaxSignStreak)
                {
                    stats.MaxSignStreak = stats.CurrentSignStreak;
                }

                // 5. 发放修为与状态更新
                stats.Experience += experienceBonus; // 增加对应阶梯的经验
                stats.LastSignDate = today;

                // 6. 插入日志
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
                    message = stats.CurrentSignStreak > 1
                        ? $"连签第{stats.CurrentSignStreak}日，经验大涨 {experienceBonus}！"
                        : $"签到成功，获得经验 {experienceBonus}",
                    experienceAdded = experienceBonus, // 返回本次增加的具体数值供前端组件显示
                    currentStreak = stats.CurrentSignStreak,
                    maxStreak = stats.MaxSignStreak
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "系统开小差了", detail = ex.Message });
            }
        }
    }
}