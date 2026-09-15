using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers.Club
{
    /// <summary>
    /// 俱乐部公开统计接口
    /// 路由前缀：api/club
    /// </summary>
    [ApiController]
    [Route("api/club")]
    public class PublicClubController : ControllerBase
    {
        private readonly AppDbContext _db;
        public PublicClubController(AppDbContext db) => _db = db;

        /// <summary>首页运营看板统计</summary>
        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            // 已认证打手
            var operatorCount = await _db.OperatorProfiles
                .AsNoTracking()
                .CountAsync(p => p.AuditStatus == "approved");

            // 上架游戏
            var gameCount = await _db.ClubGames
                .AsNoTracking()
                .CountAsync(g => g.IsActive);

            // 已完成订单
            var completedOrders = await _db.ClubOrders
                .AsNoTracking()
                .CountAsync(o => o.Status == "completed");

            // 订单总数
            var totalOrders = await _db.ClubOrders
                .AsNoTracking()
                .CountAsync();

            // 组织运行天数：从最早的打手注册 / 订单创建算起
            var earliestOperator = await _db.OperatorProfiles
                .AsNoTracking()
                .OrderBy(p => p.AppliedAt)
                .Select(p => (DateTime?)p.AppliedAt)
                .FirstOrDefaultAsync();

            var earliestOrder = await _db.ClubOrders
                .AsNoTracking()
                .OrderBy(o => o.CreatedAt)
                .Select(o => (DateTime?)o.CreatedAt)
                .FirstOrDefaultAsync();

            var anchors = new List<DateTime>();
            if (earliestOperator.HasValue) anchors.Add(earliestOperator.Value);
            if (earliestOrder.HasValue) anchors.Add(earliestOrder.Value);

            int daysActive = 1;
            if (anchors.Count > 0)
            {
                var start = anchors.Min();
                daysActive = Math.Max(1, (int)(DateTime.UtcNow - start).TotalDays + 1);
            }

            return Ok(new
            {
                daysActive,
                operatorCount,
                gameCount,
                completedOrders,
                totalOrders
            });
        }
    }
}