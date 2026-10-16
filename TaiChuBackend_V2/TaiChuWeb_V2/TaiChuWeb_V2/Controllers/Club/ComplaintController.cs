using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Club;

namespace TaiChuWeb_V2.Controllers.Club
{
    /// <summary>
    /// 订单客诉（老板提交 / 查看）
    /// 路由前缀：api/club/complaints
    /// </summary>
    [ApiController]
    [Route("api/club/complaints")]
    [Authorize]
    public class ComplaintController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ComplaintController(AppDbContext db) => _db = db;

        // ⭐ 客诉窗口：订单结束（completed / abandoned）后多少小时内可提交
        private const int COMPLAINT_WINDOW_HOURS = 24;

        // ==================================================
        //  1. 提交客诉（老板）
        // ==================================================
        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] SubmitComplaintDto dto)
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(dto?.OrderNo))
                return BadRequest(new { message = "请提供订单号" });
            if (string.IsNullOrWhiteSpace(dto.ComplaintType))
                return BadRequest(new { message = "请选择投诉类型" });
            if (string.IsNullOrWhiteSpace(dto.Description))
                return BadRequest(new { message = "请填写投诉描述" });
            if (dto.Description.Length > 2000)
                return BadRequest(new { message = "投诉描述不能超过 2000 字" });

            var order = await _db.ClubOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderNo == dto.OrderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            // 只有老板能提交
            if (order.CustomerId != userId)
                return StatusCode(403, new { message = "只有下单老板才能提交客诉" });

            // 订单状态必须是 completed 或 abandoned
            if (order.Status != "completed" && order.Status != "abandoned")
                return BadRequest(new { message = "只有已完成或已弃单的订单才能提交客诉" });

            // 时间窗口检查
            var endTime = order.Status == "completed" ? order.CompletedAt : order.CancelledAt;
            if (endTime.HasValue)
            {
                var deadline = endTime.Value.AddHours(COMPLAINT_WINDOW_HOURS);
                if (DateTime.UtcNow > deadline)
                    return BadRequest(new { message = $"客诉已过期（订单结束后 {COMPLAINT_WINDOW_HOURS} 小时内有效）" });
            }

            // 验证投诉类型
            var validTypes = new[] { "attitude", "late", "runner", "quality", "harassment", "other" };
            if (!validTypes.Contains(dto.ComplaintType))
                return BadRequest(new { message = "投诉类型无效" });

            // 如果指定了被诉打手，校验该打手确实在该订单里
            Guid? targetOpId = null;
            if (dto.TargetOperatorId.HasValue && dto.TargetOperatorId.Value != Guid.Empty)
            {
                var opIds = ParseOperatorUserIds(order.OperatorUserIdsJson);
                if (opIds.Count == 0 && order.OperatorUserId != Guid.Empty)
                    opIds.Add(order.OperatorUserId);

                if (!opIds.Contains(dto.TargetOperatorId.Value))
                    return BadRequest(new { message = "被投诉打手不属于该订单" });

                targetOpId = dto.TargetOperatorId.Value;
            }

            // 同一订单 + 同一投诉人 + 同一被诉对象，只能提交一次
            var exists = await _db.OrderComplaints.AnyAsync(c =>
                c.OrderId == order.Id
                && c.FromUserId == userId
                && c.TargetOperatorId == targetOpId);
            if (exists)
                return BadRequest(new { message = "你已经对该订单提交过客诉了" });

            var complaint = new OrderComplaint
            {
                OrderId = order.Id,
                OrderNo = order.OrderNo,
                FromUserId = userId,
                TargetOperatorId = targetOpId,
                ComplaintType = dto.ComplaintType,
                Description = dto.Description.Trim(),
                EvidenceUrl = string.IsNullOrWhiteSpace(dto.EvidenceUrl) ? null : dto.EvidenceUrl.Trim(),
                Status = "pending",
                CreatedAt = DateTime.UtcNow
            };
            _db.OrderComplaints.Add(complaint);

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "客诉已提交，客服将尽快处理",
                id = complaint.Id
            });
        }

        // ==================================================
        //  2. 我的客诉列表（老板）
        // ==================================================
        [HttpGet("my")]
        public async Task<IActionResult> GetMyComplaints(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = GetUserId();
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 20;

            var q = _db.OrderComplaints
                .AsNoTracking()
                .Where(c => c.FromUserId == userId)
                .OrderByDescending(c => c.CreatedAt);

            var total = await q.CountAsync();

            var raw = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = await EnrichComplaints(raw);
            return Ok(new { total, page, pageSize, items });
        }

        // ==================================================
        //  3. 我的客诉详情
        // ==================================================
        [HttpGet("{id:long}")]
        public async Task<IActionResult> GetDetail(long id)
        {
            var userId = GetUserId();

            var c = await _db.OrderComplaints
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
            if (c == null) return NotFound(new { message = "客诉不存在" });

            if (c.FromUserId != userId)
                return StatusCode(403, new { message = "无权查看" });

            var items = await EnrichComplaints(new List<OrderComplaint> { c });
            return Ok(items.FirstOrDefault());
        }

        // ==================================================
        //  4. 某个订单能否提交客诉（前端按钮显示判断）
        // ==================================================
        [HttpGet("can-submit")]
        public async Task<IActionResult> CanSubmit([FromQuery] string orderNo)
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(orderNo))
                return BadRequest(new { message = "请提供订单号" });

            var order = await _db.ClubOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.CustomerId != userId)
                return Ok(new { canSubmit = false, reason = "不是你的订单" });

            if (order.Status != "completed" && order.Status != "abandoned")
                return Ok(new { canSubmit = false, reason = "订单未结束" });

            var endTime = order.Status == "completed" ? order.CompletedAt : order.CancelledAt;
            if (endTime.HasValue)
            {
                var deadline = endTime.Value.AddHours(COMPLAINT_WINDOW_HOURS);
                if (DateTime.UtcNow > deadline)
                    return Ok(new
                    {
                        canSubmit = false,
                        reason = $"客诉已过期（{COMPLAINT_WINDOW_HOURS} 小时内有效）",
                        deadline
                    });
            }

            // 该订单已经提交过几条客诉
            var submittedCount = await _db.OrderComplaints
                .AsNoTracking()
                .CountAsync(c => c.OrderId == order.Id && c.FromUserId == userId);

            return Ok(new
            {
                canSubmit = true,
                deadline = endTime?.AddHours(COMPLAINT_WINDOW_HOURS),
                submittedCount
            });
        }

        // ==================================================
        //  内部
        // ==================================================
        private async Task<List<object>> EnrichComplaints(List<OrderComplaint> list)
        {
            if (list.Count == 0) return new();

            // 批量拉打手名
            var opIds = list
                .Where(c => c.TargetOperatorId.HasValue)
                .Select(c => c.TargetOperatorId!.Value)
                .Distinct()
                .ToList();

            var opMap = await _db.OperatorProfiles.AsNoTracking()
                .Where(p => opIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId, p => p.Nickname);

            // 批量拉订单（拿游戏名、类型名）
            var orderIds = list.Select(c => c.OrderId).Distinct().ToList();
            var orders = await _db.ClubOrders.AsNoTracking()
                .Where(o => orderIds.Contains(o.Id))
                .ToDictionaryAsync(o => o.Id);

            var gameCodes = orders.Values.Select(o => o.GameCode).Distinct().ToList();
            var games = await _db.ClubGames.AsNoTracking()
                .Where(g => gameCodes.Contains(g.Code))
                .ToDictionaryAsync(g => g.Code);

            var types = await _db.ClubOrderTypes.AsNoTracking()
                .Where(t => gameCodes.Contains(t.GameCode))
                .ToListAsync();
            var typeMap = types.ToDictionary(t => $"{t.GameCode}|{t.Code}");

            return list.Select(c =>
            {
                orders.TryGetValue(c.OrderId, out var o);
                games.TryGetValue(o?.GameCode ?? "", out var g);
                typeMap.TryGetValue($"{o?.GameCode}|{o?.OrderTypeCode}", out var t);

                string? targetOpName = null;
                if (c.TargetOperatorId.HasValue)
                    opMap.TryGetValue(c.TargetOperatorId.Value, out targetOpName);

                return (object)new
                {
                    id = c.Id,
                    orderId = c.OrderId,
                    orderNo = c.OrderNo,
                    gameName = g?.Name ?? o?.GameCode ?? "",
                    orderTypeName = t?.Name ?? o?.OrderTypeCode ?? "",

                    complaintType = c.ComplaintType,
                    complaintTypeLabel = TypeLabel(c.ComplaintType),
                    description = c.Description,
                    evidenceUrl = c.EvidenceUrl,

                    targetOperatorId = c.TargetOperatorId,
                    targetOperatorName = targetOpName,

                    status = c.Status,
                    handlerName = c.HandlerName,
                    handlerNote = c.HandlerNote,
                    handledAt = c.HandledAt,

                    createdAt = c.CreatedAt
                };
            }).ToList();
        }

        private static string TypeLabel(string type) => type switch
        {
            "attitude" => "服务态度差",
            "late" => "迟到 / 缺席",
            "runner" => "打手跑路",
            "quality" => "服务质量差",
            "harassment" => "辱骂 / 骚扰",
            "other" => "其他",
            _ => type
        };

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
        public class SubmitComplaintDto
        {
            public string OrderNo { get; set; } = "";

            /// <summary>被诉打手（可空，空 = 针对整单）</summary>
            public Guid? TargetOperatorId { get; set; }

            /// <summary>attitude / late / runner / quality / harassment / other</summary>
            public string ComplaintType { get; set; } = "other";

            public string Description { get; set; } = "";

            /// <summary>证据截图 URL（可选）</summary>
            public string? EvidenceUrl { get; set; }
        }
    }
}