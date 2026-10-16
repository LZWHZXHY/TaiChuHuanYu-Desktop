using Microsoft.AspNetCore.Authorization;
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
    [Authorize]
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
                    radarSystemDimsJson = g.RadarSystemDimsJson,
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
            entity.RadarSystemDimsJson = dto.RadarSystemDimsJson;

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
        //  系统评分维度清单
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
                        levelSurchargeRate = s.LevelSurchargeRate,   // ⭐ 顺便返回
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

                    // ⭐ 新增：同步价格增幅
                    skill.LevelSurchargeRate = GetLevelSurchargeRate(item.OperatorLevel);

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
        //  4. 客服管理
        // ==================================================

        /// <summary>客服列表</summary>
        [HttpGet("customer-services")]
        public async Task<IActionResult> GetCustomerServices(
            [FromQuery] string? search,
            [FromQuery] bool? isActive)
        {
            var q = _db.CustomerServices.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(c =>
                    c.Name.Contains(s) ||
                    c.Wechat.Contains(s));
            }

            if (isActive.HasValue)
                q = q.Where(c => c.IsActive == isActive.Value);

            var list = await q
                .OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt)
                .Select(c => new
                {
                    id = c.Id,
                    userId = c.UserId,
                    name = c.Name,
                    wechat = c.Wechat,
                    avatar = c.Avatar,
                    status = c.Status,
                    currentLoad = c.CurrentLoad,
                    capacity = c.Capacity,
                    isAssignable = c.IsAssignable,
                    isActive = c.IsActive,
                    auditStatus = c.AuditStatus,       // ⭐ 顺便返回
                    sortOrder = c.SortOrder,
                    createdAt = c.CreatedAt
                })
                .ToListAsync();

            return Ok(list);
        }

        /// <summary>新增 / 更新客服</summary>
        [HttpPost("customer-service")]
        public async Task<IActionResult> SaveCustomerService([FromBody] CustomerServiceDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest(new { message = "客服名称不能为空" });
            if (string.IsNullOrWhiteSpace(dto.Wechat))
                return BadRequest(new { message = "客服微信不能为空" });

            CustomerService entity;

            if (dto.Id.HasValue && dto.Id.Value != Guid.Empty)
            {
                entity = await _db.CustomerServices
                    .FirstOrDefaultAsync(c => c.Id == dto.Id.Value);
                if (entity == null) return NotFound(new { message = "客服不存在" });
            }
            else
            {
                if (!dto.UserId.HasValue || dto.UserId.Value == Guid.Empty)
                    return BadRequest(new { message = "新增客服必须指定关联用户 UserId" });

                var existsUser = await _db.CustomerServices
                    .AnyAsync(c => c.UserId == dto.UserId.Value);
                if (existsUser)
                    return BadRequest(new { message = "该用户已绑定客服身份" });

                // 打手 ↔ 客服互斥
                var isOperator = await _db.OperatorProfiles
                    .AnyAsync(p => p.UserId == dto.UserId.Value);
                if (isOperator)
                    return BadRequest(new { message = "该用户已是打手，无法同时成为客服" });

                entity = new CustomerService
                {
                    Id = Guid.NewGuid(),
                    UserId = dto.UserId.Value,
                    CreatedAt = DateTime.UtcNow,
                    Status = "offline",
                    CurrentLoad = 0
                };
                _db.CustomerServices.Add(entity);
            }

            entity.Name = dto.Name.Trim();
            entity.Wechat = dto.Wechat.Trim();
            entity.Avatar = dto.Avatar;
            entity.Capacity = dto.Capacity > 0 ? dto.Capacity : 10;
            entity.IsAssignable = dto.IsAssignable;
            entity.IsActive = dto.IsActive;
            entity.SortOrder = dto.SortOrder;

            await _db.SaveChangesAsync();
            return Ok(new { message = "保存成功", id = entity.Id });
        }

        /// <summary>删除客服（软删除）</summary>
        [HttpDelete("customer-service/{id:guid}")]
        public async Task<IActionResult> DeleteCustomerService(Guid id)
        {
            var entity = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.Id == id);
            if (entity == null) return NotFound(new { message = "客服不存在" });

            entity.IsActive = false;
            entity.IsAssignable = false;
            await _db.SaveChangesAsync();

            return Ok(new { message = "已停用" });
        }

        /// <summary>切换是否参与自动分配</summary>
        [HttpPatch("customer-service/{id:guid}/toggle-assignable")]
        public async Task<IActionResult> ToggleCustomerServiceAssignable(Guid id)
        {
            var entity = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.Id == id);
            if (entity == null) return NotFound(new { message = "客服不存在" });

            entity.IsAssignable = !entity.IsAssignable;
            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = entity.IsAssignable ? "已开启自动分配" : "已关闭自动分配",
                isAssignable = entity.IsAssignable
            });
        }

        /// <summary>手动修改客服在线状态</summary>
        [HttpPatch("customer-service/{id:guid}/status")]
        public async Task<IActionResult> UpdateCustomerServiceStatus(
            Guid id,
            [FromBody] CsStatusDto dto)
        {
            var valid = new[] { "online", "busy", "offline" };
            if (string.IsNullOrWhiteSpace(dto?.Status) || !valid.Contains(dto.Status))
                return BadRequest(new { message = "状态无效，仅支持 online / busy / offline" });

            var entity = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.Id == id);
            if (entity == null) return NotFound(new { message = "客服不存在" });

            entity.Status = dto.Status;
            await _db.SaveChangesAsync();

            return Ok(new { message = "状态已更新", status = entity.Status });
        }

        // ==================================================
        //  5. 客服申请审核
        // ==================================================

        /// <summary>客服申请列表</summary>
        [HttpGet("cs-applications")]
        public async Task<IActionResult> GetCsApplications(
            [FromQuery] string status = "pending",
            [FromQuery] string? search = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var q = _db.CustomerServices.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(status) && status != "all")
            {
                q = q.Where(c => c.AuditStatus == status);
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                q = q.Where(c =>
                    c.Name.Contains(s) ||
                    c.Wechat.Contains(s) ||
                    c.UserId.ToString().Contains(s));
            }

            var total = await q.CountAsync();

            var raw = await q
                .OrderByDescending(c => c.AppliedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var userIds = raw.Select(c => c.UserId).Distinct().ToList();
            var users = await _db.Users.AsNoTracking()
                .Where(u => userIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id);

            var items = raw.Select(c =>
            {
                users.TryGetValue(c.UserId, out var u);
                return new
                {
                    id = c.Id,
                    userId = c.UserId,
                    username = u?.Username ?? "",
                    email = u?.Email ?? "",

                    name = c.Name,
                    wechat = c.Wechat,
                    avatar = c.Avatar,

                    onlineTime = c.OnlineTime,
                    intro = c.Intro,
                    capacity = c.Capacity,

                    status = c.Status,
                    currentLoad = c.CurrentLoad,
                    isAssignable = c.IsAssignable,
                    isActive = c.IsActive,

                    auditStatus = c.AuditStatus,
                    auditNote = c.AuditNote,
                    appliedAt = c.AppliedAt,
                    reviewedAt = c.ReviewedAt,

                    reviewCount = c.ReviewCount,
                    avgOverall = Math.Round(c.AvgOverall, 2),
                    createdAt = c.CreatedAt
                };
            }).ToList();

            return Ok(new { total, page, pageSize, status, items });
        }

        /// <summary>审核通过</summary>
        [HttpPost("cs-application/{id:guid}/approve")]
        public async Task<IActionResult> ApproveCsApplication(Guid id, [FromBody] CsApproveDto? dto)
        {
            var cs = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.Id == id);
            if (cs == null) return NotFound(new { message = "客服申请不存在" });

            if (cs.AuditStatus == "approved")
                return BadRequest(new { message = "该申请已通过" });
            if (cs.AuditStatus == "banned")
                return BadRequest(new { message = "该账号已被封禁" });

            var isOperator = await _db.OperatorProfiles
                .AnyAsync(p => p.UserId == cs.UserId);
            if (isOperator)
                return BadRequest(new { message = "该用户已是打手，无法通过客服审核" });

            cs.AuditStatus = "approved";
            cs.AuditNote = string.IsNullOrWhiteSpace(dto?.Note) ? null : dto.Note.Trim();
            cs.ReviewedAt = DateTime.UtcNow;

            cs.IsActive = true;
            cs.IsAssignable = true;

            if (dto?.Capacity != null && dto.Capacity > 0)
                cs.Capacity = dto.Capacity.Value;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "已通过审核",
                id = cs.Id,
                name = cs.Name,
                capacity = cs.Capacity
            });
        }

        /// <summary>审核拒绝</summary>
        [HttpPost("cs-application/{id:guid}/reject")]
        public async Task<IActionResult> RejectCsApplication(Guid id, [FromBody] CsRejectDto dto)
        {
            var cs = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.Id == id);
            if (cs == null) return NotFound(new { message = "客服申请不存在" });

            if (cs.AuditStatus == "approved")
                return BadRequest(new { message = "该申请已通过，无法再拒绝" });

            if (string.IsNullOrWhiteSpace(dto?.Reason))
                return BadRequest(new { message = "请填写拒绝原因" });

            cs.AuditStatus = "rejected";
            cs.AuditNote = dto.Reason.Trim();
            cs.ReviewedAt = DateTime.UtcNow;

            cs.IsActive = false;
            cs.IsAssignable = false;

            await _db.SaveChangesAsync();

            return Ok(new { message = "已拒绝", reason = cs.AuditNote });
        }

        /// <summary>封禁客服</summary>
        [HttpPost("cs-application/{id:guid}/ban")]
        public async Task<IActionResult> BanCsApplication(Guid id, [FromBody] CsRejectDto dto)
        {
            var cs = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.Id == id);
            if (cs == null) return NotFound(new { message = "客服不存在" });

            cs.AuditStatus = "banned";
            cs.AuditNote = string.IsNullOrWhiteSpace(dto?.Reason) ? "违规封禁" : dto.Reason.Trim();
            cs.ReviewedAt = DateTime.UtcNow;

            cs.IsActive = false;
            cs.IsAssignable = false;
            cs.Status = "offline";

            await _db.SaveChangesAsync();

            return Ok(new { message = "已封禁" });
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

        /// <summary>⭐ 等级 → 价格增幅率</summary>
        private static decimal GetLevelSurchargeRate(string? level) => level switch
        {
            "L1" => 0.10m,
            "L2" => 0.20m,
            "L3" => 0.30m,
            "L4" => 0.40m,
            "L5" => 0.50m,
            _ => 0.10m
        };

        // ==================================================
        //  DTO
        // ==================================================

        public class ClubGameDto
        {
            public string Code { get; set; } = "";
            public string Name { get; set; } = "";
            public string? Description { get; set; }
            public string? CommonRulesText { get; set; }
            public string? RadarSystemDimsJson { get; set; }
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

        // ===== 客服相关 DTO =====
        public class CustomerServiceDto
        {
            public Guid? Id { get; set; }
            public Guid? UserId { get; set; }
            public string Name { get; set; } = "";
            public string Wechat { get; set; } = "";
            public string? Avatar { get; set; }
            public int Capacity { get; set; } = 10;
            public bool IsAssignable { get; set; } = true;
            public bool IsActive { get; set; } = true;
            public int SortOrder { get; set; } = 0;
        }

        public class CsStatusDto
        {
            public string Status { get; set; } = "";
        }

        // ===== 客服申请审核 DTO =====
        public class CsApproveDto
        {
            public string? Note { get; set; }
            public int? Capacity { get; set; }
        }

        public class CsRejectDto
        {
            public string? Reason { get; set; }
        }
    }
}