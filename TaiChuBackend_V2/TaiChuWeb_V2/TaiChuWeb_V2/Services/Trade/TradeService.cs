using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Trade;

namespace TaiChuWeb_V2.Services.Trade
{
    public class TradeService
    {
        private readonly AppDbContext _context;
        public TradeService(AppDbContext context) => _context = context;

        public async Task<TradeResult> ExecuteExchangeAsync(Guid userId, int itemId)
        {
            // 开启数据库事务，确保“扣钱”和“发货”同生共死
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var item = await _context.StoreItems
                    .Include(i => i.SecretPool)
                    .FirstOrDefaultAsync(i => i.Id == itemId && i.IsActive);

                if (item == null) return TradeResult.Fail("资源已下架或不存在");

                // 1. 获取该用户的购买进度
                var progress = await _context.UserPurchaseProgress
                    .FirstOrDefaultAsync(p => p.UserId == userId && p.StoreItemId == itemId)
                    ?? new UserPurchaseProgress { UserId = userId, StoreItemId = itemId };

                // 2. 🌟 计算个人当前价格：BaseCost * (Multiplier ^ Count)
                long currentCost = (long)(item.BaseCost * Math.Pow(item.PriceMultiplier, progress.PurchaseCount));

                // 3. 校验余额（假设从 UserStats 取经验）
                var stats = await _context.UserStats.FirstAsync(s => s.UserId == userId);
                if (stats.Experience < currentCost) return TradeResult.Fail("经验值不足，请继续积累");

                // 4. 处理交付逻辑
                string deliveryContent = "";
                if (item.Delivery == DeliveryType.SecretKey)
                {
                    // 从密钥池捞出一个未使用的 Key
                    var secret = item.SecretPool.FirstOrDefault(s => !s.IsUsed);
                    if (secret == null) return TradeResult.Fail("全局库存已售罄");

                    secret.IsUsed = true;
                    secret.AssignedUserId = userId;
                    secret.AssignedAt = DateTime.Now;
                    deliveryContent = secret.SecretCode;
                }
                else if (item.Delivery == DeliveryType.Link)
                {
                    deliveryContent = item.StaticPayload ?? "无内容";
                }

                // 5. 更新库存与个人进度
                stats.Experience -= currentCost;
                progress.PurchaseCount += 1;
                if (item.GlobalStock.HasValue) item.GlobalStock--;

                if (_context.Entry(progress).State == EntityState.Detached) _context.Add(progress);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return TradeResult.Success(deliveryContent);
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return TradeResult.Fail("系统审计异常，交易已回滚");
            }
        }
    }
}
