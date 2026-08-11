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
        private readonly IWorldCardService _cardService;
        private readonly IMemoryCache _cache;  // 🆕 新增缓存

        public WorldRelationService(AppDbContext context, IWorldCardService cardService, IMemoryCache cache)
        {
            _context = context;
            _cardService = cardService;
            _cache = cache;  // 🆕
        }

        // ============================================================
        //  1. 创建关联
        // ============================================================
        public async Task<RelationDto> CreateRelationAsync(Guid sourceCardId, Guid userId, CreateRelationDto dto)
        {
            // 1. 验证源卡片权限（只取 ProjectId 和 OwnerId）
            var sourceInfo = await _context.WorldCards
                .Where(c => c.Id == sourceCardId)
                .Select(c => new { c.ProjectId, c.Project.OwnerId })
                .FirstOrDefaultAsync();

            if (sourceInfo == null || sourceInfo.OwnerId != userId)
                throw new UnauthorizedAccessException("源卡片不存在或无权限");

            // 2. 验证目标卡片存在且在同一项目（只取 ProjectId）
            var targetProjectId = await _context.WorldCards
                .Where(c => c.Id == dto.TargetCardId)
                .Select(c => c.ProjectId)
                .FirstOrDefaultAsync();

            if (targetProjectId == Guid.Empty)
                throw new UnauthorizedAccessException("目标卡片不存在");

            if (targetProjectId != sourceInfo.ProjectId)
                throw new InvalidOperationException("卡片必须属于同一项目");

            if (sourceCardId == dto.TargetCardId)
                throw new InvalidOperationException("不能关联自己");

            // 3. 检查关联是否已存在
            bool exists = await _context.WorldRelations
                .AnyAsync(r => r.SourceCardId == sourceCardId && r.TargetCardId == dto.TargetCardId);
            if (exists)
                throw new InvalidOperationException("该关联已存在");

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

            // 5. 清除相关卡片缓存
            _cache.Remove($"card_{sourceCardId}");
            _cache.Remove($"card_{dto.TargetCardId}");

            // 6. 可选：一次查询获取源和目标卡片的标题/类型（如果需要返回）
            var titles = await _context.WorldCards
                .Where(c => c.Id == sourceCardId || c.Id == dto.TargetCardId)
                .Select(c => new { c.Id, c.Title, c.Type })
                .ToDictionaryAsync(c => c.Id);

            return new RelationDto
            {
                Id = relation.Id,
                SourceCardId = relation.SourceCardId,
                TargetCardId = relation.TargetCardId,
                RelationType = relation.RelationType,
                CreatedAt = relation.CreatedAt,
                SourceCardTitle = titles.TryGetValue(sourceCardId, out var s) ? s.Title : null,
                TargetCardTitle = titles.TryGetValue(dto.TargetCardId, out var t) ? t.Title : null,
                SourceCardType = titles.TryGetValue(sourceCardId, out var st) ? st.Type : null,
                TargetCardType = titles.TryGetValue(dto.TargetCardId, out var tt) ? tt.Type : null
            };
        }

        public async Task<bool> DeleteRelationAsync(Guid relationId, Guid userId)
        {
            var relation = await _context.WorldRelations
                .Include(r => r.SourceCard)
                    .ThenInclude(c => c.Project)
                .FirstOrDefaultAsync(r => r.Id == relationId);

            if (relation == null)
                return false;

            if (relation.SourceCard?.Project?.OwnerId != userId)
                return false;

            var sourceCardId = relation.SourceCardId;
            var targetCardId = relation.TargetCardId;

            _context.WorldRelations.Remove(relation);
            await _context.SaveChangesAsync();

            // ✅ 优化：仅清除缓存，不重新加载
            _cache.Remove($"card_{sourceCardId}");
            _cache.Remove($"card_{targetCardId}");
            // 同时清除项目卡片列表缓存（如果有）
            var projectId = relation.SourceCard.ProjectId;
            _cache.Remove($"cards_project_{projectId}");

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
        //  4. 获取项目下所有关联
        // ============================================================
        public async Task<IEnumerable<RelationDto>> GetRelationsForProjectAsync(Guid projectId)
        {
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
        //  5. 私有辅助方法
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