using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Club;

namespace TaiChuWeb_V2.Controllers.Club
{
    /// <summary>
    /// 俱乐部管理端（后台）
    /// 路由前缀：api/admin/club
    /// </summary>
    [ApiController]
    [Route("api/admin/club")]
    public class AdminClubController : ControllerBase
    {
        private readonly AppDbContext _db;
        public AdminClubController(AppDbContext db) => _db = db;

        // ==================================================
        //  1. 游戏库
        // ==================================================

        [HttpGet("games")]
        public async Task<IActionResult> GetGames()
        {
            var list = await _db.ClubGames
                .AsNoTracking()
                .OrderBy(g => g.SortOrder).ThenBy(g => g.Code)
                .Select(g => new
                {
                    code = g.Code,
                    name = g.Name,
                    description = g.Description,
                    commonRulesText = g.CommonRulesText,
                    radarSystemDimsJson = g.RadarSystemDimsJson,   // ⭐ 新增
                    isActive = g.IsActive,
                    sortOrder = g.SortOrder,
                    createdAt = g.CreatedAt
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("game")]
        public async Task<IActionResult> SaveGame([FromBody] ClubGameDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Code) || string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "Code 与 Name 不能为空" });

            var entity = await _db.ClubGames
                .FirstOrDefaultAsync(g => g.Code == dto.Code.Trim());

            if (entity == null)
            {
                entity = new ClubGame
                {
                    Code = dto.Code.Trim(),
                    CreatedAt = DateTime.UtcNow
                };
                _db.ClubGames.Add(entity);
            }

            entity.Name = dto.Name.Trim();
            entity.Description = dto.Description;
            entity.IsActive = dto.IsActive;
            entity.SortOrder = dto.SortOrder;
            entity.CommonRulesText = dto.CommonRulesText;
            entity.RadarSystemDimsJson = dto.RadarSystemDimsJson;   // ⭐ 新增

            await _db.SaveChangesAsync();
            return Ok(new { message = "保存成功", code = entity.Code });
        }

        [HttpDelete("game/{code}")]
        public async Task<IActionResult> DeleteGame(string code)
        {
            var game = await _db.ClubGames.FirstOrDefaultAsync(g => g.Code == code);
            if (game == null) return NotFound(new { message = "游戏不存在" });

            var fields = await _db.ClubGameFields
                .Where(f => f.GameCode == code)
                .ToListAsync();
            _db.ClubGameFields.RemoveRange(fields);

            _db.ClubGames.Remove(game);
            await _db.SaveChangesAsync();
            return Ok(new { message = "删除成功" });
        }

        // ==================================================
        //  ⭐ 新增：系统评分维度清单（前端展示勾选项用）
        // ==================================================
        [HttpGet("radar-system-dims")]
        public IActionResult GetRadarSystemDimOptions()
        {
            return Ok(new[]
            {
                new { code = "reputation",      label = "信誉分",       description = "打手信誉评分",           available = true  },
                new { code = "experience",      label = "经验",         description = "该游戏累计接单数",       available = true  },
                new { code = "completionRate",  label = "完成率",       description = "已完单 / 总接单",        available = true  },
                new { code = "repeatRate",      label = "复购率",       description = "回头客 / 总客户",        available = true  },
                new { code = "userRating",      label = "用户体验评分", description = "老板综合评价（待上线）",   available = false },
                new { code = "userSkillRating", label = "用户实力评分", description = "老板技术评价（待上线）",   available = false },
                new { code = "responseSpeed",   label = "响应速度",     description = "接单耗时（待上线）",     available = false }
            });
        }

        // ==================================================
        //  2. 字段编排
        // ==================================================

        [HttpGet("fields")]
        public async Task<IActionResult> GetFields([FromQuery] string gameCode)
        {
            if (string.IsNullOrWhiteSpace(gameCode))
                return Ok(Array.Empty<object>());

            var list = await _db.ClubGameFields
                .AsNoTracking()
                .Where(f => f.GameCode == gameCode)
                .OrderBy(f => f.SortOrder).ThenBy(f => f.Id)
                .Select(f => new
                {
                    id = f.Id,
                    gameCode = f.GameCode,
                    key = f.Key,
                    label = f.Label,
                    type = f.Type,
                    optionsJson = f.OptionsJson,
                    required = f.Required,
                    placeholder = f.Placeholder,
                    sortOrder = f.SortOrder,
                    isScoreDimension = f.IsScoreDimension,
                    scoreMapJson = f.ScoreMapJson
                })
                .ToListAsync();

            return Ok(list);
        }

        [HttpPost("field")]
        public async Task<IActionResult> SaveField([FromBody] ClubGameFieldDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.GameCode) ||
                string.IsNullOrWhiteSpace(dto.Key) ||
                string.IsNullOrWhiteSpace(dto.Label))
            {
                return BadRequest(new { message = "GameCode / Key / Label 不能为空" });
            }

            var gameExists = await _db.ClubGames
                .AsNoTracking()
                .AnyAsync(g => g.Code == dto.GameCode);
            if (!gameExists)
                return BadRequest(new { message = $"游戏代码 {dto.GameCode} 不存在" });

            ClubGameField entity;
            if (dto.Id.HasValue && dto.Id.Value > 0)
            {
                entity = await _db.ClubGameFields.FirstOrDefaultAsync(f => f.Id == dto.Id.Value);
                if (entity == null) return NotFound(new { message = "字段不存在" });
            }
            else
            {
                var dup = await _db.ClubGameFields.AnyAsync(f =>
                    f.GameCode == dto.GameCode && f.Key == dto.Key);
                if (dup)
                    return BadRequest(new { message = $"字段 Key「{dto.Key}」在该游戏下已存在" });

                entity = new ClubGameField { GameCode = dto.GameCode };
                _db.ClubGameFields.Add(entity);
            }

            entity.Key = dto.Key.Trim();
            entity.Label = dto.Label.Trim();
            entity.Type = string.IsNullOrWhiteSpace(dto.Type) ? "select" : dto.Type;
            entity.OptionsJson = dto.OptionsJson;
            entity.Required = dto.Required;
            entity.Placeholder = dto.Placeholder;
            entity.SortOrder = dto.SortOrder;
            entity.IsScoreDimension = dto.IsScoreDimension;
            entity.ScoreMapJson = dto.IsScoreDimension ? dto.ScoreMapJson : null;

            await _db.SaveChangesAsync();
            return Ok(new { message = "保存成功", id = entity.Id });
        }

        [HttpDelete("field/{id:long}")]
        public async Task<IActionResult> DeleteField(long id)
        {
            var entity = await _db.ClubGameFields.FindAsync(id);
            if (entity == null) return NotFound(new { message = "字段不存在" });

            _db.ClubGameFields.Remove(entity);
            await _db.SaveChangesAsync();
            return Ok(new { message = "删除成功" });
        }

        // ==================================================
        //  3. 打手审核
        // ==================================================

        [HttpGet("operators")]
        public async Task<IActionResult> GetOperators(
            [FromQuery] string? search,
            [FromQuery] string? status)
        {
            var profileQuery = _db.OperatorProfiles
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                profileQuery = profileQuery.Where(p =>
                    p.Nickname.Contains(s) ||
                    p.ContactValue.Contains(s) ||
                    p.UserId.ToString().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                profileQuery = profileQuery.Where(p => p.AuditStatus == status);
            }

            var profiles = await profileQuery
                .OrderByDescending(p => p.AppliedAt)
                .ToListAsync();

            var userIds = profiles.Select(p => p.UserId).ToList();

            var skills = await _db.OperatorGameSkills
                .AsNoTracking()
                .Where(s => userIds.Contains(s.UserId))
                .OrderBy(s => s.GameCode)
                .ToListAsync();

            var result = profiles.Select(p => new
            {
                userId = p.UserId,
                nickname = p.Nickname,
                contactType = p.ContactType,
                contactValue = p.ContactValue,
                onlineTime = p.OnlineTime,
                intro = p.Intro,
                auditStatus = p.AuditStatus,
                auditNote = p.AuditNote,
                reputation = p.Reputation,
                totalOrders = p.TotalOrders,
                appliedAt = p.AppliedAt,
                reviewCount = p.ReviewCount,
                avgOverall = p.AvgOverall,
                avgSkill = p.AvgSkill,
                avgAttitude = p.AvgAttitude,
                avgPunctual = p.AvgPunctual,
                uniqueCustomers = p.UniqueCustomers,
                repeatCustomers = p.RepeatCustomers,
                completedOrders = p.CompletedOrders,
                cancelledOrders = p.CancelledOrders,
                gameSkills = skills
                    .Where(s => s.UserId == p.UserId)
                    .Select(s => new
                    {
                        id = s.Id,
                        userId = s.UserId,
                        gameCode = s.GameCode,
                        gameName = s.GameName,
                        metricsJson = s.MetricsJson,
                        auditStatus = s.AuditStatus,
                        auditNote = s.AuditNote,
                        code = s.Code,
                        operatorLevel = s.OperatorLevel,
                        recheckStatus = s.RecheckStatus,
                        ordersInGame = s.OrdersInGame,
                        completedOrdersInGame = s.CompletedOrdersInGame,
                        failedOrdersInGame = s.FailedOrdersInGame
                    })
                    .ToList()
            });

            return Ok(result);
        }

        [HttpPost("operator/audit")]
        public async Task<IActionResult> AuditOperator([FromBody] OperatorAuditDto dto)
        {
            var profile = await _db.OperatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == dto.UserId);

            if (profile == null)
                return NotFound(new { message = "打手档案不存在" });

            profile.AuditStatus = dto.AuditStatus;
            profile.AuditNote = string.IsNullOrWhiteSpace(dto.AuditNote)
                ? null : dto.AuditNote.Trim();
            profile.ReviewedAt = DateTime.UtcNow;

            if (dto.GameSkills != null && dto.GameSkills.Count > 0)
            {
                var skills = await _db.OperatorGameSkills
                    .Where(s => s.UserId == dto.UserId)
                    .ToListAsync();

                foreach (var item in dto.GameSkills)
                {
                    var skill = item.Id > 0
                        ? skills.FirstOrDefault(s => s.Id == item.Id)
                        : null;

                    if (skill == null && skills.Count == 1)
                        skill = skills[0];

                    if (skill == null)
                        return BadRequest(new { message = $"找不到 id={item.Id} 的技能记录" });

                    var oldStatus = skill.AuditStatus;

                    skill.AuditStatus = item.AuditStatus;
                    skill.AuditNote = string.IsNullOrWhiteSpace(item.AuditNote)
                        ? null : item.AuditNote.Trim();
                    skill.Code = string.IsNullOrWhiteSpace(item.Code)
                        ? null : item.Code.Trim();
                    skill.OperatorLevel = item.OperatorLevel;

                    if (oldStatus != "approved"
                        && item.AuditStatus == "approved"
                        && string.IsNullOrWhiteSpace(skill.Code))
                    {
                        skill.Code = await GenerateOperatorCode(skill.GameCode);
                        skill.RecheckStatus = "none";
                        skill.LastReviewAt = DateTime.UtcNow;
                        skill.NextRecheckAt = DateTime.UtcNow.AddMonths(1);
                    }

                    if (oldStatus != item.AuditStatus)
                        skill.ReviewedAt = DateTime.UtcNow;
                }
            }

            await _db.SaveChangesAsync();
            return Ok(new { message = "审核结论已写入" });
        }

        // ==================================================
        //  内部
        // ==================================================

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

        // ==================================================
        //  DTO
        // ==================================================

        public class ClubGameDto
        {
            public string Code { get; set; } = "";
            public string Name { get; set; } = "";
            public string? Description { get; set; }
            public string? CommonRulesText { get; set; }
            public string? RadarSystemDimsJson { get; set; }   // ⭐ 新增
            public bool IsActive { get; set; } = true;
            public int SortOrder { get; set; }
        }

        public class ClubGameFieldDto
        {
            public long? Id { get; set; }
            public string GameCode { get; set; } = "";
            public string Key { get; set; } = "";
            public string Label { get; set; } = "";
            public string Type { get; set; } = "select";
            public string? OptionsJson { get; set; }
            public bool Required { get; set; } = true;
            public string? Placeholder { get; set; }
            public int SortOrder { get; set; }
            public bool IsScoreDimension { get; set; } = false;
            public string? ScoreMapJson { get; set; }
        }

        public class OperatorAuditDto
        {
            public Guid UserId { get; set; }
            public string AuditStatus { get; set; } = "pending";
            public string? AuditNote { get; set; }
            public List<SkillAuditDto> GameSkills { get; set; } = new();
        }

        public class SkillAuditDto
        {
            public long Id { get; set; }
            public string AuditStatus { get; set; } = "pending";
            public string? AuditNote { get; set; }
            public string? Code { get; set; }
            public string? OperatorLevel { get; set; }
        }
    }
}