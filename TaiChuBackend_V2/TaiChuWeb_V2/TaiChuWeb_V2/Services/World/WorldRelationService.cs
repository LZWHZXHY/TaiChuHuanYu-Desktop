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

        public async Task<RelationDto> CreateRelationAsync(Guid sourceCardId, Guid userId, CreateRelationDto dto)
        {
            // 1. 验证源卡片存在且用户有权限
            var sourceCard = await _cardService.GetCardByIdAsync(sourceCardId, userId);
            if (sourceCard == null)
                throw new UnauthorizedAccessException("源卡片不存在或无权操作");

            // 2. 验证目标卡片存在且用户有权限（目标卡片也需要属于用户的项目）
            var targetCard = await _cardService.GetCardByIdAsync(dto.TargetCardId, userId);
            if (targetCard == null)
                throw new UnauthorizedAccessException("目标卡片不存在或无权操作");

            // 3. 不能关联自己
            if (sourceCardId == dto.TargetCardId)
                throw new InvalidOperationException("不能关联自己");

            // 4. 检查关联是否已存在
            var existing = await _context.WorldRelations
                .FirstOrDefaultAsync(r => r.SourceCardId == sourceCardId && r.TargetCardId == dto.TargetCardId);
            if (existing != null)
                throw new InvalidOperationException("该关联已存在");

            // 5. 创建关联
            var relation = new WorldRelation
            {
                SourceCardId = sourceCardId,
                TargetCardId = dto.TargetCardId,
                RelationType = dto.RelationType,
                CreatedAt = DateTime.UtcNow
            };

            await _context.WorldRelations.AddAsync(relation);
            await _context.SaveChangesAsync();

            // 6. 返回 DTO
            return await MapToRelationDto(relation);
        }

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

        public async Task<IEnumerable<RelationDto>> GetRelationsForCardAsync(Guid cardId, Guid userId)
        {
            // 验证卡片存在且用户有权限
            var card = await _cardService.GetCardByIdAsync(cardId, userId);
            if (card == null)
                return new List<RelationDto>();

            var relations = await _context.WorldRelations
                .Include(r => r.SourceCard)
                .Include(r => r.TargetCard)
                .Where(r => r.SourceCardId == cardId || r.TargetCardId == cardId)
                .ToListAsync();

            var dtos = new List<RelationDto>();
            foreach (var rel in relations)
            {
                dtos.Add(await MapToRelationDto(rel));
            }
            return dtos;
        }

        // ===== 私有方法 =====

        private async Task<RelationDto> MapToRelationDto(WorldRelation relation)
        {
            // 重新查询以加载导航属性（如果尚未加载）
            if (relation.SourceCard == null || relation.TargetCard == null)
            {
                relation = await _context.WorldRelations
                    .Include(r => r.SourceCard)
                    .Include(r => r.TargetCard)
                    .FirstOrDefaultAsync(r => r.Id == relation.Id);
            }

            return new RelationDto
            {
                Id = relation.Id,
                SourceCardId = relation.SourceCardId,
                TargetCardId = relation.TargetCardId,
                RelationType = relation.RelationType,
                CreatedAt = relation.CreatedAt,
                SourceCardTitle = relation.SourceCard?.Title,
                TargetCardTitle = relation.TargetCard?.Title,
                SourceCardType = relation.SourceCard?.Type,
                TargetCardType = relation.TargetCard?.Type
            };
        }
    }
}