using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Club;

namespace TaiChuWeb_V2.Controllers.Club
{
    /// <summary>订单评价（双向：老板 ↔ 打手）</summary>
    [ApiController]
    [Route("api/club")]
    [Authorize]
    public class OrderReviewController : ControllerBase
    {
        private readonly AppDbContext _db;
        public OrderReviewController(AppDbContext db) => _db = db;

        // ==================================================
        //  1. 提交评价
        // ==================================================
        [HttpPost("orders/{orderNo}/review")]
        public async Task<IActionResult> Submit(string orderNo, [FromBody] ReviewDto dto)
        {
            var userId = GetUserId();

            var order = await _db.ClubOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.CustomerId != userId && order.OperatorUserId != userId)
                return StatusCode(403, new { message = "无权评价该订单" });

            if (order.Status != "completed")
                return BadRequest(new { message = "订单未完成，无法评价" });

            if (!InRange(dto.SkillScore) || !InRange(dto.AttitudeScore)
                || !InRange(dto.PunctualScore) || !InRange(dto.OverallScore))
                return BadRequest(new { message = "评分必须在 1-5 之间" });

            var isCustomer = order.CustomerId == userId;
            var toUserId = isCustomer ? order.OperatorUserId : order.CustomerId;

            var exists = await _db.OrderReviews.AnyAsync(r =>
                r.OrderId == order.Id &&
                r.FromUserId == userId &&
                r.FromCustomer == isCustomer);
            if (exists)
                return BadRequest(new { message = "你已经评价过这个订单了" });

            var review = new OrderReview
            {
                OrderId = order.Id,
                FromUserId = userId,
                ToUserId = toUserId,
                FromCustomer = isCustomer,
                SkillScore = dto.SkillScore,
                AttitudeScore = dto.AttitudeScore,
                PunctualScore = dto.PunctualScore,
                OverallScore = dto.OverallScore,
                Comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _db.OrderReviews.Add(review);

            // 只有"老板评打手"才重算打手统计 + 调信誉
            if (isCustomer)
            {
                await RecalcOperatorStats(order.OperatorUserId);
                await AdjustReputation(order.OperatorUserId, dto.OverallScore);
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "评价已提交" });
        }

        // ==================================================
        //  2. 某订单的评价
        // ==================================================
        [HttpGet("orders/{orderNo}/reviews")]
        public async Task<IActionResult> GetOrderReviews(string orderNo)
        {
            var userId = GetUserId();

            var order = await _db.ClubOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.CustomerId != userId && order.OperatorUserId != userId)
                return StatusCode(403, new { message = "无权查看" });

            var reviews = await _db.OrderReviews
                .AsNoTracking()
                .Where(r => r.OrderId == order.Id)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync();

            var userIds = reviews.Select(r => r.FromUserId).Distinct().ToList();
            var users = await _db.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var items = reviews.Select(r => new
            {
                id = r.Id,
                fromUserId = r.FromUserId,
                fromCustomer = r.FromCustomer,
                fromUserName = users.TryGetValue(r.FromUserId, out var u)
                    ? (u.Username ?? "") : "",
                skillScore = r.SkillScore,
                attitudeScore = r.AttitudeScore,
                punctualScore = r.PunctualScore,
                overallScore = r.OverallScore,
                comment = r.Comment,
                createdAt = r.CreatedAt
            }).ToList();

            return Ok(items);
        }

        // ==================================================
        //  3. 待我评价的订单
        // ==================================================
        [HttpGet("orders/reviews/pending")]
        public async Task<IActionResult> GetPending()
        {
            var userId = GetUserId();

            var asBoss = await _db.ClubOrders
                .AsNoTracking()
                .Where(o => o.CustomerId == userId && o.Status == "completed")
                .Where(o => !_db.OrderReviews.Any(r =>
                    r.OrderId == o.Id && r.FromUserId == userId && r.FromCustomer))
                .OrderByDescending(o => o.CompletedAt)
                .Take(50)
                .ToListAsync();

            var asOperator = await _db.ClubOrders
                .AsNoTracking()
                .Where(o => o.OperatorUserId == userId && o.Status == "completed")
                .Where(o => !_db.OrderReviews.Any(r =>
                    r.OrderId == o.Id && r.FromUserId == userId && !r.FromCustomer))
                .OrderByDescending(o => o.CompletedAt)
                .Take(50)
                .ToListAsync();

            var all = asBoss.Concat(asOperator).ToList();
            var enriched = await EnrichBrief(all);

            var bossIds = asBoss.Select(o => o.Id).ToHashSet();

            return Ok(new
            {
                asBoss = enriched.Where(x => bossIds.Contains(x.orderId)).ToList(),
                asOperator = enriched.Where(x => !bossIds.Contains(x.orderId)).ToList()
            });
        }

        // ==================================================
        //  内部
        // ==================================================
        private async Task RecalcOperatorStats(Guid operatorUserId)
        {
            var profile = await _db.OperatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == operatorUserId);
            if (profile == null) return;

            var reviews = await _db.OrderReviews
                .AsNoTracking()
                .Where(r => r.ToUserId == operatorUserId && r.FromCustomer)
                .ToListAsync();

            profile.ReviewCount = reviews.Count;

            if (reviews.Count == 0)
            {
                profile.AvgSkill = 0;
                profile.AvgAttitude = 0;
                profile.AvgPunctual = 0;
                profile.AvgOverall = 0;
                return;
            }

            profile.AvgSkill = Math.Round(reviews.Average(r => r.SkillScore), 2);
            profile.AvgAttitude = Math.Round(reviews.Average(r => r.AttitudeScore), 2);
            profile.AvgPunctual = Math.Round(reviews.Average(r => r.PunctualScore), 2);
            profile.AvgOverall = Math.Round(reviews.Average(r => r.OverallScore), 2);
        }

        private async Task AdjustReputation(Guid operatorUserId, int overallScore)
        {
            var profile = await _db.OperatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == operatorUserId);
            if (profile == null) return;

            var delta = overallScore switch
            {
                5 => 1,
                4 => 0,
                3 => -2,
                2 => -5,
                1 => -10,
                _ => 0
            };

            profile.Reputation = Math.Clamp(profile.Reputation + delta, 0, 100);
        }

        private async Task<List<PendingItem>> EnrichBrief(List<ClubOrder> orders)
        {
            if (orders.Count == 0) return new();

            var gameCodes = orders.Select(o => o.GameCode).Distinct().ToList();
            var opIds = orders.Select(o => o.OperatorUserId).Distinct().ToList();
            var custIds = orders.Select(o => o.CustomerId).Distinct().ToList();

            var games = await _db.ClubGames.AsNoTracking()
                .Where(g => gameCodes.Contains(g.Code))
                .ToDictionaryAsync(g => g.Code);

            var profiles = await _db.OperatorProfiles.AsNoTracking()
                .Where(p => opIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId);

            var users = await _db.Users.AsNoTracking()
                .Where(u => custIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var types = await _db.ClubOrderTypes.AsNoTracking()
                .Where(t => gameCodes.Contains(t.GameCode))
                .ToListAsync();
            var typeMap = types.ToDictionary(t => $"{t.GameCode}|{t.Code}");

            return orders.Select(o =>
            {
                games.TryGetValue(o.GameCode, out var g);
                profiles.TryGetValue(o.OperatorUserId, out var p);
                users.TryGetValue(o.CustomerId, out var u);
                typeMap.TryGetValue($"{o.GameCode}|{o.OrderTypeCode}", out var t);

                return new PendingItem
                {
                    orderId = o.Id,
                    orderNo = o.OrderNo,
                    gameName = g?.Name ?? o.GameCode,
                    orderTypeName = t?.Name ?? o.OrderTypeCode,
                    price = o.Price,
                    completedAt = o.CompletedAt,
                    operatorName = p?.Nickname ?? "",
                    customerName = u?.Username ?? ""
                };
            }).ToList();
        }

        private static bool InRange(int s) => s >= 1 && s <= 5;

        private Guid GetUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? throw new UnauthorizedAccessException();
            return Guid.Parse(sub);
        }

        public class ReviewDto
        {
            public int SkillScore { get; set; }
            public int AttitudeScore { get; set; }
            public int PunctualScore { get; set; }
            public int OverallScore { get; set; }
            public string? Comment { get; set; }
        }

        private class PendingItem
        {
            public long orderId { get; set; }
            public string orderNo { get; set; } = "";
            public string gameName { get; set; } = "";
            public string orderTypeName { get; set; } = "";
            public decimal price { get; set; }
            public DateTime? completedAt { get; set; }
            public string operatorName { get; set; } = "";
            public string customerName { get; set; } = "";
        }
    }
}