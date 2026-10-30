using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Utils;

namespace TaiChuWeb_V2.Services.World
{
    public class WorldQuotaService : IWorldQuotaService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<WorldQuotaService> _logger;

        public WorldQuotaService(AppDbContext context, ILogger<WorldQuotaService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<UserStats> GetUserStatsAsync(Guid userId)
        {
            var stats = await _context.UserStats
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (stats == null)
            {
                stats = new UserStats { UserId = userId };
                _context.UserStats.Add(stats);
                await _context.SaveChangesAsync();
            }

            // 「已用世界数」以实际拥有的世界为准：老数据（迁移过来、早于计数器的世界）计数器可能是 0。
            // 这里只改内存里的值，由调用方决定是否保存；读取时总能拿到正确数字。
            var realCount = await _context.WorldProjects.AsNoTracking().CountAsync(p => p.OwnerId == userId);
            if (stats.UsedWorldCount != realCount) stats.UsedWorldCount = realCount;

            return stats;
        }

        public async Task<(bool CanCreate, string Message, int Used, int Max)> CanCreateProjectAsync(Guid userId)
        {
            var stats = await GetUserStatsAsync(userId);
            var used = stats.UsedWorldCount;
            var max = stats.MaxWorldCount;

            if (used >= max)
            {
                return (false, $"已创建 {used} 个世界观（上限 {max} 个），可以去交易行扩容", used, max);
            }

            return (true, $"还可创建 {Math.Max(0, max - used)} 个世界观", used, max);
        }

        public async Task<(bool CanAdd, string Message, int CurrentCount, int MaxCount)> CanAddCardAsync(Guid projectId, Guid userId)
        {
            var project = await _context.WorldProjects
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return (false, "项目不存在", 0, 0);
            }

            if (!await WorldAccess.CanEditProjectAsync(_context, projectId, userId))
            {
                return (false, "无权操作此项目", 0, 0);
            }

            // 词条上限按世界创建者的额度算（协作者帮忙写也占创建者的额度）
            var stats = await GetUserStatsAsync(project.OwnerId);
            var cardCount = await _context.WorldCards
                .CountAsync(c => c.ProjectId == projectId);

            var maxCards = stats.MaxCardsPerWorld;

            if (cardCount >= maxCards)
            {
                return (false, $"这个世界的节点已达上限（每个世界最多 {maxCards} 个，所有世界通用），可以去交易行扩容，所有世界的上限一起提高", cardCount, maxCards);
            }

            return (true, $"还可添加 {maxCards - cardCount} 张词条", cardCount, maxCards);
        }

        public async Task IncrementUsedWorldCountAsync(Guid userId)
        {
            // 新世界已经保存，按实际数量同步即可（不再 +1，避免重复计数）
            await GetUserStatsAsync(userId);
            await _context.SaveChangesAsync();
        }

        public async Task DecrementUsedWorldCountAsync(Guid userId)
        {
            // 世界已经删除，按实际数量同步
            await GetUserStatsAsync(userId);
            await _context.SaveChangesAsync();
        }

        public async Task<QuotaUpgradeResult> UpgradeQuotaAsync(Guid userId, QuotaUpgradeType upgradeType)
        {
            WorldValidation.Validate(new TaiChuWeb_V2.Dtos.World.UpgradeQuotaRequest { UpgradeType = upgradeType });
            var stats = await GetUserStatsAsync(userId);

            int costExp;
            int increment;
            string upgradeTypeName;
            int previousValue;
            int newValue;

            if (upgradeType == QuotaUpgradeType.WorldCount)
            {
                costExp = WorldQuotaConstants.EXP_COST_PER_WORLD_SLOT;
                increment = WorldQuotaConstants.WORLD_SLOT_INCREMENT;
                upgradeTypeName = "世界观数量";
                previousValue = stats.MaxWorldCount;
                newValue = stats.MaxWorldCount + increment;
            }
            else // WorldCardCapacity
            {
                costExp = WorldQuotaConstants.EXP_COST_PER_10_CARDS;
                increment = WorldQuotaConstants.CARD_CAPACITY_INCREMENT;
                upgradeTypeName = $"单世界词汇量（+{increment}）";
                previousValue = stats.MaxCardsPerWorld;
                newValue = stats.MaxCardsPerWorld + increment;
            }

            if (stats.Experience < costExp)
            {
                return new QuotaUpgradeResult
                {
                    Success = false,
                    Message = $"经验不足，需要 {costExp} 经验，当前 {stats.Experience} 经验",
                    RemainingExp = (int)stats.Experience,
                    PreviousValue = previousValue,
                    NewValue = previousValue,
                    UpgradeTypeName = upgradeTypeName
                };
            }

            stats.Experience -= costExp;

            if (upgradeType == QuotaUpgradeType.WorldCount)
            {
                stats.MaxWorldCount = newValue;
            }
            else
            {
                stats.MaxCardsPerWorld = newValue;
            }

            // 记录经验变动日志
            var expLog = new UserExpLog
            {
                UserId = userId,
                Change = -costExp,
                Reason = $"扩容：{upgradeTypeName}（{previousValue} → {newValue}）"
            };
            _context.UserExpLogs.Add(expLog);

            // 记录扩容详情
            var upgradeRecord = new QuotaUpgradeRecord
            {
                UserId = userId,
                UpgradeType = upgradeType.ToString(),
                Amount = increment,
                CostExp = costExp,
                PreviousValue = previousValue,
                NewValue = newValue,
                CreatedAt = DateTime.UtcNow
            };
            _context.QuotaUpgradeRecords.Add(upgradeRecord);

            await _context.SaveChangesAsync();

            _logger.LogInformation($"用户 {userId} 扩容成功：{upgradeTypeName}，{previousValue} → {newValue}，消耗 {costExp} 经验");

            return new QuotaUpgradeResult
            {
                Success = true,
                Message = $"扩容成功！{upgradeTypeName}：{previousValue} → {newValue}，消耗 {costExp} 经验",
                NewValue = newValue,
                PreviousValue = previousValue,
                CostExp = costExp,
                RemainingExp = (int)stats.Experience,
                UpgradeTypeName = upgradeTypeName
            };
        }

        public async Task<List<QuotaUpgradeRecord>> GetUpgradeHistoryAsync(Guid userId, int limit = 20)
        {
            if (limit < 0)
                throw new WorldValidationException(new() { [nameof(limit)] = ["limit 不能小于 0"] });
            return await _context.QuotaUpgradeRecords
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }
    }
}
