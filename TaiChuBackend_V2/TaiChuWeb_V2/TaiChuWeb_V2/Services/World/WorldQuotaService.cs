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

            return stats;
        }

        public async Task<(bool CanCreate, string Message, int Used, int Max)> CanCreateProjectAsync(Guid userId)
        {
            var stats = await GetUserStatsAsync(userId);
            var used = stats.UsedWorldCount;
            var max = stats.MaxWorldCount;

            if (used >= max)
            {
                return (false, $"已创建 {used} 个世界观（上限 {max} 个），可用经验扩容", used, max);
            }

            return (true, $"还可创建 {max - used} 个世界观", used, max);
        }

        public async Task<(bool CanAdd, string Message, int CurrentCount, int MaxCount)> CanAddCardAsync(Guid projectId, Guid userId)
        {
            var project = await _context.WorldProjects
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
            {
                return (false, "项目不存在", 0, 0);
            }

            if (project.OwnerId != userId)
            {
                return (false, "无权操作此项目", 0, 0);
            }

            var stats = await GetUserStatsAsync(userId);
            var cardCount = await _context.WorldCards
                .CountAsync(c => c.ProjectId == projectId);

            var maxCards = stats.MaxCardsPerWorld;

            if (cardCount >= maxCards)
            {
                return (false, $"词条已达上限（{maxCards} 张/世界观），可用经验扩容", cardCount, maxCards);
            }

            return (true, $"还可添加 {maxCards - cardCount} 张词条", cardCount, maxCards);
        }

        public async Task IncrementUsedWorldCountAsync(Guid userId)
        {
            var stats = await GetUserStatsAsync(userId);
            stats.UsedWorldCount++;
            await _context.SaveChangesAsync();
        }

        public async Task DecrementUsedWorldCountAsync(Guid userId)
        {
            var stats = await GetUserStatsAsync(userId);
            stats.UsedWorldCount = Math.Max(0, stats.UsedWorldCount - 1);
            await _context.SaveChangesAsync();
        }

        public async Task<QuotaUpgradeResult> UpgradeQuotaAsync(Guid userId, QuotaUpgradeType upgradeType)
        {
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
            return await _context.QuotaUpgradeRecords
                .Where(r => r.UserId == userId)
                .OrderByDescending(r => r.CreatedAt)
                .Take(limit)
                .ToListAsync();
        }
    }
}