using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Game;

namespace TaiChuWeb_V2.Controllers.Game
{
    public class SaveGameRecordDto
    {
        public string GameType { get; set; } = string.Empty;
        public string Player1Name { get; set; } = "先手";
        public string Player2Name { get; set; } = "后手";
        public string Winner { get; set; } = "undefined"; // player1 / player2 / draw
        public int MovesCount { get; set; }
        public string? MovesJson { get; set; }
    }

    /// <summary>棋类对局记录（五子棋 / 围棋 / 象棋）。</summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GameRecordController : ControllerBase
    {
        private readonly AppDbContext _context;
        public GameRecordController(AppDbContext context) { _context = context; }

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        [HttpPost]
        public async Task<IActionResult> Save([FromBody] SaveGameRecordDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.GameType))
                return BadRequest(new { message = "游戏类型不能为空" });

            var record = new BoardGameRecord
            {
                GameType = dto.GameType,
                Player1Id = CurrentUserId,
                Player1Name = string.IsNullOrWhiteSpace(dto.Player1Name) ? "先手" : dto.Player1Name,
                Player2Name = string.IsNullOrWhiteSpace(dto.Player2Name) ? "后手" : dto.Player2Name,
                Winner = dto.Winner,
                MovesCount = dto.MovesCount,
                MovesJson = dto.MovesJson,
                CreatedAt = DateTime.UtcNow
            };
            _context.BoardGameRecords.Add(record);
            await _context.SaveChangesAsync();
            return Ok(new { id = record.Id });
        }

        [HttpGet("my")]
        public async Task<IActionResult> My([FromQuery] string? type = null)
        {
            if (!CurrentUserId.HasValue) return Ok(new List<object>());
            var query = _context.BoardGameRecords
                .Where(r => r.Player1Id == CurrentUserId.Value || r.Player2Id == CurrentUserId.Value);
            if (!string.IsNullOrWhiteSpace(type)) query = query.Where(r => r.GameType == type);
            var list = await query.OrderByDescending(r => r.CreatedAt).Take(50)
                .Select(r => new { r.Id, r.GameType, r.Player1Name, r.Player2Name, r.Winner, r.MovesCount, r.CreatedAt })
                .ToListAsync();
            return Ok(list);
        }

        [HttpGet("recent")]
        [AllowAnonymous]
        public async Task<IActionResult> Recent([FromQuery] string? type = null, [FromQuery] int limit = 20)
        {
            if (limit < 1 || limit > 100) limit = 20;
            var query = _context.BoardGameRecords.AsQueryable();
            if (!string.IsNullOrWhiteSpace(type)) query = query.Where(r => r.GameType == type);
            var list = await query.OrderByDescending(r => r.CreatedAt).Take(limit)
                .Select(r => new { r.Id, r.GameType, r.Player1Name, r.Player2Name, r.Winner, r.MovesCount, r.CreatedAt })
                .ToListAsync();
            return Ok(list);
        }
    }
}
