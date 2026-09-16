using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Club;

namespace TaiChuWeb_V2.Controllers.Club
{
    /// <summary>
    /// 后台订单管理
    /// 路由前缀：api/admin/club
    /// </summary>
    [ApiController]
    [Route("api/admin/club")]
    public class AdminOrderController : ControllerBase
    {
        private readonly AppDbContext _db;
        public AdminOrderController(AppDbContext db) => _db = db;

        /// <summary>
        /// 订单列表（后台）
        /// GET /api/admin/club/orders?status=&search=&page=1&pageSize=20
        /// </summary>
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders(
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var q = _db.ClubOrders.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(o => o.Status == status);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(o =>
                    o.OrderNo.Contains(s) ||
                    o.CustomerId.ToString().Contains(s) ||
                    o.OperatorUserId.ToString().Contains(s));
            }

            var total = await q.CountAsync();

            var raw = await q
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = await Enrich(raw);
            return Ok(new { total, page, pageSize, items });
        }

        /// <summary>
        /// 客服确认收款
        /// POST /api/admin/club/order/{orderNo}/confirm-paid
        /// </summary>
        [HttpPost("order/{orderNo}/confirm-paid")]
        public async Task<IActionResult> ConfirmPaid(string orderNo)
        {
            var order = await _db.ClubOrders
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.PaidConfirmedAt.HasValue)
                return BadRequest(new { message = "该订单已确认过收款" });

            if (order.Status != "pending")
                return BadRequest(new { message = $"当前状态 {order.Status}，无法确认" });

            order.PaidConfirmedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = "已确认收款", paidConfirmedAt = order.PaidConfirmedAt });
        }

        /// <summary>
        /// 客服取消订单
        /// POST /api/admin/club/order/{orderNo}/cancel
        /// </summary>
        [HttpPost("order/{orderNo}/cancel")]
        public async Task<IActionResult> Cancel(string orderNo, [FromBody] AdminCancelDto dto)
        {
            var order = await _db.ClubOrders
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.Status != "pending")
                return BadRequest(new { message = $"当前状态 {order.Status}，无法取消" });

            order.Status = "cancelled";
            order.CancelledAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(dto?.Reason))
                order.Remark = $"{order.Remark} | 管理员取消：{dto.Reason}";

            await _db.SaveChangesAsync();
            return Ok(new { message = "已取消" });
        }

        // ==================================================
        //  内部：订单富化（补充老板/打手/游戏/订单类型信息）
        // ==================================================
        private async Task<List<object>> Enrich(List<ClubOrder> orders)
        {
            if (orders.Count == 0) return new();

            var opIds = orders.Select(o => o.OperatorUserId).Distinct().ToList();
            var custIds = orders.Select(o => o.CustomerId).Distinct().ToList();
            var gameCodes = orders.Select(o => o.GameCode).Distinct().ToList();

            var profiles = await _db.OperatorProfiles.AsNoTracking()
                .Where(p => opIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId);

            var users = await _db.Users.AsNoTracking()
                .Where(u => custIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var games = await _db.ClubGames.AsNoTracking()
                .Where(g => gameCodes.Contains(g.Code))
                .ToDictionaryAsync(g => g.Code);

            var orderTypes = await _db.ClubOrderTypes.AsNoTracking()
                .Where(t => gameCodes.Contains(t.GameCode))
                .ToListAsync();
            var otMap = orderTypes.ToDictionary(t => $"{t.GameCode}|{t.Code}");

            return orders.Select(o =>
            {
                profiles.TryGetValue(o.OperatorUserId, out var p);
                users.TryGetValue(o.CustomerId, out var u);
                games.TryGetValue(o.GameCode, out var g);
                otMap.TryGetValue($"{o.GameCode}|{o.OrderTypeCode}", out var t);

                return (object)new
                {
                    orderNo = o.OrderNo,
                    status = o.Status,
                    price = o.Price,
                    remark = o.Remark,
                    createdAt = o.CreatedAt,
                    userPaidAt = o.UserPaidAt,               // ⭐ 新增
                    paidConfirmedAt = o.PaidConfirmedAt,
                    acceptedAt = o.AcceptedAt,
                    startedAt = o.StartedAt,
                    completedAt = o.CompletedAt,
                    cancelledAt = o.CancelledAt,
                    gameCode = o.GameCode,
                    gameName = g?.Name ?? o.GameCode,
                    orderTypeCode = o.OrderTypeCode,
                    orderTypeName = t?.Name ?? o.OrderTypeCode,
                    customerId = o.CustomerId,
                    customerName = u?.Username ?? "",
                    operatorUserId = o.OperatorUserId,
                    operatorName = p?.Nickname ?? "",
                    @params = ParseJsonDict(o.ParamsJson)
                };
            }).ToList();
        }

        private static Dictionary<string, string> ParseJsonDict(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try
            {
                return System.Text.Json.JsonSerializer
                    .Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            catch { return new(); }
        }

        public class AdminCancelDto
        {
            public string? Reason { get; set; }
        }
    }
}