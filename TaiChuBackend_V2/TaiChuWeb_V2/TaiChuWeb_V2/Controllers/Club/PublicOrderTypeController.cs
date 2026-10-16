using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers.Club
{
    /// <summary>
    /// 订单类型公开读取（前台用，无需登录）
    /// 路由前缀：api/club
    /// </summary>
    [ApiController]
    [Route("api/club")]
    public class PublicOrderTypeController : ControllerBase
    {
        private readonly AppDbContext _db;
        public PublicOrderTypeController(AppDbContext db) => _db = db;

        /// <summary>
        /// 按游戏取当前有效的订单类型（前台展示用，已过期/未生效自动过滤）
        /// GET /api/club/order-types?gameCode=delta
        /// </summary>
        [HttpGet("order-types")]
        public async Task<IActionResult> GetPublicList([FromQuery] string gameCode)
        {
            if (string.IsNullOrWhiteSpace(gameCode))
                return Ok(Array.Empty<object>());

            var now = DateTime.UtcNow;

            var list = await _db.ClubOrderTypes
                .AsNoTracking()
                .Where(t => t.GameCode == gameCode && t.IsActive)
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
                    pricingMode = t.PricingMode,
                    minOperatorCount = t.MinOperatorCount,
                    maxOperatorCount = t.MaxOperatorCount
                })
                .ToListAsync();

            return Ok(list);
        }
    }
}