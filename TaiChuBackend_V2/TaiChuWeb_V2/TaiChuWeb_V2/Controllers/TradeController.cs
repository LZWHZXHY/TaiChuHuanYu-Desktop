using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Models.Trade;
using System.Reflection;

namespace TaiChuWeb_V2.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/trade")]
    public class TradeController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TradeController(AppDbContext context) => _context = context;

        /// <summary>
        /// 1. 获取流转中的资源列表 (已合并购买进度)
        /// </summary>
        [HttpGet("items")]
        public async Task<IActionResult> GetActiveItems()
        {
            var userIdStr = GetCurrentUserId();
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Ok(await _context.StoreItems.Where(i => i.IsActive).ToListAsync());
            }

            var itemsWithProgress = await _context.StoreItems
                .Where(i => i.IsActive)
                .OrderByDescending(i => i.BaseWeight)
                .Select(item => new {
                    item.Id,
                    item.Name,
                    item.Description,
                    item.Benefit,
                    item.Category,
                    item.BaseCost,
                    item.PriceMultiplier,
                    item.Delivery,
                    item.StaticPayload,
                    // 注入当前用户的购买次数，用于前端计算动态价格
                    PurchaseCount = _context.UserPurchaseProgress
                        .Where(p => p.UserId == userId && p.StoreItemId == item.Id)
                        .Select(p => p.PurchaseCount)
                        .FirstOrDefault()
                })
                .ToListAsync();

            return Ok(itemsWithProgress);
        }

        /// <summary>
        /// 2. 获取当前账户审计状态 (实时查询真实已用配额)
        /// </summary>
        /// <summary>
        /// 2. 获取当前账户审计状态 (实时查询真实已用配额)
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetMyStatus()
        {
            var userIdStr = GetCurrentUserId();
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(TradeResult.Fail("令牌无效"));

            var user = await _context.Users
                .Include(u => u.Stats)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.Stats == null) return NotFound(TradeResult.Fail("统计数据丢失"));

            // 🌟 统一逻辑：获取当前用户拥有的所有空间 ID 列表
            var userSpaceIds = await _context.Spaces
                .Where(s => s.UserId == userIdStr)
                .Select(s => s.Id)
                .ToListAsync();

            // 1. 统计空间数：保持原样
            int realUsedSpaces = userSpaceIds.Count;

            // 2. 🌟 核心修复：统计笔记数（与 Quota 接口完全一致）
            int realUsedNotes = await _context.Notes
                .CountAsync(n => userSpaceIds.Contains(n.SpaceId));

            // 3. 统计项目数：保持原样
            int realUsedProjects = await _context.Projects.CountAsync(p => p.OwnerId == userId);

            return Ok(new
            {
                Experience = user.Stats.Experience,
                Level = user.Stats.Level,
                MaxSpaces = user.Stats.MaxSpaces,
                MaxNotes = user.Stats.MaxNotes,
                MaxProjectCount = user.Stats.MaxProjectCount,
                UsedSpaces = realUsedSpaces,
                UsedNotes = realUsedNotes,
                UsedProjectCount = realUsedProjects
            });
        }

        /// <summary>
        /// 3. 执行资源兑换 (通用反射注入版 - 修复重试策略事务报错)
        /// </summary>
        [HttpPost("purchase/{itemId}")]
        public async Task<IActionResult> Purchase(int itemId)
        {
            var userIdStr = GetCurrentUserId();
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(TradeResult.Fail("身份核验失败"));

            // 🌟 核心修复：获取当前配置的执行策略 (支持重试机制)
            var strategy = _context.Database.CreateExecutionStrategy();

            // 🌟 将整个事务逻辑包裹在策略的 ExecuteAsync 中
            return await strategy.ExecuteAsync(async () =>
            {
                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var item = await _context.StoreItems.Include(i => i.SecretPool).FirstOrDefaultAsync(i => i.Id == itemId);
                    var user = await _context.Users.Include(u => u.Stats).FirstOrDefaultAsync(u => u.Id == userId);

                    if (item == null || user?.Stats == null) return BadRequest(TradeResult.Fail("感应目标不存在"));

                    var progress = await _context.UserPurchaseProgress
                        .FirstOrDefaultAsync(p => p.UserId == userId && p.StoreItemId == itemId)
                        ?? new UserPurchaseProgress { UserId = userId, StoreItemId = itemId, PurchaseCount = 0 };

                    // 🌟 计算当前成本
                    long currentCost = (long)(item.BaseCost * Math.Pow(item.PriceMultiplier, progress.PurchaseCount));

                    if (user.Stats.Experience < currentCost)
                        return BadRequest(TradeResult.Fail($"经验不足，需 {currentCost}"));

                    // --- 核心逻辑执行 ---

                    // A. 扣费与购买次数累加
                    user.Stats.Experience -= currentCost;
                    progress.PurchaseCount++;

                    if (_context.Entry(progress).State == EntityState.Detached)
                        _context.UserPurchaseProgress.Add(progress);

                    // B. 🌟 通用属性注入引擎 (反射)
                    if (!string.IsNullOrWhiteSpace(item.StaticPayload))
                    {
                        var parts = item.StaticPayload.Split(':');
                        if (parts.Length == 2)
                        {
                            string propertyName = parts[0].Trim();
                            if (int.TryParse(parts[1], out int valueToAdd))
                            {
                                // 查找 UserStats 中对应的属性 (忽略大小写)
                                var prop = typeof(UserStats).GetProperty(propertyName,
                                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

                                if (prop != null && prop.CanWrite)
                                {
                                    // 获取当前值并相加
                                    var currentVal = Convert.ToInt32(prop.GetValue(user.Stats));
                                    prop.SetValue(user.Stats, currentVal + valueToAdd);

                                    // 🌟 关键：显式标记该属性已修改，强制 EF 生成 SQL UPDATE 语句
                                    _context.Entry(user.Stats).Property(propertyName).IsModified = true;
                                }
                            }
                        }
                    }

                    // C. 处理密钥/链接交付
                    string deliveryData = "";
                    if (item.Delivery == DeliveryType.SecretKey)
                    {
                        var secret = item.SecretPool?.FirstOrDefault(s => !s.IsUsed);
                        if (secret == null) return BadRequest(TradeResult.Fail("资源已罄"));
                        secret.IsUsed = true;
                        secret.AssignedUserId = userId;
                        secret.AssignedAt = DateTime.Now;
                        deliveryData = secret.SecretCode;
                    }
                    else if (item.Delivery == DeliveryType.Link)
                    {
                        deliveryData = item.StaticPayload ?? "";
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    // 🌟 强行刷新内存，确保返回的是数据库最新状态
                    await _context.Entry(user.Stats).ReloadAsync();

                    return Ok(TradeResult.Success(deliveryData));
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return StatusCode(500, TradeResult.Fail("灵脉震荡: " + ex.Message));
                }
            });
        }

        private string GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;
    }
}