using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Club;
using TaiChuWeb_V2.Models.Club;

namespace TaiChuWeb_V2.Controllers.Club
{
    [ApiController]
    [Route("api/club/operators")]
    [Authorize]
    public class OperatorController : ControllerBase
    {
        private readonly AppDbContext _db;
        public OperatorController(AppDbContext db) => _db = db;

        [HttpGet("me")]
        public async Task<IActionResult> GetMyStatus()
        {
            var userId = GetUserId();

            var profile = await _db.OperatorProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null)
                return Ok(new { hasApplied = false });

            var skills = await _db.OperatorGameSkills
                .AsNoTracking()
                .Where(s => s.UserId == userId)
                .OrderBy(s => s.AppliedAt)
                .ToListAsync();

            var games = skills.Select(s => new
            {
                gameCode = s.GameCode,
                gameName = s.GameName,
                metrics = ParseMetrics(s.MetricsJson),
                auditStatus = s.AuditStatus,
                auditNote = s.AuditNote,
                code = s.Code,
                level = s.OperatorLevel,
                recheckStatus = s.RecheckStatus,
                appliedAt = s.AppliedAt,
                reviewedAt = s.ReviewedAt,
                // ⭐ 新增
                ordersInGame = s.OrdersInGame,
                completedOrdersInGame = s.CompletedOrdersInGame,
                failedOrdersInGame = s.FailedOrdersInGame
            }).ToList();

            return Ok(new
            {
                hasApplied = true,
                nickname = profile.Nickname,
                contactType = profile.ContactType,
                contactValue = profile.ContactValue,
                onlineTime = profile.OnlineTime,
                intro = profile.Intro,
                // ⭐ 新增：自己的统计
                reputation = profile.Reputation,
                totalOrders = profile.TotalOrders,
                reviewCount = profile.ReviewCount,
                avgOverall = Math.Round(profile.AvgOverall, 2),
                avgSkill = Math.Round(profile.AvgSkill, 2),
                avgAttitude = Math.Round(profile.AvgAttitude, 2),
                avgPunctual = Math.Round(profile.AvgPunctual, 2),
                uniqueCustomers = profile.UniqueCustomers,
                repeatCustomers = profile.RepeatCustomers,
                completedOrders = profile.CompletedOrders,
                cancelledOrders = profile.CancelledOrders,
                games
            });
        }

        // Apply / ApplyGame / ValidateMetrics / ParseMetrics / GetUserId 全部保持不变
        // 直接保留你原来那几段

        [HttpPost("apply")]
        public async Task<IActionResult> Apply([FromBody] OperatorApplyDto dto)
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(dto.Nickname)) return BadRequest(new { message = "请填写打手名称" });
            if (string.IsNullOrWhiteSpace(dto.ContactType)) return BadRequest(new { message = "请选择联系方式平台" });
            if (string.IsNullOrWhiteSpace(dto.ContactValue)) return BadRequest(new { message = "请填写联系账号" });
            if (string.IsNullOrWhiteSpace(dto.OnlineTime)) return BadRequest(new { message = "请选择可在线时段" });
            if (string.IsNullOrWhiteSpace(dto.GameCode)) return BadRequest(new { message = "请选择申请的游戏" });

            var game = await _db.ClubGames
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Code == dto.GameCode && g.IsActive);
            if (game == null) return BadRequest(new { message = "游戏不存在或已下架" });

            var existingProfile = await _db.OperatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);
            if (existingProfile != null)
                return BadRequest(new { message = "你已提交过打手申请，如需增加游戏请使用追加申请" });

            var fieldError = await ValidateMetrics(dto.GameCode, dto.Metrics);
            if (fieldError != null) return BadRequest(new { message = fieldError });

            var profile = new OperatorProfile
            {
                UserId = userId,
                Nickname = dto.Nickname.Trim(),
                ContactType = dto.ContactType,
                ContactValue = dto.ContactValue.Trim(),
                OnlineTime = dto.OnlineTime,
                Intro = dto.Intro?.Trim(),
                AuditStatus = "pending",
                AppliedAt = DateTime.UtcNow
            };
            _db.OperatorProfiles.Add(profile);

            var skill = new OperatorGameSkill
            {
                UserId = userId,
                GameCode = dto.GameCode,
                GameName = game.Name,
                MetricsJson = JsonSerializer.Serialize(dto.Metrics),
                AuditStatus = "pending",
                AppliedAt = DateTime.UtcNow
            };
            _db.OperatorGameSkills.Add(skill);

            await _db.SaveChangesAsync();
            return Ok(new { message = "申请已提交，请等待审核" });
        }

        [HttpPost("games/apply")]
        public async Task<IActionResult> ApplyGame([FromBody] OperatorGameApplyDto dto)
        {
            var userId = GetUserId();

            if (string.IsNullOrWhiteSpace(dto.GameCode))
                return BadRequest(new { message = "请选择游戏" });

            var profile = await _db.OperatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null) return BadRequest(new { message = "请先提交打手申请" });

            var game = await _db.ClubGames
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Code == dto.GameCode && g.IsActive);
            if (game == null) return BadRequest(new { message = "游戏不存在或已下架" });

            var existingSkill = await _db.OperatorGameSkills
                .FirstOrDefaultAsync(s => s.UserId == userId && s.GameCode == dto.GameCode);
            if (existingSkill != null)
            {
                if (existingSkill.AuditStatus == "pending" || existingSkill.AuditStatus == "reviewing")
                    return BadRequest(new { message = "该游戏的申请正在审核中" });
                if (existingSkill.AuditStatus == "approved")
                    return BadRequest(new { message = "你已是该游戏的打手" });
                if (existingSkill.AuditStatus == "banned")
                    return BadRequest(new { message = "该游戏账号已被封禁" });
            }

            var fieldError = await ValidateMetrics(dto.GameCode, dto.Metrics);
            if (fieldError != null) return BadRequest(new { message = fieldError });

            var metricsJson = JsonSerializer.Serialize(dto.Metrics);

            if (existingSkill == null)
            {
                _db.OperatorGameSkills.Add(new OperatorGameSkill
                {
                    UserId = userId,
                    GameCode = dto.GameCode,
                    GameName = game.Name,
                    MetricsJson = metricsJson,
                    AuditStatus = "pending",
                    AppliedAt = DateTime.UtcNow
                });
            }
            else
            {
                existingSkill.MetricsJson = metricsJson;
                existingSkill.AuditStatus = "pending";
                existingSkill.AuditNote = null;
                existingSkill.RecheckStatus = "none";
                existingSkill.ReviewedAt = null;
                existingSkill.AppliedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "申请已提交" });
        }

        // ==================================================
        // 内部
        // ==================================================

        private async Task<string?> ValidateMetrics(string gameCode, Dictionary<string, string> metrics)
        {
            var fields = await _db.ClubGameFields
                .AsNoTracking()
                .Where(f => f.GameCode == gameCode)
                .ToListAsync();

            foreach (var f in fields)
            {
                if (f.Required)
                {
                    if (metrics == null || !metrics.ContainsKey(f.Key) ||
                        string.IsNullOrWhiteSpace(metrics[f.Key]))
                    {
                        return $"请填写：{f.Label}";
                    }
                }
            }
            return null;
        }

        private static Dictionary<string, string> ParseMetrics(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new Dictionary<string, string>();
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                       ?? new Dictionary<string, string>();
            }
            catch
            {
                return new Dictionary<string, string>();
            }
        }

        private Guid GetUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? throw new UnauthorizedAccessException();
            return Guid.Parse(sub);
        }
    }
}