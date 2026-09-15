using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers.Club
{
    [ApiController]
    [Route("api/club/games")]
    public class GameController : ControllerBase
    {
        private readonly AppDbContext _db;

        public GameController(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>获取所有启用的游戏 + 字段配置（前端动态渲染用，无需登录）</summary>
        [HttpGet]
        public async Task<IActionResult> GetGames()
        {
            var games = await _db.ClubGames
                .AsNoTracking()
                .Where(g => g.IsActive)
                .OrderBy(g => g.SortOrder)
                .ToListAsync();

            var gameCodes = games.Select(g => g.Code).ToList();

            var fields = await _db.ClubGameFields
                .AsNoTracking()
                .Where(f => gameCodes.Contains(f.GameCode))
                .OrderBy(f => f.GameCode)
                .ThenBy(f => f.SortOrder)
                .ToListAsync();

            var result = games.Select(g => new
            {
                code = g.Code,
                name = g.Name,
                description = g.Description,
                commonRulesText = g.CommonRulesText,          // ⭐ 关键
                radarSystemDimsJson = g.RadarSystemDimsJson,  // ⭐ 顺便
                fields = fields
                    .Where(f => f.GameCode == g.Code)
                    .Select(f => new
                    {
                        key = f.Key,
                        label = f.Label,
                        type = f.Type,
                        required = f.Required,
                        placeholder = f.Placeholder,
                        options = ParseOptions(f.OptionsJson),
                        isScoreDimension = f.IsScoreDimension   // ⭐ 顺便
                    })
                    .ToList()
            });

            return Ok(result);
        }

        private static string[] ParseOptions(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
            try { return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
            catch { return Array.Empty<string>(); }
        }
    }
}