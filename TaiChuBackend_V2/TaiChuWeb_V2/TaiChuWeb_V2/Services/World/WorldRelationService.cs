using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.World;

namespace TaiChuWeb_V2.Services.World
{
    public class WorldRelationService : IWorldRelationService
    {
        private readonly AppDbContext _context;
        private readonly IWorldCardService _cardService;

        public WorldRelationService(AppDbContext context, IWorldCardService cardService)
        {
            _context = context;
            _cardService = cardService;
        }

        // ============================================================
        //  1. 创建关联
        // ============================================================
        public async Task<RelationDto> CreateRelationAsync(Guid sourceCardId, Guid userId, CreateRelationDto dto)
        {
            // 1. 验证源卡片存在且用户有权限
            var sourceCard = await _cardService.GetCardByIdAsync(sourceCardId, userId);
            if (sourceCard == null)
                throw new UnauthorizedAccessException("源卡片不存在或无权操作");

            // 2. 验证目标卡片存在且用户有权限
            var targetCard = await _cardService.GetCardByIdAsync(dto.TargetCardId, userId);
            if (targetCard == null)
                throw new UnauthorizedAccessException("目标卡片不存在或无权操作");

            // 3. 不能关联自己
            if (sourceCardId == dto.TargetCardId)
                throw new InvalidOperationException("不能关联自己");

            // 4. 检查关联是否已存在
            var existing = await _context.WorldRelations
                .AnyAsync(r => r.SourceCardId == sourceCardId && r.TargetCardId == dto.TargetCardId);
            if (existing)
                throw new InvalidOperationException("该关联已存在");

            // 5. 创建关联
            var relation = new WorldRelation
            {
                Id = Guid.NewGuid(),
                SourceCardId = sourceCardId,
                TargetCardId = dto.TargetCardId,
                RelationType = dto.RelationType,
                CreatedAt = DateTime.UtcNow
            };

            await _context.WorldRelations.AddAsync(relation);
            await _context.SaveChangesAsync();

            // 6. 返回 DTO（直接查询，避免导航属性加载问题）
            return await MapToRelationDto(relation.Id);
        }

        // ============================================================
        //  2. 删除关联
        // ============================================================
        public async Task<bool> DeleteRelationAsync(Guid relationId, Guid userId)
        {
            var relation = await _context.WorldRelations
                .Include(r => r.SourceCard)
                    .ThenInclude(c => c.Project)
                .FirstOrDefaultAsync(r => r.Id == relationId);

            if (relation == null)
                return false;

            // 检查权限：只有源卡片所属项目的所有者才能删除
            if (relation.SourceCard?.Project?.OwnerId != userId)
                return false;

            _context.WorldRelations.Remove(relation);
            await _context.SaveChangesAsync();
            return true;
        }

        // ============================================================
        //  3. 获取卡片的所有关联（双向）
        // ============================================================
        public async Task<IEnumerable<RelationDto>> GetRelationsForCardAsync(Guid cardId, Guid userId)
        {
            // 验证卡片存在且用户有权限
            var card = await _cardService.GetCardByIdAsync(cardId, userId);
            if (card == null)
                return new List<RelationDto>();

            // ✅ 优化：使用 Select 直接投影，无需额外的 MapToRelationDto
            var relations = await _context.WorldRelations
                .AsNoTracking()
                .Where(r => r.SourceCardId == cardId || r.TargetCardId == cardId)
                .Select(r => new RelationDto
                {
                    Id = r.Id,
                    SourceCardId = r.SourceCardId,
                    TargetCardId = r.TargetCardId,
                    RelationType = r.RelationType,
                    CreatedAt = r.CreatedAt,
                    SourceCardTitle = r.SourceCard != null ? r.SourceCard.Title : null,
                    TargetCardTitle = r.TargetCard != null ? r.TargetCard.Title : null,
                    SourceCardType = r.SourceCard != null ? r.SourceCard.Type : null,
                    TargetCardType = r.TargetCard != null ? r.TargetCard.Type : null
                })
                .ToListAsync();

            return relations;
        }

        // ============================================================
        //  4. 获取项目下所有关联（优化版 - 单次查询 + 直接投影）
        // ============================================================
        public async Task<IEnumerable<RelationDto>> GetRelationsForProjectAsync(Guid projectId)
        {
            // ✅ 优化：直接用 Select 投影，不需要 Include + 二次转换
            // 这会生成一个 SQL JOIN 查询，一次性返回所有数据
            var relations = await _context.WorldRelations
                .AsNoTracking()
                .Where(r =>
                    r.SourceCard != null && r.SourceCard.ProjectId == projectId ||
                    r.TargetCard != null && r.TargetCard.ProjectId == projectId)
                .Select(r => new RelationDto
                {
                    Id = r.Id,
                    SourceCardId = r.SourceCardId,
                    TargetCardId = r.TargetCardId,
                    RelationType = r.RelationType,
                    CreatedAt = r.CreatedAt,
                    SourceCardTitle = r.SourceCard != null ? r.SourceCard.Title : null,
                    TargetCardTitle = r.TargetCard != null ? r.TargetCard.Title : null,
                    SourceCardType = r.SourceCard != null ? r.SourceCard.Type : null,
                    TargetCardType = r.TargetCard != null ? r.TargetCard.Type : null
                })
                .ToListAsync();

            return relations;
        }

        // ============================================================
        //  5. 私有辅助方法（按 ID 查询并映射）
        // ============================================================
        private async Task<RelationDto> MapToRelationDto(Guid relationId)
        {
            var relation = await _context.WorldRelations
                .AsNoTracking()
                .Where(r => r.Id == relationId)
                .Select(r => new RelationDto
                {
                    Id = r.Id,
                    SourceCardId = r.SourceCardId,
                    TargetCardId = r.TargetCardId,
                    RelationType = r.RelationType,
                    CreatedAt = r.CreatedAt,
                    SourceCardTitle = r.SourceCard != null ? r.SourceCard.Title : null,
                    TargetCardTitle = r.TargetCard != null ? r.TargetCard.Title : null,
                    SourceCardType = r.SourceCard != null ? r.SourceCard.Type : null,
                    TargetCardType = r.TargetCard != null ? r.TargetCard.Type : null
                })
                .FirstOrDefaultAsync();

            return relation ?? new RelationDto();
        }
    }
}