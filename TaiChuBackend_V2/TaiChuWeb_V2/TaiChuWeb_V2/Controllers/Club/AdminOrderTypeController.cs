using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Club;

namespace TaiChuWeb_V2.Controllers.Club
{
    /// <summary>
    /// 订单类型管理（后台配置 + 公开读取）
    /// 路由前缀：api/admin/club 与 api/club
    /// </summary>
    [ApiController]
    public class AdminOrderTypeController : ControllerBase
    {
        private readonly AppDbContext _db;
        public AdminOrderTypeController(AppDbContext db) => _db = db;

        // ==================================================
        //  后台接口
        // ==================================================

        /// <summary>按游戏取订单类型列表（含已过期、已停用）</summary>
        [HttpGet("api/admin/club/order-types")]
        public async Task<IActionResult> GetList([FromQuery] string gameCode)
        {
            if (string.IsNullOrWhiteSpace(gameCode))
                return Ok(Array.Empty<object>());

            var list = await _db.ClubOrderTypes
                .AsNoTracking()
                .Where(t => t.GameCode == gameCode)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
                .Select(t => new
                {
                    id = t.Id,
                    gameCode = t.GameCode,
                    code = t.Code,
                    name = t.Name,
                    description = t.Description,
                    paramsSchemaJson = t.ParamsSchemaJson,
                    pricingJson = t.PricingJson,
                    specificRulesText = t.SpecificRulesText,
                    pricingMode = t.PricingMode,
                    isActive = t.IsActive,
                    sortOrder = t.SortOrder,
                    startAt = t.StartAt,
                    endAt = t.EndAt,
                    createdAt = t.CreatedAt
                })
                .ToListAsync();

            return Ok(list);
        }

        /// <summary>新增 / 更新订单类型</summary>
        [HttpPost("api/admin/club/order-type")]
        public async Task<IActionResult> Save([FromBody] OrderTypeDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.GameCode) ||
                string.IsNullOrWhiteSpace(dto.Code) ||
                string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest(new { message = "GameCode / Code / Name 不能为空" });
            }

            var gameExists = await _db.ClubGames
                .AsNoTracking()
                .AnyAsync(g => g.Code == dto.GameCode);
            if (!gameExists)
                return BadRequest(new { message = $"游戏代码 {dto.GameCode} 不存在" });

            // 时间窗口校验
            if (dto.StartAt.HasValue && dto.EndAt.HasValue &&
                dto.StartAt.Value >= dto.EndAt.Value)
            {
                return BadRequest(new { message = "上架时间不能晚于下架时间" });
            }

            ClubOrderType entity;
            if (dto.Id.HasValue && dto.Id.Value > 0)
            {
                entity = await _db.ClubOrderTypes.FirstOrDefaultAsync(t => t.Id == dto.Id.Value);
                if (entity == null) return NotFound(new { message = "订单类型不存在" });
            }
            else
            {
                var dup = await _db.ClubOrderTypes.AnyAsync(t =>
                    t.GameCode == dto.GameCode && t.Code == dto.Code);
                if (dup)
                    return BadRequest(new { message = $"订单代码「{dto.Code}」在该游戏下已存在" });

                entity = new ClubOrderType
                {
                    GameCode = dto.GameCode,
                    Code = dto.Code.Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                _db.ClubOrderTypes.Add(entity);
            }

            entity.Name = dto.Name.Trim();
            entity.Description = dto.Description;
            entity.ParamsSchemaJson = dto.ParamsSchemaJson;
            entity.PricingJson = dto.PricingJson;
            entity.SpecificRulesText = dto.SpecificRulesText;
            entity.PricingMode = string.IsNullOrWhiteSpace(dto.PricingMode) ? "fixed" : dto.PricingMode;
            entity.IsActive = dto.IsActive;
            entity.SortOrder = dto.SortOrder;
            entity.StartAt = dto.StartAt;
            entity.EndAt = dto.EndAt;

            await _db.SaveChangesAsync();
            return Ok(new { message = "保存成功", id = entity.Id });
        }

        /// <summary>删除订单类型</summary>
        [HttpDelete("api/admin/club/order-type/{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            var entity = await _db.ClubOrderTypes.FindAsync(id);
            if (entity == null) return NotFound(new { message = "订单类型不存在" });

            _db.ClubOrderTypes.Remove(entity);
            await _db.SaveChangesAsync();
            return Ok(new { message = "删除成功" });
        }

        // ==================================================
        //  ⭐ 新增：一键上下架
        // ==================================================
        [HttpPatch("api/admin/club/order-type/{id:long}/toggle")]
        public async Task<IActionResult> Toggle(long id)
        {
            var entity = await _db.ClubOrderTypes.FindAsync(id);
            if (entity == null) return NotFound(new { message = "订单类型不存在" });

            entity.IsActive = !entity.IsActive;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = entity.IsActive ? "已上架" : "已下架",
                isActive = entity.IsActive
            });
        }

        // ==================================================
        //  ⭐ 新增：复制订单类型
        // ==================================================
        [HttpPost("api/admin/club/order-type/{id:long}/copy")]
        public async Task<IActionResult> Copy(long id)
        {
            var src = await _db.ClubOrderTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);
            if (src == null) return NotFound(new { message = "订单类型不存在" });

            // 生成不重复的 code：escort → escort-copy → escort-copy1 → escort-copy2
            var newCode = src.Code + "-copy";
            var i = 1;
            while (await _db.ClubOrderTypes.AnyAsync(t =>
                t.GameCode == src.GameCode && t.Code == newCode))
            {
                newCode = $"{src.Code}-copy{i++}";
            }

            var copy = new ClubOrderType
            {
                GameCode = src.GameCode,
                Code = newCode,
                Name = src.Name + " (副本)",
                Description = src.Description,
                ParamsSchemaJson = src.ParamsSchemaJson,
                PricingJson = src.PricingJson,
                SpecificRulesText = src.SpecificRulesText,
                PricingMode = src.PricingMode,
                IsActive = false,          // 副本默认停用，避免误上架
                SortOrder = src.SortOrder + 1,
                StartAt = null,             // 副本不继承时效
                EndAt = null,
                CreatedAt = DateTime.UtcNow
            };

            _db.ClubOrderTypes.Add(copy);
            await _db.SaveChangesAsync();

            return Ok(new { message = "复制成功", id = copy.Id, code = copy.Code });
        }

        // ==================================================
        //  ⭐ 新增：快速修改时效
        // ==================================================
        [HttpPatch("api/admin/club/order-type/{id:long}/time")]
        public async Task<IActionResult> UpdateTime(long id, [FromBody] UpdateTimeDto dto)
        {
            var entity = await _db.ClubOrderTypes.FindAsync(id);
            if (entity == null) return NotFound(new { message = "订单类型不存在" });

            if (dto.StartAt.HasValue && dto.EndAt.HasValue &&
                dto.StartAt.Value >= dto.EndAt.Value)
            {
                return BadRequest(new { message = "上架时间不能晚于下架时间" });
            }

            entity.StartAt = dto.StartAt;
            entity.EndAt = dto.EndAt;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "时效已更新",
                startAt = entity.StartAt,
                endAt = entity.EndAt
            });
        }

        // ==================================================
        //  公开接口（前台用）
        // ==================================================

        /// <summary>按游戏取当前有效的订单类型（前台展示用，已过期/未生效自动过滤）</summary>
        [HttpGet("api/club/order-types")]
        public async Task<IActionResult> GetPublicList([FromQuery] string gameCode)
        {
            if (string.IsNullOrWhiteSpace(gameCode))
                return Ok(Array.Empty<object>());

            var now = DateTime.UtcNow;

            var list = await _db.ClubOrderTypes
                .AsNoTracking()
                .Where(t => t.GameCode == gameCode && t.IsActive)
                // ⭐ 时间窗口过滤
                .Where(t => t.StartAt == null || t.StartAt <= now)
                .Where(t => t.EndAt == null || t.EndAt >= now)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.Id)
                .Select(t => new
                {
                    code = t.Code,
                    name = t.Name,
                    description = t.Description,
                    paramsSchemaJson = t.ParamsSchemaJson,
                    pricingJson = t.PricingJson,
                    specificRulesText = t.SpecificRulesText,
                    pricingMode = t.PricingMode
                })
                .ToListAsync();

            return Ok(list);
        }

        // ==================================================
        //  DTO
        // ==================================================
        public class OrderTypeDto
        {
            public long? Id { get; set; }
            public string GameCode { get; set; } = "";
            public string Code { get; set; } = "";
            public string Name { get; set; } = "";
            public string? Description { get; set; }
            public string? ParamsSchemaJson { get; set; }
            public string? PricingJson { get; set; }
            public string? SpecificRulesText { get; set; }
            public string PricingMode { get; set; } = "fixed";
            public bool IsActive { get; set; } = true;
            public int SortOrder { get; set; }
            public DateTime? StartAt { get; set; }
            public DateTime? EndAt { get; set; }
        }

        public class UpdateTimeDto
        {
            public DateTime? StartAt { get; set; }
            public DateTime? EndAt { get; set; }
        }
    }
}