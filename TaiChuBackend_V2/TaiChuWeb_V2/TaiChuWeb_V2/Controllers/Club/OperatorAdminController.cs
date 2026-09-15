using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Club;
using TaiChuWeb_V2.Models.Club;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers.Club
{
    [ApiController]
    [Route("api/club/admin/operators")]
    [Authorize]
    public class OperatorAdminController : ControllerBase
    {
        private readonly AppDbContext _db;

        public OperatorAdminController(AppDbContext db)
        {
            _db = db;
        }

        // ==================================================
        // 列表（按游戏 + 状态）
        // ==================================================

        /// <summary>按游戏 + 审核状态查申请</summary>
        [HttpGet]
        public async Task<IActionResult> GetList(
            [FromQuery] string? game = null,
            [FromQuery] string status = "pending",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (!await HasPermission(AdminPermission.Club_OperatorAudit))
                return StatusCode(403, new { message = "无权限" });

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _db.OperatorGameSkills
                .AsNoTracking()
                .Where(s => s.AuditStatus == status);

            if (!string.IsNullOrWhiteSpace(game))
                query = query.Where(s => s.GameCode == game);

            var total = await query.CountAsync();

            var raw = await query
                .OrderBy(s => s.AppliedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 一次拉所有涉及的 UserId 对应的 Profile 和 User
            var userIds = raw.Select(s => s.UserId).Distinct().ToList();

            var profiles = await _db.OperatorProfiles
                .AsNoTracking()
                .Where(p => userIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId);

            var users = await _db.Users
                .AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var items = raw.Select(s =>
            {
                profiles.TryGetValue(s.UserId, out var p);
                users.TryGetValue(s.UserId, out var u);
                return new OperatorPendingItem
                {
                    UserId = s.UserId,
                    Username = u?.Username ?? "",
                    Email = u?.Email,
                    Nickname = p?.Nickname ?? "",
                    ContactType = p?.ContactType ?? "",
                    ContactValue = p?.ContactValue ?? "",
                    OnlineTime = p?.OnlineTime ?? "",
                    Intro = p?.Intro,
                    GameCode = s.GameCode,
                    GameName = s.GameName,
                    Metrics = ParseMetrics(s.MetricsJson),
                    ReviewCount = p?.ReviewCount ?? 0,
                    AvgOverall = p?.AvgOverall ?? 0,
                    AuditStatus = s.AuditStatus,
                    AuditNote = s.AuditNote,
                    Code = s.Code,
                    OperatorLevel = s.OperatorLevel,
                    RecheckStatus = s.RecheckStatus,
                    AppliedAt = s.AppliedAt
                };
            }).ToList();

            return Ok(new { total, page, pageSize, items });
        }

        /// <summary>已通过的打手列表</summary>
        [HttpGet("active")]
        public async Task<IActionResult> GetActiveList(
            [FromQuery] string? game = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (!await HasPermission(AdminPermission.Club_OperatorAudit))
                return StatusCode(403, new { message = "无权限" });

            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var query = _db.OperatorGameSkills
                .AsNoTracking()
                .Where(s => s.AuditStatus == "approved");

            if (!string.IsNullOrWhiteSpace(game))
                query = query.Where(s => s.GameCode == game);

            var total = await query.CountAsync();

            var raw = await query
                .OrderByDescending(s => s.ReviewedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var userIds = raw.Select(s => s.UserId).Distinct().ToList();
            var profiles = await _db.OperatorProfiles
                .AsNoTracking()
                .Where(p => userIds.Contains(p.UserId))
                .ToDictionaryAsync(p => p.UserId);

            var items = raw.Select(s =>
            {
                profiles.TryGetValue(s.UserId, out var p);
                return new OperatorListItem
                {
                    UserId = s.UserId,
                    Nickname = p?.Nickname ?? "",
                    GameCode = s.GameCode,
                    GameName = s.GameName,
                    Metrics = ParseMetrics(s.MetricsJson),
                    Code = s.Code,
                    OperatorLevel = s.OperatorLevel,
                    RecheckStatus = s.RecheckStatus,
                    AuditStatus = s.AuditStatus,
                    OrdersInGame = s.OrdersInGame,
                    ReviewedAt = s.ReviewedAt
                };
            }).ToList();

            return Ok(new { total, page, pageSize, items });
        }

        // ==================================================
        // 审核动作（按 userId + gameCode）
        // ==================================================

        [HttpPost("{userId:guid}/games/{gameCode}/start-review")]
        public async Task<IActionResult> StartReview(Guid userId, string gameCode)
        {
            if (!await HasPermission(AdminPermission.Club_OperatorAudit))
                return StatusCode(403, new { message = "无权限" });

            var s = await _db.OperatorGameSkills
                .FirstOrDefaultAsync(x => x.UserId == userId && x.GameCode == gameCode);

            if (s == null) return NotFound(new { message = "申请不存在" });
            if (s.AuditStatus != "pending")
                return BadRequest(new { message = "该申请不是待审核状态" });

            s.AuditStatus = "reviewing";
            s.ReviewedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { message = "已进入考核阶段" });
        }

        [HttpPost("{userId:guid}/games/{gameCode}/approve")]
        public async Task<IActionResult> Approve(Guid userId, string gameCode, [FromBody] OperatorApproveDto dto)
        {
            if (!await HasPermission(AdminPermission.Club_OperatorAudit))
                return StatusCode(403, new { message = "无权限" });

            var validLevels = new[] { "L1", "L2", "L3", "L4", "L5" };
            if (!validLevels.Contains(dto.Level))
                return BadRequest(new { message = "护子等级无效" });

            var s = await _db.OperatorGameSkills
                .FirstOrDefaultAsync(x => x.UserId == userId && x.GameCode == gameCode);

            if (s == null) return NotFound(new { message = "申请不存在" });
            if (s.AuditStatus != "pending" && s.AuditStatus != "reviewing")
                return BadRequest(new { message = "该申请不在待审核或考核中状态" });

            var code = await GenerateOperatorCode(gameCode);

            s.AuditStatus = "approved";
            s.AuditNote = null;
            s.Code = code;
            s.OperatorLevel = dto.Level;
            s.RecheckStatus = "none";
            s.LastReviewAt = DateTime.UtcNow;
            s.NextRecheckAt = DateTime.UtcNow.AddMonths(1);
            s.ReviewedAt = DateTime.UtcNow;

            // 把通用档案也标记为已通过（如果有）
            var profile = await _db.OperatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile != null && profile.AuditStatus != "approved")
            {
                profile.AuditStatus = "approved";
                profile.ReviewedAt = DateTime.UtcNow;
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "考核通过", code, level = dto.Level });
        }

        [HttpPost("{userId:guid}/games/{gameCode}/reject")]
        public async Task<IActionResult> Reject(Guid userId, string gameCode, [FromBody] OperatorAuditDto dto)
        {
            if (!await HasPermission(AdminPermission.Club_OperatorAudit))
                return StatusCode(403, new { message = "无权限" });

            if (string.IsNullOrWhiteSpace(dto.Reason))
                return BadRequest(new { message = "请填写拒绝原因" });

            var s = await _db.OperatorGameSkills
                .FirstOrDefaultAsync(x => x.UserId == userId && x.GameCode == gameCode);

            if (s == null) return NotFound(new { message = "申请不存在" });
            if (s.AuditStatus != "pending" && s.AuditStatus != "reviewing")
                return BadRequest(new { message = "该申请不在待审核或考核中状态" });

            s.AuditStatus = "rejected";
            s.AuditNote = dto.Reason.Trim();
            s.ReviewedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { message = "已拒绝" });
        }

        // ==================================================
        // 复查
        // ==================================================

        [HttpPost("{userId:guid}/games/{gameCode}/start-recheck")]
        public async Task<IActionResult> StartRecheck(Guid userId, string gameCode)
        {
            if (!await HasPermission(AdminPermission.Club_OperatorAudit))
                return StatusCode(403, new { message = "无权限" });

            var s = await _db.OperatorGameSkills
                .FirstOrDefaultAsync(x => x.UserId == userId && x.GameCode == gameCode);

            if (s == null) return NotFound(new { message = "打手不存在" });
            if (s.AuditStatus != "approved")
                return BadRequest(new { message = "该打手不是已通过状态" });
            if (s.RecheckStatus == "rechecking")
                return BadRequest(new { message = "复查已在进行中" });

            s.RecheckStatus = "rechecking";
            s.LastReviewAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { message = "已发起复查" });
        }

        [HttpPost("{userId:guid}/games/{gameCode}/recheck-pass")]
        public async Task<IActionResult> RecheckPass(Guid userId, string gameCode)
        {
            if (!await HasPermission(AdminPermission.Club_OperatorAudit))
                return StatusCode(403, new { message = "无权限" });

            var s = await _db.OperatorGameSkills
                .FirstOrDefaultAsync(x => x.UserId == userId && x.GameCode == gameCode);

            if (s == null) return NotFound(new { message = "打手不存在" });
            if (s.RecheckStatus != "rechecking")
                return BadRequest(new { message = "该打手不在复查中" });

            s.RecheckStatus = "none";
            s.RecheckNote = null;
            s.LastReviewAt = DateTime.UtcNow;
            s.NextRecheckAt = DateTime.UtcNow.AddMonths(1);

            await _db.SaveChangesAsync();
            return Ok(new { message = "复查通过" });
        }

        [HttpPost("{userId:guid}/games/{gameCode}/recheck-fail")]
        public async Task<IActionResult> RecheckFail(Guid userId, string gameCode, [FromBody] OperatorRecheckFailDto dto)
        {
            if (!await HasPermission(AdminPermission.Club_OperatorAudit))
                return StatusCode(403, new { message = "无权限" });

            var s = await _db.OperatorGameSkills
                .FirstOrDefaultAsync(x => x.UserId == userId && x.GameCode == gameCode);

            if (s == null) return NotFound(new { message = "打手不存在" });
            if (s.RecheckStatus != "rechecking")
                return BadRequest(new { message = "该打手不在复查中" });

            var reason = string.IsNullOrWhiteSpace(dto.Reason) ? "复查未通过" : dto.Reason.Trim();

            if (dto.Demote)
            {
                s.OperatorLevel = DemoteLevel(s.OperatorLevel);
                s.DemoteCount += 1;
                s.RecheckStatus = "demoted";
                s.RecheckNote = reason;

                if (s.DemoteCount >= 3)
                {
                    s.RecheckStatus = "suspended";
                    s.AuditStatus = "banned";
                }
            }
            else
            {
                s.RecheckStatus = "flagged";
                s.RecheckNote = reason + "（7天内整改）";
                s.NextRecheckAt = DateTime.UtcNow.AddDays(7);
            }

            s.LastReviewAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new
            {
                message = dto.Demote ? "已降级" : "已标记待整改",
                recheckStatus = s.RecheckStatus,
                operatorLevel = s.OperatorLevel,
                demoteCount = s.DemoteCount
            });
        }

        // ==================================================
        // 封禁 / 解封
        // ==================================================

        [HttpPost("{userId:guid}/games/{gameCode}/ban")]
        public async Task<IActionResult> Ban(Guid userId, string gameCode, [FromBody] OperatorAuditDto dto)
        {
            if (!await HasPermission(AdminPermission.Club_OperatorAudit))
                return StatusCode(403, new { message = "无权限" });

            var s = await _db.OperatorGameSkills
                .FirstOrDefaultAsync(x => x.UserId == userId && x.GameCode == gameCode);

            if (s == null) return NotFound(new { message = "打手不存在" });

            s.AuditStatus = "banned";
            s.AuditNote = string.IsNullOrWhiteSpace(dto.Reason) ? "违规封禁" : dto.Reason.Trim();
            s.ReviewedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { message = "已封禁" });
        }

        [HttpPost("{userId:guid}/games/{gameCode}/unban")]
        public async Task<IActionResult> Unban(Guid userId, string gameCode)
        {
            if (!await HasPermission(AdminPermission.Club_OperatorAudit))
                return StatusCode(403, new { message = "无权限" });

            var s = await _db.OperatorGameSkills
                .FirstOrDefaultAsync(x => x.UserId == userId && x.GameCode == gameCode);

            if (s == null) return NotFound(new { message = "打手不存在" });
            if (s.AuditStatus != "banned")
                return BadRequest(new { message = "该打手未被封禁" });

            s.AuditStatus = "approved";
            s.AuditNote = null;
            s.RecheckStatus = "none";
            s.DemoteCount = 0;
            s.ReviewedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
            return Ok(new { message = "已解封" });
        }

        // ==================================================
        // 内部
        // ==================================================

        private async Task<bool> HasPermission(AdminPermission perm)
        {
            var uid = GetUserId();
            return await _db.UserPermissions.AnyAsync(p =>
                p.UserId == uid &&
                (p.Permission == perm || p.Permission == AdminPermission.SuperAdmin));
        }

        /// <summary>生成打手编号，形如 OP-DELTA-0001</summary>
        private async Task<string> GenerateOperatorCode(string gameCode)
        {
            var prefix = $"OP-{gameCode.ToUpper()}-";
            var maxCode = await _db.OperatorGameSkills
                .Where(s => s.Code != null && s.Code.StartsWith(prefix))
                .OrderByDescending(s => s.Code)
                .Select(s => s.Code)
                .FirstOrDefaultAsync();

            int next = 1;
            if (!string.IsNullOrEmpty(maxCode))
            {
                var numPart = maxCode.Substring(prefix.Length);
                if (int.TryParse(numPart, out var n)) next = n + 1;
            }

            return $"{prefix}{next:D4}";
        }

        private static string DemoteLevel(string? level) => level switch
        {
            "L5" => "L4",
            "L4" => "L3",
            "L3" => "L2",
            "L2" => "L1",
            _ => "L1"
        };

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