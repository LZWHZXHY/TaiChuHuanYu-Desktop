using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Services.World
{
    public interface IWorldQuotaService
    {
        /// <summary>
        /// 获取用户统计信息（包含配额）
        /// </summary>
        Task<UserStats> GetUserStatsAsync(Guid userId);

        /// <summary>
        /// 检查用户是否可以创建新的世界观
        /// </summary>
        Task<(bool CanCreate, string Message, int Used, int Max)> CanCreateProjectAsync(Guid userId);

        /// <summary>
        /// 检查世界观是否可以添加新卡片
        /// </summary>
        Task<(bool CanAdd, string Message, int CurrentCount, int MaxCount)> CanAddCardAsync(Guid projectId, Guid userId);

        /// <summary>
        /// 增加已使用的世界观数量（创建项目时调用）
        /// </summary>
        Task IncrementUsedWorldCountAsync(Guid userId);

        /// <summary>
        /// 减少已使用的世界观数量（删除项目时调用）
        /// </summary>
        Task DecrementUsedWorldCountAsync(Guid userId);

        /// <summary>
        /// 用经验扩容配额
        /// </summary>
        Task<QuotaUpgradeResult> UpgradeQuotaAsync(Guid userId, QuotaUpgradeType upgradeType);

        /// <summary>
        /// 获取用户的扩容历史
        /// </summary>
        Task<List<QuotaUpgradeRecord>> GetUpgradeHistoryAsync(Guid userId, int limit = 20);
    }

    public enum QuotaUpgradeType
    {
        WorldCount,        // 增加世界观数量
        WorldCardCapacity  // 增加单世界词汇量
    }

    public class QuotaUpgradeResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int NewValue { get; set; }
        public int PreviousValue { get; set; }
        public int CostExp { get; set; }
        public int RemainingExp { get; set; }
        public string UpgradeTypeName { get; set; } = string.Empty;
    }
}
