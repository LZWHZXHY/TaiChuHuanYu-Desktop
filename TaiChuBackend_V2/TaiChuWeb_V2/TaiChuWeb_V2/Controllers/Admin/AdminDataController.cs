using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers.Admin
{
    [Authorize] // 实际生产中建议加上权限校验，例如 [Authorize(Roles = "SuperAdmin,System_Monitor")]
    [ApiController]
    [Route("api/Admin/Data")]
    public class AdminDataController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminDataController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("Metrics")]
        public async Task<IActionResult> GetCoreMetrics()
        {
            var now = DateTime.UtcNow;
            var today = now.Date;

            // 定义时间节点
            var yesterday = today.AddDays(-1);
            var sevenDaysAgo = today.AddDays(-7);
            var fourteenDaysAgo = today.AddDays(-14);
            var thirtyDaysAgo = today.AddDays(-30);
            var sixtyDaysAgo = today.AddDays(-60);

            // 1. 注册用户总数 (总计与本周新增)
            var totalUsers = await _context.Users.CountAsync();
            var newUsersThisWeek = await _context.Users.CountAsync(u => u.CreatedAt >= sevenDaysAgo);

            // 2. DAU (日活：今日活跃 vs 昨日活跃)
            var dauToday = await _context.UserSignLogs
                .Where(l => l.SignDate >= today)
                .Select(l => l.UserId)
                .Distinct()
                .CountAsync();

            var dauYesterday = await _context.UserSignLogs
                .Where(l => l.SignDate >= yesterday && l.SignDate < today)
                .Select(l => l.UserId)
                .Distinct()
                .CountAsync();

            // 3. WAU (周活：近7天活跃 vs 上个7天活跃)
            var wauThisWeek = await _context.UserSignLogs
                .Where(l => l.SignDate >= sevenDaysAgo)
                .Select(l => l.UserId)
                .Distinct()
                .CountAsync();

            var wauLastWeek = await _context.UserSignLogs
                .Where(l => l.SignDate >= fourteenDaysAgo && l.SignDate < sevenDaysAgo)
                .Select(l => l.UserId)
                .Distinct()
                .CountAsync();

            // 4. MAU (月活：近30天活跃 vs 上个30天活跃)
            var mauThisMonth = await _context.UserSignLogs
                .Where(l => l.SignDate >= thirtyDaysAgo)
                .Select(l => l.UserId)
                .Distinct()
                .CountAsync();

            var mauLastMonth = await _context.UserSignLogs
                .Where(l => l.SignDate >= sixtyDaysAgo && l.SignDate < thirtyDaysAgo)
                .Select(l => l.UserId)
                .Distinct()
                .CountAsync();

            // 封装返回结果，自动计算百分比趋势
            return Ok(new
            {
                totalUsers = new
                {
                    value = totalUsers,
                    trendValue = newUsersThisWeek,
                    isUp = true
                },
                dau = new
                {
                    value = dauToday,
                    trendPercent = CalculateTrend(dauToday, dauYesterday),
                    isUp = dauToday >= dauYesterday
                },
                wau = new
                {
                    value = wauThisWeek,
                    trendPercent = CalculateTrend(wauThisWeek, wauLastWeek),
                    isUp = wauThisWeek >= wauLastWeek
                },
                mau = new
                {
                    value = mauThisMonth,
                    trendPercent = CalculateTrend(mauThisMonth, mauLastMonth),
                    isUp = mauThisMonth >= mauLastMonth
                }
            });
        }

        // 计算涨跌百分比的辅助方法
        private static double CalculateTrend(int current, int previous)
        {
            if (previous == 0) return current > 0 ? 100.0 : 0.0;
            var diff = current - previous;
            return Math.Round(Math.Abs((double)diff / previous * 100), 1);
        }
    }
}