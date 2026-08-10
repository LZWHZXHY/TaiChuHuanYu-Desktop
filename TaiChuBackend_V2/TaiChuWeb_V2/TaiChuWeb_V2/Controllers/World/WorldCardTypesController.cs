using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.World;

namespace TaiChuWeb_V2.Controllers.World
{
    /// <summary>
    /// 卡片类型管理接口
    /// </summary>
    [Route("api/world/card-types")]
    [ApiController]
    [AllowAnonymous]  // 公开接口，无需登录（或改为 [Authorize]）
    public class WorldCardTypesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public WorldCardTypesController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 获取所有可用的卡片类型
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetCardTypes()
        {
            try
            {
                var types = await _context.CardTypes
                    .Where(t => t.IsActive)
                    .OrderBy(t => t.SortOrder)
                    .Select(t => new CardTypeDto
                    {
                        Id = t.Id,
                        Label = t.Label,
                        Icon = t.Icon,
                        Description = t.Description,
                        SortOrder = t.SortOrder,
                        IsActive = t.IsActive,
                        IsSystem = t.IsSystem,
                        CreatedAt = t.CreatedAt,
                        UpdatedAt = t.UpdatedAt
                    })
                    .ToListAsync();

                return Ok(types);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "获取卡片类型失败", error = ex.Message });
            }
        }

        /// <summary>
        /// 获取单个卡片类型详情
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetCardType(string id)
        {
            var type = await _context.CardTypes
                .Where(t => t.Id == id && t.IsActive)
                .Select(t => new CardTypeDto
                {
                    Id = t.Id,
                    Label = t.Label,
                    Icon = t.Icon,
                    Description = t.Description,
                    SortOrder = t.SortOrder,
                    IsActive = t.IsActive,
                    IsSystem = t.IsSystem,
                    CreatedAt = t.CreatedAt,
                    UpdatedAt = t.UpdatedAt
                })
                .FirstOrDefaultAsync();

            if (type == null)
                return NotFound(new { message = $"卡片类型 '{id}' 不存在" });

            return Ok(type);
        }

        /// <summary>
        /// 创建新卡片类型（仅管理员）
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Admin")]  // 仅管理员可操作
        public async Task<IActionResult> CreateCardType([FromBody] CreateCardTypeDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            // 检查是否已存在
            if (await _context.CardTypes.AnyAsync(t => t.Id == dto.Id))
                return Conflict(new { message = $"卡片类型 '{dto.Id}' 已存在" });

            var cardType = new CardType
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

            await _context.CardTypes.AddAsync(cardType);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCardType), new { id = cardType.Id }, cardType);
        }

        /// <summary>
        /// 更新卡片类型（仅管理员）
        /// </summary>
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> UpdateCardType(string id, [FromBody] UpdateCardTypeDto dto)
        {
            var cardType = await _context.CardTypes.FirstOrDefaultAsync(t => t.Id == id);
            if (cardType == null)
                return NotFound(new { message = $"卡片类型 '{id}' 不存在" });

            // 系统预设类型不允许修改 Label 和 Id
            if (cardType.IsSystem)
            {
                if (!string.IsNullOrEmpty(dto.Label))
                    return BadRequest(new { message = "系统预设类型不允许修改 Label" });
            }

            if (!string.IsNullOrEmpty(dto.Label))
                cardType.Label = dto.Label;

            if (dto.Icon != null)
                cardType.Icon = dto.Icon;

            if (dto.Description != null)
                cardType.Description = dto.Description;

            if (dto.SortOrder.HasValue)
                cardType.SortOrder = dto.SortOrder.Value;

            if (dto.IsActive.HasValue)
                cardType.IsActive = dto.IsActive.Value;

            cardType.UpdatedAt = DateTime.UtcNow;

            _context.CardTypes.Update(cardType);
            await _context.SaveChangesAsync();

            return Ok(new CardTypeDto
            {
                Id = cardType.Id,
                Label = cardType.Label,
                Icon = cardType.Icon,
                Description = cardType.Description,
                SortOrder = cardType.SortOrder,
                IsActive = cardType.IsActive,
                IsSystem = cardType.IsSystem,
                CreatedAt = cardType.CreatedAt,
                UpdatedAt = cardType.UpdatedAt
            });
        }

        /// <summary>
        /// 删除卡片类型（仅管理员，系统预设类型不可删除）
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteCardType(string id)
        {
            var cardType = await _context.CardTypes.FirstOrDefaultAsync(t => t.Id == id);
            if (cardType == null)
                return NotFound(new { message = $"卡片类型 '{id}' 不存在" });

            if (cardType.IsSystem)
                return BadRequest(new { message = "系统预设类型不可删除" });

            // 检查是否有关联的卡片在使用该类型
            var hasCards = await _context.WorldCards.AnyAsync(c => c.Type == id);
            if (hasCards)
                return Conflict(new { message = $"已有卡片使用该类型，不可删除" });

            _context.CardTypes.Remove(cardType);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}