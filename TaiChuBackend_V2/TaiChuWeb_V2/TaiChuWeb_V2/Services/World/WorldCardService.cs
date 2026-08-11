using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.World;

namespace TaiChuWeb_V2.Services.World
{
    public class WorldCardService : IWorldCardService
    {
        private readonly AppDbContext _context;
        private readonly IWorldProjectService _projectService;
        private readonly IMemoryCache _cache;

        public WorldCardService(AppDbContext context, IWorldProjectService projectService, IMemoryCache cache)
        {
            _context = context;
            _projectService = projectService;
            _cache = cache;
        }

        public async Task<IEnumerable<CardSummaryDto>> GetCardSummariesByProjectAsync(Guid projectId, Guid userId, string? type = null)
        {
            // 1. 验证项目权限
            var project = await _context.WorldProjects
                .FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null)
                throw new KeyNotFoundException("项目不存在");
            if (!project.IsPublic && project.OwnerId != userId)
                throw new UnauthorizedAccessException("无权访问此项目");

            // 2. 构建卡片查询
            var query = _context.WorldCards
                .Where(c => c.ProjectId == projectId);

            if (!string.IsNullOrEmpty(type))
                query = query.Where(c => c.Type == type);

            // 3. 投影为精简 DTO
            var summaries = await query
                .Select(c => new CardSummaryDto
                {
                    Id = c.Id,
                    ProjectId = c.ProjectId,
                    Title = c.Title,
                    Type = c.Type,
                    CoverImage = c.CoverImage,   // 前端自行解析 JSON 数组取第一张
                    UpdatedAt = c.UpdatedAt,
                    // 使用子查询计数，避免加载整个关系集合
                    OutRelationCount = _context.WorldRelations.Count(r => r.SourceCardId == c.Id),
                    InRelationCount = _context.WorldRelations.Count(r => r.TargetCardId == c.Id)
                })
                .OrderByDescending(c => c.UpdatedAt)
                .ToListAsync();

            return summaries;
        }



        // ============================================================
        //  1. 获取项目下的所有卡片（支持按类型筛选）
        // ============================================================
        public async Task<IEnumerable<CardResponseDto>> GetCardsByProjectAsync(Guid projectId, Guid userId, string? typeFilter = null)
        {
            // ✅ 使用轻量级权限验证
            var isAccessible = await _projectService.IsProjectAccessibleAsync(projectId, userId);
            if (!isAccessible)
                return new List<CardResponseDto>();

            var query = _context.WorldCards
                .AsNoTracking()
                .Include(c => c.OutRelations)
                    .ThenInclude(r => r.TargetCard)
                .Include(c => c.InRelations)
                    .ThenInclude(r => r.SourceCard)
                .Where(c => c.ProjectId == projectId);

            if (!string.IsNullOrEmpty(typeFilter))
                query = query.Where(c => c.Type == typeFilter);

            var cards = await query.ToListAsync();

            return cards.Select(c => MapToResponseDto(c)).ToList();
        }

        // ============================================================
        //  2. 获取卡片详情（公开接口，带权限验证）
        // ============================================================
        public async Task<CardResponseDto> GetCardByIdAsync(Guid cardId, Guid userId)
        {
            // ✅ 尝试从缓存获取
            var cacheKey = $"card_{cardId}";
            if (_cache.TryGetValue(cacheKey, out CardResponseDto? cached))
                return cached;

            // ✅ 使用内部方法获取卡片（已验证权限）
            var card = await GetCardByIdInternalAsync(cardId);

            if (card == null)
                return null;

            // 权限检查：使用项目的公开状态和所有者
            var project = await _context.WorldProjects
                .AsNoTracking()
                .Select(p => new { p.Id, p.IsPublic, p.OwnerId })
                .FirstOrDefaultAsync(p => p.Id == card.ProjectId);

            if (project == null)
                return null;

            if (!project.IsPublic && project.OwnerId != userId)
                return null;

            // ✅ 缓存 5 分钟
            _cache.Set(cacheKey, card, TimeSpan.FromMinutes(5));

            return card;
        }

        // ============================================================
        //  3. 获取卡片详情（内部使用，已验证权限，不重复检查）
        // ============================================================
        // 文件：WorldCardService.cs
        public async Task<CardResponseDto> GetCardByIdInternalAsync(Guid cardId)
        {
            // 🔥 一次查询加载卡片 + 所有关系
            var card = await _context.WorldCards
                .AsNoTracking()
                .Include(c => c.OutRelations)
                    .ThenInclude(r => r.TargetCard)
                .Include(c => c.InRelations)
                    .ThenInclude(r => r.SourceCard)
                .FirstOrDefaultAsync(c => c.Id == cardId);

            if (card == null)
                return null;

            // 直接映射，因为关系已加载
            return MapToResponseDto(card);
        }

        // ============================================================
        //  4. 轻量级权限验证
        // ============================================================
        public async Task<bool> IsCardAccessibleAsync(Guid cardId, Guid userId)
        {
            var project = await _context.WorldCards
                .AsNoTracking()
                .Where(c => c.Id == cardId)
                .Select(c => new { c.ProjectId, c.Project.IsPublic, c.Project.OwnerId })
                .FirstOrDefaultAsync();

            if (project == null)
                return false;

            return project.IsPublic || project.OwnerId == userId;
        }

        // ============================================================
        //  5. 批量获取卡片详情（用于关系图谱）
        // ============================================================
        public async Task<IEnumerable<CardResponseDto>> GetCardsByIdsAsync(Guid projectId, IEnumerable<Guid> cardIds, Guid userId)
        {
            var idList = cardIds.Distinct().ToList();
            if (idList.Count == 0)
                return new List<CardResponseDto>();

            // 验证项目权限
            var isAccessible = await _projectService.IsProjectAccessibleAsync(projectId, userId);
            if (!isAccessible)
                return new List<CardResponseDto>();

            // ✅ 批量查询，一次 SQL
            var cards = await _context.WorldCards
                .AsNoTracking()
                .Include(c => c.OutRelations)
                    .ThenInclude(r => r.TargetCard)
                .Include(c => c.InRelations)
                    .ThenInclude(r => r.SourceCard)
                .Where(c => c.ProjectId == projectId && idList.Contains(c.Id))
                .ToListAsync();

            return cards.Select(c => MapToResponseDto(c)).ToList();
        }

        // ============================================================
        //  6. 创建卡片
        // ============================================================
        public async Task<CardResponseDto> CreateCardAsync(Guid projectId, Guid userId, CreateCardDto dto)
        {
            var isOwner = await _projectService.IsProjectOwnerAsync(projectId, userId);
            if (!isOwner)
                throw new UnauthorizedAccessException("无权在此项目中创建卡片");

            var card = new WorldCard
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                Title = dto.Title,
                Type = dto.Type,
                SubType = dto.SubType,
                CoverImage = dto.CoverImage,
                // 🆕 GalleryImages
                GalleryImages = JsonSerializer.Serialize(dto.GalleryImages ?? new List<string>()),
                Aliases = JsonSerializer.Serialize(dto.Aliases ?? new List<string>()),
                Attributes = JsonSerializer.Serialize(dto.Attributes ?? new List<AttributeDto>()),
                Description = dto.Description,
                ContentBlocks = JsonSerializer.Serialize(dto.ContentBlocks ?? new List<ContentBlockDto>()),
                TimelineEvents = JsonSerializer.Serialize(dto.TimelineEvents ?? new List<TimelineEventDto>()),
                Tags = JsonSerializer.Serialize(dto.Tags ?? new List<string>()),
                EmbeddedCards = JsonSerializer.Serialize(dto.EmbeddedCards ?? new List<Guid>()),
                Content = dto.Content ?? "{}",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.WorldCards.AddAsync(card);
            await _context.SaveChangesAsync();

            // ✅ 清除项目卡片列表缓存
            ClearProjectCardCache(projectId);

            // 返回创建的卡片
            return await GetCardByIdInternalAsync(card.Id);
        }

        public async Task<CardResponseDto> UpdateCardAsync(Guid cardId, Guid userId, UpdateCardDto dto)
        {
            // 1. 一次查询加载所有必需数据（卡片 + 项目 + 出度/入度关系）
            var card = await _context.WorldCards
                .Include(c => c.Project)                        // 用于权限检查
                .Include(c => c.OutRelations)
                    .ThenInclude(r => r.TargetCard)             // 目标卡片标题/类型
                .Include(c => c.InRelations)
                    .ThenInclude(r => r.SourceCard)             // 源卡片标题/类型
                .FirstOrDefaultAsync(c => c.Id == cardId);

            if (card == null)
                return null;

            // 2. 权限检查：只有项目所有者才能修改
            if (card.Project.OwnerId != userId)
                return null;

            // 3. 更新字段（只更新有值的）
            if (dto.CoverImage != null)
                card.CoverImage = dto.CoverImage;

            if (dto.GalleryImages != null)
                card.GalleryImages = JsonSerializer.Serialize(dto.GalleryImages);

            if (!string.IsNullOrEmpty(dto.Title))
                card.Title = dto.Title;

            // 🔥 关键修复：显式处理 Type 字段，并强制标记为已修改
            if (!string.IsNullOrEmpty(dto.Type))
            {
                card.Type = dto.Type;
                _context.Entry(card).Property(c => c.Type).IsModified = true;
            }

            if (dto.SubType != null)
                card.SubType = dto.SubType;

            if (dto.Aliases != null)
                card.Aliases = JsonSerializer.Serialize(dto.Aliases);

            if (dto.Attributes != null)
                card.Attributes = JsonSerializer.Serialize(dto.Attributes);

            if (dto.Description != null)
                card.Description = dto.Description;

            if (dto.ContentBlocks != null)
                card.ContentBlocks = JsonSerializer.Serialize(dto.ContentBlocks);

            if (dto.TimelineEvents != null)
                card.TimelineEvents = JsonSerializer.Serialize(dto.TimelineEvents);

            if (dto.Tags != null)
                card.Tags = JsonSerializer.Serialize(dto.Tags);

            if (dto.EmbeddedCards != null)
                card.EmbeddedCards = JsonSerializer.Serialize(dto.EmbeddedCards);

            if (dto.Content != null)
                card.Content = dto.Content;

            // 4. 更新时间戳
            card.UpdatedAt = DateTime.UtcNow;

            // 5. 保存更改
            _context.WorldCards.Update(card);
            await _context.SaveChangesAsync();

            // 6. 清除缓存
            ClearCardCache(cardId);
            ClearProjectCardCache(card.ProjectId);

            // 7. 直接使用已加载关系的 card 实体构造 DTO，无需额外查询
            return MapToResponseDto(card);
        }

        // ============================================================
        //  8. 删除卡片
        // ============================================================
        public async Task<bool> DeleteCardAsync(Guid cardId, Guid userId)
        {
            var card = await _context.WorldCards
                .Include(c => c.Project)
                .Include(c => c.OutRelations)
                .Include(c => c.InRelations)
                .FirstOrDefaultAsync(c => c.Id == cardId);

            if (card == null)
                return false;

            if (card.Project.OwnerId != userId)
                return false;

            var projectId = card.ProjectId;

            // 手动删除关联（因为配置了 Restrict）
            var relations = card.OutRelations.Concat(card.InRelations).ToList();
            _context.WorldRelations.RemoveRange(relations);

            _context.WorldCards.Remove(card);
            await _context.SaveChangesAsync();

            // ✅ 清除缓存
            ClearCardCache(cardId);
            ClearProjectCardCache(projectId);

            return true;
        }

        // ============================================================
        //  9. 检查卡片是否属于指定项目（轻量级）
        // ============================================================
        public async Task<bool> IsCardInProjectAsync(Guid cardId, Guid projectId)
        {
            return await _context.WorldCards
                .AsNoTracking()
                .AnyAsync(c => c.Id == cardId && c.ProjectId == projectId);
        }

        // ============================================================
        //  10. 私有辅助方法
        // ============================================================

        private CardResponseDto MapToResponseDto(WorldCard card)
        {
            var outRelations = card.OutRelations?.Select(r => new RelationDto
            {
                Id = r.Id,
                SourceCardId = r.SourceCardId,
                TargetCardId = r.TargetCardId,
                RelationType = r.RelationType,
                CreatedAt = r.CreatedAt,
                SourceCardTitle = r.SourceCard?.Title,
                TargetCardTitle = r.TargetCard?.Title,
                SourceCardType = r.SourceCard?.Type,
                TargetCardType = r.TargetCard?.Type
            }).ToList() ?? new();

            var inRelations = card.InRelations?.Select(r => new RelationDto
            {
                Id = r.Id,
                SourceCardId = r.SourceCardId,
                TargetCardId = r.TargetCardId,
                RelationType = r.RelationType,
                CreatedAt = r.CreatedAt,
                SourceCardTitle = r.SourceCard?.Title,
                TargetCardTitle = r.TargetCard?.Title,
                SourceCardType = r.SourceCard?.Type,
                TargetCardType = r.TargetCard?.Type
            }).ToList() ?? new();

            return new CardResponseDto
            {
                Id = card.Id,
                ProjectId = card.ProjectId,
                Title = card.Title,
                Type = card.Type,
                SubType = card.SubType,
                CoverImage = card.CoverImage,
                // 🆕 GalleryImages 反序列化
                GalleryImages = JsonSerializer.Deserialize<List<string>>(card.GalleryImages ?? "[]") ?? new(),
                Aliases = JsonSerializer.Deserialize<List<string>>(card.Aliases ?? "[]") ?? new(),
                Attributes = JsonSerializer.Deserialize<List<AttributeDto>>(card.Attributes ?? "[]") ?? new(),
                Description = card.Description,
                ContentBlocks = JsonSerializer.Deserialize<List<ContentBlockDto>>(card.ContentBlocks ?? "[]") ?? new(),
                TimelineEvents = JsonSerializer.Deserialize<List<TimelineEventDto>>(card.TimelineEvents ?? "[]") ?? new(),
                Tags = JsonSerializer.Deserialize<List<string>>(card.Tags ?? "[]") ?? new(),
                EmbeddedCards = JsonSerializer.Deserialize<List<Guid>>(card.EmbeddedCards ?? "[]") ?? new(),
                Content = card.Content ?? "{}",
                CreatedAt = card.CreatedAt,
                UpdatedAt = card.UpdatedAt,
                OutRelations = outRelations,
                InRelations = inRelations
            };
        }

        // ============================================================
        //  11. 缓存管理
        // ============================================================

        private void ClearCardCache(Guid cardId)
        {
            _cache.Remove($"card_{cardId}");
        }

        private void ClearProjectCardCache(Guid projectId)
        {
            _cache.Remove($"cards_project_{projectId}");
        }
    }
}