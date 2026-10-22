using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Chai.Battle;
using TaiChuWeb_V2.Models.ChaiCommunity.Battle;
using TaiChuWeb_V2.Models.ChaiCommunity;

namespace TaiChuWeb_V2.Controllers.ChaiCommunity
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BattleController : ControllerBase
    {
        private readonly AppDbContext _context;

        public BattleController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<BattleListResponse>> GetList(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            [FromQuery] string? status = null,
            [FromQuery] string? keyword = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 12;

            var query = _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .AsQueryable();

            query = query.Where(b => b.Status != "cancelled");

            if (!string.IsNullOrEmpty(status))
                query = query.Where(b => b.Status == status);

            if (!string.IsNullOrEmpty(keyword))
                query = query.Where(b => b.Title.Contains(keyword) ||
                                         (b.Content != null && b.Content.Contains(keyword)));

            var total = await query.CountAsync();

            var battles = await query.ToListAsync();

            var sortedBattles = battles
                .OrderBy(b => GetStatusWeight(b.Status))
                .ThenByDescending(b => b.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = sortedBattles.Select(b => MapToDto(b)).ToList();

            return Ok(new BattleListResponse
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            });
        }


        // ============================================================
        // 拒绝约战（仅被指定的对手）
        // ============================================================
        [HttpPost("{id}/reject")]
        public async Task<ActionResult<BattleDto>> Reject(string id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var battle = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (battle == null)
                return NotFound(new { message = "约战不存在" });

            if (battle.Status != "open")
                return BadRequest(new { message = "约战已结束或已取消" });

            // ⭐ 检查是否是指定对手
            if (string.IsNullOrEmpty(battle.OpponentOcIds))
                return BadRequest(new { message = "此约战为公开约战，无需拒绝" });

            try
            {
                var opponentDict = JsonSerializer.Deserialize<Dictionary<string, List<Guid>>>(battle.OpponentOcIds);
                if (opponentDict == null || !opponentDict.ContainsKey(userId.ToString()))
                    return BadRequest(new { message = "你并非此约战的指定对手" });
            }
            catch
            {
                return BadRequest(new { message = "约战配置异常，请联系管理员" });
            }

            // ⭐ 清除指定对手信息 → 变为公开约战
            battle.OpponentOcIds = null;
            battle.IsPublic = true;

            await _context.SaveChangesAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            return Ok(MapToDto(updated!));
        }





        // ============================================================
        // 2. 获取约战详情
        // ============================================================
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<BattleDto>> GetDetail(string id)
        {
            var battle = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                    .ThenInclude(s => s.Participant)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (battle == null)
                return NotFound(new { message = "约战不存在" });

            return Ok(MapToDto(battle));
        }

        [HttpPost]
        public async Task<ActionResult<BattleDto>> Create([FromBody] CreateBattleRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var username = User.FindFirstValue(ClaimTypes.Name) ?? "未知用户";

            // ===== 1. 验证发起者OC列表 =====
            if (request.ChallengerOcIds == null || !request.ChallengerOcIds.Any())
                return BadRequest(new { message = "请至少选择一个OC" });

            var ocIds = request.ChallengerOcIds.Distinct().ToList();
            var ocs = await _context.StickmanCharacters
                .Where(c => ocIds.Contains(c.Id) && c.AuthorId == userId)
                .ToListAsync();

            if (ocs.Count != ocIds.Count)
                return BadRequest(new { message = "部分OC不存在或不属于你" });

            var existingBattles = await _context.Battles
                .Where(b => b.Status != "finished" && b.Status != "cancelled")
                .Include(b => b.Participants)
                .ToListAsync();

            foreach (var oc in ocs)
            {
                if (oc.Status != "published")
                    return BadRequest(new { message = $"OC「{oc.Title}」未发布" });
                if (!oc.IsBattleEnabled)
                    return BadRequest(new { message = $"OC「{oc.Title}」不允许参与约战" });

                var ocIdJson = $"\"{oc.Id}\"";
                if (existingBattles.Any(b => b.Participants.Any(p => p.OcIdsJson.Contains(ocIdJson))))
                {
                    return BadRequest(new { message = $"OC「{oc.Title}」已在其他进行中的约战中" });
                }
            }

            // ===== 2. 验证对手OC（但不要添加为参与者） =====
            List<Guid>? opponentOcIds = null;
            Guid? opponentUserId = null;
            if (request.OpponentOcIds != null && request.OpponentOcIds.Any())
            {
                var first = request.OpponentOcIds.First();
                opponentUserId = first.Key;
                opponentOcIds = first.Value;

                if (opponentOcIds == null || !opponentOcIds.Any())
                    return BadRequest(new { message = "对手OC列表不能为空" });

                var oppOcs = await _context.StickmanCharacters
                    .Where(c => opponentOcIds.Contains(c.Id) && c.AuthorId == opponentUserId)
                    .ToListAsync();

                if (oppOcs.Count != opponentOcIds.Count)
                    return BadRequest(new { message = "部分对手OC不存在或不属于指定用户" });

                foreach (var oc in oppOcs)
                {
                    if (oc.Status != "published")
                        return BadRequest(new { message = $"对手OC「{oc.Title}」未发布" });
                    if (!oc.IsBattleEnabled)
                        return BadRequest(new { message = $"对手OC「{oc.Title}」不允许参与约战" });

                    var ocIdJson = $"\"{oc.Id}\"";
                    if (existingBattles.Any(b => b.Participants.Any(p => p.OcIdsJson.Contains(ocIdJson))))
                    {
                        return BadRequest(new { message = $"对手OC「{oc.Title}」已在其他进行中的约战中" });
                    }
                }
            }

            // ===== 3. 创建约战 =====
            var battle = new Battle
            {
                Id = Guid.NewGuid().ToString(),
                Title = request.Title,
                Content = request.Content,
                CoverUrl = request.CoverUrl,
                BattleType = request.BattleType,
                Rules = request.Rules,
                JudgmentType = request.JudgmentType ?? "vote",
                BattleConfigJson = "{}",
                IsPublic = request.OpponentOcIds == null || !request.OpponentOcIds.Any(),
                OpponentOcIds = request.OpponentOcIds != null && request.OpponentOcIds.Any()
                    ? JsonSerializer.Serialize(request.OpponentOcIds)
                    : null,
                Status = "open",
                CreatedAt = DateTime.UtcNow
            };

            // ===== 4. 安全序列化辅助函数 =====
            string SafeSerialize<T>(T obj)
            {
                if (obj == null) return "[]";
                try
                {
                    var json = JsonSerializer.Serialize(obj);
                    return string.IsNullOrEmpty(json) ? "[]" : json;
                }
                catch
                {
                    return "[]";
                }
            }

            // ===== 5. 添加发起者参与者（只有发起者） =====
            var ocNames = ocs.Select(o => o.Title).ToList();
            var initiatorParticipant = new BattleParticipant
            {
                Id = Guid.NewGuid().ToString(),
                BattleId = battle.Id,
                UserId = userId,
                UserName = username,
                OcIdsJson = SafeSerialize(ocIds),
                OcNamesJson = SafeSerialize(ocNames),
                Status = "registered",
                JoinedAt = DateTime.UtcNow
            };
            battle.Participants.Add(initiatorParticipant);

            // ⭐ 不再自动添加对手参与者

            _context.Battles.Add(battle);
            await _context.SaveChangesAsync();

            var created = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == battle.Id);

            return Ok(MapToDto(created!));
        }

        // ============================================================
        // 4. 报名参加约战（公开约战专用）
        // ============================================================
        [HttpPost("{id}/register")]
        public async Task<ActionResult<BattleDto>> Register(string id, [FromBody] RegisterBattleRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            if (request.OcIds == null || !request.OcIds.Any())
                return BadRequest(new { message = "请至少选择一个OC" });

            var username = User.FindFirstValue(ClaimTypes.Name) ?? "未知用户";

            var battle = await _context.Battles
                .Include(b => b.Participants)
                .ThenInclude(p => p.User)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (battle == null)
                return NotFound(new { message = "约战不存在" });

            if (battle.Status != "open")
                return BadRequest(new { message = "约战已结束或已取消" });

            if (battle.Participants.Any(p => p.UserId == userId))
                return BadRequest(new { message = "你已经报名了此约战" });

            // ⭐ 新增：指定对手校验
            if (!battle.IsPublic && !string.IsNullOrEmpty(battle.OpponentOcIds))
            {
                try
                {
                    var opponentOcIdsDict = JsonSerializer.Deserialize<Dictionary<string, List<Guid>>>(battle.OpponentOcIds);
                    if (opponentOcIdsDict != null && !opponentOcIdsDict.ContainsKey(userId.ToString()))
                    {
                        return BadRequest(new { message = "此约战已指定对手，你无权应战" });
                    }
                }
                catch
                {
                    return BadRequest(new { message = "约战配置异常，请联系管理员" });
                }
            }

            var ocIds = request.OcIds.Distinct().ToList();
            var ocs = await _context.StickmanCharacters
                .Where(c => ocIds.Contains(c.Id) && c.AuthorId == userId)
                .ToListAsync();

            if (ocs.Count != ocIds.Count)
                return BadRequest(new { message = "部分OC不存在或不属于你" });

            foreach (var oc in ocs)
            {
                if (oc.Status != "published")
                    return BadRequest(new { message = $"OC「{oc.Title}」未发布" });
                if (!oc.IsBattleEnabled)
                    return BadRequest(new { message = $"OC「{oc.Title}」不允许参与约战" });
            }

            var participant = new BattleParticipant
            {
                Id = Guid.NewGuid().ToString(),
                BattleId = battle.Id,
                UserId = userId,
                UserName = username,
                OcIdsJson = JsonSerializer.Serialize(ocIds),
                OcNamesJson = JsonSerializer.Serialize(ocs.Select(o => o.Title).ToList()),
                Status = "registered",
                JoinedAt = DateTime.UtcNow
            };

            battle.Participants.Add(participant);
            await _context.SaveChangesAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            return Ok(MapToDto(updated!));
        }

        // ============================================================
        // 5. 取消报名
        // ============================================================
        [HttpPost("{id}/unregister")]
        public async Task<ActionResult<BattleDto>> Unregister(string id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var battle = await _context.Battles
                .Include(b => b.Participants)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (battle == null)
                return NotFound(new { message = "约战不存在" });

            if (battle.Status != "open")
                return BadRequest(new { message = "约战已结束或已取消" });

            var participant = battle.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant == null)
                return BadRequest(new { message = "你尚未报名此约战" });

            battle.Participants.Remove(participant);
            await _context.SaveChangesAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            return Ok(MapToDto(updated!));
        }

        // ============================================================
        // 6. 结束报名 → 进入创作期
        // ============================================================
        [HttpPost("{id}/close-registration")]
        public async Task<ActionResult<BattleDto>> CloseRegistration(string id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var battle = await _context.Battles
                .Include(b => b.Participants)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (battle == null)
                return NotFound(new { message = "约战不存在" });

            var initiator = battle.Participants.OrderBy(p => p.JoinedAt).FirstOrDefault();
            if (initiator == null || initiator.UserId != userId)
                return Forbid();

            if (battle.Status != "open")
                return BadRequest(new { message = "约战状态不正确" });

            if (battle.Participants.Count < 2)
                return BadRequest(new { message = "至少需要2名参与者才能结束报名" });

            battle.Status = "ongoing";
            battle.RegistrationDeadline = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            return Ok(MapToDto(updated!));
        }

        // ============================================================
        // 7. 结束创作 → 进入判定期
        // ============================================================
        [HttpPost("{id}/close-creation")]
        public async Task<ActionResult<BattleDto>> CloseCreation(string id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var battle = await _context.Battles
                .Include(b => b.Participants)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (battle == null)
                return NotFound(new { message = "约战不存在" });

            var initiator = battle.Participants.OrderBy(p => p.JoinedAt).FirstOrDefault();
            if (initiator == null || initiator.UserId != userId)
                return Forbid();

            if (battle.Status != "ongoing")
                return BadRequest(new { message = "约战状态不正确" });

            battle.Status = "judging";
            battle.SubmissionDeadline = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            return Ok(MapToDto(updated!));
        }

        // ============================================================
        // 8. 提交作品
        // ============================================================
        [HttpPost("{id}/submit")]
        public async Task<ActionResult<BattleDto>> SubmitWork(string id, [FromBody] SubmitWorkRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var battle = await _context.Battles
                .Include(b => b.Submissions)
                .Include(b => b.Participants)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (battle == null)
                return NotFound(new { message = "约战不存在" });

            if (battle.Status != "ongoing")
                return BadRequest(new { message = "当前不是创作期" });

            var participant = battle.Participants.FirstOrDefault(p => p.UserId == userId);
            if (participant == null)
                return BadRequest(new { message = "你尚未报名此约战" });

            if (battle.Submissions.Any(s => s.ParticipantId == participant.Id))
                return BadRequest(new { message = "你已提交作品" });

            var submission = new BattleSubmission
            {
                Id = Guid.NewGuid().ToString(),
                BattleId = battle.Id,
                ParticipantId = participant.Id,
                Title = request.Title,
                Description = request.Description,
                ContentUrl = request.ContentUrl,
                ContentType = request.ContentType,
                CreatedAt = DateTime.UtcNow
            };

            battle.Submissions.Add(submission);
            participant.Status = "submitted";
            participant.SubmittedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                    .ThenInclude(s => s.Participant)
                .FirstOrDefaultAsync(b => b.Id == id);

            return Ok(MapToDto(updated!));
        }

        // ============================================================
        // 9. 录入内定结果（仅发起者）
        // ============================================================
        [HttpPost("{id}/set-internal-result")]
        public async Task<ActionResult<BattleDto>> SetInternalResult(string id, [FromBody] InternalResultRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var battle = await _context.Battles
                .Include(b => b.Participants)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (battle == null)
                return NotFound(new { message = "约战不存在" });

            var initiator = battle.Participants.OrderBy(p => p.JoinedAt).FirstOrDefault();
            if (initiator == null || initiator.UserId != userId)
                return Forbid();

            if (battle.Status != "judging")
                return BadRequest(new { message = "当前不是判定期" });

            if (battle.JudgmentType != "internal")
                return BadRequest(new { message = "此约战不是内定制" });

            foreach (var p in battle.Participants)
            {
                if (request.WinnerIds.Contains(p.Id))
                    p.Result = "win";
                else
                    p.Result = "lose";
            }

            if (request.WinnerIds.Contains("draw"))
            {
                foreach (var p in battle.Participants)
                    p.Result = "draw";
                battle.Result = "draw";
            }
            else
            {
                battle.Result = "completed";
            }

            battle.ResultDescription = request.ResultDescription;

            await _context.SaveChangesAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            return Ok(MapToDto(updated!));
        }

        // ============================================================
        // 10. 发布结果 → 完成约战（仅发起者）
        // ============================================================
        [HttpPost("{id}/publish-result")]
        public async Task<ActionResult<BattleDto>> PublishResult(string id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var battle = await _context.Battles
                .Include(b => b.Participants)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (battle == null)
                return NotFound(new { message = "约战不存在" });

            var initiator = battle.Participants.OrderBy(p => p.JoinedAt).FirstOrDefault();
            if (initiator == null || initiator.UserId != userId)
                return Forbid();

            if (battle.Status != "judging")
                return BadRequest(new { message = "当前不是判定期" });

            if (battle.Result != "draw" && !battle.Participants.Any(p => p.Result == "win"))
                return BadRequest(new { message = "请先录入结果" });

            // ⭐⭐⭐ 新增：更新 OC 战绩 ⭐⭐⭐
            foreach (var participant in battle.Participants)
            {
                if (string.IsNullOrEmpty(participant.Result) || string.IsNullOrEmpty(participant.OcIdsJson))
                    continue;

                try
                {
                    var ocIds = JsonSerializer.Deserialize<List<Guid>>(participant.OcIdsJson);
                    if (ocIds == null || !ocIds.Any())
                        continue;

                    string result = participant.Result;

                    foreach (var ocId in ocIds)
                    {
                        var oc = await _context.StickmanCharacters.FindAsync(ocId);
                        if (oc == null) continue;

                        if (result == "win")
                            oc.BattleWins++;
                        else if (result == "lose")
                            oc.BattleLosses++;
                        else if (result == "draw")
                            oc.BattleDraws++;
                    }
                }
                catch (Exception ex)
                {
                    // 记录日志，继续处理下一个参与者
                    Console.WriteLine($"更新OC战绩失败: {ex.Message}");
                }
            }

            battle.Status = "finished";
            battle.FinishedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            return Ok(MapToDto(updated!));
        }

        // ============================================================
        // 11. 取消约战（仅发起者）
        // ============================================================
        [HttpPost("{id}/cancel")]
        public async Task<ActionResult<BattleDto>> Cancel(string id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var battle = await _context.Battles
                .Include(b => b.Participants)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (battle == null)
                return NotFound(new { message = "约战不存在" });

            var initiator = battle.Participants.OrderBy(p => p.JoinedAt).FirstOrDefault();
            if (initiator == null || initiator.UserId != userId)
                return Forbid();

            if (battle.Status == "finished" || battle.Status == "cancelled")
                return BadRequest(new { message = "约战已结束或已取消" });

            battle.Status = "cancelled";
            await _context.SaveChangesAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            return Ok(MapToDto(updated!));
        }

        // ============================================================
        // 12. 获取我的约战
        // ============================================================
        [HttpGet("my")]
        public async Task<ActionResult<List<BattleDto>>> GetMyBattles()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var battles = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                    .ThenInclude(s => s.Participant)
                .Where(b => b.Participants.Any(p => p.UserId == userId))
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return Ok(battles.Select(MapToDto).ToList());
        }

        // ============================================================
        // 辅助方法
        // ============================================================

        private int GetStatusWeight(string status)
        {
            return status switch
            {
                "open" => 1,
                "ongoing" => 2,
                "judging" => 3,
                "finished" => 4,
                "cancelled" => 5,
                _ => 99
            };
        }

        private BattleDto MapToDto(Battle battle)
        {
            // ⭐ 按 JoinedAt 排序，确保发起者在第一个
            var sortedParticipants = battle.Participants?.OrderBy(p => p.JoinedAt).ToList() ?? new List<BattleParticipant>();
            var initiator = sortedParticipants.FirstOrDefault();

            return new BattleDto
            {
                Id = battle.Id,
                Title = battle.Title,
                Description = battle.Content,
                CoverUrl = battle.CoverUrl,
                BattleType = battle.BattleType ?? "自定义",
                Rules = battle.Rules ?? string.Empty,
                JudgmentType = battle.JudgmentType,
                Status = battle.Status,
                Result = battle.Result,
                ResultDescription = battle.ResultDescription,
                CreatedAt = battle.CreatedAt,
                RegistrationDeadline = battle.RegistrationDeadline,
                SubmissionDeadline = battle.SubmissionDeadline,
                FinishedAt = battle.FinishedAt,
                SurveyId = battle.SurveyId,
                BattleConfigJson = battle.BattleConfigJson,
                IsPublic = battle.IsPublic,
                OpponentOcIds = !string.IsNullOrEmpty(battle.OpponentOcIds)
                    ? JsonSerializer.Deserialize<Dictionary<string, List<Guid>>>(battle.OpponentOcIds)
                    : null,
                // ⭐ 新增：明确标识发起者
                InitiatorId = initiator?.UserId ?? Guid.Empty,
                InitiatorName = initiator?.UserName ?? "未知",
                Participants = sortedParticipants.Select(p => new BattleParticipantDto
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    UserName = p.UserName,
                    OcIds = JsonSerializer.Deserialize<List<Guid>>(p.OcIdsJson) ?? new List<Guid>(),
                    OcNames = JsonSerializer.Deserialize<List<string>>(p.OcNamesJson) ?? new List<string>(),
                    TeamName = p.TeamName,
                    TeamNumber = p.TeamNumber,
                    Status = p.Status,
                    Result = p.Result,
                    JoinedAt = p.JoinedAt,
                    SubmittedAt = p.SubmittedAt
                }).ToList(),
                Submissions = battle.Submissions?.Select(s => new BattleSubmissionDto
                {
                    Id = s.Id,
                    ParticipantId = s.ParticipantId,
                    Title = s.Title,
                    Description = s.Description,
                    ContentUrl = s.ContentUrl,
                    ContentType = s.ContentType,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt,
                    Participant = s.Participant != null ? new BattleParticipantDto
                    {
                        Id = s.Participant.Id,
                        UserId = s.Participant.UserId,
                        UserName = s.Participant.UserName,
                        OcIds = JsonSerializer.Deserialize<List<Guid>>(s.Participant.OcIdsJson) ?? new List<Guid>(),
                        OcNames = JsonSerializer.Deserialize<List<string>>(s.Participant.OcNamesJson) ?? new List<string>(),
                        TeamName = s.Participant.TeamName,
                        TeamNumber = s.Participant.TeamNumber,
                        Status = s.Participant.Status,
                        Result = s.Participant.Result,
                        JoinedAt = s.Participant.JoinedAt,
                        SubmittedAt = s.Participant.SubmittedAt
                    } : null
                }).ToList() ?? new List<BattleSubmissionDto>(),
                ParticipantCount = battle.Participants?.Count ?? 0,
                SubmissionCount = battle.Submissions?.Count ?? 0
            };
        }
    }
}