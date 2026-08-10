using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaiChuWeb_V2.Dtos.World;

namespace TaiChuWeb_V2.Services.World
{
    public interface IWorldCardService
    {
        // ============================================================
        //  1. 卡片列表
        // ============================================================

        /// <summary>
        /// 获取项目下的所有卡片（支持按类型筛选）
        /// </summary>
        Task<IEnumerable<CardResponseDto>> GetCardsByProjectAsync(Guid projectId, Guid userId, string? typeFilter = null);

        // ============================================================
        //  2. 卡片详情（带权限验证）
        // ============================================================

        /// <summary>
        /// 获取卡片详情（验证用户是否有权访问）
        /// </summary>
        Task<CardResponseDto> GetCardByIdAsync(Guid cardId, Guid userId);

        /// <summary>
        /// 获取卡片详情（已验证权限，内部使用）
        /// </summary>
        Task<CardResponseDto> GetCardByIdInternalAsync(Guid cardId);

        // ============================================================
        //  3. 卡片 CRUD
        // ============================================================

        /// <summary>
        /// 创建新卡片
        /// </summary>
        Task<CardResponseDto> CreateCardAsync(Guid projectId, Guid userId, CreateCardDto dto);

        /// <summary>
        /// 更新卡片
        /// </summary>
        Task<CardResponseDto> UpdateCardAsync(Guid cardId, Guid userId, UpdateCardDto dto);

        /// <summary>
        /// 删除卡片
        /// </summary>
        Task<bool> DeleteCardAsync(Guid cardId, Guid userId);

        // ============================================================
        //  4. 权限验证
        // ============================================================

        /// <summary>
        /// 检查卡片是否属于指定项目（轻量级）
        /// </summary>
        Task<bool> IsCardInProjectAsync(Guid cardId, Guid projectId);

        /// <summary>
        /// 检查卡片是否存在且用户有权访问（轻量级）
        /// </summary>
        Task<bool> IsCardAccessibleAsync(Guid cardId, Guid userId);


        Task<IEnumerable<CardSummaryDto>> GetCardSummariesByProjectAsync(Guid projectId, Guid userId, string? type = null);

        // ============================================================
        //  5. 批量操作
        // ============================================================

        /// <summary>
        /// 批量获取卡片详情（用于关系图谱加载）
        /// </summary>
        Task<IEnumerable<CardResponseDto>> GetCardsByIdsAsync(Guid projectId, IEnumerable<Guid> cardIds, Guid userId);
    }
}