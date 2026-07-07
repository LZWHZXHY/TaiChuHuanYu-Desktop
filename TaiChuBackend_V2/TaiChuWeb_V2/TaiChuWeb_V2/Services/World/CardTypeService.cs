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
    public class CardTypeService : ICardTypeService
    {
        private readonly AppDbContext _context;

        public CardTypeService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<CardTypeDto>> GetAllActiveTypesAsync()
        {
            var types = await _context.CardTypes
                .Where(t => t.IsActive)
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.Label)
                .ToListAsync();

            return types.Select(MapToDto);
        }

        public async Task<IEnumerable<CardTypeDto>> GetAllTypesAsync()
        {
            var types = await _context.CardTypes
                .OrderBy(t => t.SortOrder)
                .ThenBy(t => t.Label)
                .ToListAsync();

            return types.Select(MapToDto);
        }

        public async Task<CardTypeDto> GetTypeByIdAsync(string id)
        {
            var type = await _context.CardTypes.FindAsync(id);
            if (type == null)
                return null;

            return MapToDto(type);
        }

        public async Task<CardTypeDto> CreateTypeAsync(CreateCardTypeDto dto)
        {
            // 检查ID是否已存在
            var exists = await _context.CardTypes.AnyAsync(t => t.Id == dto.Id);
            if (exists)
                throw new InvalidOperationException($"类型 '{dto.Id}' 已存在");

            var type = new CardType
            {
                Id = dto.Id,
                Label = dto.Label,
                Icon = dto.Icon,
                Description = dto.Description,
                SortOrder = dto.SortOrder,
                IsActive = true,
                IsSystem = false,
                CreatedAt = DateTime.UtcNow
            };

            await _context.CardTypes.AddAsync(type);
            await _context.SaveChangesAsync();

            return MapToDto(type);
        }

        public async Task<CardTypeDto> UpdateTypeAsync(string id, UpdateCardTypeDto dto)
        {
            var type = await _context.CardTypes.FindAsync(id);
            if (type == null)
                return null;

            // 系统类型只能修改部分字段
            if (type.IsSystem)
            {
                // 系统类型只能修改显示名称、图标、描述、排序，不能删除
                if (dto.Label != null)
                    type.Label = dto.Label;

                if (dto.Icon != null)
                    type.Icon = dto.Icon;

                if (dto.Description != null)
                    type.Description = dto.Description;

                if (dto.SortOrder.HasValue)
                    type.SortOrder = dto.SortOrder.Value;

                // 系统类型不能停用
                if (dto.IsActive.HasValue && !dto.IsActive.Value)
                    throw new InvalidOperationException("系统预设类型不能停用");
            }
            else
            {
                // 非系统类型可修改所有字段
                if (dto.Label != null)
                    type.Label = dto.Label;

                if (dto.Icon != null)
                    type.Icon = dto.Icon;

                if (dto.Description != null)
                    type.Description = dto.Description;

                if (dto.SortOrder.HasValue)
                    type.SortOrder = dto.SortOrder.Value;

                if (dto.IsActive.HasValue)
                    type.IsActive = dto.IsActive.Value;
            }

            type.UpdatedAt = DateTime.UtcNow;

            _context.CardTypes.Update(type);
            await _context.SaveChangesAsync();

            return MapToDto(type);
        }

        public async Task<bool> DeleteTypeAsync(string id)
        {
            var type = await _context.CardTypes.FindAsync(id);
            if (type == null)
                return false;

            // 系统类型不可删除
            if (type.IsSystem)
                throw new InvalidOperationException("系统预设类型不能删除");

            // 检查是否有卡片正在使用该类型
            var hasCards = await _context.WorldCards.AnyAsync(c => c.Type == id);
            if (hasCards)
                throw new InvalidOperationException($"有卡片正在使用类型 '{type.Label}'，无法删除");

            _context.CardTypes.Remove(type);
            await _context.SaveChangesAsync();
            return true;
        }

        // ===== 私有辅助方法 =====

        private CardTypeDto MapToDto(CardType type)
        {
            return new CardTypeDto
            {
                Id = type.Id,
                Label = type.Label,
                Icon = type.Icon,
                Description = type.Description,
                SortOrder = type.SortOrder,
                IsActive = type.IsActive,
                IsSystem = type.IsSystem,
                CreatedAt = type.CreatedAt,
                UpdatedAt = type.UpdatedAt
            };
        }
    }
}