using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Club;

namespace TaiChuWeb_V2.Controllers.Club
{
    /// <summary>
    /// 客服评价（老板评客服）
    /// 路由前缀：api/club/cs
    /// </summary>
    [ApiController]
    [Route("api/club/cs")]
    [Authorize]
    public class CsReviewController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CsReviewController(AppDbContext db) => _db = db;

        // ==================================================
        //  1. 提交评价（老板评客服）
        // ==================================================
        [HttpPost("orders/{orderNo}/review")]
        public async Task<IActionResult> Submit(string orderNo, [FromBody] CsReviewDto dto)
        {
            var userId = GetUserId();

            var order = await _db.ClubOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.CustomerId != userId)
                return StatusCode(403, new { message = "只有下单老板才能评价客服" });

            if (order.Status != "completed")
                return BadRequest(new { message = "订单未完成，无法评价" });

            if (!order.CustomerServiceId.HasValue)
                return BadRequest(new { message = "该订单没有关联客服" });

            if (!InRange(dto.ResponseScore) || !InRange(dto.AttitudeScore)
                || !InRange(dto.ProfessionalScore) || !InRange(dto.OverallScore))
                return BadRequest(new { message = "评分必须在 1-5 之间" });

            var exists = await _db.CsReviews.AnyAsync(r =>
                r.OrderId == order.Id && r.FromUserId == userId);
            if (exists)
                return BadRequest(new { message = "你已经评价过这个订单的客服了" });

            var review = new CsReview
            {
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                FromUserId = userId,
                CustomerServiceId = order.CustomerServiceId.Value,
                ResponseScore = dto.ResponseScore,
                AttitudeScore = dto.AttitudeScore,
                ProfessionalScore = dto.ProfessionalScore,
                OverallScore = dto.OverallScore,
                Comment = string.IsNullOrWhiteSpace(dto.Comment) ? null : dto.Comment.Trim(),
                CreatedAt = DateTime.UtcNow
            };
            _db.CsReviews.Add(review);

            await RecalcCsStats(order.CustomerServiceId.Value);

            await _db.SaveChangesAsync();
            return Ok(new { message = "评价已提交" });
        }

        // ==================================================
        //  2. 某订单的客服评价
        // ==================================================
        [HttpGet("orders/{orderNo}/reviews")]
        public async Task<IActionResult> GetOrderReviews(string orderNo)
        {
            var userId = GetUserId();

            var order = await _db.ClubOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.CustomerId != userId)
                return StatusCode(403, new { message = "无权查看" });

            var reviews = await _db.CsReviews
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
                orderNo = r.OrderNo,
                fromUserId = r.FromUserId,
                fromUserName = users.TryGetValue(r.FromUserId, out var u)
                    ? (u.Username ?? "") : "",
                responseScore = r.ResponseScore,
                attitudeScore = r.AttitudeScore,
                professionalScore = r.ProfessionalScore,
                overallScore = r.OverallScore,
                comment = r.Comment,
                createdAt = r.CreatedAt
            }).ToList();

            return Ok(items);
        }

        // ==================================================
        //  3. 待我评价的订单（老板侧）
        // ==================================================
        [HttpGet("reviews/pending")]
        public async Task<IActionResult> GetPending()
        {
            var userId = GetUserId();

            var pending = await _db.ClubOrders
                .AsNoTracking()
                .Where(o => o.CustomerId == userId
                         && o.Status == "completed"
                         && o.CustomerServiceId != null)
                .Where(o => !_db.CsReviews.Any(r =>
                    r.OrderId == o.Id && r.FromUserId == userId))
                .OrderByDescending(o => o.CompletedAt)
                .Take(50)
                .ToListAsync();

            if (pending.Count == 0)
                return Ok(Array.Empty<object>());

            var gameCodes = pending.Select(o => o.GameCode).Distinct().ToList();
            var csIds = pending.Select(o => o.CustomerServiceId!.Value).Distinct().ToList();

            var games = await _db.ClubGames.AsNoTracking()
                .Where(g => gameCodes.Contains(g.Code))
                .ToDictionaryAsync(g => g.Code);

            var css = await _db.CustomerServices.AsNoTracking()
                .Where(c => csIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            var types = await _db.ClubOrderTypes.AsNoTracking()
                .Where(t => gameCodes.Contains(t.GameCode))
                .ToListAsync();
            var typeMap = types.ToDictionary(t => $"{t.GameCode}|{t.Code}");

            var items = pending.Select(o =>
            {
                games.TryGetValue(o.GameCode, out var g);
                css.TryGetValue(o.CustomerServiceId!.Value, out var cs);
                typeMap.TryGetValue($"{o.GameCode}|{o.OrderTypeCode}", out var t);

                return new
                {
                    orderId = o.Id,
                    orderNo = o.OrderNo,
                    gameName = g?.Name ?? o.GameCode,
                    orderTypeName = t?.Name ?? o.OrderTypeCode,
                    price = o.Price,
                    completedAt = o.CompletedAt,
                    customerServiceId = o.CustomerServiceId,
                    customerServiceName = cs?.Name ?? "",
                    customerServiceWechat = cs?.Wechat ?? ""
                };
            }).ToList();

            return Ok(items);
        }

        // ==================================================
        //  4. 某客服收到的评价（公开）
        //  GET /api/club/cs/{csId:guid}/reviews
        // ==================================================
        [HttpGet("{csId:guid}/reviews")]
        public async Task<IActionResult> GetCsReviews(
            Guid csId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 20;

            var cs = await _db.CustomerServices
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == csId && c.AuditStatus == "approved");
            if (cs == null) return NotFound(new { message = "客服不存在" });

            var q = _db.CsReviews
                .AsNoTracking()
                .Where(r => r.CustomerServiceId == csId)
                .OrderByDescending(r => r.CreatedAt);

            var total = await q.CountAsync();

            var raw = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var orderIds = raw.Select(r => r.OrderId).Distinct().ToList();
            var orders = await _db.ClubOrders.AsNoTracking()
                .Where(o => orderIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id);

            var gameCodes = orders.Values.Select(o => o.GameCode).Distinct().ToList();
            var games = await _db.ClubGames.AsNoTracking()
                .Where(g => gameCodes.Contains(g.Code))
                .ToDictionaryAsync(g => g.Code);

            var custIds = raw.Select(r => r.FromUserId).Distinct().ToList();
            var users = await _db.Users.AsNoTracking()
                .Where(u => custIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var items = raw.Select(r =>
            {
                orders.TryGetValue(r.OrderId, out var o);
                games.TryGetValue(o?.GameCode ?? "", out var g);
                users.TryGetValue(r.FromUserId, out var u);

                return new
                {
                    id = r.Id,
                    orderNo = r.OrderNo,
                    gameCode = o?.GameCode ?? "",
                    gameName = g?.Name ?? o?.GameCode ?? "",
                    customerName = u?.Username ?? "匿名老板",
                    responseScore = r.ResponseScore,
                    attitudeScore = r.AttitudeScore,
                    professionalScore = r.ProfessionalScore,
                    overallScore = r.OverallScore,
                    comment = r.Comment,
                    createdAt = r.CreatedAt
                };
            }).ToList();

            return Ok(new
            {
                total,
                page,
                pageSize,
                csId = cs.Id,
                csName = cs.Name,
                avgOverall = Math.Round(cs.AvgOverall, 2),
                reviewCount = cs.ReviewCount,
                items
            });
        }

        // ==================================================
        //  5. 客服看自己收到的评价
        //  GET /api/club/cs/me/reviews
        // ==================================================
        [HttpGet("me/reviews")]
        public async Task<IActionResult> GetMyReviews(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = GetUserId();

            var cs = await _db.CustomerServices
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (cs == null)
                return BadRequest(new { message = "你还没有客服身份" });

            return await GetCsReviews(cs.Id, page, pageSize);
        }

        // ==================================================
        //  内部：重算客服评价统计
        // ==================================================
        private async Task RecalcCsStats(Guid csId)
        {
            var cs = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.Id == csId);
            if (cs == null) return;

            var reviews = await _db.CsReviews
                .AsNoTracking()
                .Where(r => r.CustomerServiceId == csId)
                .ToListAsync();

            cs.ReviewCount = reviews.Count;

            if (reviews.Count == 0)
            {
                cs.AvgResponse = 0;
                cs.AvgAttitude = 0;
                cs.AvgProfessional = 0;
                cs.AvgOverall = 0;
                return;
            }

            cs.AvgResponse = Math.Round(reviews.Average(r => r.ResponseScore), 2);
            cs.AvgAttitude = Math.Round(reviews.Average(r => r.AttitudeScore), 2);
            cs.AvgProfessional = Math.Round(reviews.Average(r => r.ProfessionalScore), 2);
            cs.AvgOverall = Math.Round(reviews.Average(r => r.OverallScore), 2);
        }

        private static bool InRange(int s) => s >= 1 && s <= 5;

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
        public class CsReviewDto
        {
            public int ResponseScore { get; set; }
            public int AttitudeScore { get; set; }
            public int ProfessionalScore { get; set; }
            public int OverallScore { get; set; }
            public string? Comment { get; set; }
        }
    }
}