using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Club;

namespace TaiChuWeb_V2.Controllers.Club
{
    /// <summary>
    /// 订单核心控制器（老板端 + 打手端）
    /// 路由前缀：api/club/orders
    /// </summary>
    [ApiController]
    [Route("api/club/orders")]
    [Authorize]
    public class OrderController : ControllerBase
    {
        private readonly AppDbContext _db;
        public OrderController(AppDbContext db) => _db = db;

        // ==================================================
        //  1. 老板下单
        // ==================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
        {
            var customerId = GetUserId();

            // ① 游戏
            var game = await _db.ClubGames
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Code == dto.GameCode && g.IsActive);
            if (game == null) return BadRequest(new { message = "游戏不存在或已下架" });

            // ② 订单类型
            var orderType = await _db.ClubOrderTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t =>
                    t.GameCode == dto.GameCode &&
                    t.Code == dto.OrderTypeCode &&
                    t.IsActive);
            if (orderType == null) return BadRequest(new { message = "订单类型不存在或已停用" });

            // ③ 打手必须已通过该游戏
            var skill = await _db.OperatorGameSkills
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.UserId == dto.OperatorUserId &&
                    s.GameCode == dto.GameCode &&
                    s.AuditStatus == "approved");
            if (skill == null) return BadRequest(new { message = "该打手未通过此游戏认证" });

            // ④ 参数校验
            var paramError = ValidateParams(orderType.ParamsSchemaJson, dto.Params);
            if (paramError != null) return BadRequest(new { message = paramError });

            // ⑤ 价格计算
            var price = CalculatePrice(orderType.PricingJson, orderType.ParamsSchemaJson, dto.Params);
            if (price < 0)
                return BadRequest(new { message = "无法计算价格，请检查订单参数" });

            // ⑥ 订单号
            var orderNo = await GenerateOrderNo();

            // ⑦ 落库
            var order = new ClubOrder
            {
                OrderNo = orderNo,
                CustomerId = customerId,
                OperatorUserId = dto.OperatorUserId,
                GameCode = dto.GameCode,
                OrderTypeCode = dto.OrderTypeCode,
                ParamsJson = JsonSerializer.Serialize(dto.Params ?? new Dictionary<string, string>()),
                Price = price,
                Status = "pending",
                Remark = dto.Remark,
                CreatedAt = DateTime.UtcNow
            };
            _db.ClubOrders.Add(order);
            await _db.SaveChangesAsync();

            return Ok(new
            {
                orderNo = order.OrderNo,
                price = order.Price,
                status = order.Status
            });
        }

        // ==================================================
        //  2. 老板的订单列表
        // ==================================================
        [HttpGet("my")]
        public async Task<IActionResult> MyOrders(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var customerId = GetUserId();
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 20;

            var q = _db.ClubOrders
                .AsNoTracking()
                .Where(o => o.CustomerId == customerId);

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(o => o.Status == status);

            var total = await q.CountAsync();
            var raw = await q
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = await EnrichOrders(raw);
            return Ok(new { total, page, pageSize, items });
        }

        // ==================================================
        //  3. 打手的订单列表
        // ==================================================
        [HttpGet("operator")]
        public async Task<IActionResult> OperatorOrders(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var operatorId = GetUserId();
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 20;

            var q = _db.ClubOrders
                .AsNoTracking()
                .Where(o => o.OperatorUserId == operatorId)
                // ⭐ 只展示：客服已确认收款，或订单已进入非 pending 阶段
                .Where(o => o.PaidConfirmedAt != null || o.Status != "pending");

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(o => o.Status == status);

            var total = await q.CountAsync();
            var raw = await q
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = await EnrichOrders(raw);
            return Ok(new { total, page, pageSize, items });
        }

        // ==================================================
        //  4. 订单详情
        // ==================================================
        [HttpGet("{orderNo}")]
        public async Task<IActionResult> Detail(string orderNo)
        {
            var userId = GetUserId();

            var order = await _db.ClubOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.CustomerId != userId && order.OperatorUserId != userId)
                return StatusCode(403, new { message = "无权查看该订单" });

            var enriched = await EnrichOrders(new List<ClubOrder> { order });
            return Ok(enriched.FirstOrDefault());
        }

        // ==================================================
        //  5. 打手接单
        // ==================================================
        [HttpPost("{orderNo}/accept")]
        public async Task<IActionResult> Accept(string orderNo)
        {
            var operatorId = GetUserId();
            var order = await _db.ClubOrders
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.OperatorUserId != operatorId)
                return StatusCode(403, new { message = "这不是属于你的订单" });
            if (order.Status != "pending")
                return BadRequest(new { message = $"当前状态 {order.Status}，无法接单" });

            // ⭐ 必须客服已确认收款
            if (!order.PaidConfirmedAt.HasValue)
                return BadRequest(new { message = "客服尚未确认收款，暂时无法接单" });

            order.Status = "accepted";
            order.AcceptedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = "已接单", status = order.Status });
        }

        // ==================================================
        //  6. 打手开始服务
        // ==================================================
        [HttpPost("{orderNo}/start")]
        public async Task<IActionResult> Start(string orderNo)
        {
            var operatorId = GetUserId();
            var order = await _db.ClubOrders
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.OperatorUserId != operatorId)
                return StatusCode(403, new { message = "这不是属于你的订单" });
            if (order.Status != "accepted")
                return BadRequest(new { message = $"当前状态 {order.Status}，无法开始" });

            order.Status = "processing";
            order.StartedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = "服务已开始", status = order.Status });
        }

        // ==================================================
        //  7. 打手完成订单
        // ==================================================
        [HttpPost("{orderNo}/complete")]
        public async Task<IActionResult> Complete(string orderNo)
        {
            var operatorId = GetUserId();
            var order = await _db.ClubOrders
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.OperatorUserId != operatorId)
                return StatusCode(403, new { message = "这不是属于你的订单" });
            if (order.Status != "processing")
                return BadRequest(new { message = $"当前状态 {order.Status}，无法完成" });

            order.Status = "completed";
            order.CompletedAt = DateTime.UtcNow;

            await UpdateOperatorStats(order, success: true);
            await SettleIncome(order);          // ⭐ 结算收益到冻结余额

            await _db.SaveChangesAsync();
            return Ok(new { message = "订单已完成", status = order.Status });
        }

        // ==================================================
        //  8. 取消订单（老板 或 打手 都可以）
        // ==================================================
        [HttpPost("{orderNo}/cancel")]
        public async Task<IActionResult> Cancel(string orderNo, [FromBody] CancelOrderDto dto)
        {
            var userId = GetUserId();
            var order = await _db.ClubOrders
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.CustomerId != userId && order.OperatorUserId != userId)
                return StatusCode(403, new { message = "无权操作该订单" });
            if (order.Status != "pending" && order.Status != "accepted")
                return BadRequest(new { message = $"当前状态 {order.Status}，无法取消" });

            order.Status = "cancelled";
            order.CancelledAt = DateTime.UtcNow;
            order.Remark = string.IsNullOrWhiteSpace(dto?.Reason)
                ? order.Remark
                : $"{order.Remark} | 取消原因：{dto.Reason}";

            await UpdateOperatorStats(order, success: false);

            await _db.SaveChangesAsync();
            return Ok(new { message = "订单已取消", status = order.Status });
        }

        // ==================================================
        //  9. 用户标记"我已扫码并完成付款"
        // ==================================================
        [HttpPost("{orderNo}/user-paid")]
        public async Task<IActionResult> UserPaid(string orderNo)
        {
            var userId = GetUserId();
            var order = await _db.ClubOrders
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.CustomerId != userId)
                return StatusCode(403, new { message = "无权操作" });

            if (order.Status != "pending")
                return BadRequest(new { message = "订单状态异常，无法标记" });

            if (order.UserPaidAt.HasValue)
                return Ok(new { message = "已标记", userPaidAt = order.UserPaidAt });

            order.UserPaidAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = "已通知客服核对", userPaidAt = order.UserPaidAt });
        }

        // ==================================================
        //  内部：订单富化（补充老板/打手/游戏/订单类型信息）
        // ==================================================
        private async Task<List<object>> EnrichOrders(List<ClubOrder> orders)
        {
            if (orders.Count == 0) return new List<object>();

            var opIds = orders.Select(o => o.OperatorUserId).Distinct().ToList();
            var custIds = orders.Select(o => o.CustomerId).Distinct().ToList();
            var gameCodes = orders.Select(o => o.GameCode).Distinct().ToList();

            var profiles = await _db.OperatorProfiles
                .AsNoTracking()
                .Where(p => opIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId);

            var customers = await _db.Users
                .AsNoTracking()
                .Where(u => custIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var games = await _db.ClubGames
                .AsNoTracking()
                .Where(g => gameCodes.Contains(g.Code))
                .ToDictionaryAsync(g => g.Code);

            var orderTypes = await _db.ClubOrderTypes
                .AsNoTracking()
                .Where(t => gameCodes.Contains(t.GameCode))
                .ToListAsync();
            var orderTypeMap = orderTypes.ToDictionary(t => $"{t.GameCode}|{t.Code}");

            return orders.Select(o =>
            {
                profiles.TryGetValue(o.OperatorUserId, out var p);
                customers.TryGetValue(o.CustomerId, out var cu);
                games.TryGetValue(o.GameCode, out var g);
                orderTypeMap.TryGetValue($"{o.GameCode}|{o.OrderTypeCode}", out var t);

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
                    gameCode = o.GameCode,
                    gameName = g?.Name ?? o.GameCode,
                    orderTypeCode = o.OrderTypeCode,
                    orderTypeName = t?.Name ?? o.OrderTypeCode,
                    customerId = o.CustomerId,
                    customerName = cu?.Username ?? "",
                    operatorUserId = o.OperatorUserId,
                    operatorName = p?.Nickname ?? "",
                    @params = ParseJsonDict(o.ParamsJson)
                };
            }).ToList();
        }

        // ==================================================
        //  内部：参数校验（按 ParamsSchemaJson）
        // ==================================================
        private static string? ValidateParams(string? schemaJson, Dictionary<string, string>? values)
        {
            if (string.IsNullOrWhiteSpace(schemaJson)) return null;
            values ??= new Dictionary<string, string>();

            try
            {
                using var doc = JsonDocument.Parse(schemaJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return null;

                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var fieldName = prop.Name;
                    var fieldDef = prop.Value;

                    var required = true;
                    if (fieldDef.TryGetProperty("required", out var reqEl)
                        && reqEl.ValueKind == JsonValueKind.False)
                        required = false;

                    if (!required) continue;

                    if (!values.TryGetValue(fieldName, out var val) ||
                        string.IsNullOrWhiteSpace(val))
                    {
                        return $"请填写：{fieldName}";
                    }
                }
            }
            catch
            {
                // schema 本身坏掉就跳过校验
            }

            return null;
        }

        // ==================================================
        //  内部：价格计算
        // ==================================================
        private static decimal CalculatePrice(
            string? pricingJson,
            string? schemaJson,
            Dictionary<string, string>? values)
        {
            if (string.IsNullOrWhiteSpace(pricingJson)) return -1;
            values ??= new Dictionary<string, string>();

            try
            {
                using var priceDoc = JsonDocument.Parse(pricingJson);
                if (priceDoc.RootElement.ValueKind != JsonValueKind.Object) return -1;

                if (priceDoc.RootElement.TryGetProperty("default", out var defEl)
                    && defEl.ValueKind == JsonValueKind.Number)
                {
                    return defEl.GetDecimal();
                }

                if (string.IsNullOrWhiteSpace(schemaJson)) return -1;

                using var schemaDoc = JsonDocument.Parse(schemaJson);
                if (schemaDoc.RootElement.ValueKind != JsonValueKind.Object) return -1;

                foreach (var prop in schemaDoc.RootElement.EnumerateObject())
                {
                    var fieldName = prop.Name;
                    var fieldDef = prop.Value;

                    var isSelect = fieldDef.TryGetProperty("type", out var typeEl)
                        && typeEl.GetString() == "select";
                    if (!isSelect) continue;

                    if (!values.TryGetValue(fieldName, out var val)) continue;

                    if (priceDoc.RootElement.TryGetProperty(val, out var hit)
                        && hit.ValueKind == JsonValueKind.Number)
                    {
                        return hit.GetDecimal();
                    }
                }
            }
            catch
            {
                return -1;
            }

            return -1;
        }

        // ==================================================
        //  内部：订单号生成
        // ==================================================
        private async Task<string> GenerateOrderNo()
        {
            var today = DateTime.UtcNow.ToString("yyyyMMdd");
            var prefix = $"ORD-{today}-";

            var maxNo = await _db.ClubOrders
                .Where(o => o.OrderNo.StartsWith(prefix))
                .OrderByDescending(o => o.OrderNo)
                .Select(o => o.OrderNo)
                .FirstOrDefaultAsync();

            int next = 1;
            if (!string.IsNullOrEmpty(maxNo) && maxNo.Length == prefix.Length + 4)
            {
                var numPart = maxNo.Substring(prefix.Length);
                if (int.TryParse(numPart, out var n)) next = n + 1;
            }

            return $"{prefix}{next:D4}";
        }

        // ==================================================
        //  内部：更新打手统计
        // ==================================================
        private async Task UpdateOperatorStats(ClubOrder order, bool success)
        {
            var profile = await _db.OperatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == order.OperatorUserId);
            if (profile == null) return;

            var skill = await _db.OperatorGameSkills
                .FirstOrDefaultAsync(s =>
                    s.UserId == order.OperatorUserId &&
                    s.GameCode == order.GameCode);

            if (success)
            {
                profile.TotalOrders += 1;
                profile.CompletedOrders += 1;

                var previous = await _db.ClubOrders
                    .AsNoTracking()
                    .AnyAsync(o =>
                        o.CustomerId == order.CustomerId &&
                        o.OperatorUserId == order.OperatorUserId &&
                        o.Status == "completed" &&
                        o.Id != order.Id);

                if (!previous)
                {
                    profile.UniqueCustomers += 1;
                }
                else
                {
                    var repeatAlreadyCounted = profile.RepeatCustomers > 0
                        && profile.RepeatCustomers < profile.UniqueCustomers;
                    if (!repeatAlreadyCounted)
                        profile.RepeatCustomers += 1;
                }

                if (skill != null)
                {
                    skill.OrdersInGame += 1;
                    skill.CompletedOrdersInGame += 1;
                }
            }
            else
            {
                profile.CancelledOrders += 1;
                if (skill != null)
                {
                    skill.FailedOrdersInGame += 1;
                }
            }
        }

        // ==================================================
        //  内部：结算打手收益（完成订单时调用）
        // ==================================================
        private static readonly Dictionary<string, decimal> LevelRates = new()
        {
            ["L1"] = 0.60m,
            ["L2"] = 0.68m,
            ["L3"] = 0.76m,
            ["L4"] = 0.84m,
            ["L5"] = 0.92m
        };

        /// <summary>订单完成后的冻结天数</summary>
        private const int FROZEN_DAYS = 3;

        private async Task SettleIncome(ClubOrder order)
        {
            var profile = await _db.OperatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == order.OperatorUserId);
            if (profile == null) return;

            var skill = await _db.OperatorGameSkills
                .FirstOrDefaultAsync(s =>
                    s.UserId == order.OperatorUserId &&
                    s.GameCode == order.GameCode);

            var level = skill?.OperatorLevel ?? "L1";
            var rate = LevelRates.TryGetValue(level, out var r) ? r : 0.60m;
            var income = Math.Round(order.Price * rate, 2);
            if (income <= 0) return;

            profile.FrozenBalance += income;

            _db.OperatorWalletTransactions.Add(new OperatorWalletTransaction
            {
                UserId = order.OperatorUserId,
                Type = "income",
                Amount = income,
                OrderNo = order.OrderNo,
                Status = "completed",
                Remark = $"订单收益 · {level} · {(rate * 100):0}%",
                UnfreezeAt = DateTime.UtcNow.AddDays(FROZEN_DAYS),
                CreatedAt = DateTime.UtcNow,
                CompletedAt = DateTime.UtcNow
            });
        }

        // ==================================================
        //  内部：工具
        // ==================================================
        private static Dictionary<string, string> ParseJsonDict(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            catch
            {
                return new();
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
        public class CreateOrderDto
        {
            public string GameCode { get; set; } = "";
            public string OrderTypeCode { get; set; } = "";
            public Guid OperatorUserId { get; set; }
            public Dictionary<string, string>? Params { get; set; }
            public string? Remark { get; set; }
        }

        public class CancelOrderDto
        {
            public string? Reason { get; set; }
        }
    }
}