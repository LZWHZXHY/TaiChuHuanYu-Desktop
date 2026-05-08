using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Models.Trade;

namespace TaiChuWeb_V2.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/trade")]
    public class TradeController : ControllerBase
    {
        private readonly AppDbContext _context;

        public TradeController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 获取当前流转中的资源列表 (修复之前的 404 问题)
        /// </summary>
        [HttpGet("items")]
        public async Task<IActionResult> GetActiveItems()
        {
            // 这里的查询会自动过滤掉未激活的资源
            var items = await _context.StoreItems
                .Where(i => i.IsActive)
                .OrderByDescending(i => i.BaseWeight)
                .ToListAsync();

            return Ok(items);
        }

        /// <summary>
        /// 获取当前账户的真实审计状态
        /// </summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetMyStatus()
        {
            var userIdStr = GetCurrentUserId();
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(TradeResult.Fail("身份令牌解析失败，请重新登录"));
            }

            // 必须 Include Stats 表，确保数据链路完整
            var user = await _context.Users
                .Include(u => u.Stats)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user?.Stats == null) return NotFound(TradeResult.Fail("无法加载账户统计信息"));

            return Ok(new
            {
                Experience = user.Stats.Experience,
                Level = user.Stats.Level,
                MaxSpaces = user.Stats.MaxSpaces,
                MaxNotes = user.Stats.MaxNotes,
                UsedNotes = 0 // 后续可根据业务逻辑计算
            });
        }

        /// <summary>
        /// 执行资源兑换
        /// </summary>
        [HttpPost("purchase/{itemId}")]
        public async Task<IActionResult> Purchase(int itemId)
        {
            var userIdStr = GetCurrentUserId();
            if (!Guid.TryParse(userIdStr, out var userId))
            {
                return Unauthorized(TradeResult.Fail("身份核验失败"));
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. 载入资源定义与密钥池
                var item = await _context.StoreItems
                    .Include(i => i.SecretPool)
                    .FirstOrDefaultAsync(i => i.Id == itemId);

                if (item == null || !item.IsActive)
                    return BadRequest(TradeResult.Fail("该资源已静默或不存在"));

                // 2. 载入用户进度与统计数据
                var progress = await _context.UserPurchaseProgress
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.StoreItemId == itemId)
                    ?? new UserPurchaseProgress { UserId = userId, StoreItemId = itemId, PurchaseCount = 0 };

                var user = await _context.Users
                    .Include(u => u.Stats)
                    .FirstOrDefaultAsync(u => u.Id == userId);

                if (user?.Stats == null) return BadRequest(TradeResult.Fail("账户审计数据解析失败"));

                // 3. 价格演化逻辑
                var currentCost = (long)(item.BaseCost * Math.Pow(item.PriceMultiplier, progress.PurchaseCount));

                if (user.Stats.Experience < currentCost)
                    return BadRequest(TradeResult.Fail("经验值(EXP)不足，无法共鸣"));

                if (item.GlobalStock.HasValue && item.GlobalStock <= 0)
                    return BadRequest(TradeResult.Fail("全局库存已耗尽"));

                // 4. 资产分发逻辑
                string payload = "";
                if (item.Delivery == DeliveryType.SecretKey)
                {
                    var secret = item.SecretPool?.FirstOrDefault(s => !s.IsUsed);
                    if (secret == null) return BadRequest(TradeResult.Fail("密钥池分配异常，请联系管理员"));

                    secret.IsUsed = true;
                    secret.AssignedUserId = userId;
                    secret.AssignedAt = DateTime.Now;
                    payload = secret.SecretCode;
                }
                else if (item.Delivery == DeliveryType.Link)
                {
                    payload = item.StaticPayload ?? "";
                }

                // 5. 数据状态回写
                user.Stats.Experience -= currentCost;
                progress.PurchaseCount++;

                if (_context.Entry(progress).State == EntityState.Detached)
                {
                    _context.UserPurchaseProgress.Add(progress);
                }

                if (item.GlobalStock.HasValue)
                {
                    item.GlobalStock--;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(TradeResult.Success(payload));
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, TradeResult.Fail("灵脉传输震荡，请重新感应"));
            }
        }

        /// <summary>
        /// 安全获取当前登录用户 ID
        /// </summary>
        private string GetCurrentUserId()
        {
            // 优先从标准的 NameIdentifier 获取，如果不存在则尝试自定义的 "UserId"
            return User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("UserId")?.Value
                ?? string.Empty;
        }
    }
}