using Microsoft.AspNetCore.Authorization;
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
    [Authorize]
    public class AdminOrderController : ControllerBase
    {
        private readonly AppDbContext _db;
        public AdminOrderController(AppDbContext db) => _db = db;

        /// <summary>
        /// 订单列表（后台）
        /// GET /api/admin/club/orders?status=&search=&customerServiceId=&page=1&pageSize=20
        ///
        /// customerServiceId 特殊值：
        ///   - 空 / null   → 不筛选
        ///   - "none"      → 未分配客服
        ///   - 合法 GUID   → 指定客服
        /// </summary>
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders(
            [FromQuery] string? status,
            [FromQuery] string? search,
            [FromQuery] string? customerServiceId,
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

            // ⭐ 客服筛选
            if (!string.IsNullOrWhiteSpace(customerServiceId))
            {
                if (customerServiceId.Equals("none", StringComparison.OrdinalIgnoreCase))
                {
                    q = q.Where(o => o.CustomerServiceId == null);
                }
                else if (Guid.TryParse(customerServiceId, out var csId))
                {
                    q = q.Where(o => o.CustomerServiceId == csId);
                }
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
        /// 取消订单
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

            // 释放客服负载
            if (order.CustomerServiceId.HasValue)
            {
                var cs = await _db.CustomerServices
                    .FirstOrDefaultAsync(c => c.Id == order.CustomerServiceId.Value);
                if (cs != null)
                    cs.CurrentLoad = Math.Max(0, cs.CurrentLoad - 1);
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "已取消" });
        }

        /// <summary>
        /// 客服/管理员手动完成订单（老板长时间未结单时使用）
        /// POST /api/admin/club/order/{orderNo}/force-complete
        /// body: { reason: "老板长时间未结单" }
        /// </summary>
        [HttpPost("order/{orderNo}/force-complete")]
        public async Task<IActionResult> ForceComplete(string orderNo, [FromBody] ForceCompleteDto dto)
        {
            var order = await _db.ClubOrders
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.Status != "processing")
                return BadRequest(new { message = $"当前状态 {order.Status}，无法代完成" });

            if (string.IsNullOrWhiteSpace(dto?.Reason))
                return BadRequest(new { message = "请填写代完成原因" });

            // 记录备注
            var note = $"【客服代完成】{dto.Reason.Trim()}";
            order.CsRemark = string.IsNullOrWhiteSpace(order.CsRemark)
                ? note
                : $"{order.CsRemark} | {note}";

            order.Status = "completed";
            order.CompletedAt = DateTime.UtcNow;

            // 解析打手
            var opIds = ParseOperatorUserIds(order.OperatorUserIdsJson);
            if (opIds.Count == 0 && order.OperatorUserId != Guid.Empty)
                opIds.Add(order.OperatorUserId);

            // 结算 + 客服入账 + 更新统计 + 释放客服负载
            await ForceSettleIncome(order, opIds);
            await ForceSettleCsIncome(order);              // ⭐ 客服入账
            foreach (var opId in opIds)
                await ForceUpdateOperatorStats(order, opId);
            await ForceReleaseCustomerService(order);

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "已代完成",
                status = order.Status,
                note
            });
        }

        // ==================================================
        //  内部：订单富化
        // ==================================================
        private async Task<List<object>> Enrich(List<ClubOrder> orders)
        {
            if (orders.Count == 0) return new();

            var opIds = orders
                .Where(o => o.OperatorUserId != Guid.Empty)
                .Select(o => o.OperatorUserId)
                .Distinct().ToList();

            var custIds = orders.Select(o => o.CustomerId).Distinct().ToList();
            var gameCodes = orders.Select(o => o.GameCode).Distinct().ToList();

            // ⭐ 批量拉客服
            var csIds = orders
                .Where(o => o.CustomerServiceId.HasValue)
                .Select(o => o.CustomerServiceId!.Value)
                .Distinct().ToList();

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

            var csMap = await _db.CustomerServices.AsNoTracking()
                .Where(c => csIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            return orders.Select(o =>
            {
                profiles.TryGetValue(o.OperatorUserId, out var p);
                users.TryGetValue(o.CustomerId, out var u);
                games.TryGetValue(o.GameCode, out var g);
                otMap.TryGetValue($"{o.GameCode}|{o.OrderTypeCode}", out var t);

                CustomerService? cs = null;
                if (o.CustomerServiceId.HasValue)
                    csMap.TryGetValue(o.CustomerServiceId.Value, out cs);

                return (object)new
                {
                    orderNo = o.OrderNo,
                    status = o.Status,
                    price = o.Price,
                    remark = o.Remark,
                    createdAt = o.CreatedAt,
                    userPaidAt = o.UserPaidAt,
                    paidConfirmedAt = o.PaidConfirmedAt,
                    acceptedAt = o.AcceptedAt,
                    startedAt = o.StartedAt,
                    completedAt = o.CompletedAt,
                    cancelledAt = o.CancelledAt,

                    // 来源 + 客服
                    source = o.Source,
                    customerServiceId = o.CustomerServiceId,
                    customerServiceName = cs?.Name,
                    customerServiceWechat = cs?.Wechat,

                    gameCode = o.GameCode,
                    gameName = g?.Name ?? o.GameCode,
                    orderTypeCode = o.OrderTypeCode,
                    orderTypeName = t?.Name ?? o.OrderTypeCode,
                    customerId = o.CustomerId,
                    customerName = u?.Username ?? "",
                    operatorUserId = o.OperatorUserId == Guid.Empty ? (Guid?)null : o.OperatorUserId,
                    operatorName = p?.Nickname ?? "",

                    // 结算截图
                    settlementProofUrl = o.SettlementProofUrl,
                    settlementProofUploaded = o.SettlementProofUploaded,

                    @params = ParseJsonDict(o.ParamsJson)
                };
            }).ToList();
        }

        // ==================================================
        //  ⭐ 内部：代完成的结算逻辑
        // ==================================================
        private const int FROZEN_DAYS = 3;

        /// <summary>客服抽成率（按订单来源）</summary>
        private static decimal GetCsCommissionRate(string source)
        {
            return source == "cs" ? 0.10m : 0.05m;  // 自拉 10% / 匹配 5%
        }

        private async Task ForceSettleIncome(ClubOrder order, List<Guid> operatorIds)
        {
            if (operatorIds.Count == 0) return;

            var commissionRate = order.CommissionRate;
            if (commissionRate <= 0) commissionRate = 0.20m;

            var distributableAmount = order.Price * (1 - commissionRate);
            var perOperatorIncome = Math.Round(distributableAmount / operatorIds.Count, 2);

            foreach (var opId in operatorIds)
            {
                var profile = await _db.OperatorProfiles
                    .FirstOrDefaultAsync(p => p.UserId == opId);
                if (profile == null) continue;

                profile.FrozenBalance += perOperatorIncome;

                _db.OperatorWalletTransactions.Add(new OperatorWalletTransaction
                {
                    UserId = opId,
                    Type = "income",
                    Amount = perOperatorIncome,
                    OrderNo = order.OrderNo,
                    Status = "completed",
                    Remark = $"订单收益 · 总价 ¥{order.Price} · 抽成 {(commissionRate * 100):0}% · {operatorIds.Count} 人平分（客服代完成）",
                    UnfreezeAt = DateTime.UtcNow.AddDays(FROZEN_DAYS),
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                });
            }
        }

        /// <summary>⭐ 客服代完成时，给客服入账</summary>
        private async Task ForceSettleCsIncome(ClubOrder order)
        {
            if (!order.CustomerServiceId.HasValue) return;

            var cs = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.Id == order.CustomerServiceId.Value);
            if (cs == null) return;

            var rate = GetCsCommissionRate(order.Source);
            var income = Math.Round(order.Price * rate, 2);
            if (income <= 0) return;

            cs.FrozenBalance += income;
            cs.TotalIncome += income;

            _db.CustomerServiceWalletTransactions.Add(new CustomerServiceWalletTransaction
            {
                UserId = cs.UserId,
                CustomerServiceId = cs.Id,
                Type = "income",
                Amount = income,
                OrderNo = order.OrderNo,
                Status = "completed",
                Remark = $"订单收益 · 总价 ¥{order.Price} · 客服抽成 {(rate * 100):0}%（客服代完成）",
                UnfreezeAt = DateTime.UtcNow.AddDays(FROZEN_DAYS),
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });
        }

        private async Task ForceUpdateOperatorStats(ClubOrder order, Guid operatorUserId)
        {
            if (operatorUserId == Guid.Empty) return;

            var profile = await _db.OperatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == operatorUserId);
            if (profile == null) return;

            var skill = await _db.OperatorGameSkills
                .FirstOrDefaultAsync(s => s.UserId == operatorUserId
                                       && s.GameCode == order.GameCode);

            profile.TotalOrders += 1;
            profile.CompletedOrders += 1;

            if (skill != null)
            {
                skill.OrdersInGame += 1;
                skill.CompletedOrdersInGame += 1;
            }

            profile.ActiveOrderCount = Math.Max(0, profile.ActiveOrderCount - 1);
            if (profile.ActiveOrderCount == 0 && profile.Status == "busy")
                profile.Status = "online";
        }

        private async Task ForceReleaseCustomerService(ClubOrder order)
        {
            if (!order.CustomerServiceId.HasValue) return;

            var cs = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.Id == order.CustomerServiceId.Value);
            if (cs != null)
                cs.CurrentLoad = Math.Max(0, cs.CurrentLoad - 1);
        }

        private static List<Guid> ParseOperatorUserIds(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new List<Guid>();
            try
            {
                var arr = System.Text.Json.JsonSerializer.Deserialize<List<Guid>>(json);
                return arr ?? new List<Guid>();
            }
            catch
            {
                return new List<Guid>();
            }
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

        // ==================================================
        //  DTO
        // ==================================================
        public class AdminCancelDto
        {
            public string? Reason { get; set; }
        }

        public class ForceCompleteDto
        {
            public string? Reason { get; set; }
        }
    }
}