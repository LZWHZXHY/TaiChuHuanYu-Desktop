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
        //  1. 老板下单（支持多打手 + 分级定价）
        // ==================================================
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateOrderDto dto)
        {
            var customerId = GetUserId();

            // ① 游戏
            var game = await _db.ClubGames.AsNoTracking()
                .FirstOrDefaultAsync(g => g.Code == dto.GameCode && g.IsActive);
            if (game == null) return BadRequest(new { message = "游戏不存在或已下架" });

            // ② 订单类型
            var orderType = await _db.ClubOrderTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.GameCode == dto.GameCode
                                        && t.Code == dto.OrderTypeCode
                                        && t.IsActive);
            if (orderType == null) return BadRequest(new { message = "订单类型不存在或已停用" });

            // ③ 解析打手列表（去重）
            var opIds = (dto.OperatorUserIds ?? new List<Guid>())
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            bool isCsAssign = opIds.Count == 0;

            // 数量校验
            if (!isCsAssign)
            {
                if (opIds.Count < orderType.MinOperatorCount)
                    return BadRequest(new { message = $"本订单至少需要 {orderType.MinOperatorCount} 位打手" });
                if (opIds.Count > orderType.MaxOperatorCount)
                    return BadRequest(new { message = $"本订单最多 {orderType.MaxOperatorCount} 位打手" });
            }

            // ④ 校验打手（认证 + 在线）
            var validSkills = new List<OperatorGameSkill>();
            if (!isCsAssign)
            {
                foreach (var opId in opIds)
                {
                    var skill = await _db.OperatorGameSkills.AsNoTracking()
                        .FirstOrDefaultAsync(s => s.UserId == opId
                                               && s.GameCode == dto.GameCode
                                               && s.AuditStatus == "approved");
                    if (skill == null)
                        return BadRequest(new { message = "有打手未通过此游戏认证" });

                    var opProfile = await _db.OperatorProfiles.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.UserId == opId);
                    if (opProfile == null)
                        return BadRequest(new { message = "有打手档案不存在" });
                    if (opProfile.Status == "offline")
                        return BadRequest(new { message = $"打手【{opProfile.Nickname}】当前离线" });

                    validSkills.Add(skill);
                }
            }

            // ⑤ 参数校验
            var paramError = ValidateParams(orderType.ParamsSchemaJson, dto.Params);
            if (paramError != null) return BadRequest(new { message = paramError });

            // ⑥ 价格计算
            var basePrice = ResolveBasePrice(orderType.PricingJson, dto.Params);
            if (basePrice <= 0) return BadRequest(new { message = "订单类型价格配置错误" });

            decimal totalSurcharge = 0;

            if (orderType.PricingMode == "level-based")
            {
                if (!isCsAssign)
                {
                    foreach (var skill in validSkills)
                        totalSurcharge += basePrice * skill.LevelSurchargeRate;
                }
                else
                {
                    const decimal MAX_RATE = 0.50m;
                    var maxCount = Math.Max(1, orderType.MaxOperatorCount);
                    totalSurcharge = basePrice * MAX_RATE * maxCount;
                }
            }

            var price = Math.Round(basePrice + totalSurcharge, 2);

            // ⑦ 抽成率（多人取平均）
            // ⭐ 新规则：<15 单 25%；15-30 单 20%；≥30 单 15%
            decimal avgRate = 0.25m;
            if (!isCsAssign && opIds.Count > 0)
            {
                var rates = new List<decimal>();
                foreach (var opId in opIds)
                {
                    var profile = await _db.OperatorProfiles.AsNoTracking()
                        .FirstOrDefaultAsync(p => p.UserId == opId);
                    var totalOrders = profile?.TotalOrders ?? 0;
                    rates.Add(GetOperatorCommissionRate(totalOrders));
                }
                avgRate = Math.Round(rates.Average(), 3);
            }
            // 客服指派模式：暂按 25%（客服指派打手后由客服端重算）

            var platformShare = Math.Round(price * avgRate, 2);

            // ⑧ 客服归属
            var source = dto.Source == "cs" ? "cs" : "self";
            Guid? csId = null;

            if (source == "cs")
            {
                if (!dto.CustomerServiceId.HasValue)
                    return BadRequest(new { message = "客服拉单必须指定客服" });
                var cs = await _db.CustomerServices.AsNoTracking()
                    .FirstOrDefaultAsync(c => c.Id == dto.CustomerServiceId.Value && c.IsActive);
                if (cs == null) return BadRequest(new { message = "指定的客服不存在" });
                if (cs.Status == "offline") return BadRequest(new { message = "该客服当前离线" });
                csId = cs.Id;
            }
            else
            {
                csId = await _db.CustomerServices.AsNoTracking()
                    .Where(c => c.IsActive && c.IsAssignable
                             && c.Status != "offline"
                             && c.CurrentLoad < c.Capacity)
                    .OrderBy(c => c.CurrentLoad)
                    .Select(c => (Guid?)c.Id)
                    .FirstOrDefaultAsync();
            }

            // ⑨ 订单号
            var orderNo = await GenerateOrderNo();

            // ⑩ 落库
            var order = new ClubOrder
            {
                OrderNo = orderNo,
                CustomerId = customerId,
                OperatorUserId = opIds.Count > 0 ? opIds[0] : Guid.Empty,
                OperatorUserIdsJson = JsonSerializer.Serialize(opIds),
                GameCode = dto.GameCode,
                OrderTypeCode = dto.OrderTypeCode,
                ParamsJson = JsonSerializer.Serialize(dto.Params ?? new Dictionary<string, string>()),
                Price = price,
                BasePrice = basePrice,
                OperatorSurcharge = Math.Round(totalSurcharge, 2),
                CommissionRate = avgRate,
                PlatformShare = platformShare,
                Status = "pending",
                Remark = dto.Remark,
                Source = source,
                CustomerServiceId = csId,
                CreatedAt = DateTime.UtcNow
            };
            _db.ClubOrders.Add(order);

            // 客服负载 +1
            if (csId.HasValue)
            {
                var cs = await _db.CustomerServices.FindAsync(csId.Value);
                if (cs != null) cs.CurrentLoad++;
            }

            _db.OrderAssignmentLogs.Add(new OrderAssignmentLog
            {
                OrderNo = orderNo,
                Action = "assign_cs",
                TargetId = csId,
                Note = source == "cs" ? "客服拉单指定" : "系统随机分配"
            });

            await _db.SaveChangesAsync();

            // ⑪ 返回
            object? csInfo = null;
            if (csId.HasValue)
            {
                csInfo = await _db.CustomerServices.AsNoTracking()
                    .Where(c => c.Id == csId.Value)
                    .Select(c => new { id = c.Id, name = c.Name, wechat = c.Wechat })
                    .FirstOrDefaultAsync();
            }

            var opInfos = new List<object>();
            if (opIds.Count > 0)
            {
                var profiles = await _db.OperatorProfiles.AsNoTracking()
                    .Where(p => opIds.Contains(p.UserId))
                    .ToListAsync();
                opInfos = profiles.Select(p => (object)new
                {
                    userId = p.UserId,
                    name = p.Nickname
                }).ToList();
            }

            return Ok(new
            {
                orderNo = order.OrderNo,
                price = order.Price,
                basePrice = order.BasePrice,
                operatorSurcharge = order.OperatorSurcharge,
                commissionRate = order.CommissionRate,
                platformShare = order.PlatformShare,
                status = order.Status,
                source = order.Source,
                customerService = csInfo,
                operators = opInfos
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

            var q = _db.ClubOrders.AsNoTracking()
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
        //  ⚠️ 不用 SQL LIKE 查 JSON 列（MySQL 会报 500），
        //     先取候选集，再内存过滤 + 手动分页。
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

            // ① 取所有候选项（不带 JSON 过滤）
            var q = _db.ClubOrders.AsNoTracking()
                .Where(o => o.PaidConfirmedAt != null || o.Status != "pending");

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(o => o.Status == status);

            var allCandidates = await q
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            // ② 内存过滤：公海单 / 主打手 / JSON 列表包含我
            var filtered = allCandidates.Where(o =>
            {
                if (o.OperatorUserId == Guid.Empty) return true;   // 公海单
                if (o.OperatorUserId == operatorId) return true;   // 主打手（兼容旧数据）
                var ids = ParseOperatorUserIds(o.OperatorUserIdsJson);
                return ids.Contains(operatorId);                   // 多打手之一
            }).ToList();

            var total = filtered.Count;

            // ③ 手动分页
            var pageItems = filtered
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = await EnrichOrders(pageItems);
            return Ok(new { total, page, pageSize, items });
        }

        // ==================================================
        //  4. 订单详情
        // ==================================================
        [HttpGet("{orderNo}")]
        public async Task<IActionResult> Detail(string orderNo)
        {
            var userId = GetUserId();

            var order = await _db.ClubOrders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            var opIds = ParseOperatorUserIds(order.OperatorUserIdsJson);
            bool isCustomer = order.CustomerId == userId;
            bool isOperator = opIds.Contains(userId) || order.OperatorUserId == userId;
            bool isUnassigned = order.OperatorUserId == Guid.Empty;

            if (!isCustomer && !isOperator && !isUnassigned)
                return StatusCode(403, new { message = "无权查看该订单" });

            var enriched = await EnrichOrders(new List<ClubOrder> { order });
            return Ok(enriched.FirstOrDefault());
        }

        // ==================================================
        //  5. 打手接单（多人需全部接单）
        // ==================================================
        // ==================================================
        //  5. 打手接单 / 抢单
        // ==================================================
        [HttpPost("{orderNo}/accept")]
        public async Task<IActionResult> Accept(string orderNo)
        {
            var operatorId = GetUserId();
            var order = await _db.ClubOrders.FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.Status != "pending" && order.Status != "accepted")
                return BadRequest(new { message = $"当前状态 {order.Status}，无法接单" });

            if (!order.PaidConfirmedAt.HasValue)
                return BadRequest(new { message = "客服尚未确认收款，暂时无法接单" });

            var orderType = await _db.ClubOrderTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.GameCode == order.GameCode
                                       && t.Code == order.OrderTypeCode);
            if (orderType == null)
                return BadRequest(new { message = "订单类型不存在" });

            var opIds = ParseOperatorUserIds(order.OperatorUserIdsJson);
            if (opIds.Count == 0 && order.OperatorUserId != Guid.Empty)
                opIds.Add(order.OperatorUserId);

            bool isCsAssignOrder = order.OperatorUserId != Guid.Empty;

            if (isCsAssignOrder)
            {
                // 客服指派单：必须在 opIds 里
                if (!opIds.Contains(operatorId))
                    return StatusCode(403, new { message = "这不是属于你的订单" });
            }
            else
            {
                // 公海单：检查是否已满
                if (opIds.Count >= orderType.MaxOperatorCount)
                    return BadRequest(new { message = "打手已满，无法抢单" });

                // ⭐ 检查是否已经抢过
                if (opIds.Contains(operatorId))
                    return BadRequest(new { message = "你已经抢过这单了" });

                // 检查游戏认证
                var skill = await _db.OperatorGameSkills.AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == operatorId
                                           && s.GameCode == order.GameCode
                                           && s.AuditStatus == "approved");
                if (skill == null)
                    return BadRequest(new { message = "你未通过此游戏认证" });

                opIds.Add(operatorId);
                order.OperatorUserIdsJson = JsonSerializer.Serialize(opIds);
            }

            // ⭐ 关键修复：检查该打手对这张单的"最后一次"操作
            //    - 最后一次是 accept → 已经接过，不允许重复
            //    - 最后一次是 abandon 或没有记录 → 允许接单
            var lastAction = await _db.OrderAssignmentLogs
                .AsNoTracking()
                .Where(l => l.OrderNo == orderNo
                         && l.ActorId == operatorId
                         && (l.Action == "accept" || l.Action == "abandon"))
                .OrderByDescending(l => l.Id)
                .Select(l => l.Action)
                .FirstOrDefaultAsync();

            if (lastAction == "accept")
                return BadRequest(new { message = "你已经接过这个订单了，等待其他打手" });

            // 记录接单
            _db.OrderAssignmentLogs.Add(new OrderAssignmentLog
            {
                OrderNo = orderNo,
                Action = "accept",
                ActorId = operatorId,
                TargetId = operatorId,
                Note = "打手接单"
            });

            // 打手状态置 busy
            var profile = await _db.OperatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == operatorId);
            if (profile != null)
            {
                profile.ActiveOrderCount += 1;
                profile.Status = "busy";
                profile.LastSeenAt = DateTime.UtcNow;
            }

            // 检查是否凑够 min + 全部接单
            var acceptedIds = await _db.OrderAssignmentLogs
                .AsNoTracking()
                .Where(l => l.OrderNo == orderNo && l.Action == "accept")
                .Select(l => l.ActorId)
                .ToListAsync();
            var acceptedSet = acceptedIds
                .Where(a => a.HasValue)
                .Select(a => a!.Value)
                .ToHashSet();
            acceptedSet.Add(operatorId);

            bool isFull = opIds.Count >= orderType.MaxOperatorCount;
            bool allAccepted = opIds.Count >= orderType.MinOperatorCount
                            && opIds.All(id => acceptedSet.Contains(id));

            if (allAccepted && order.Status == "pending")
            {
                order.Status = "accepted";
                order.AcceptedAt = DateTime.UtcNow;

                // 锁定订单
                if (order.OperatorUserId == Guid.Empty && opIds.Count > 0)
                    order.OperatorUserId = opIds[0];
            }

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = isFull ? "全员接单，准备开始服务" :
                          (opIds.Count >= orderType.MinOperatorCount ? "已接单" : "已接单，等待其他打手"),
                status = order.Status,
                acceptedCount = opIds.Count(id => acceptedSet.Contains(id)),
                totalCount = orderType.MaxOperatorCount,
                minCount = orderType.MinOperatorCount,
                isFull
            });
        }














        // ==================================================
        //  6. 打手开始服务
        // ==================================================
        [HttpPost("{orderNo}/start")]
        public async Task<IActionResult> Start(string orderNo)
        {
            var operatorId = GetUserId();
            var order = await _db.ClubOrders.FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            var opIds = ParseOperatorUserIds(order.OperatorUserIdsJson);
            if (opIds.Count == 0 && order.OperatorUserId != Guid.Empty)
                opIds.Add(order.OperatorUserId);

            if (!opIds.Contains(operatorId) && order.OperatorUserId != operatorId)
                return StatusCode(403, new { message = "这不是属于你的订单" });

            if (order.Status != "accepted")
                return BadRequest(new { message = $"当前状态 {order.Status}，无法开始" });

            // ⭐ 人数不足不能开始
            var orderType = await _db.ClubOrderTypes.AsNoTracking()
                .FirstOrDefaultAsync(t => t.GameCode == order.GameCode
                                       && t.Code == order.OrderTypeCode);
            if (orderType != null && opIds.Count < orderType.MinOperatorCount)
                return BadRequest(new
                {
                    message = $"当前打手不足（{opIds.Count}/{orderType.MinOperatorCount}），等待客服补人"
                });

            order.Status = "processing";
            order.StartedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = "服务已开始", status = order.Status });
        }












        // ==================================================
        //  7. 完成订单（只有老板能调）
        //     老板结单时可选择上传结算截图（非必传）
        // ==================================================
        [HttpPost("{orderNo}/complete")]
        public async Task<IActionResult> Complete(string orderNo, [FromBody] CompleteOrderDto? dto)
        {
            var userId = GetUserId();
            var order = await _db.ClubOrders.FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            // ⭐ 只有老板能结单
            if (order.CustomerId != userId)
                return StatusCode(403, new { message = "只有下单老板才能完成订单" });

            if (order.Status != "processing")
                return BadRequest(new { message = $"当前状态 {order.Status}，无法完成" });

            // 结算截图（可选）
            if (!string.IsNullOrWhiteSpace(dto?.SettlementProofUrl))
            {
                order.SettlementProofUrl = dto.SettlementProofUrl.Trim();
                order.SettlementProofUploaded = true;
            }

            if (!string.IsNullOrWhiteSpace(dto?.Remark))
            {
                order.Remark = string.IsNullOrWhiteSpace(order.Remark)
                    ? $"结单备注：{dto.Remark}"
                    : $"{order.Remark} | 结单备注：{dto.Remark}";

            }
            // ⭐ 写入炸单次数
            order.FailedCount = dto?.FailedCount ?? 0;
            order.Status = "completed";
            order.CompletedAt = DateTime.UtcNow;

            var opIds = ParseOperatorUserIds(order.OperatorUserIdsJson);
            if (opIds.Count == 0 && order.OperatorUserId != Guid.Empty)
                opIds.Add(order.OperatorUserId);

            await SettleIncomeMultiple(order, opIds);
            await SettleCsIncome(order);                     // ⭐ 客服入账
            foreach (var opId in opIds)
                await UpdateOperatorStatsSingle(order, opId, success: true);
            await ReleaseCustomerService(order);

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "订单已完成",
                status = order.Status,
                settlementProofUploaded = order.SettlementProofUploaded
            });
        }

        // ==================================================
        //  7.5 上传结算截图（仅老板，结单前调用）
        // ==================================================
        [HttpPost("{orderNo}/settlement-proof")]
        public async Task<IActionResult> UploadSettlementProof(string orderNo, IFormFile file)
        {
            var userId = GetUserId();
            var order = await _db.ClubOrders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            if (order.CustomerId != userId)
                return StatusCode(403, new { message = "只有老板才能上传结算截图" });

            if (file == null || file.Length == 0)
                return BadRequest(new { message = "请选择图片" });

            if (file.Length > 5 * 1024 * 1024)
                return BadRequest(new { message = "图片不能超过 5MB" });

            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            var allowed = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            if (!allowed.Contains(ext))
                return BadRequest(new { message = "只支持 jpg / png / webp 格式" });

            var dir = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot", "uploads", "settlement-proofs");
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            var fileName = $"{orderNo}_{Guid.NewGuid():N}{ext}";
            var filePath = Path.Combine(dir, fileName);

            using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var url = $"/uploads/settlement-proofs/{fileName}";

            var dbOrder = await _db.ClubOrders
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (dbOrder != null)
            {
                dbOrder.SettlementProofUrl = url;
                dbOrder.SettlementProofUploaded = true;
                await _db.SaveChangesAsync();
            }

            return Ok(new { url, message = "上传成功" });
        }

        // ==================================================
        //  8. 取消订单
        // ==================================================
        [HttpPost("{orderNo}/cancel")]
        public async Task<IActionResult> Cancel(string orderNo, [FromBody] CancelOrderDto dto)
        {
            var userId = GetUserId();
            var order = await _db.ClubOrders.FirstOrDefaultAsync(o => o.OrderNo == orderNo);
            if (order == null) return NotFound(new { message = "订单不存在" });

            var opIds = ParseOperatorUserIds(order.OperatorUserIdsJson);

            if (order.CustomerId != userId && !opIds.Contains(userId))
                return StatusCode(403, new { message = "无权操作该订单" });

            if (order.Status != "pending" && order.Status != "accepted")
                return BadRequest(new { message = $"当前状态 {order.Status}，无法取消" });

            order.Status = "cancelled";
            order.CancelledAt = DateTime.UtcNow;
            order.Remark = string.IsNullOrWhiteSpace(dto?.Reason)
                ? order.Remark
                : $"{order.Remark} | 取消原因：{dto.Reason}";

            foreach (var opId in opIds)
                await UpdateOperatorStatsSingle(order, opId, success: false);

            await ReleaseCustomerService(order);

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
            var order = await _db.ClubOrders.FirstOrDefaultAsync(o => o.OrderNo == orderNo);
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
        //  ⭐ 内部：订单富化
        // ==================================================
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

            // ⭐ 大小写不敏感
            var orderTypes = await _db.ClubOrderTypes
                .AsNoTracking()
                .Where(t => gameCodes.Contains(t.GameCode))
                .ToListAsync();
            var otMap = orderTypes.ToDictionary(
                t => $"{t.GameCode}|{t.Code}".ToLower().Trim(),
                t => t
            );

            // ⭐ 每个订单每个打手的"最后一次" accept/abandon
            var orderNos = orders.Select(o => o.OrderNo).Distinct().ToList();

            var opActionLogs = await _db.OrderAssignmentLogs.AsNoTracking()
                .Where(l => orderNos.Contains(l.OrderNo)
                         && (l.Action == "accept" || l.Action == "abandon")
                         && l.ActorId.HasValue)
                .OrderBy(l => l.Id)
                .ToListAsync();

            var lastActionMap = opActionLogs
                .GroupBy(l => $"{l.OrderNo}|{l.ActorId}")
                .ToDictionary(g => g.Key, g => g.Last().Action);

            // 换人历史
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
                otMap.TryGetValue($"{o.GameCode}|{o.OrderTypeCode}".ToLower().Trim(), out var t);

                var opIds = ParseOperatorUserIds(o.OperatorUserIdsJson);
                if (opIds.Count == 0 && o.OperatorUserId != Guid.Empty)
                    opIds.Add(o.OperatorUserId);

                // ⭐ 用 lastActionMap 判断"当前是否已接单"
                var opList = opIds
                    .Select(id =>
                    {
                        profiles.TryGetValue(id, out var p);
                        var key = $"{o.OrderNo}|{id}";
                        bool accepted = lastActionMap.TryGetValue(key, out var act) && act == "accept";
                        return new
                        {
                            userId = id,
                            name = p?.Nickname ?? "",
                            accepted
                        };
                    })
                    .ToList();

                var acceptedCount = opList.Count(x => x.accepted);
                var orderTypeMax = t?.MaxOperatorCount ?? opList.Count;
                var totalOperatorCount = Math.Max(opList.Count, orderTypeMax);

                var primaryOp = profiles.TryGetValue(o.OperatorUserId, out var p) ? p : null;

                reassignMap.TryGetValue(o.OrderNo, out var reassigns);
                var reassignHistory = (reassigns ?? new List<OrderAssignmentLog>())
                    .Select(l => new { note = l.Note, at = l.CreatedAt })
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

                    // ⭐ 订单类型配置（前端用）
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
        //  6.5 ⭐ 打手放弃接单（从订单移除自己）
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

            // 从 opIds 移除
            opIds.Remove(operatorId);
            order.OperatorUserIdsJson = JsonSerializer.Serialize(opIds);

            // 如果我是主打手，换一个
            if (order.OperatorUserId == operatorId)
                order.OperatorUserId = opIds.Count > 0 ? opIds[0] : Guid.Empty;

            // 打手状态回落
            var profile = await _db.OperatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == operatorId);
            if (profile != null)
            {
                profile.ActiveOrderCount = Math.Max(0, profile.ActiveOrderCount - 1);
                if (profile.ActiveOrderCount == 0 && profile.Status == "busy")
                    profile.Status = "online";
            }

            // 记日志
            _db.OrderAssignmentLogs.Add(new OrderAssignmentLog
            {
                OrderNo = orderNo,
                Action = "abandon",
                ActorId = operatorId,
                TargetId = operatorId,
                Note = "打手放弃接单"
            });

            // 如果空了：回到公海
            if (opIds.Count == 0)
            {
                order.OperatorUserId = Guid.Empty;
                order.OperatorUserIdsJson = "[]";
                if (order.Status == "accepted")
                    order.Status = "pending";
            }
            else
            {
                // 还有人，但人数可能不够 min，回退到 pending
                var orderType = await _db.ClubOrderTypes.AsNoTracking()
                    .FirstOrDefaultAsync(t => t.GameCode == order.GameCode
                                           && t.Code == order.OrderTypeCode);
                if (orderType != null && opIds.Count < orderType.MinOperatorCount)
                {
                    if (order.Status == "accepted")
                        order.Status = "pending";
                }
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "已放弃接单", status = order.Status });
        }







        // ==================================================
        //  ⭐ 内部：抽成规则
        // ==================================================
        /// <summary>打手抽成率（按累计完成订单数分档）</summary>
        private static decimal GetOperatorCommissionRate(int totalOrders)
        {
            if (totalOrders < 15) return 0.25m;   // 新手
            if (totalOrders < 30) return 0.20m;   // 中间
            return 0.15m;                          // 老手
        }

        /// <summary>客服抽成率（按订单来源）</summary>
        private static decimal GetCsCommissionRate(string source)
        {
            return source == "cs" ? 0.10m : 0.05m;  // 自拉 10% / 匹配 5%
        }

        // ==================================================
        //  内部：解析基础价
        //  优先按 dto.Params 里选中的档位去 pricingJson 里查；
        //  查不到再回退到 "default"。
        // ==================================================
        private static decimal ResolveBasePrice(string? pricingJson, Dictionary<string, string>? orderParams)
        {
            if (string.IsNullOrWhiteSpace(pricingJson)) return 0;
            try
            {
                using var doc = JsonDocument.Parse(pricingJson);
                if (doc.RootElement.ValueKind != JsonValueKind.Object) return 0;

                // ① 优先用 params 里选中的值作为 key 去查价格
                if (orderParams != null)
                {
                    foreach (var kv in orderParams)
                    {
                        var key = kv.Value;
                        if (string.IsNullOrWhiteSpace(key)) continue;

                        if (doc.RootElement.TryGetProperty(key, out var priceEl)
                            && priceEl.ValueKind == JsonValueKind.Number)
                        {
                            return priceEl.GetDecimal();
                        }
                    }
                }

                // ② 回退到 default
                if (doc.RootElement.TryGetProperty("default", out var defEl)
                    && defEl.ValueKind == JsonValueKind.Number)
                {
                    return defEl.GetDecimal();
                }
            }
            catch { }
            return 0;
        }

        // ==================================================
        //  内部：解析 OperatorUserIdsJson
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

        // ==================================================
        //  内部：参数校验
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
            catch { }
            return null;
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
        //  ⭐ 内部：单个打手统计更新
        // ==================================================
        private async Task UpdateOperatorStatsSingle(ClubOrder order, Guid operatorUserId, bool success)
        {
            if (operatorUserId == Guid.Empty) return;

            var profile = await _db.OperatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == operatorUserId);
            if (profile == null) return;

            var skill = await _db.OperatorGameSkills
                .FirstOrDefaultAsync(s => s.UserId == operatorUserId
                                       && s.GameCode == order.GameCode);

            if (success)
            {
                profile.TotalOrders += 1;
                profile.CompletedOrders += 1;

                var previous = await _db.ClubOrders.AsNoTracking()
                    .AnyAsync(o => o.CustomerId == order.CustomerId
                                && o.OperatorUserId == operatorUserId
                                && o.Status == "completed"
                                && o.Id != order.Id);

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
                    skill.FailedOrdersInGame += 1;
            }

            // 打手状态回落（取消或完成）
            profile.ActiveOrderCount = Math.Max(0, profile.ActiveOrderCount - 1);
            if (profile.ActiveOrderCount == 0 && profile.Status == "busy")
                profile.Status = "online";
        }

        // ==================================================
        //  ⭐ 内部：释放客服负载
        // ==================================================
        private async Task ReleaseCustomerService(ClubOrder order)
        {
            if (!order.CustomerServiceId.HasValue) return;

            var cs = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.Id == order.CustomerServiceId.Value);
            if (cs != null)
                cs.CurrentLoad = Math.Max(0, cs.CurrentLoad - 1);
        }

        // ==================================================
        //  ⭐ 内部：多人结算（按平均抽成率）
        // ==================================================
        private const int FROZEN_DAYS = 3;

        private async Task SettleIncomeMultiple(ClubOrder order, List<Guid> operatorIds)
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
                    Remark = $"订单收益 · 总价 ¥{order.Price} · 抽成 {(commissionRate * 100):0}% · {operatorIds.Count} 人平分",
                    UnfreezeAt = DateTime.UtcNow.AddDays(FROZEN_DAYS),
                    CreatedAt = DateTime.UtcNow,
                    CompletedAt = DateTime.UtcNow
                });
            }
        }

        // ==================================================
        //  ⭐ 内部：客服入账（订单完成时）
        // ==================================================
        private async Task SettleCsIncome(ClubOrder order)
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
                Remark = $"订单收益 · 总价 ¥{order.Price} · 客服抽成 {(rate * 100):0}%",
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
        public class CompleteOrderDto
        {
            /// <summary>结算截图 URL（老板可选上传）</summary>
            public string? SettlementProofUrl { get; set; }

            /// <summary>结单备注（可选）</summary>
            public string? Remark { get; set; }

            /// <summary>炸单次数（撤离失败次数，默认 0）</summary>
            public int FailedCount { get; set; } = 0;
        }
        public class CreateOrderDto
        {
            public string GameCode { get; set; } = "";
            public string OrderTypeCode { get; set; } = "";

            /// <summary>打手列表（空数组 = 客服指派）</summary>
            public List<Guid>? OperatorUserIds { get; set; }

            public Dictionary<string, string>? Params { get; set; }
            public string? Remark { get; set; }

            public string Source { get; set; } = "self";     // 'cs' | 'self'
            public Guid? CustomerServiceId { get; set; }
        }

        public class CancelOrderDto
        {
            public string? Reason { get; set; }
        }
    }
}