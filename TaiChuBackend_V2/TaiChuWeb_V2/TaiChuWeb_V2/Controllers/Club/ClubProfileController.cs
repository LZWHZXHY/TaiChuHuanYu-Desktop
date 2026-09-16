using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers.Club
{
    /// <summary>
    /// 个人中心聚合接口
    /// 路由前缀：api/club/profile
    /// </summary>
    [ApiController]
    [Route("api/club/profile")]
    [Authorize]
    public class ClubProfileController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ClubProfileController(AppDbContext db) => _db = db;

        // ==================================================
        //  个人中心总览：老板侧 + 打手侧
        // ==================================================
        /// <summary>
        /// GET /api/club/profile/summary
        /// </summary>
        [HttpGet("summary")]
        public async Task<IActionResult> GetSummary()
        {
            var userId = GetUserId();

            // ================= 老板侧 =================
            var bossOrders = await _db.ClubOrders
                .AsNoTracking()
                .Where(o => o.CustomerId == userId)
                .Select(o => new { o.Status, o.Price })
                .ToListAsync();

            var boss = new
            {
                totalOrders = bossOrders.Count,
                completedOrders = bossOrders.Count(o => o.Status == "completed"),
                cancelledOrders = bossOrders.Count(o => o.Status == "cancelled"),
                processingOrders = bossOrders.Count(o =>
                    o.Status == "pending" ||
                    o.Status == "accepted" ||
                    o.Status == "processing"),
                totalSpent = bossOrders
                    .Where(o => o.Status == "completed")
                    .Sum(o => o.Price)
            };

            // ================= 打手侧 =================
            var profile = await _db.OperatorProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            object? op = null;

            if (profile != null)
            {
                var skills = await _db.OperatorGameSkills
                    .AsNoTracking()
                    .Where(s => s.UserId == userId)
                    .OrderBy(s => s.AppliedAt)
                    .ToListAsync();

                op = new
                {
                    nickname = profile.Nickname,
                    contactType = profile.ContactType,
                    contactValue = profile.ContactValue,
                    onlineTime = profile.OnlineTime,
                    intro = profile.Intro,
                    auditStatus = profile.AuditStatus,
                    reputation = profile.Reputation,
                    totalOrders = profile.TotalOrders,
                    completedOrders = profile.CompletedOrders,
                    cancelledOrders = profile.CancelledOrders,
                    uniqueCustomers = profile.UniqueCustomers,
                    repeatCustomers = profile.RepeatCustomers,
                    reviewCount = profile.ReviewCount,
                    avgOverall = Math.Round(profile.AvgOverall, 2),
                    avgSkill = Math.Round(profile.AvgSkill, 2),
                    avgAttitude = Math.Round(profile.AvgAttitude, 2),
                    avgPunctual = Math.Round(profile.AvgPunctual, 2),
                    games = skills.Select(s => new
                    {
                        gameCode = s.GameCode,
                        gameName = s.GameName,
                        level = s.OperatorLevel,
                        code = s.Code,
                        auditStatus = s.AuditStatus,
                        recheckStatus = s.RecheckStatus,
                        ordersInGame = s.OrdersInGame,
                        completedOrdersInGame = s.CompletedOrdersInGame,
                        failedOrdersInGame = s.FailedOrdersInGame
                    }).ToList()
                };
            }

            return Ok(new { boss, @operator = op });
        }

        // ==================================================
        //  内部
        // ==================================================
        private Guid GetUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? throw new UnauthorizedAccessException();
            return Guid.Parse(sub);
        }
    }
}