using Microsoft.EntityFrameworkCore;
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

        public WorldCardService(AppDbContext context, IWorldProjectService projectService)
        {
            _context = context;
            _projectService = projectService;
        }

        public async Task<IEnumerable<CardResponseDto>> GetCardsByProjectAsync(Guid projectId, Guid userId, string? typeFilter = null)
        {
            // 检查项目是否存在且用户有权限
            var project = await _projectService.GetProjectByIdAsync(projectId, userId);
            if (project == null)
                return new List<CardResponseDto>();

            var query = _context.WorldCards
                .Include(c => c.OutRelations)
                .Include(c => c.InRelations)
                .Where(c => c.ProjectId == projectId);

            if (!string.IsNullOrEmpty(typeFilter))
                query = query.Where(c => c.Type == typeFilter);

            var cards = await query.ToListAsync();

            return cards.Select(c => MapToResponseDto(c, userId)).ToList();
        }

        public async Task<CardResponseDto> GetCardByIdAsync(Guid cardId, Guid userId)
        {
            var card = await _context.WorldCards
                .Include(c => c.Project)
                .Include(c => c.OutRelations)
                    .ThenInclude(r => r.TargetCard)
                .Include(c => c.InRelations)
                    .ThenInclude(r => r.SourceCard)
                .FirstOrDefaultAsync(c => c.Id == cardId);

            if (card == null)
                return null;

            // 检查权限：公开项目或自己是所有者
            if (!card.Project.IsPublic && card.Project.OwnerId != userId)
                return null;

            return MapToResponseDto(card, userId);
        }

        public async Task<CardResponseDto> CreateCardAsync(Guid projectId, Guid userId, CreateCardDto dto)
        {
            // 检查项目是否存在且用户是所有者
            var isOwner = await _projectService.IsProjectOwnerAsync(projectId, userId);
            if (!isOwner)
                throw new UnauthorizedAccessException("无权在此项目中创建卡片");

            var card = new WorldCard
            {
                ProjectId = projectId,
                Title = dto.Title,
                Type = dto.Type,
                SubType = dto.SubType,
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

            return await GetCardByIdAsync(card.Id, userId);
        }

        public async Task<CardResponseDto> UpdateCardAsync(Guid cardId, Guid userId, UpdateCardDto dto)
        {
            var card = await _context.WorldCards
                .Include(c => c.Project)
                .FirstOrDefaultAsync(c => c.Id == cardId);

            if (card == null)
                return null;

            // 检查权限
            if (card.Project.OwnerId != userId)
                return null;

            if (!string.IsNullOrEmpty(dto.Title))
                card.Title = dto.Title;

            if (!string.IsNullOrEmpty(dto.Type))
                card.Type = dto.Type;

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

            card.UpdatedAt = DateTime.UtcNow;

            _context.WorldCards.Update(card);
            await _context.SaveChangesAsync();

            return await GetCardByIdAsync(card.Id, userId);
        }

        public async Task<bool> DeleteCardAsync(Guid cardId, Guid userId)
        {
            var card = await _context.WorldCards
                .Include(c => c.Project)
                .Include(c => c.OutRelations)
                .Include(c => c.InRelations)
                .FirstOrDefaultAsync(c => c.Id == cardId);

            if (card == null)
                return false;

            // 检查权限
            if (card.Project.OwnerId != userId)
                return false;

            // 手动删除关联（因为配置了 Restrict）
            var relations = card.OutRelations.Concat(card.InRelations).ToList();
            _context.WorldRelations.RemoveRange(relations);

            _context.WorldCards.Remove(card);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsCardInProjectAsync(Guid cardId, Guid projectId)
        {
            return await _context.WorldCards
                .AnyAsync(c => c.Id == cardId && c.ProjectId == projectId);
        }

        // ===== 私有辅助方法 =====

        private CardResponseDto MapToResponseDto(WorldCard card, Guid userId)
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