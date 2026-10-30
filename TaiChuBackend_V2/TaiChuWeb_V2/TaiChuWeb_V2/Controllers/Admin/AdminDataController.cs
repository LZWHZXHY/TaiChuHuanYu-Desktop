using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Analytics;

namespace TaiChuWeb_V2.Controllers.Admin
{
    /// <summary>
    /// 旧的核心指标接口（注册总数、日活 / 周活 / 月活）。现在管理面板「数据」页用的是 AdminAnalyticsController，
    /// 这里保留给可能还在调用的地方，但算法已经和「数据」页统一：
    /// 活跃 = 当天在网站上出现过（analytics_user_days），不是「签到过」；日期按北京时间分天。
    /// 权限：System_Monitor（AdminAccessFilter）。
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/Admin/Data")]
    public class AdminDataController(AppDbContext context, AnalyticsStore store, PresenceTracker tracker) : ControllerBase
    {
        [HttpGet("Metrics")]
        public async Task<IActionResult> GetCoreMetrics()
        {
            await store.FlushAsync(tracker);
            var today = ChinaClock.Today;
            var weekStartUtc = ChinaClock.DayStartUtc(today.AddDays(-6));

            var totalUsers = await context.Users.CountAsync();
            var newUsersThisWeek = await context.Users.CountAsync(u => u.CreatedAt >= weekStartUtc);

            // 今天 vs 昨天；最近 7 天 vs 之前 7 天；最近 30 天 vs 之前 30 天（都是不重复的人数）
            var dauToday = await store.UniqueActiveAsync(today, today);
            var dauYesterday = await store.UniqueActiveAsync(today.AddDays(-1), today.AddDays(-1));
            var wau = await store.UniqueActiveAsync(today.AddDays(-6), today);
            var wauPrev = await store.UniqueActiveAsync(today.AddDays(-13), today.AddDays(-7));
            var mau = await store.UniqueActiveAsync(today.AddDays(-29), today);
            var mauPrev = await store.UniqueActiveAsync(today.AddDays(-59), today.AddDays(-30));

            return Ok(new
            {
                totalUsers = new { value = totalUsers, trendValue = newUsersThisWeek, isUp = true },
                dau = new { value = dauToday, trendPercent = CalculateTrend(dauToday, dauYesterday), isUp = dauToday >= dauYesterday },
                wau = new { value = wau, trendPercent = CalculateTrend(wau, wauPrev), isUp = wau >= wauPrev },
                mau = new { value = mau, trendPercent = CalculateTrend(mau, mauPrev), isUp = mau >= mauPrev },
                trackingSince = (await store.TrackingSinceAsync())?.ToString("yyyy-MM-dd"),
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
