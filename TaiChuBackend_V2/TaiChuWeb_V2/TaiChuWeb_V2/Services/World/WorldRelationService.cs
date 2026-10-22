using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
        private readonly IMemoryCache _cache;  // 🆕 新增缓存

        public WorldRelationService(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;  // 🆕
        }

        // ============================================================
        //  1. 创建关联
        // ============================================================
        public async Task<RelationDto> CreateRelationAsync(Guid sourceCardId, Guid userId, CreateRelationDto dto)
        {
            WorldValidation.Validate(dto);
            // 1. 验证源卡片权限（只取 ProjectId 和 OwnerId）
            var sourceInfo = await _context.WorldCards
                .Where(c => c.Id == sourceCardId)
                .Select(c => new { c.ProjectId, OwnerId = c.Project != null ? c.Project.OwnerId : Guid.Empty })
                .FirstOrDefaultAsync();

            if (sourceInfo == null || !WorldAccess.IsOwner(sourceInfo.OwnerId, userId))
                throw new WorldOperationException(WorldErrorKind.Forbidden, "源卡片不存在或无权限");

            // 2. 验证目标卡片存在且在同一项目（只取 ProjectId）
            var targetProjectId = await _context.WorldCards
                .Where(c => c.Id == dto.TargetCardId)
                .Select(c => c.ProjectId)
                .FirstOrDefaultAsync();

            if (targetProjectId == Guid.Empty)
                throw new WorldOperationException(WorldErrorKind.Forbidden, "目标卡片不存在");

            if (targetProjectId != sourceInfo.ProjectId)
                throw new WorldOperationException(WorldErrorKind.Invalid, "卡片必须属于同一项目");

            if (sourceCardId == dto.TargetCardId)
                throw new WorldOperationException(WorldErrorKind.Invalid, "不能关联自己");

            // 3. 检查关联是否已存在
            bool exists = await _context.WorldRelations
                .AnyAsync(r => r.SourceCardId == sourceCardId && r.TargetCardId == dto.TargetCardId);
            if (exists)
                throw new WorldOperationException(WorldErrorKind.Invalid, "该关联已存在");

            // 4. 创建新关系
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

            WorldCache.InvalidateCards(_cache, new[] { sourceCardId, dto.TargetCardId });
            WorldCache.InvalidateProject(_cache, sourceInfo.ProjectId, sourceInfo.OwnerId);

            return await _context.WorldRelations.AsNoTracking()
                .Where(item => item.Id == relation.Id)
                .Select(WorldRelationMapper.Projection)
                .SingleAsync();
        }

        public async Task<bool> DeleteRelationAsync(Guid relationId, Guid userId, Guid? cardId = null)
        {
            if (cardId.HasValue) await WorldAccess.RequireReadableCardAsync(_context, cardId.Value, userId);
            var relation = await _context.WorldRelations
                .Include(r => r.SourceCard)
                    .ThenInclude(c => c!.Project)
                .FirstOrDefaultAsync(r => r.Id == relationId &&
                    (!cardId.HasValue || r.SourceCardId == cardId.Value || r.TargetCardId == cardId.Value));

            if (relation == null)
                return false;

            if (relation.SourceCard?.Project == null || !WorldAccess.IsOwner(relation.SourceCard.Project.OwnerId, userId))
                return false;

            var projectId = relation.SourceCard!.ProjectId;
            var sourceCardId = relation.SourceCardId;
            var targetCardId = relation.TargetCardId;

            _context.WorldRelations.Remove(relation);
            await _context.SaveChangesAsync();

            WorldCache.InvalidateCards(_cache, new[] { sourceCardId, targetCardId });
            WorldCache.InvalidateProject(_cache, projectId, userId);

            return true;
        }

        // ============================================================
        //  3. 获取卡片的所有关联（双向）
        // ============================================================
        public async Task<IEnumerable<RelationDto>> GetRelationsForCardAsync(Guid cardId, Guid userId)
        {
            // 验证卡片存在且用户有权限
            await WorldAccess.RequireReadableCardAsync(_context, cardId, userId);

            var relations = await _context.WorldRelations
                .AsNoTracking()
                .Where(r => r.SourceCardId == cardId || r.TargetCardId == cardId)
                .Select(WorldRelationMapper.Projection)
                .ToListAsync();

            return relations;
        }

        // ============================================================
        //  4. 获取项目下所有关联
        // ============================================================
        public async Task<IEnumerable<RelationDto>> GetRelationsForProjectAsync(Guid projectId, Guid userId)
        {
            await WorldAccess.RequireReadableProjectAsync(_context, projectId, userId);
            var relations = await _context.WorldRelations
                .AsNoTracking()
                .Where(r =>
                    r.SourceCard != null && r.SourceCard.ProjectId == projectId ||
                    r.TargetCard != null && r.TargetCard.ProjectId == projectId)
                .Select(WorldRelationMapper.Projection)
                .ToListAsync();

            return relations;
        }

    }
}
