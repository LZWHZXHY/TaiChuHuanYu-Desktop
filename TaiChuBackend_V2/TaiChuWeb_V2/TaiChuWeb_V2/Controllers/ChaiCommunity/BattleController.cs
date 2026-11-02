using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Chai.Battle;
using TaiChuWeb_V2.Models.ChaiCommunity.Battle;
using TaiChuWeb_V2.Models.ChaiCommunity;
using TaiChuWeb_V2.Services.Notify;
using TaiChuWeb_V2.Services.ChaiCommunity;
using TaiChuWeb_V2.Models.User;
// 问卷系统的表（投票制约战用）。用别名：Controllers 下有个 Survey 命名空间，直接写 Survey 会被当成命名空间
using SurveyModel = TaiChuWeb_V2.Models.Survey.Survey;
using SurveyQuestionModel = TaiChuWeb_V2.Models.Survey.Question;
using SurveyOptionModel = TaiChuWeb_V2.Models.Survey.QuestionOption;
using SurveySubmissionModel = TaiChuWeb_V2.Models.Survey.SurveySubmission;
using SurveyAnswerModel = TaiChuWeb_V2.Models.Survey.Answer;

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

        // ===== 约战通知（以前整个约战流程一条通知都没有：被指定的对手根本不知道有人约战自己） =====
        private void Notify(Guid to, Guid? from, Battle battle, string content)
            => new NotificationService(_context).Add(to, from, "battle", "battle", battle.Id, content, $"/battles/{battle.Id}");
        private void NotifyParticipants(Battle battle, Guid? from, Func<BattleParticipant, string> content)
        {
            foreach (var p in battle.Participants) Notify(p.UserId, from, battle, content(p));
        }
        /// <summary>作品链接只收 http(s)，防止 javascript: 之类的链接被别人点到</summary>
        private static bool IsWebLink(string? url)
            => Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

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
            await ApplyReviewAsync(items);

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

            if (battle.Participants.Any(p => p.UserId == userId))
                return BadRequest(new { message = "已报名，请使用取消报名" });

            try
            {
                var opponentDict = JsonSerializer.Deserialize<Dictionary<Guid, List<Guid>>>(battle.OpponentOcIds);
                if (opponentDict == null || !opponentDict.ContainsKey(userId))
                    return BadRequest(new { message = "你并非此约战的指定对手" });
                opponentDict.Remove(userId);
                battle.IsPublic = opponentDict.Count == 0;
                battle.OpponentOcIds = battle.IsPublic ? null : JsonSerializer.Serialize(opponentDict);
            }
            catch
            {
                return BadRequest(new { message = "约战配置异常，请联系管理员" });
            }

            // 告诉发起人（被指定的对手都拒绝后，约战会自动变成公开约战）
            var starter = battle.Participants.OrderBy(p => p.JoinedAt).FirstOrDefault();
            if (starter != null)
                Notify(starter.UserId, userId, battle, battle.IsPublic
                    ? $"拒绝了你的约战「{battle.Title}」。指定的对手都拒绝了，约战已自动改成公开约战，任何人都能报名；不想继续可以取消约战"
                    : $"拒绝了你的约战「{battle.Title}」");

            await _context.SaveChangesAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            return await ResultAsync(updated!);
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

            return await ResultAsync(battle);
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
            foreach (var invitation in request.OpponentOcIds ?? new())
            {
                var opponentUserId = invitation.Key;
                var opponentOcIds = invitation.Value?.Distinct().ToList();
                if (opponentUserId == userId)
                    return BadRequest(new { message = "不能邀请自己，请在发起者栏选择自己的OC" });

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

            Dictionary<Guid, int> teams;
            System.Text.Json.Nodes.JsonObject configuration;
            try
            {
                using var config = JsonDocument.Parse(request.BattleConfigJson ?? "{}");
                teams = config.RootElement.TryGetProperty("teams", out var value)
                    ? value.Deserialize<Dictionary<Guid, int>>() ?? new() : new();
                if (teams.Any(t => t.Value is not (1 or 2) ||
                    (t.Key != userId && !(request.OpponentOcIds?.ContainsKey(t.Key) ?? false))))
                    return BadRequest(new { message = "队伍配置无效" });
                teams[userId] = 1;
                configuration = System.Text.Json.Nodes.JsonNode.Parse(request.BattleConfigJson ?? "{}")!.AsObject();
                configuration["teams"] = JsonSerializer.SerializeToNode(teams);
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            {
                return BadRequest(new { message = "队伍配置无效" });
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
                BattleConfigJson = configuration.ToJsonString(),
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
                TeamNumber = 1,
                OcIdsJson = SafeSerialize(ocIds),
                OcNamesJson = SafeSerialize(ocNames),
                Status = "registered",
                JoinedAt = DateTime.UtcNow
            };
            battle.Participants.Add(initiatorParticipant);

            // ⭐ 不再自动添加对手参与者

            _context.Battles.Add(battle);
            // 告诉每位被指定的对手：有人约战你了（点开可以应战或拒绝）
            foreach (var invited in request.OpponentOcIds?.Keys ?? Enumerable.Empty<Guid>())
                Notify(invited, userId, battle, $"向你发起了约战「{battle.Title}」，点开可以应战或拒绝");
            await _context.SaveChangesAsync();

            var created = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == battle.Id);

            return await ResultAsync(created!);
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
            if (!battle.IsPublic)
            {
                try
                {
                    var opponentOcIdsDict = JsonSerializer.Deserialize<Dictionary<Guid, List<Guid>>>(battle.OpponentOcIds ?? "null");
                    if (opponentOcIdsDict == null || !opponentOcIdsDict.ContainsKey(userId))
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

            var activeBattles = await _context.Battles
                .Where(b => b.Id != id && b.Status != "finished" && b.Status != "cancelled")
                .Include(b => b.Participants).ToListAsync();
            if (ocIds.Any(oc => activeBattles.Any(b => b.Participants.Any(p =>
                p.OcIdsJson.Contains($"\"{oc}\"", StringComparison.OrdinalIgnoreCase)))))
                return BadRequest(new { message = "所选OC已在其他进行中的约战中" });

            var teamNumber = 2;
            try
            {
                using var config = JsonDocument.Parse(battle.BattleConfigJson ?? "{}");
                if (config.RootElement.TryGetProperty("teams", out var value))
                {
                    var teams = value.Deserialize<Dictionary<Guid, int>>();
                    if (teams?.TryGetValue(userId, out var team) == true && team is 1 or 2) teamNumber = team;
                }
            }
            catch (Exception ex) when (ex is JsonException or InvalidOperationException)
            { return BadRequest(new { message = "队伍配置异常，请联系发起者" }); }

            var participant = new BattleParticipant
            {
                Id = Guid.NewGuid().ToString(),
                BattleId = battle.Id,
                UserId = userId,
                TeamNumber = teamNumber,
                UserName = username,
                OcIdsJson = JsonSerializer.Serialize(ocIds),
                OcNamesJson = JsonSerializer.Serialize(ocs.Select(o => o.Title).ToList()),
                Status = "registered",
                JoinedAt = DateTime.UtcNow
            };

            battle.Participants.Add(participant);
            var host = battle.Participants.OrderBy(p => p.JoinedAt).FirstOrDefault();
            if (host != null && host.UserId != userId)
                Notify(host.UserId, userId, battle, battle.IsPublic ? $"报名了你的约战「{battle.Title}」" : $"接受了你的约战「{battle.Title}」");
            await _context.SaveChangesAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            return await ResultAsync(updated!);
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

            if (battle.Participants.OrderBy(p => p.JoinedAt).FirstOrDefault()?.UserId == userId)
                return BadRequest(new { message = "发起者不能退出报名，请使用取消约战" });

            battle.Participants.Remove(participant);
            var owner = battle.Participants.OrderBy(p => p.JoinedAt).FirstOrDefault();
            if (owner != null) Notify(owner.UserId, userId, battle, $"取消了约战「{battle.Title}」的报名");
            await _context.SaveChangesAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            return await ResultAsync(updated!);
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
            NotifyParticipants(battle, userId, _ => $"约战「{battle.Title}」报名结束，进入创作期，记得提交作品");

            await _context.SaveChangesAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            return await ResultAsync(updated!);
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
            // 投票制：进入判定期时自动建一份投票（用问卷系统的表，问卷管理里也能看到）
            if (battle.JudgmentType == "vote") EnsureVoteSurvey(battle, userId);
            NotifyParticipants(battle, userId, _ => $"约战「{battle.Title}」创作期结束，进入判定期，等发起人公布结果");

            await _context.SaveChangesAsync();
            await SaveVoteSurveyIdAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            return await ResultAsync(updated!);
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
                return BadRequest(new { message = "你已经提交过作品了，可以点「修改作品」" });
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 100)
                return BadRequest(new { message = "作品标题要填，最多 100 个字" });
            if (!IsWebLink(request.ContentUrl) || request.ContentUrl.Trim().Length > 500)
                return BadRequest(new { message = "作品链接要以 http:// 或 https:// 开头（比如 B 站视频地址）" });
            request.Title = request.Title.Trim();
            request.ContentUrl = request.ContentUrl.Trim();
            if (request.Description?.Length > 2000) request.Description = request.Description[..2000];

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
            var lead = battle.Participants.OrderBy(p => p.JoinedAt).FirstOrDefault();
            if (lead != null) Notify(lead.UserId, userId, battle, $"在约战「{battle.Title}」里提交了作品「{submission.Title}」（管理员审核通过后大家才能看到链接）");

            await _context.SaveChangesAsync();
            // 作品链接要管理员审核通过后才对别人显示
            await BattleReviewStore.SetAsync(_context, submission.Id, BattleReviewStore.Pending, null, null);

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                    .ThenInclude(s => s.Participant)
                .FirstOrDefaultAsync(b => b.Id == id);

            return await ResultAsync(updated!);
        }

        // ============================================================
        // 8.5 修改自己的作品（创作期内；以前提交后就改不了，链接填错只能干瞪眼）
        // ============================================================
        [HttpPut("{id}/submit")]
        public async Task<ActionResult<BattleDto>> UpdateWork(string id, [FromBody] SubmitWorkRequest request)
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized(new { message = "用户未登录" });
            var battle = await _context.Battles.Include(b => b.Submissions).Include(b => b.Participants).FirstOrDefaultAsync(b => b.Id == id);
            if (battle == null) return NotFound(new { message = "约战不存在" });
            var participant = battle.Participants.FirstOrDefault(p => p.UserId == userId);
            var submission = participant == null ? null : battle.Submissions.FirstOrDefault(s => s.ParticipantId == participant.Id);
            if (submission == null) return BadRequest(new { message = "你还没有提交作品" });
            // 创作期内随时能改；创作期结束后，只有被审核打回的作品还能改（改好重新审核）
            var oldReview = (await BattleReviewStore.GetAsync(_context, new[] { submission.Id })).GetValueOrDefault(submission.Id);
            if (battle.Status != "ongoing" && !(battle.Status == "judging" && oldReview?.Status == BattleReviewStore.Rejected))
                return BadRequest(new { message = "创作期已经结束，不能再改作品了" });
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Trim().Length > 100)
                return BadRequest(new { message = "作品标题要填，最多 100 个字" });
            if (!IsWebLink(request.ContentUrl) || request.ContentUrl.Trim().Length > 500)
                return BadRequest(new { message = "作品链接要以 http:// 或 https:// 开头（比如 B 站视频地址）" });
            submission.Title = request.Title.Trim();
            submission.ContentUrl = request.ContentUrl.Trim();
            submission.Description = request.Description?.Length > 2000 ? request.Description[..2000] : request.Description;
            if (request.ContentType != null) submission.ContentType = request.ContentType;
            submission.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            // 改过的作品重新审核
            await BattleReviewStore.SetAsync(_context, submission.Id, BattleReviewStore.Pending, null, null);

            var updated = await _context.Battles
                .Include(b => b.Participants).ThenInclude(p => p.User)
                .Include(b => b.Submissions).ThenInclude(s => s.Participant)
                .FirstOrDefaultAsync(b => b.Id == id);
            return await ResultAsync(updated!);
        }

        // ============================================================
        // 作品审核（管理员：超级管理员或「用户审核」权限）
        // 作品提交后是「待审核」，通过后路人才看得到链接；打回要写原因，作者改好后重新审核。
        // 作者本人和审核员任何时候都看得到链接
        // ============================================================
        private bool? _isReviewer;
        private async Task<bool> IsReviewerAsync()
        {
            if (_isReviewer.HasValue) return _isReviewer.Value;
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var uid)) return (_isReviewer = false).Value;
            var perms = await _context.UserPermissions.AsNoTracking().Where(p => p.UserId == uid).Select(p => p.Permission).ToListAsync();
            _isReviewer = perms.Contains(AdminPermission.SuperAdmin) || perms.Contains(AdminPermission.User_Audit);
            return _isReviewer.Value;
        }

        private async Task<ActionResult<BattleDto>> ResultAsync(Battle battle)
        {
            var dto = MapToDto(battle);
            await ApplyReviewAsync(new List<BattleDto> { dto });
            return Ok(dto);
        }

        /// <summary>填上每件作品的审核状态；没通过审核的作品，别人看不到链接和简介</summary>
        private async Task ApplyReviewAsync(List<BattleDto> dtos)
        {
            var reviewer = await IsReviewerAsync();
            Guid? me = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var u) ? u : null;
            var ids = dtos.SelectMany(d => d.Submissions).Select(s => s.Id).ToList();
            Dictionary<string, BattleReviewStore.Review> reviews;
            try { reviews = await BattleReviewStore.GetAsync(_context, ids); }
            catch (Exception) { reviews = new(); } // 审核表还没建好时：按待审核处理（链接只给作者和审核员看）
            foreach (var dto in dtos)
            {
                dto.CanReview = reviewer;
                foreach (var s in dto.Submissions)
                {
                    var review = reviews.GetValueOrDefault(s.Id);
                    s.ReviewStatus = review?.Status ?? BattleReviewStore.Pending;
                    var ownerId = s.Participant?.UserId ?? dto.Participants.FirstOrDefault(p => p.Id == s.ParticipantId)?.UserId;
                    var isOwner = me.HasValue && ownerId == me.Value;
                    s.ReviewNote = isOwner || reviewer ? review?.Note : null;
                    if (s.ReviewStatus != BattleReviewStore.Approved && !isOwner && !reviewer)
                    {
                        s.ContentUrl = string.Empty;
                        s.Description = null;
                    }
                }
            }
        }

        /// <summary>待审核的作品（审核员用）</summary>
        [HttpGet("reviews/pending")]
        public async Task<IActionResult> PendingReviews()
        {
            if (!await IsReviewerAsync()) return Forbid();
            var ids = await BattleReviewStore.PendingIdsAsync(_context);
            var subs = await _context.BattleSubmissions.AsNoTracking()
                .Where(s => ids.Contains(s.Id))
                .Select(s => new { s.Id, s.BattleId, s.ParticipantId, s.Title, s.Description, s.ContentUrl, s.CreatedAt, s.UpdatedAt })
                .ToListAsync();
            var battleIds = subs.Select(s => s.BattleId).Distinct().ToList();
            var battles = await _context.Battles.AsNoTracking().Where(b => battleIds.Contains(b.Id)).Select(b => new { b.Id, b.Title, b.Status }).ToDictionaryAsync(b => b.Id);
            var partIds = subs.Select(s => s.ParticipantId).Distinct().ToList();
            var parts = await _context.BattleParticipants.AsNoTracking().Where(p => partIds.Contains(p.Id)).Select(p => new { p.Id, p.UserId, p.UserName }).ToDictionaryAsync(p => p.Id);
            var items = ids.Select(id => subs.FirstOrDefault(s => s.Id == id)).Where(s => s != null).Select(s => new
            {
                id = s!.Id,
                battleId = s.BattleId,
                battleTitle = battles.GetValueOrDefault(s.BattleId)?.Title ?? "（约战已删除）",
                battleStatus = battles.GetValueOrDefault(s.BattleId)?.Status,
                userId = parts.GetValueOrDefault(s.ParticipantId)?.UserId,
                userName = parts.GetValueOrDefault(s.ParticipantId)?.UserName ?? "未知",
                title = s.Title,
                description = s.Description,
                contentUrl = s.ContentUrl,
                submittedAt = s.UpdatedAt ?? s.CreatedAt,
            }).ToList();
            return Ok(new { total = items.Count, items });
        }

        public sealed class ReviewRequest
        {
            public bool Approve { get; set; }
            /// <summary>打回原因（打回时必填）</summary>
            public string? Reason { get; set; }
        }

        /// <summary>审核一件作品：通过 / 打回（写原因），并通知作者</summary>
        [HttpPost("submissions/{submissionId}/review")]
        public async Task<IActionResult> Review(string submissionId, [FromBody] ReviewRequest request)
        {
            if (!await IsReviewerAsync()) return Forbid();
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var reviewerId)) return Unauthorized();
            var sub = await _context.BattleSubmissions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == submissionId);
            if (sub == null) return NotFound(new { message = "作品不存在" });
            if (!request.Approve && string.IsNullOrWhiteSpace(request.Reason)) return BadRequest(new { message = "打回要写原因，作者才知道怎么改" });
            var battle = await _context.Battles.Include(b => b.Participants).FirstOrDefaultAsync(b => b.Id == sub.BattleId);
            if (battle == null) return NotFound(new { message = "约战不存在" });

            await BattleReviewStore.SetAsync(_context, sub.Id, request.Approve ? BattleReviewStore.Approved : BattleReviewStore.Rejected, request.Approve ? null : request.Reason, reviewerId);
            var author = battle.Participants.FirstOrDefault(p => p.Id == sub.ParticipantId);
            if (author != null)
                Notify(author.UserId, reviewerId, battle, request.Approve
                    ? $"你在约战「{battle.Title}」提交的作品「{sub.Title}」通过审核了"
                    : $"你在约战「{battle.Title}」提交的作品「{sub.Title}」没有通过审核：{request.Reason!.Trim()}。请修改后重新提交");
            await _context.SaveChangesAsync();
            return Ok(new { message = request.Approve ? "已通过" : "已打回" });
        }

        // ============================================================
        // 投票制约战：用问卷系统（surveys / questions / question_options / survey_submissions / answers）存投票
        // 一场约战 = 一份问卷、一道单选题「你觉得谁赢了？」，选项 = 每位参赛者（团战按队伍）+「平局」。
        // 选项的 OptionValue 记对应关系：p:参赛者编号 / t:队伍号 / draw
        // 参赛者自己不能投；每人一票；判定期内可投；发起人发布结果时按票数定胜负
        // ============================================================
        private void EnsureVoteSurvey(Battle battle, Guid creatorId)
        {
            if (!string.IsNullOrEmpty(battle.SurveyId)) return;
            var sorted = battle.Participants.OrderBy(p => p.JoinedAt).ToList();
            var teams = sorted.Where(p => p.TeamNumber is 1 or 2).Select(p => p.TeamNumber!.Value).Distinct().Count() == 2
                && sorted.Count > 2;
            var question = new SurveyQuestionModel { QuestionType = 1, Title = "你觉得这场约战谁赢了？", IsRequired = true, SortOrder = 0, CreatedAt = DateTime.UtcNow };
            var order = 0;
            string OcText(BattleParticipant p) { try { var n = JsonSerializer.Deserialize<List<string>>(p.OcNamesJson); return n is { Count: > 0 } ? string.Join("、", n) : "无 OC"; } catch { return "无 OC"; } }
            if (teams)
            {
                foreach (var t in new[] { 1, 2 })
                {
                    var members = sorted.Where(p => p.TeamNumber == t).Select(p => p.UserName);
                    question.Options.Add(new SurveyOptionModel { OptionText = $"队伍 {(t == 1 ? "A" : "B")}（{string.Join("、", members)}）", OptionValue = $"t:{t}", SortOrder = order++, CreatedAt = DateTime.UtcNow });
                }
            }
            else
            {
                foreach (var p in sorted)
                    question.Options.Add(new SurveyOptionModel { OptionText = $"{p.UserName}（{OcText(p)}）", OptionValue = $"p:{p.Id}", SortOrder = order++, CreatedAt = DateTime.UtcNow });
            }
            question.Options.Add(new SurveyOptionModel { OptionText = "平局", OptionValue = "draw", SortOrder = order, CreatedAt = DateTime.UtcNow });
            var survey = new SurveyModel
            {
                Title = $"约战投票：{battle.Title}".Length > 200 ? $"约战投票：{battle.Title}"[..200] : $"约战投票：{battle.Title}",
                Description = $"柴圈约战「{battle.Title}」的判定投票，在约战页面投票。",
                StartTime = DateTime.UtcNow.AddMinutes(-1),
                EndTime = DateTime.UtcNow.AddYears(1),
                Status = 1,
                IsPublic = true,
                AllowAnonymous = false,
                MaxSubmissions = 1,
                CreatedBy = creatorId,
                CreatedAt = DateTime.UtcNow,
            };
            survey.Questions.Add(question);
            _context.Surveys.Add(survey);
            // 问卷编号要保存后才有：先记一个占位，保存后在 SaveVoteSurveyIdAsync 里补上
            _pendingSurvey = (battle, survey);
        }
        private (Battle Battle, SurveyModel Survey)? _pendingSurvey;
        /// <summary>保存后把新问卷的编号写回约战（问卷编号是自增的，保存前拿不到）</summary>
        private async Task SaveVoteSurveyIdAsync()
        {
            if (_pendingSurvey is not { } p) return;
            p.Battle.SurveyId = p.Survey.Id.ToString();
            _pendingSurvey = null;
            await _context.SaveChangesAsync();
        }

        private sealed record VoteTally(int SurveyId, int QuestionId, List<(SurveyOptionModel Option, int Count)> Options, int Total, int? MyOptionId);

        private async Task<VoteTally?> TallyAsync(Battle battle, Guid? viewer)
        {
            if (!int.TryParse(battle.SurveyId, out var sid)) return null;
            var question = await _context.Questions.AsNoTracking().Include(q => q.Options).Where(q => q.SurveyId == sid).OrderBy(q => q.SortOrder).FirstOrDefaultAsync();
            if (question == null) return null;
            var rows = await (from a in _context.Answers.AsNoTracking()
                              join s in _context.SurveySubmissions.AsNoTracking() on a.SubmissionId equals s.Id
                              where a.QuestionId == question.Id && s.IsValid
                              select new { s.UserId, a.SelectedOptionIds }).ToListAsync();
            var counts = new Dictionary<int, int>();
            int? mine = null;
            foreach (var r in rows)
            {
                List<int>? ids = null;
                try { ids = JsonSerializer.Deserialize<List<int>>(r.SelectedOptionIds ?? "[]"); } catch (JsonException) { }
                var id = ids?.FirstOrDefault() ?? 0;
                if (id == 0) continue;
                counts[id] = counts.GetValueOrDefault(id) + 1;
                if (viewer.HasValue && r.UserId == viewer) mine = id;
            }
            var options = question.Options.OrderBy(o => o.SortOrder).Select(o => (o, counts.GetValueOrDefault(o.Id))).ToList();
            return new VoteTally(sid, question.Id, options, options.Sum(o => o.Item2), mine);
        }

        /// <summary>投票情况：每个选项多少票、我投了哪个、我能不能投</summary>
        [HttpGet("{id}/votes")]
        [AllowAnonymous]
        public async Task<IActionResult> Votes(string id)
        {
            var battle = await _context.Battles.AsNoTracking().Include(b => b.Participants).FirstOrDefaultAsync(b => b.Id == id);
            if (battle == null) return NotFound(new { message = "约战不存在" });
            Guid? me = Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var u) ? u : null;
            var tally = await TallyAsync(battle, me);
            if (tally == null) return Ok(new { open = false, total = 0, options = Array.Empty<object>() });
            var isParticipant = me.HasValue && battle.Participants.Any(p => p.UserId == me.Value);
            return Ok(new
            {
                open = battle.Status == "judging",
                total = tally.Total,
                myOptionId = tally.MyOptionId,
                canVote = me.HasValue && !isParticipant && tally.MyOptionId == null && battle.Status == "judging",
                reason = !me.HasValue ? "登录后才能投票" : isParticipant ? "参赛的人不能给这场投票" : tally.MyOptionId != null ? "你已经投过了" : battle.Status != "judging" ? "投票已经结束" : null,
                options = tally.Options.Select(o => new { id = o.Option.Id, text = o.Option.OptionText, value = o.Option.OptionValue, count = o.Count }),
            });
        }

        public sealed class VoteRequest { public int OptionId { get; set; } }

        /// <summary>投一票（参赛者不能投，每人一票，判定期内）</summary>
        [HttpPost("{id}/vote")]
        public async Task<IActionResult> Vote(string id, [FromBody] VoteRequest request)
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized(new { message = "请先登录" });
            var battle = await _context.Battles.Include(b => b.Participants).FirstOrDefaultAsync(b => b.Id == id);
            if (battle == null) return NotFound(new { message = "约战不存在" });
            if (battle.Status != "judging" || battle.JudgmentType != "vote") return BadRequest(new { message = "现在不能投票" });
            if (battle.Participants.Any(p => p.UserId == userId)) return BadRequest(new { message = "参赛的人不能给这场投票" });
            var tally = await TallyAsync(battle, userId);
            if (tally == null) return BadRequest(new { message = "这场约战还没开始投票" });
            if (tally.MyOptionId != null || await _context.SurveySubmissions.AnyAsync(s => s.SurveyId == tally.SurveyId && s.UserId == userId && s.IsValid))
                return BadRequest(new { message = "你已经投过了，每人只能投一票" });
            if (tally.Options.All(o => o.Option.Id != request.OptionId)) return BadRequest(new { message = "选项不对，请刷新后再投" });

            var submission = new SurveySubmissionModel { SurveyId = tally.SurveyId, UserId = userId, SubmittedAt = DateTime.UtcNow, IsValid = true };
            submission.Answers.Add(new SurveyAnswerModel { QuestionId = tally.QuestionId, SelectedOptionIds = JsonSerializer.Serialize(new List<int> { request.OptionId }), CreatedAt = DateTime.UtcNow });
            _context.SurveySubmissions.Add(submission);
            await _context.SaveChangesAsync();
            await _context.Surveys.Where(s => s.Id == tally.SurveyId).ExecuteUpdateAsync(s => s.SetProperty(x => x.TotalSubmissions, x => x.TotalSubmissions + 1));
            return Ok(new { message = "投票成功" });
        }

        /// <summary>已经卡在判定期、还没有投票的老投票制约战：发起人手动开启投票</summary>
        [HttpPost("{id}/start-vote")]
        public async Task<IActionResult> StartVote(string id)
        {
            if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
            var battle = await _context.Battles.Include(b => b.Participants).FirstOrDefaultAsync(b => b.Id == id);
            if (battle == null) return NotFound(new { message = "约战不存在" });
            if (battle.Participants.OrderBy(p => p.JoinedAt).FirstOrDefault()?.UserId != userId) return Forbid();
            if (battle.Status != "judging" || battle.JudgmentType != "vote") return BadRequest(new { message = "只有判定期的投票制约战能开启投票" });
            if (!string.IsNullOrEmpty(battle.SurveyId)) return Ok(new { message = "投票已经开启了" });
            EnsureVoteSurvey(battle, userId);
            await _context.SaveChangesAsync();
            await SaveVoteSurveyIdAsync();
            return Ok(new { message = "投票已开启" });
        }

        // ============================================================
        // 约战结果的种类（存在 BattleConfigJson 的 "outcome" 里，不改表）：
        //   win 一方胜 / pyrrhic 惨胜（战绩同胜利）/ draw 平手 / mutual 同归于尽（战绩按平局）/ nocontest 无结果·中止（不计战绩）
        // Battle.Result 仍然只用 completed / draw / nocontest，老代码照常能读
        // ============================================================
        private const string OutcomeWin = "win", OutcomePyrrhic = "pyrrhic", OutcomeDraw = "draw", OutcomeMutual = "mutual", OutcomeNoContest = "nocontest";
        private static readonly List<string> Outcomes = new() { OutcomeWin, OutcomePyrrhic, OutcomeDraw, OutcomeMutual, OutcomeNoContest };

        /// <summary>检查录入 / 修正结果的请求；没问题返回 null，并给出结果种类（老前端不传 outcome：勾了 draw 就是平手，否则是一方胜）</summary>
        private static string? CheckResultRequest(Battle battle, InternalResultRequest request, out string outcome)
        {
            var winnerIds = request.WinnerIds ?? new List<string>();
            outcome = !string.IsNullOrEmpty(request.Outcome) && Outcomes.Contains(request.Outcome)
                ? request.Outcome
                : winnerIds.Contains("draw") ? OutcomeDraw : OutcomeWin;
            var validIds = battle.Participants.Select(p => p.Id).ToList();
            if (winnerIds.Any(w => w != "draw" && !validIds.Contains(w)))
                return "选的胜者不在这场约战里，请刷新页面后重试";
            if (outcome is OutcomeWin or OutcomePyrrhic && !winnerIds.Any(w => validIds.Contains(w)))
                return "请选出胜者，或者选「平手」「同归于尽」「无结果」";
            return null;
        }

        /// <summary>
        /// 按结果种类设置每个人的结果：
        /// · 一方胜 / 惨胜：选中的人赢，其他人输；团队战勾了队里任何一个人就算整队赢；所有人都被选成胜者（人人都赢不合理）按平手算；
        /// · 平手、同归于尽：所有人记平局；· 无结果：所有人不计战绩。
        /// </summary>
        private static void ApplyOutcome(Battle battle, string outcome, List<string> winnerIds)
        {
            if (outcome is OutcomeWin or OutcomePyrrhic)
            {
                var winners = battle.Participants.Where(p => winnerIds.Contains(p.Id)).ToList();
                var winTeams = winners.Where(p => p.TeamNumber is 1 or 2).Select(p => p.TeamNumber).ToHashSet();
                foreach (var p in battle.Participants)
                    p.Result = winners.Contains(p) || (p.TeamNumber is 1 or 2 && winTeams.Contains(p.TeamNumber)) ? "win" : "lose";
                if (battle.Participants.Count > 1 && battle.Participants.All(p => p.Result == "win")) outcome = OutcomeDraw;
            }
            if (outcome is OutcomeDraw or OutcomeMutual)
            {
                foreach (var p in battle.Participants) p.Result = "draw";
                battle.Result = "draw";
            }
            else if (outcome == OutcomeNoContest)
            {
                foreach (var p in battle.Participants) p.Result = "none";
                battle.Result = "nocontest";
            }
            else battle.Result = "completed";
            SetOutcome(battle, outcome);
        }

        private static void SetOutcome(Battle battle, string outcome)
        {
            System.Text.Json.Nodes.JsonObject config;
            try { config = System.Text.Json.Nodes.JsonNode.Parse(string.IsNullOrWhiteSpace(battle.BattleConfigJson) ? "{}" : battle.BattleConfigJson) as System.Text.Json.Nodes.JsonObject ?? new System.Text.Json.Nodes.JsonObject(); }
            catch (JsonException) { config = new System.Text.Json.Nodes.JsonObject(); }
            config["outcome"] = outcome;
            battle.BattleConfigJson = config.ToJsonString();
        }

        /// <summary>读结果种类；老约战没存 outcome 的按 Result 推断</summary>
        private static string OutcomeOf(Battle battle)
        {
            try
            {
                using var config = JsonDocument.Parse(string.IsNullOrWhiteSpace(battle.BattleConfigJson) ? "{}" : battle.BattleConfigJson);
                if (config.RootElement.ValueKind == JsonValueKind.Object && config.RootElement.TryGetProperty("outcome", out var v)
                    && v.ValueKind == JsonValueKind.String && Outcomes.Contains(v.GetString()!))
                    return v.GetString()!;
            }
            catch (JsonException) { }
            return battle.Result switch { "draw" => OutcomeDraw, "completed" => OutcomeWin, "nocontest" => OutcomeNoContest, _ => "" };
        }

        /// <summary>通知里「你怎么样了」的那句话</summary>
        private static string ResultText(Battle battle, BattleParticipant p) => OutcomeOf(battle) switch
        {
            OutcomeNoContest => "这场没有分出结果（中止），不计战绩",
            OutcomeMutual => "你和对手同归于尽了 ☠（战绩按平局算）",
            OutcomeDraw => "打平了 🤝",
            OutcomePyrrhic when p.Result == "win" => "你惨胜了 🩸",
            _ => p.Result == "win" ? "你赢了 🏆" : "你输了，下次加油",
        };

        // ============================================================
        // 9b. 管理员修正已经公布的结果
        //     （比如以前没有平局选项，两边都被勾成「胜」；或者发起人录错了）
        //     旧结果先记进 battle_result_corrections 表，OC 战绩先减旧的再加新的
        // ============================================================
        [HttpPost("{id}/correct-result")]
        public async Task<ActionResult<BattleDto>> CorrectResult(string id, [FromBody] InternalResultRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });
            if (!await IsReviewerAsync())
                return StatusCode(403, new { message = "只有管理员能修正约战结果" });

            var battle = await _context.Battles
                .Include(b => b.Participants)
                .FirstOrDefaultAsync(b => b.Id == id);
            if (battle == null)
                return NotFound(new { message = "约战不存在" });
            if (battle.Status != "finished")
                return BadRequest(new { message = "只有已经结束的约战需要修正；还没结束的请发起人直接录入结果" });

            var invalid = CheckResultRequest(battle, request, out var outcome);
            if (invalid != null) return BadRequest(new { message = invalid });

            var before = battle.Participants.ToDictionary(p => p.Id, p => p.Result);
            var oldJson = JsonSerializer.Serialize(new { result = battle.Result, outcome = OutcomeOf(battle), resultDescription = battle.ResultDescription, participants = before });

            ApplyOutcome(battle, outcome, request.WinnerIds ?? new List<string>());
            if (!string.IsNullOrWhiteSpace(request.ResultDescription))
                battle.ResultDescription = request.ResultDescription.Trim();

            // OC 战绩：结果变了的参与者，先减掉旧结果、再加上新结果（最少减到 0）
            foreach (var p in battle.Participants)
            {
                before.TryGetValue(p.Id, out var old);
                if (old == p.Result || string.IsNullOrEmpty(p.OcIdsJson)) continue;
                List<Guid>? ocIds = null;
                try { ocIds = JsonSerializer.Deserialize<List<Guid>>(p.OcIdsJson); } catch (JsonException) { }
                foreach (var ocId in ocIds ?? new List<Guid>())
                {
                    var oc = await _context.StickmanCharacters.FindAsync(ocId);
                    if (oc == null) continue;
                    AdjustRecord(oc, old, -1);
                    AdjustRecord(oc, p.Result, 1);
                }
            }

            NotifyParticipants(battle, userId, p => $"管理员修正了约战「{battle.Title}」的结果：{ResultText(battle, p)}");
            await _context.SaveChangesAsync();

            // 修正记录（谁、什么时候、改之前是什么），表第一次用时自动建
            var newJson = JsonSerializer.Serialize(new { result = battle.Result, outcome, resultDescription = battle.ResultDescription, participants = battle.Participants.ToDictionary(p => p.Id, p => p.Result) });
            await _context.Database.ExecuteSqlRawAsync(@"CREATE TABLE IF NOT EXISTS battle_result_corrections (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  BattleId VARCHAR(64) NOT NULL,
  OldJson TEXT NOT NULL,
  NewJson TEXT NOT NULL,
  OperatorId CHAR(36) NOT NULL,
  CreatedAt DATETIME NOT NULL,
  KEY IX_battle_result_corrections_battle (BattleId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4");
            await _context.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO battle_result_corrections (BattleId, OldJson, NewJson, OperatorId, CreatedAt) VALUES ({battle.Id}, {oldJson}, {newJson}, {userId.ToString()}, {DateTime.UtcNow})");

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);
            return await ResultAsync(updated!);
        }

        private static void AdjustRecord(StickmanCharacter oc, string? result, int delta)
        {
            switch (result)
            {
                case "win": oc.BattleWins = Math.Max(0, oc.BattleWins + delta); break;
                case "lose": oc.BattleLosses = Math.Max(0, oc.BattleLosses + delta); break;
                case "draw": oc.BattleDraws = Math.Max(0, oc.BattleDraws + delta); break;
            }
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

            // 投票判定还没做出来：投票制的约战以前在判定期就卡死了（没人能录结果、也发布不了），现在也由发起人录入
            if (battle.JudgmentType != "internal" && battle.JudgmentType != "vote")
                return BadRequest(new { message = "此约战不能手动录入结果" });
            var invalid = CheckResultRequest(battle, request, out var outcome);
            if (invalid != null) return BadRequest(new { message = invalid });
            ApplyOutcome(battle, outcome, request.WinnerIds ?? new List<string>());

            battle.ResultDescription = request.ResultDescription;

            await _context.SaveChangesAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            return await ResultAsync(updated!);
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

            // 投票制：有人投票就按票数定胜负（最高票的一方赢；平局票最多或最高票并列 → 平局）；没人投票时用发起人录入的结果
            if (battle.JudgmentType == "vote" && await TallyAsync(battle, null) is { Total: > 0 } tally)
            {
                var top = tally.Options.Max(o => o.Count);
                var leaders = tally.Options.Where(o => o.Count == top).Select(o => o.Option.OptionValue ?? "").ToList();
                var summary = string.Join("，", tally.Options.Select(o => $"{o.Option.OptionText} {o.Count} 票"));
                if (leaders.Count != 1 || leaders[0] == "draw")
                {
                    foreach (var p in battle.Participants) p.Result = "draw";
                    battle.Result = "draw";
                    SetOutcome(battle, OutcomeDraw);
                }
                else
                {
                    var lead = leaders[0];
                    foreach (var p in battle.Participants)
                        p.Result = lead == $"p:{p.Id}" || lead == $"t:{p.TeamNumber}" ? "win" : "lose";
                    battle.Result = "completed";
                    SetOutcome(battle, OutcomeWin);
                }
                battle.ResultDescription = $"按投票结果（共 {tally.Total} 票）：{summary}" + (string.IsNullOrWhiteSpace(battle.ResultDescription) ? "" : $"。{battle.ResultDescription}");
                // 投票结束：问卷标记为已结束
                await _context.Surveys.Where(s => s.Id == tally.SurveyId).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, 2));
            }

            // 平手 / 同归于尽（都记成 draw）、无结果（nocontest）没有胜者，也能发布
            if (battle.Result is not ("draw" or "nocontest") && !battle.Participants.Any(p => p.Result == "win"))
                return BadRequest(new { message = battle.JudgmentType == "vote" ? "还没有人投票，请先「录入结果」再发布" : "请先录入结果" });

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
            NotifyParticipants(battle, userId, p => $"约战「{battle.Title}」结果公布了：{ResultText(battle, p)}");

            await _context.SaveChangesAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            return await ResultAsync(updated!);
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
            // 告诉已经报名的人和还没回应的指定对手
            NotifyParticipants(battle, userId, _ => $"约战「{battle.Title}」被发起人取消了");
            try
            {
                var invited = string.IsNullOrEmpty(battle.OpponentOcIds) ? null : JsonSerializer.Deserialize<Dictionary<Guid, List<Guid>>>(battle.OpponentOcIds);
                foreach (var uid in invited?.Keys ?? Enumerable.Empty<Guid>())
                    if (battle.Participants.All(p => p.UserId != uid)) Notify(uid, userId, battle, $"约战「{battle.Title}」被发起人取消了");
            }
            catch (JsonException) { /* 配置坏了就不通知 */ }
            await _context.SaveChangesAsync();

            var updated = await _context.Battles
                .Include(b => b.Participants)
                    .ThenInclude(p => p.User)
                .Include(b => b.Submissions)
                .FirstOrDefaultAsync(b => b.Id == id);

            return await ResultAsync(updated!);
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

            var mine = battles.Select(MapToDto).ToList();
            await ApplyReviewAsync(mine);
            return Ok(mine);
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
                Outcome = OutcomeOf(battle),
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
