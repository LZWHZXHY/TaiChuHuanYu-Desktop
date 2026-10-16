using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Club;

namespace TaiChuWeb_V2.Controllers.Club
{
    /// <summary>订单评价（仅老板评打手，支持多打手分开评）</summary>
    [ApiController]
    [Route("api/club")]
    [Authorize]
    public class OrderReviewController : ControllerBase
    {
        private readonly AppDbContext _db;
        public OrderReviewController(AppDbContext db) => _db = db;

        // ⭐ 评价有效期：订单完成后多少天内可评价
        private const int REVIEW_WINDOW_DAYS = 1;

        // ==================================================
        //  1. 提交评价（老板评打手）
        // ==================================================
        [HttpPost("orders/{orderNo}/review")]
        public async Task<IActionResult> Submit(string orderNo, [FromBody] ReviewDto dto)
        {
            var userId = GetUserId();

            var order = await _db.ClubOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            // ⭐ 只允许老板评价
            if (order.CustomerId != userId)
                return StatusCode(403, new { message = "只有下单老板才能评价" });

            if (order.Status != "completed")
                return BadRequest(new { message = "订单未完成，无法评价" });

            // ⭐ 评价有效期
            if (order.CompletedAt.HasValue)
            {
                var deadline = order.CompletedAt.Value.AddDays(REVIEW_WINDOW_DAYS);
                if (DateTime.UtcNow > deadline)
                    return BadRequest(new { message = $"评价已过期（订单完成后 {REVIEW_WINDOW_DAYS} 天内有效）" });
            }

            // 解析该订单的所有打手
            var opIds = ParseOperatorUserIds(order.OperatorUserIdsJson);
            if (opIds.Count == 0 && order.OperatorUserId != Guid.Empty)
                opIds.Add(order.OperatorUserId);

            if (opIds.Count == 0)
                return BadRequest(new { message = "该订单没有打手，无法评价" });

            // ⭐ 确定被评打手
            Guid toOperatorId;
            if (dto.ToOperatorUserId.HasValue && dto.ToOperatorUserId.Value != Guid.Empty)
            {
                if (!opIds.Contains(dto.ToOperatorUserId.Value))
                    return BadRequest(new { message = "被评打手不属于该订单" });
                toOperatorId = dto.ToOperatorUserId.Value;
            }
            else
            {
                if (opIds.Count > 1)
                    return BadRequest(new { message = "多打手订单请指定要评价的打手" });
                toOperatorId = opIds[0];
            }

            if (!InRange(dto.SkillScore) || !InRange(dto.AttitudeScore)
                || !InRange(dto.PunctualScore) || !InRange(dto.OverallScore))
                return BadRequest(new { message = "评分必须在 1-5 之间" });

            // ⭐ 唯一性：(OrderId, FromUserId, ToUserId) 三元组
            var exists = await _db.OrderReviews.AnyAsync(r =>
                r.OrderId == order.Id &&
                r.FromUserId == userId &&
                r.ToUserId == toOperatorId);
            if (exists)
                return BadRequest(new { message = "你已经评价过这位打手了" });

            var review = new OrderReview
            {
                OrderId = order.Id,
                FromUserId = userId,
                ToUserId = toOperatorId,
                FromCustomer = true,
                SkillScore = dto.SkillScore,
                AttitudeScore = dto.AttitudeScore,
                PunctualScore = dto.PunctualScore,
                OverallScore = dto.OverallScore,
                Comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _db.OrderReviews.Add(review);

            await RecalcOperatorStats(toOperatorId);
            await AdjustReputation(toOperatorId, dto.OverallScore);

            await _db.SaveChangesAsync();
            return Ok(new { message = "评价已提交", toOperatorId });
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

            var opIds = ParseOperatorUserIds(order.OperatorUserIdsJson);
            if (opIds.Count == 0 && order.OperatorUserId != Guid.Empty)
                opIds.Add(order.OperatorUserId);

            var isCustomer = order.CustomerId == userId;
            var isOperator = opIds.Contains(userId);
            if (!isCustomer && !isOperator)
                return StatusCode(403, new { message = "无权查看" });

            var reviews = await _db.OrderReviews
                .AsNoTracking()
                .Where(r => r.OrderId == order.Id)
                .OrderBy(r => r.CreatedAt)
                .ToListAsync();

            var userIds = reviews
                .Select(r => r.FromUserId)
                .Concat(reviews.Select(r => r.ToUserId))
                .Distinct()
                .ToList();

            var users = await _db.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var items = reviews.Select(r => new
            {
                id = r.Id,
                fromUserId = r.FromUserId,
                fromUserName = users.TryGetValue(r.FromUserId, out var fu) ? (fu.Username ?? "") : "",
                toUserId = r.ToUserId,
                toUserName = users.TryGetValue(r.ToUserId, out var tu) ? (tu.Username ?? "") : "",
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
        //  3. 待我评价的订单（每个未评打手独立一条）
        // ==================================================
        [HttpGet("orders/reviews/pending")]
        public async Task<IActionResult> GetPending()
        {
            var userId = GetUserId();

            var orders = await _db.ClubOrders
                .AsNoTracking()
                .Where(o => o.CustomerId == userId && o.Status == "completed")
                .Where(o => o.CompletedAt.HasValue
                         && o.CompletedAt.Value.AddDays(REVIEW_WINDOW_DAYS) >= DateTime.UtcNow)
                .OrderByDescending(o => o.CompletedAt)
                .Take(100)
                .ToListAsync();

            if (orders.Count == 0)
                return Ok(new { asBoss = Array.Empty<object>() });

            var orderIds = orders.Select(o => o.Id).ToList();

            var myReviews = await _db.OrderReviews
                .AsNoTracking()
                .Where(r => orderIds.Contains(r.OrderId) && r.FromUserId == userId)
                .Select(r => new { r.OrderId, r.ToUserId })
                .ToListAsync();

            var reviewedMap = myReviews
                .GroupBy(r => r.OrderId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.ToUserId).ToHashSet());

            var allOpIds = new HashSet<Guid>();
            foreach (var o in orders)
            {
                foreach (var id in ParseOperatorUserIds(o.OperatorUserIdsJson))
                    allOpIds.Add(id);
                if (o.OperatorUserId != Guid.Empty)
                    allOpIds.Add(o.OperatorUserId);
            }

            var profiles = await _db.OperatorProfiles.AsNoTracking()
                .Where(p => allOpIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId);

            var gameCodes = orders.Select(o => o.GameCode).Distinct().ToList();
            var games = await _db.ClubGames.AsNoTracking()
                .Where(g => gameCodes.Contains(g.Code))
                .ToDictionaryAsync(g => g.Code);

            var types = await _db.ClubOrderTypes.AsNoTracking()
                .Where(t => gameCodes.Contains(t.GameCode))
                .ToListAsync();
            var typeMap = types.ToDictionary(t => $"{t.GameCode}|{t.Code}");

            var result = new List<object>();
            foreach (var o in orders)
            {
                var opIds = ParseOperatorUserIds(o.OperatorUserIdsJson);
                if (opIds.Count == 0 && o.OperatorUserId != Guid.Empty)
                    opIds.Add(o.OperatorUserId);
                if (opIds.Count == 0) continue;

                reviewedMap.TryGetValue(o.Id, out var reviewed);
                reviewed ??= new HashSet<Guid>();

                games.TryGetValue(o.GameCode, out var g);
                typeMap.TryGetValue($"{o.GameCode}|{o.OrderTypeCode}", out var t);

                foreach (var opId in opIds)
                {
                    if (reviewed.Contains(opId)) continue;

                    profiles.TryGetValue(opId, out var p);

                    result.Add(new
                    {
                        orderId = o.Id,
                        orderNo = o.OrderNo,
                        gameName = g?.Name ?? o.GameCode,
                        orderTypeName = t?.Name ?? o.OrderTypeCode,
                        price = o.Price,
                        completedAt = o.CompletedAt,
                        operatorUserId = opId,
                        operatorName = p?.Nickname ?? "",
                        operatorCount = opIds.Count
                    });
                }
            }

            return Ok(new { asBoss = result, asOperator = Array.Empty<object>() });
        }

        // ==================================================
        //  3.5 我（老板）已提交的评价
        // ==================================================
        [HttpGet("orders/reviews/mine")]
        public async Task<IActionResult> GetMyReviews()
        {
            var userId = GetUserId();

            var rows = await (from r in _db.OrderReviews.AsNoTracking()
                              join o in _db.ClubOrders.AsNoTracking()
                                  on r.OrderId equals o.Id
                              where r.FromUserId == userId
                              select new
                              {
                                  orderNo = o.OrderNo,       // ⭐ 改 camelCase
                                  toUserId = r.ToUserId      // ⭐ 改 camelCase
                              })
                             .ToListAsync();

            return Ok(rows);
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

        private static bool InRange(int s) => s >= 1 && s <= 5;

        private static List<Guid> ParseOperatorUserIds(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<Guid>();
            try
            {
                var arr = JsonSerializer.Deserialize<List<Guid>>(json);
                return arr ?? new List<Guid>();
            }
            catch
            {
                return new List<Guid>();
            }
        }

        private Guid GetUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? throw new UnauthorizedAccessException();
            return Guid.Parse(sub);
        }

        // ==================================================
        //  DTO
        // ==================================================
        public class ReviewDto
        {
            /// <summary>⭐ 被评打手（多打手时必须指定）</summary>
            public Guid? ToOperatorUserId { get; set; }

            public int SkillScore { get; set; }
            public int AttitudeScore { get; set; }
            public int PunctualScore { get; set; }
            public int OverallScore { get; set; }
            public string? Comment { get; set; }
        }
    }
}