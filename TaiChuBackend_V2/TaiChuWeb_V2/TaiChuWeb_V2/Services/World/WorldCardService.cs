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
        private readonly IWorldQuotaService _quotaService;

        public WorldCardService(AppDbContext context, IWorldProjectService projectService, IMemoryCache cache, IWorldQuotaService quotaService)
        {
            _context = context;
            _projectService = projectService;
            _cache = cache;
            _quotaService = quotaService;
        }

        public async Task<IEnumerable<CardSummaryDto>> GetCardSummariesByProjectAsync(Guid projectId, Guid userId, string? type = null)
        {
            await WorldAccess.RequireReadableProjectAsync(_context, projectId, userId);

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
        public async Task<CardResponseDto?> GetCardByIdAsync(Guid cardId, Guid userId, Guid? projectId = null)
        {
            // 缓存只保存数据；每次读取仍验证项目当前的访问权限。
            if (!await IsCardAccessibleAsync(cardId, userId)) return null;

            // 编辑冲突核对必须取得数据库当前内容，不能返回其他实例遗留的缓存。
            return EnsureProject(await GetCardByIdInternalAsync(cardId), projectId);
        }

        // ============================================================
        //  3. 获取卡片详情（内部使用，已验证权限，不重复检查）
        // ============================================================
        // 文件：WorldCardService.cs
        public async Task<CardResponseDto?> GetCardByIdInternalAsync(Guid cardId)
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
        public Task<bool> IsCardAccessibleAsync(Guid cardId, Guid userId) =>
            WorldAccess.CanReadCardAsync(_context, cardId, userId);

        // ============================================================
        //  5. 批量获取卡片详情（用于关系图谱）
        // ============================================================
        public async Task<IEnumerable<CardResponseDto>> GetCardsByIdsAsync(Guid projectId, IEnumerable<Guid> cardIds, Guid userId)
        {
            var idList = cardIds.Distinct().ToList();
            if (idList.Count == 0)
                return new List<CardResponseDto>();

            await WorldAccess.RequireReadableProjectAsync(_context, projectId, userId);

            // ✅ 批量查询，一次 SQL
            var cards = await _context.WorldCards
                .AsNoTracking().AsSplitQuery()
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
            WorldValidation.Validate(dto);
            var quota = await _quotaService.CanAddCardAsync(projectId, userId);
            if (!quota.CanAdd)
                throw new WorldOperationException(WorldErrorKind.Invalid, quota.Message, new Dictionary<string, object?>
                {
                    ["success"] = false, ["code"] = "CARD_LIMIT_EXCEEDED",
                    ["currentCount"] = quota.CurrentCount, ["maxCount"] = quota.MaxCount
                });
            // CanAddCardAsync 已验证项目存在和所有者权限。

            var card = CreateEntity(projectId, Guid.NewGuid(), dto);
            await ApplyRelationsAsync(card, dto.Relations);
            await _context.WorldCards.AddAsync(card);
            await _context.SaveChangesAsync();

            // ✅ 清除项目卡片列表缓存
            WorldCache.InvalidateProject(_cache, projectId, userId);

            WorldCache.InvalidateCards(_cache, card.OutRelations.Select(r => r.TargetCardId));

            // 返回创建的卡片
            return MapToResponseDto(card);
        }

        internal static WorldCard CreateEntity(Guid projectId, Guid id, CreateCardDto dto)
        {
            var card = new WorldCard
            {
                Id = id,
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

            card.AdvanceVersion();
            return card;
        }

        public async Task<CardResponseDto?> UpdateCardAsync(Guid cardId, Guid userId, UpdateCardDto dto, Guid? projectId = null)
        {
            WorldValidation.Validate(dto);
            // 1. 一次查询加载所有必需数据（卡片 + 项目 + 出度/入度关系）
            var card = await _context.WorldCards
                .Include(c => c.Project)                        // 用于权限检查
                .Include(c => c.OutRelations)
                    .ThenInclude(r => r.TargetCard)             // 目标卡片标题/类型
                .Include(c => c.InRelations)
                    .ThenInclude(r => r.SourceCard)             // 源卡片标题/类型
                .FirstOrDefaultAsync(c => c.Id == cardId && (!projectId.HasValue || c.ProjectId == projectId.Value));

            if (card == null)
                return null;

            // 2. 权限检查：只有项目所有者才能修改
            if (card.Project == null || !WorldAccess.IsOwner(card.Project.OwnerId, userId))
                return null;

            if (dto.ExpectedUpdatedAt.HasValue && dto.ExpectedUpdatedAt.Value.Ticks != card.UpdatedAt.Ticks)
                throw new WorldOperationException(WorldErrorKind.Conflict, "条目已被更新，请核对最新内容后再保存",
                    new Dictionary<string, object?> { ["code"] = "WORLD_CARD_CONFLICT" });

            var affectedIds = card.OutRelations.Concat(card.InRelations)
                .SelectMany(r => new[] { r.SourceCardId, r.TargetCardId }).Append(cardId).ToList();
            await ApplyRelationsAsync(card, dto.Relations);

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
            card.AdvanceVersion();

            // 5. 保存更改
            await _context.SaveChangesAsync();

            // 6. 清除缓存
            WorldCache.InvalidateCards(_cache, affectedIds.Concat(card.OutRelations.Select(r => r.TargetCardId)));
            WorldCache.InvalidateProject(_cache, card.ProjectId, userId);

            // 7. 直接使用已加载关系的 card 实体构造 DTO，无需额外查询
            return MapToResponseDto(card);
        }

        // ============================================================
        //  8. 删除卡片
        // ============================================================
        public async Task<bool> DeleteCardAsync(Guid cardId, Guid userId, Guid? projectId = null)
        {
            var card = await _context.WorldCards
                .Include(c => c.Project)
                .Include(c => c.OutRelations)
                .Include(c => c.InRelations)
                .FirstOrDefaultAsync(c => c.Id == cardId && (!projectId.HasValue || c.ProjectId == projectId.Value));

            if (card == null)
                return false;

            if (card.Project == null || !WorldAccess.IsOwner(card.Project.OwnerId, userId))
                return false;

            var owningProjectId = card.ProjectId;

            // 手动删除关联（因为配置了 Restrict）
            var relations = card.OutRelations.Concat(card.InRelations).ToList();
            var affectedCardIds = relations.SelectMany(relation => new[] { relation.SourceCardId, relation.TargetCardId })
                .Append(cardId).ToArray();
            _context.WorldRelations.RemoveRange(relations);

            _context.WorldCards.Remove(card);
            await _context.SaveChangesAsync();

            // ✅ 清除缓存
            WorldCache.InvalidateCards(_cache, affectedCardIds);
            WorldCache.InvalidateProject(_cache, owningProjectId, userId);

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

        // ============================================================
        //  11. 反向引用：哪些卡片引用了当前卡片
        // ============================================================
        public async Task<IEnumerable<CardSummaryDto>> GetBacklinksAsync(Guid cardId, Guid userId, Guid? projectId = null)
        {
            // 无权限读取当前卡片时，同样不暴露反向引用。
            if (!await WorldAccess.CanReadCardAsync(_context, cardId, userId))
                return new List<CardSummaryDto>();

            var target = await _context.WorldCards.AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == cardId);
            if (target == null || (projectId.HasValue && target.ProjectId != projectId.Value)) return new List<CardSummaryDto>();

            var idStr = cardId.ToString();
            var inlineToken = "{CARD:" + idStr + "}";

            // 显式搜索 JSON 内的字符串值，避免把普通 ID 参数按 JSON 类型 CAST。
            // 仅检索已授权的当前项目，兼容已有内容块的 JSON 字段格式。
            return await _context.WorldCards.AsNoTracking()
                .Where(c => c.ProjectId == target.ProjectId && c.Id != cardId)
                .Where(c => (c.Description != null && c.Description.Contains(inlineToken)) || EF.Functions.JsonSearchAny(c.ContentBlocks, idStr))
                .OrderByDescending(c => c.UpdatedAt)
                .Select(c => new CardSummaryDto
                {
                    Id = c.Id,
                    ProjectId = c.ProjectId,
                    Title = c.Title,
                    Type = c.Type,
                    CoverImage = c.CoverImage,
                    UpdatedAt = c.UpdatedAt,
                    OutRelationCount = 0,
                    InRelationCount = 0
                })
                .ToListAsync();
        }

        public async Task<Dictionary<Guid, List<Guid>>> GetReferencesAsync(Guid projectId, Guid userId)
        {
            await WorldAccess.RequireReadableProjectAsync(_context, projectId, userId);
            var rows = await _context.WorldCards.AsNoTracking().Where(c => c.ProjectId == projectId)
                .Select(c => new { c.Id, c.Description, c.ContentBlocks }).ToListAsync();
            var allowed = rows.Select(c => c.Id).ToHashSet();
            var result = new Dictionary<Guid, List<Guid>>();
            foreach (var row in rows)
            {
                var ids = new HashSet<Guid>();
                foreach (System.Text.RegularExpressions.Match match in System.Text.RegularExpressions.Regex.Matches(row.Description ?? "", @"\{CARD:([^}]+)\}"))
                    if (Guid.TryParse(match.Groups[1].Value, out var id)) ids.Add(id);
                try
                {
                    using var blocks = JsonDocument.Parse(row.ContentBlocks ?? "[]");
                    if (blocks.RootElement.ValueKind == JsonValueKind.Array)
                        foreach (var block in blocks.RootElement.EnumerateArray())
                            if (block.ValueKind == JsonValueKind.Object && (block.TryGetProperty("cardId", out var value) || block.TryGetProperty("CardId", out value))
                                && value.ValueKind == JsonValueKind.String && Guid.TryParse(value.GetString(), out var id)) ids.Add(id);
                }
                catch (JsonException) { /* 旧数据损坏时仍保留正文中的有效引用。 */ }
                ids.Remove(row.Id);
                var targets = ids.Where(allowed.Contains).OrderBy(id => id).ToList();
                if (targets.Count > 0) result[row.Id] = targets;
            }
            return result;
        }

        public async Task<IEnumerable<TimelineItemDto>> GetTimelineAsync(Guid projectId, Guid userId)
        {
            await WorldAccess.RequireReadableProjectAsync(_context, projectId, userId);

            var cards = await _context.WorldCards.AsNoTracking()
                .Where(c => c.ProjectId == projectId)
                .Select(c => new { c.Id, c.Title, c.Type, c.TimelineEvents })
                .ToListAsync();

            var items = new List<TimelineItemDto>();
            foreach (var card in cards)
            {
                var events = JsonSerializer.Deserialize<List<TimelineEventDto>>(card.TimelineEvents ?? "[]") ?? new();
                foreach (var e in events)
                {
                    items.Add(new TimelineItemDto
                    {
                        CardId = card.Id,
                        CardTitle = card.Title,
                        CardType = card.Type,
                        Date = e.Date,
                        Era = e.Era,
                        Title = e.Title,
                        Description = e.Description
                    });
                }
            }
            return items;
        }

        public async Task<IEnumerable<WorldSearchResultDto>> SearchCardsAsync(Guid userId, string query, int limit)
        {
            if (string.IsNullOrWhiteSpace(query)) return new List<WorldSearchResultDto>();
            if (limit < 1 || limit > 50) limit = 20;
            var q = query.Trim().ToLower();

            var accessibleProjectIds = await _context.WorldProjects
                .ReadableBy(userId)
                .Select(p => p.Id)
                .ToListAsync();

            return await (from c in _context.WorldCards
                          join p in _context.WorldProjects on c.ProjectId equals p.Id
                          where accessibleProjectIds.Contains(c.ProjectId)
                            && (c.Title.ToLower().Contains(q) || (c.Description != null && c.Description.ToLower().Contains(q)))
                          orderby c.UpdatedAt descending
                          select new WorldSearchResultDto
                          {
                              CardId = c.Id,
                              CardTitle = c.Title,
                              CardType = c.Type,
                              ProjectId = c.ProjectId,
                              ProjectName = p.Name
                          })
                          .Take(limit)
                          .ToListAsync();
        }

        // 先验证完整关系集合，再与正文一起由一次 SaveChanges 提交，避免部分保存。
        private async Task ApplyRelationsAsync(WorldCard card, List<CreateRelationDto>? desired)
        {
            if (desired == null) return;
            var ids = ValidateRelations(card.Id, desired);
            var projectId = card.ProjectId;
            var targetIds = ids.ToList();
            var targets = targetIds.Count == 0 ? new Dictionary<Guid, WorldCard>() :
                await _context.WorldCards.Where(c => c.ProjectId == projectId && targetIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id);
            if (targets.Count != ids.Length)
                throw new WorldOperationException(WorldErrorKind.Invalid, "关联目标不存在或不属于当前世界");

            foreach (var old in card.OutRelations.Where(r => !ids.Contains(r.TargetCardId)).ToList())
            {
                _context.WorldRelations.Remove(old);
                card.OutRelations.Remove(old);
            }
            foreach (var input in desired)
            {
                var relation = card.OutRelations.FirstOrDefault(r => r.TargetCardId == input.TargetCardId);
                if (relation != null) relation.RelationType = input.RelationType;
                else
                {
                    relation = new WorldRelation { Id = Guid.NewGuid(), SourceCard = card, SourceCardId = card.Id,
                        TargetCard = targets[input.TargetCardId], TargetCardId = input.TargetCardId,
                        RelationType = input.RelationType, CreatedAt = DateTime.UtcNow };
                    _context.WorldRelations.Add(relation);
                }
            }
        }

        internal static Guid[] ValidateRelations(Guid cardId, List<CreateRelationDto> desired)
        {
            foreach (var relation in desired) WorldValidation.Validate(relation);
            var ids = desired.Select(r => r.TargetCardId).ToArray();
            if (ids.Contains(cardId) || ids.Distinct().Count() != ids.Length)
                throw new WorldOperationException(WorldErrorKind.Invalid, "不能关联自己或重复关联同一条目");
            return ids;
        }

        private static CardResponseDto? EnsureProject(CardResponseDto? card, Guid? projectId)
        {
            if (card != null && projectId.HasValue && card.ProjectId != projectId.Value)
                throw new WorldOperationException(WorldErrorKind.NotFound, "卡片不属于该项目");
            return card;
        }

        private CardResponseDto MapToResponseDto(WorldCard card)
        {
            var outRelations = card.OutRelations.Select(WorldRelationMapper.FromEntity).ToList();
            var inRelations = card.InRelations.Select(WorldRelationMapper.FromEntity).ToList();

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

    }
}
