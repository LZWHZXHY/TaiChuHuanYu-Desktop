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
    /// 客服端接口（客服自己用）
    /// 路由前缀：api/club/cs
    /// </summary>
    [ApiController]
    [Route("api/club/cs")]
    [Authorize]
    public class CsController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CsController(AppDbContext db) => _db = db;

        // ==================================================
        //  1. 查我的客服状态
        // ==================================================
        [HttpGet("me")]
        public async Task<IActionResult> GetMyStatus()
        {
            var userId = GetUserId();

            var cs = await _db.CustomerServices
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cs == null)
                return Ok(new { hasApplied = false });

            return Ok(new
            {
                hasApplied = true,
                id = cs.Id,
                name = cs.Name,
                wechat = cs.Wechat,
                avatar = cs.Avatar,
                status = cs.Status,
                currentLoad = cs.CurrentLoad,
                capacity = cs.Capacity,
                isActive = cs.IsActive,
                isAssignable = cs.IsAssignable,

                auditStatus = cs.AuditStatus,
                auditNote = cs.AuditNote,
                appliedAt = cs.AppliedAt,
                reviewedAt = cs.ReviewedAt,

                intro = cs.Intro,
                onlineTime = cs.OnlineTime,

                reviewCount = cs.ReviewCount,
                avgResponse = Math.Round(cs.AvgResponse, 2),
                avgAttitude = Math.Round(cs.AvgAttitude, 2),
                avgProfessional = Math.Round(cs.AvgProfessional, 2),
                avgOverall = Math.Round(cs.AvgOverall, 2),

                balance = cs.Balance,
                frozenBalance = cs.FrozenBalance,
                totalIncome = cs.TotalIncome,

                createdAt = cs.CreatedAt
            });
        }

        // ==================================================
        //  2. 提交客服申请
        // ==================================================
        [HttpPost("apply")]
        public async Task<IActionResult> Apply([FromBody] CsApplyDto dto)
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "请填写客服名称" });
            if (string.IsNullOrWhiteSpace(dto.Wechat))
                return BadRequest(new { message = "请填写客服微信" });
            if (string.IsNullOrWhiteSpace(dto.OnlineTime))
                return BadRequest(new { message = "请选择可工作时间" });

            var existingCs = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (existingCs != null)
            {
                if (existingCs.AuditStatus == "approved")
                    return BadRequest(new { message = "你已是客服，无需重复申请" });
                if (existingCs.AuditStatus == "pending")
                    return BadRequest(new { message = "你已提交过申请，请等待审核" });
                if (existingCs.AuditStatus == "banned")
                    return BadRequest(new { message = "该账号已被封禁" });
            }

            var isOperator = await _db.OperatorProfiles
                .AnyAsync(p => p.UserId == userId);
            if (isOperator)
                return BadRequest(new { message = "你已是打手，无法同时成为客服" });

            if (existingCs == null)
            {
                existingCs = new CustomerService
                {
                    Id = Guid.NewGuid(),
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow,
                    Status = "offline",
                    CurrentLoad = 0,
                    IsActive = false,
                    IsAssignable = false,
                    AppliedAt = DateTime.UtcNow,
                    AuditStatus = "pending"
                };
                _db.CustomerServices.Add(existingCs);
            }
            else
            {
                existingCs.AppliedAt = DateTime.UtcNow;
                existingCs.AuditStatus = "pending";
                existingCs.AuditNote = null;
                existingCs.ReviewedAt = null;
            }

            existingCs.Name = dto.Name.Trim();
            existingCs.Wechat = dto.Wechat.Trim();
            existingCs.OnlineTime = dto.OnlineTime;
            existingCs.Intro = dto.Intro?.Trim();
            existingCs.Capacity = dto.Capacity > 0 ? dto.Capacity : 10;

            await _db.SaveChangesAsync();
            return Ok(new { message = "申请已提交，请等待审核" });
        }

        // ==================================================
        //  3. 切换在线状态
        // ==================================================
        [HttpPost("me/status")]
        public async Task<IActionResult> UpdateStatus([FromBody] CsStatusDto dto)
        {
            var userId = GetUserId();

            var valid = new[] { "online", "busy", "offline" };
            if (string.IsNullOrWhiteSpace(dto?.Status) || !valid.Contains(dto.Status))
                return BadRequest(new { message = "状态无效，仅支持 online / busy / offline" });

            var cs = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (cs == null)
                return BadRequest(new { message = "你还没有客服身份" });
            if (cs.AuditStatus != "approved")
                return BadRequest(new { message = "你的客服身份还未审核通过" });

            if (dto.Status == "busy")
                return BadRequest(new { message = "busy 状态由系统自动维护" });

            cs.Status = dto.Status;

            await _db.SaveChangesAsync();
            return Ok(new { message = "状态已更新", status = cs.Status });
        }

        // ==================================================
        //  4. 我的订单列表（按 tab 筛选）
        // ==================================================
        [HttpGet("orders")]
        public async Task<IActionResult> GetOrders(
            [FromQuery] string tab = "all",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = GetUserId();

            var cs = await _db.CustomerServices
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (cs == null || cs.AuditStatus != "approved")
                return BadRequest(new { message = "你的客服身份未通过审核" });

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var q = _db.ClubOrders
                .AsNoTracking()
                .Where(o => o.CustomerServiceId == cs.Id);

            switch (tab)
            {
                case "waiting-confirm":
                    q = q.Where(o =>
                        o.UserPaidAt != null &&
                        o.PaidConfirmedAt == null &&
                        o.Status == "pending");
                    break;

                case "waiting-assign":
                    q = q.Where(o =>
                        o.PaidConfirmedAt != null &&
                        o.OperatorUserId == Guid.Empty &&
                        o.Status == "pending");
                    break;

                case "processing":
                    q = q.Where(o =>
                        o.PaidConfirmedAt != null &&
                        (o.Status == "pending" ||
                         o.Status == "accepted" ||
                         o.Status == "processing"));
                    break;

                case "completed":
                    q = q.Where(o => o.Status == "completed");
                    break;

                case "abandoned":
                    q = q.Where(o => o.Status == "abandoned");
                    break;

                case "cancelled":
                    q = q.Where(o => o.Status == "cancelled");
                    break;

                case "all":
                default:
                    break;
            }

            var total = await q.CountAsync();

            var raw = await q
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = await EnrichOrders(raw);
            return Ok(new { total, page, pageSize, tab, items });
        }

        // ==================================================
        //  5. 确认收款
        // ==================================================
        [HttpPost("orders/{orderNo}/confirm-paid")]
        public async Task<IActionResult> ConfirmPaid(string orderNo)
        {
            var userId = GetUserId();

            var cs = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (cs == null || cs.AuditStatus != "approved")
                return BadRequest(new { message = "你的客服身份未通过审核" });

            var order = await _db.ClubOrders
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.CustomerServiceId != cs.Id)
                return StatusCode(403, new { message = "这不是你负责的订单" });

            if (order.PaidConfirmedAt.HasValue)
                return BadRequest(new { message = "该订单已确认过收款" });

            if (order.Status != "pending")
                return BadRequest(new { message = $"当前状态 {order.Status}，无法确认收款" });

            order.PaidConfirmedAt = DateTime.UtcNow;
            order.CsConfirmedAt = DateTime.UtcNow;
            order.CsConfirmedBy = userId;

            _db.OrderAssignmentLogs.Add(new OrderAssignmentLog
            {
                OrderNo = order.OrderNo,
                Action = "confirm_paid",
                ActorId = userId,
                Note = "客服确认收款"
            });

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "已确认收款",
                paidConfirmedAt = order.PaidConfirmedAt
            });
        }

        // ==================================================
        //  6. 指派打手（首次指派）
        // ==================================================
        [HttpPost("orders/{orderNo}/assign-operator")]
        public async Task<IActionResult> AssignOperator(
            string orderNo,
            [FromBody] AssignOperatorDto dto)
        {
            var userId = GetUserId();

            var cs = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (cs == null || cs.AuditStatus != "approved")
                return BadRequest(new { message = "你的客服身份未通过审核" });

            var order = await _db.ClubOrders
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.CustomerServiceId != cs.Id)
                return StatusCode(403, new { message = "这不是你负责的订单" });

            if (!order.PaidConfirmedAt.HasValue)
                return BadRequest(new { message = "请先确认收款后再指派打手" });

            if (order.Status != "pending")
                return BadRequest(new { message = $"当前状态 {order.Status}，无法指派" });

            var opIds = (dto.OperatorUserIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (opIds.Count == 0)
                return BadRequest(new { message = "请至少选择一位打手" });

            var orderType = await _db.ClubOrderTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.GameCode == order.GameCode
                                        && t.Code == order.OrderTypeCode);
            if (orderType != null)
            {
                if (opIds.Count < orderType.MinOperatorCount)
                    return BadRequest(new { message = $"本订单至少需要 {orderType.MinOperatorCount} 位打手" });
                if (opIds.Count > orderType.MaxOperatorCount)
                    return BadRequest(new { message = $"本订单最多 {orderType.MaxOperatorCount} 位打手" });
            }

            // ===== 校验打手 =====
            var validSkills = new List<OperatorGameSkill>();
            foreach (var opId in opIds)
            {
                var skill = await _db.OperatorGameSkills.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == opId
                                           && s.GameCode == order.GameCode
                                           && s.AuditStatus == "approved");
                if (skill == null)
                    return BadRequest(new { message = "有打手未通过此游戏认证" });

                var opProfile = await _db.OperatorProfiles.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == opId);
                if (opProfile == null)
                    return BadRequest(new { message = "有打手档案不存在" });
                if (opProfile.Status == "offline")
                    return BadRequest(new { message = $"打手【{opProfile.Nickname}】当前离线，无法指派" });

                validSkills.Add(skill);
            }

            // ===== 价格重算 =====
            var basePrice = order.BasePrice > 0 ? order.BasePrice : order.Price;
            decimal totalSurcharge = 0;

            if (orderType?.PricingMode == "level-based")
            {
                foreach (var skill in validSkills)
                    totalSurcharge += basePrice * skill.LevelSurchargeRate;
            }

            var newPrice = Math.Round(basePrice + totalSurcharge, 2);

            // ===== 抽成率 =====
            var rates = new List<decimal>();
            foreach (var opId in opIds)
            {
                var profile = await _db.OperatorProfiles.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == opId);
                var totalOrders = profile?.TotalOrders ?? 0;
                rates.Add(GetOperatorCommissionRate(totalOrders));
            }
            var avgRate = rates.Count > 0 ? Math.Round(rates.Average(), 3) : 0.25m;

            // ===== 写订单 =====
            var oldOperatorId = order.OperatorUserId;
            order.OperatorUserId = opIds[0];
            order.OperatorUserIdsJson = JsonSerializer.Serialize(opIds);
            order.BasePrice = basePrice;
            order.OperatorSurcharge = Math.Round(totalSurcharge, 2);
            order.Price = newPrice;
            order.CommissionRate = avgRate;
            order.PlatformShare = Math.Round(newPrice * avgRate, 2);

            // ⭐⭐⭐ 关键：客服指派 = 打手已接单 ⭐⭐⭐
            order.Status = "accepted";
            order.AcceptedAt = DateTime.UtcNow;

            // ⭐ 每个打手：写 accept 日志 + ActiveOrderCount++
            foreach (var opId in opIds)
            {
                _db.OrderAssignmentLogs.Add(new OrderAssignmentLog
                {
                    OrderNo = order.OrderNo,
                    Action = "accept",
                    ActorId = opId,
                    TargetId = opId,
                    Note = "客服指派自动接单"
                });

                var opProfile = await _db.OperatorProfiles
                    .FirstOrDefaultAsync(p => p.UserId == opId);
                if (opProfile != null)
                {
                    opProfile.ActiveOrderCount += 1;
                    opProfile.Status = "busy";
                    opProfile.LastSeenAt = DateTime.UtcNow;
                }
            }

            // ===== 记录客服操作日志 =====
            _db.OrderAssignmentLogs.Add(new OrderAssignmentLog
            {
                OrderNo = order.OrderNo,
                Action = "assign_op",
                ActorId = userId,
                TargetId = opIds[0],
                Note = oldOperatorId == Guid.Empty
                    ? $"客服指派 {opIds.Count} 位打手"
                    : $"客服改派打手（原 {oldOperatorId}）"
            });

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "已指派打手",
                status = order.Status,
                operatorUserIds = opIds,
                price = newPrice,
                basePrice = basePrice,
                operatorSurcharge = order.OperatorSurcharge,
                commissionRate = avgRate
            });
        }









        // ==================================================
        //  7. 可指派打手列表（按游戏过滤）
        // ==================================================
        [HttpGet("operators")]
        public async Task<IActionResult> GetAssignableOperators([FromQuery] string gameCode)
        {
            var userId = GetUserId();

            var cs = await _db.CustomerServices
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (cs == null || cs.AuditStatus != "approved")
                return BadRequest(new { message = "你的客服身份未通过审核" });

            if (string.IsNullOrWhiteSpace(gameCode))
                return BadRequest(new { message = "gameCode 不能为空" });

            var skills = await _db.OperatorGameSkills
                .AsNoTracking()
                .Where(s => s.GameCode == gameCode && s.AuditStatus == "approved")
                .ToListAsync();

            if (skills.Count == 0)
                return Ok(new { total = 0, items = Array.Empty<object>() });

            var userIds = skills.Select(s => s.UserId).Distinct().ToList();

            var profiles = await _db.OperatorProfiles
                .AsNoTracking()
                .Where(p => userIds.Contains(p.UserId))
                .ToListAsync();

            var items = profiles
                .OrderBy(p => p.Status == "offline" ? 2 : (p.ActiveOrderCount == 0 ? 0 : 1))
                .ThenByDescending(p => p.Reputation)
                .Select(p =>
                {
                    var skill = skills.First(s => s.UserId == p.UserId);
                    return new
                    {
                        userId = p.UserId,
                        name = p.Nickname,
                        status = p.Status,
                        activeOrderCount = p.ActiveOrderCount,
                        reputation = p.Reputation,
                        totalOrders = p.TotalOrders,
                        avgOverall = Math.Round(p.AvgOverall, 2),
                        reviewCount = p.ReviewCount,
                        level = skill.OperatorLevel,
                        levelSurchargeRate = skill.LevelSurchargeRate,
                        code = skill.Code,
                        ordersInGame = skill.OrdersInGame
                    };
                })
                .ToList();

            return Ok(new { total = items.Count, items });
        }






        // ==================================================
        //  8. ⭐ 换打手（炸单换人）
        // ==================================================
        // ==================================================
        //  8. ⭐ 换打手（炸单换人）
        // ==================================================
        [HttpPost("orders/{orderNo}/reassign-operators")]
        public async Task<IActionResult> ReassignOperators(
            string orderNo,
            [FromBody] ReassignOperatorsDto dto)
        {
            var userId = GetUserId();

            var cs = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (cs == null || cs.AuditStatus != "approved")
                return BadRequest(new { message = "你的客服身份未通过审核" });

            var order = await _db.ClubOrders
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.CustomerServiceId != cs.Id)
                return StatusCode(403, new { message = "这不是你负责的订单" });

            if (order.Status != "processing")
                return BadRequest(new { message = $"当前状态 {order.Status}，只有服务中才能换人" });

            if (string.IsNullOrWhiteSpace(dto?.Reason))
                return BadRequest(new { message = "请填写换人原因" });

            var newOpIds = (dto.NewOperatorUserIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (newOpIds.Count == 0)
                return BadRequest(new { message = "请至少选择一位新打手" });

            // ===== 订单类型的打手数量约束 =====
            var orderType = await _db.ClubOrderTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.GameCode == order.GameCode
                                        && t.Code == order.OrderTypeCode);
            if (orderType != null)
            {
                if (newOpIds.Count < orderType.MinOperatorCount)
                    return BadRequest(new { message = $"本订单至少需要 {orderType.MinOperatorCount} 位打手" });
                if (newOpIds.Count > orderType.MaxOperatorCount)
                    return BadRequest(new { message = $"本订单最多 {orderType.MaxOperatorCount} 位打手" });
            }

            // ===== 解析原打手列表 =====
            var oldOpIds = ParseOperatorUserIds(order.OperatorUserIdsJson);
            if (oldOpIds.Count == 0 && order.OperatorUserId != Guid.Empty)
                oldOpIds.Add(order.OperatorUserId);

            // ===== 校验每个新打手（认证 + 在线）=====
            //   保留在原队的打手跳过在线检查
            foreach (var opId in newOpIds)
            {
                var skill = await _db.OperatorGameSkills.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == opId
                                           && s.GameCode == order.GameCode
                                           && s.AuditStatus == "approved");
                if (skill == null)
                    return BadRequest(new { message = "有打手未通过此游戏认证" });

                var opProfile = await _db.OperatorProfiles.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == opId);
                if (opProfile == null)
                    return BadRequest(new { message = "有打手档案不存在" });

                if (!oldOpIds.Contains(opId) && opProfile.Status == "offline")
                    return BadRequest(new { message = $"打手【{opProfile.Nickname}】当前离线" });
            }

            // ===== 计算差集 =====
            var removedIds = oldOpIds.Where(id => !newOpIds.Contains(id)).ToList();
            var addedIds = newOpIds.Where(id => !oldOpIds.Contains(id)).ToList();

            // ===== 被移除的打手 =====
            foreach (var opId in removedIds)
            {
                var profile = await _db.OperatorProfiles
                    .FirstOrDefaultAsync(p => p.UserId == opId);
                if (profile != null)
                {
                    profile.ActiveOrderCount = Math.Max(0, profile.ActiveOrderCount - 1);
                    if (profile.ActiveOrderCount == 0 && profile.Status == "busy")
                        profile.Status = "online";

                    var skill = await _db.OperatorGameSkills
                        .FirstOrDefaultAsync(s => s.UserId == opId
                                               && s.GameCode == order.GameCode);
                    if (skill != null) skill.FailedOrdersInGame += 1;
                }

                _db.OrderAssignmentLogs.Add(new OrderAssignmentLog
                {
                    OrderNo = orderNo,
                    Action = "abandon",
                    ActorId = opId,
                    TargetId = opId,
                    Note = "客服换人移除"
                });
            }

            // ===== 新加入的打手 =====
            foreach (var opId in addedIds)
            {
                var profile = await _db.OperatorProfiles
                    .FirstOrDefaultAsync(p => p.UserId == opId);
                if (profile != null)
                {
                    profile.ActiveOrderCount += 1;
                    profile.Status = "busy";
                    profile.LastSeenAt = DateTime.UtcNow;
                }

                _db.OrderAssignmentLogs.Add(new OrderAssignmentLog
                {
                    OrderNo = orderNo,
                    Action = "accept",
                    ActorId = opId,
                    TargetId = opId,
                    Note = "换人自动接单"
                });
            }

            // ==================================================
            //  ⭐⭐⭐ 抽成率重算 ⭐⭐⭐
            //  按新打手组合各自的 TotalOrders 档位，取平均
            //  护航单 / 陪玩单都适用（价格不变，只重算抽成）
            // ==================================================
            var newRates = new List<decimal>();
            foreach (var opId in newOpIds)
            {
                var profile = await _db.OperatorProfiles.AsNoTracking()
                    .FirstOrDefaultAsync(p => p.UserId == opId);
                var totalOrders = profile?.TotalOrders ?? 0;
                newRates.Add(GetOperatorCommissionRate(totalOrders));
            }
            var newAvgRate = newRates.Count > 0
                ? Math.Round(newRates.Average(), 3)
                : 0.25m;

            var oldRate = order.CommissionRate;
            order.CommissionRate = newAvgRate;
            order.PlatformShare = Math.Round(order.Price * newAvgRate, 2);

            // ⚠️ 价格不变（护航单固定价，陪玩单原价也保持，差价线下处理）
            //    如果以后陪玩单要重算价格，在这里加逻辑：
            //    if (orderType?.PricingMode == "level-based") { ... 重算 Price ... }

            // ===== 更新订单主字段 =====
            order.OperatorUserIdsJson = JsonSerializer.Serialize(newOpIds);
            order.OperatorUserId = newOpIds[0];

            // ===== 换人历史日志 =====
            var oldNames = await GetOperatorNames(oldOpIds);
            var newNames = await GetOperatorNames(newOpIds);

            _db.OrderAssignmentLogs.Add(new OrderAssignmentLog
            {
                OrderNo = orderNo,
                Action = "reassign",
                ActorId = userId,
                Note = $"{oldNames} → {newNames} · 原因：{dto.Reason.Trim()}"
            });

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "已换人",
                status = order.Status,
                operators = newOpIds,
                oldNames,
                newNames,
                removedCount = removedIds.Count,
                addedCount = addedIds.Count,

                // ⭐ 返回抽成变化，前端可提示
                oldCommissionRate = oldRate,
                newCommissionRate = newAvgRate,
                newPlatformShare = order.PlatformShare
            });
        }













        // ==================================================
        //  6.5 打手放弃接单（从订单移除自己）
        // ==================================================
        [HttpPost("{orderNo}/abandon")]
        public async Task<IActionResult> Abandon(string orderNo)
        {
            var operatorId = GetUserId();
            var order = await _db.ClubOrders.FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.Status != "pending" && order.Status != "accepted")
                return BadRequest(new { message = $"当前状态 {order.Status}，无法放弃。如需更换请联系客服" });

            var opIds = ParseOperatorUserIds(order.OperatorUserIdsJson);
            if (opIds.Count == 0 && order.OperatorUserId != Guid.Empty)
                opIds.Add(order.OperatorUserId);

            if (!opIds.Contains(operatorId))
                return StatusCode(403, new { message = "你不在此订单的打手名单中" });

            // 移除自己
            opIds.Remove(operatorId);
            order.OperatorUserIdsJson = JsonSerializer.Serialize(opIds);

            if (order.OperatorUserId == operatorId)
                order.OperatorUserId = opIds.Count > 0 ? opIds[0] : Guid.Empty;

            // 放弃者状态回落
            var profile = await _db.OperatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == operatorId);
            if (profile != null)
            {
                profile.ActiveOrderCount = Math.Max(0, profile.ActiveOrderCount - 1);
                if (profile.ActiveOrderCount == 0 && profile.Status == "busy")
                    profile.Status = "online";
            }

            // 放弃日志
            _db.OrderAssignmentLogs.Add(new OrderAssignmentLog
            {
                OrderNo = orderNo,
                Action = "abandon",
                ActorId = operatorId,
                TargetId = operatorId,
                Note = "打手放弃接单"
            });

            var orderType = await _db.ClubOrderTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.GameCode == order.GameCode
                                       && t.Code == order.OrderTypeCode);

            if (opIds.Count == 0)
            {
                // 全走了 → 回公海
                order.OperatorUserId = Guid.Empty;
                order.OperatorUserIdsJson = "[]";
                if (order.Status == "accepted")
                    order.Status = "pending";
            }
            else if (orderType != null && opIds.Count < orderType.MinOperatorCount)
            {
                // ⭐ 双打手场景：剩余人数不足 min
                //    保持 accepted，不强行回退（否则剩余打手会看到"接单"按钮，状态错位）
                //    前端应显示"X/min 等待客服补人"；剩余打手不能点开始（Start 会二次校验）
            }
            // else：剩余人数充足，状态不变

            await _db.SaveChangesAsync();
            return Ok(new { message = "已放弃接单", status = order.Status });
        }












        // ==================================================
        //  10. ⭐ 我负责的订单的客诉列表
        // ==================================================
        [HttpGet("complaints")]
        public async Task<IActionResult> GetComplaints(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var userId = GetUserId();

            var cs = await _db.CustomerServices
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (cs == null || cs.AuditStatus != "approved")
                return BadRequest(new { message = "你的客服身份未通过审核" });

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var orderNos = await _db.ClubOrders
                .AsNoTracking()
                .Where(o => o.CustomerServiceId == cs.Id)
                .Select(o => o.OrderNo)
                .ToListAsync();

            var q = _db.OrderComplaints
                .AsNoTracking()
                .Where(c => orderNos.Contains(c.OrderNo));

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(c => c.Status == status);

            q = q.OrderByDescending(c => c.CreatedAt);

            var total = await q.CountAsync();

            var raw = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = await EnrichComplaints(raw);
            return Ok(new { total, page, pageSize, items });
        }

        // ==================================================
        //  11. ⭐ 处理客诉（客服）
        // ==================================================
        [HttpPost("complaints/{id:long}/handle")]
        public async Task<IActionResult> HandleComplaint(
            long id,
            [FromBody] HandleComplaintDto dto)
        {
            var userId = GetUserId();

            var cs = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.UserId == userId);
            if (cs == null || cs.AuditStatus != "approved")
                return BadRequest(new { message = "你的客服身份未通过审核" });

            var complaint = await _db.OrderComplaints
                .FirstOrDefaultAsync(c => c.Id == id);
            if (complaint == null) return NotFound(new { message = "客诉不存在" });

            var order = await _db.ClubOrders
                .AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderNo == complaint.OrderNo);
            if (order == null)
                return BadRequest(new { message = "关联订单不存在" });

            if (order.CustomerServiceId != cs.Id)
                return StatusCode(403, new { message = "这不是你负责的订单" });

            if (complaint.Status != "pending")
                return BadRequest(new { message = "该客诉已处理" });

            if (string.IsNullOrWhiteSpace(dto?.Note))
                return BadRequest(new { message = "请填写回复内容" });

            var validActions = new[] { "resolve", "reject" };
            if (!validActions.Contains(dto.Action))
                return BadRequest(new { message = "操作无效，仅支持 resolve / reject" });

            complaint.Status = dto.Action == "resolve" ? "resolved" : "rejected";
            complaint.HandlerId = userId;
            complaint.HandlerName = cs.Name;
            complaint.HandlerNote = dto.Note.Trim();
            complaint.HandledAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = dto.Action == "resolve" ? "已处理" : "已驳回",
                status = complaint.Status
            });
        }

        // ==================================================
        //  ⭐ 内部：抽成规则
        // ==================================================
        private static decimal GetOperatorCommissionRate(int totalOrders)
        {
            if (totalOrders < 15) return 0.25m;
            if (totalOrders < 30) return 0.20m;
            return 0.15m;
        }

        private async Task<string> GetOperatorNames(List<Guid> ids)
        {
            if (ids.Count == 0) return "无";
            var profiles = await _db.OperatorProfiles.AsNoTracking()
                .Where(p => ids.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId, p => p.Nickname);
            return string.Join("、", ids.Select(id =>
                profiles.TryGetValue(id, out var n) ? n : "未知"));
        }

        // ==================================================
        //  内部：订单富化
        // ==================================================
        private async Task<List<object>> EnrichOrders(List<ClubOrder> orders)
        {
            if (orders.Count == 0) return new();

            var allOpIds = new HashSet<Guid>();
            foreach (var o in orders)
            {
                foreach (var id in ParseOperatorUserIds(o.OperatorUserIdsJson))
                    allOpIds.Add(id);
                if (o.OperatorUserId != Guid.Empty)
                    allOpIds.Add(o.OperatorUserId);
            }

            var custIds = orders.Select(o => o.CustomerId).Distinct().ToList();
            var gameCodes = orders.Select(o => o.GameCode).Distinct().ToList();

            var profiles = await _db.OperatorProfiles
                .AsNoTracking()
                .Where(p => allOpIds.Contains(p.UserId))
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
            var otMap = orderTypes.ToDictionary(t => $"{t.GameCode}|{t.Code}");

            var orderNos = orders.Select(o => o.OrderNo).Distinct().ToList();
            var acceptLogs = await _db.OrderAssignmentLogs.AsNoTracking()
                .Where(l => orderNos.Contains(l.OrderNo) && l.Action == "accept")
                .ToListAsync();
            var acceptMap = acceptLogs
                .Where(l => l.ActorId.HasValue)
                .GroupBy(l => l.OrderNo)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.ActorId!.Value).ToHashSet()
                );

            var reassignLogs = await _db.OrderAssignmentLogs.AsNoTracking()
                .Where(l => orderNos.Contains(l.OrderNo) && l.Action == "reassign")
                .OrderBy(l => l.CreatedAt)
                .ToListAsync();
            var reassignMap = reassignLogs
                .GroupBy(l => l.OrderNo)
                .ToDictionary(g => g.Key, g => g.ToList());

            return orders.Select(o =>
            {
                customers.TryGetValue(o.CustomerId, out var cu);
                games.TryGetValue(o.GameCode, out var g);
                otMap.TryGetValue($"{o.GameCode}|{o.OrderTypeCode}", out var t);

                var opIds = ParseOperatorUserIds(o.OperatorUserIdsJson);
                if (opIds.Count == 0 && o.OperatorUserId != Guid.Empty)
                    opIds.Add(o.OperatorUserId);

                acceptMap.TryGetValue(o.OrderNo, out var acceptedSet);
                acceptedSet ??= new HashSet<Guid>();

                var opList = opIds
                    .Select(id =>
                    {
                        profiles.TryGetValue(id, out var p);
                        return new
                        {
                            userId = id,
                            name = p?.Nickname ?? "",
                            accepted = acceptedSet.Contains(id)
                        };
                    })
                    .ToList();

                var acceptedCount = opList.Count(x => x.accepted);
                var orderTypeMax = t?.MaxOperatorCount ?? opList.Count;
                var totalOperatorCount = Math.Max(opList.Count, orderTypeMax);

                var primaryOp = profiles.TryGetValue(o.OperatorUserId, out var p) ? p : null;

                reassignMap.TryGetValue(o.OrderNo, out var reassigns);
                var reassignHistory = (reassigns ?? new List<OrderAssignmentLog>())
                    .Select(l => new
                    {
                        note = l.Note,
                        at = l.CreatedAt
                    })
                    .ToList();

                return (object)new
                {
                    orderNo = o.OrderNo,
                    status = o.Status,
                    price = o.Price,
                    basePrice = o.BasePrice,
                    operatorSurcharge = o.OperatorSurcharge,
                    commissionRate = o.CommissionRate,
                    platformShare = o.PlatformShare,
                    remark = o.Remark,
                    csRemark = o.CsRemark,
                    failedCount = o.FailedCount,
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

                    pricingMode = t?.PricingMode,
                    minOperatorCount = t?.MinOperatorCount ?? 1,
                    maxOperatorCount = t?.MaxOperatorCount ?? 1,

                    customerId = o.CustomerId,
                    customerName = cu?.Username ?? "",

                    operatorUserId = o.OperatorUserId == Guid.Empty ? (Guid?)null : o.OperatorUserId,
                    operatorName = primaryOp?.Nickname ?? "",
                    operatorUserIds = opIds,
                    operators = opList,
                    operatorCount = opList.Count,

                    acceptedCount,
                    totalOperatorCount,
                    acceptedOperatorIds = opList
                        .Where(x => x.accepted)
                        .Select(x => x.userId)
                        .ToList(),

                    reassignHistory,

                    settlementProofUrl = o.SettlementProofUrl,
                    settlementProofUploaded = o.SettlementProofUploaded,

                    @params = ParseJsonDict(o.ParamsJson)
                };
            }).ToList();
        }

        // ==================================================
        //  内部：客诉富化
        // ==================================================
        private async Task<List<object>> EnrichComplaints(List<OrderComplaint> list)
        {
            if (list.Count == 0) return new();

            var opIds = list
                .Where(c => c.TargetOperatorId.HasValue)
                .Select(c => c.TargetOperatorId!.Value)
                .Distinct()
                .ToList();

            var opMap = await _db.OperatorProfiles.AsNoTracking()
                .Where(p => opIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId, p => p.Nickname);

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
                    complaintTypeLabel = ComplaintTypeLabel(c.ComplaintType),
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

        private static string ComplaintTypeLabel(string type) => type switch
        {
            "attitude" => "服务态度差",
            "late" => "迟到 / 缺席",
            "runner" => "打手跑路",
            "quality" => "服务质量差",
            "harassment" => "辱骂 / 骚扰",
            "other" => "其他",
            _ => type
        };

        // ==================================================
        //  内部：解析
        // ==================================================
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

        private static Dictionary<string, string> ParseJsonDict(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            catch { return new(); }
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
        public class CsApplyDto
        {
            public string Name { get; set; } = "";
            public string Wechat { get; set; } = "";
            public string OnlineTime { get; set; } = "";
            public string? Intro { get; set; }
            public int Capacity { get; set; } = 10;
        }

        public class CsStatusDto
        {
            public string Status { get; set; } = "";
        }

        public class AssignOperatorDto
        {
            public List<Guid>? OperatorUserIds { get; set; }
        }

        public class ReassignOperatorsDto
        {
            public List<Guid>? NewOperatorUserIds { get; set; }
            public string? Reason { get; set; }
        }

        public class AbandonOrderDto
        {
            public string? Reason { get; set; }
        }

        public class HandleComplaintDto
        {
            /// <summary>resolve = 已处理 / reject = 驳回</summary>
            public string Action { get; set; } = "";

            /// <summary>回复老板的内容（必填）</summary>
            public string? Note { get; set; }
        }
    }
}