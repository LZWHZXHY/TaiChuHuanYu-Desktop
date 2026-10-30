using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Quest;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services.Notify;
using TaiChuWeb_V2.Services.Quest;

namespace TaiChuWeb_V2.Controllers;

/// <summary>
/// 我的世界服务器「世界任务」：全服一起完成目标，完成后公布新 mod / 新内容。
/// 玩家：看任务、提交贡献（截图 + 数量），等管理员审核。
/// 管理员（超级管理员或「太初官方」权限）：审核贡献、直接记录进度、改进度、管理任务链。
/// </summary>
[ApiController]
[Route("api/quests")]
public class QuestsController(AppDbContext db, ServerQuestService quests) : ControllerBase
{
    public sealed class QuestDto
    {
        public string Title { get; set; } = "";
        /// <summary>count = 数量目标；goal = 一次性目标（做到就完成，不用填数量）</summary>
        public string? Kind { get; set; }
        /// <summary>main = 主线；side = 支线</summary>
        public string? Line { get; set; }
        /// <summary>前置任务：它完成后自动开启这个任务</summary>
        public int? PrereqId { get; set; }
        public string? Description { get; set; }
        public string Unit { get; set; } = "";
        public long Target { get; set; }
        public string? RewardTeaser { get; set; }
        public string? RewardText { get; set; }
        public int RewardExp { get; set; }
        public int RewardPotato { get; set; }
        public string? CoverImage { get; set; }
    }
    public sealed class ContributeDto
    {
        public long Amount { get; set; }
        public string? Note { get; set; }
        public string? PlayerName { get; set; }
        public List<string>? Images { get; set; }
        /// <summary>论坛里的报告帖（发帖时一起提交）</summary>
        public Guid? PostId { get; set; }
    }
    public sealed class ReviewDto
    {
        public bool Approve { get; set; }
        /// <summary>通过时可以改数量（游戏里核对的实际数量）</summary>
        public long? Amount { get; set; }
        public string? Note { get; set; }
    }
    public sealed class RecordDto
    {
        public long Amount { get; set; }
        /// <summary>记在哪个网站账号名下（可选）</summary>
        public string? Username { get; set; }
        public string? PlayerName { get; set; }
        public string? Note { get; set; }
    }
    public sealed class ProgressDto { public long Progress { get; set; } }
    public sealed class ReorderDto { public List<int> Ids { get; set; } = new(); }

    private const long MaxAmount = 1_000_000_000_000;

    private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private async Task<bool> IsManagerAsync()
    {
        if (CurrentUserId is not Guid me) return false;
        return await db.UserPermissions.AsNoTracking().AnyAsync(p => p.UserId == me
            && (p.Permission == AdminPermission.SuperAdmin || p.Permission == AdminPermission.TaiChu));
    }

    /// <param name="all">全部任务，用来算「完成后开启了 / 会开启哪些任务」</param>
    private static object View(ServerQuest q, bool reveal, int contributors, List<ServerQuest> all)
    {
        var followUps = all.Where(x => x.PrereqId == q.Id).ToList();
        return new
        {
            q.Id, q.Title, q.Kind, q.Line, q.PrereqId,
            // 前置任务的名字：排队中的只有管理员能看到，所以这里不怕剧透
            PrereqTitle = q.PrereqId is int pid ? all.FirstOrDefault(x => x.Id == pid)?.Title : null,
            q.Description, q.Unit, q.Target, q.Progress, q.Status, q.SortOrder, q.RewardTeaser,
            // 完成前「解锁内容」只给管理员看
            RewardText = reveal || q.Status == ServerQuest.Completed ? q.RewardText : null,
            q.RewardExp, q.RewardPotato, q.CoverImage, q.StartedAt, q.CompletedAt, Contributors = contributors,
            // 完成后会开启几个任务（不剧透名字）；已经开启的列出名字
            FollowUpCount = followUps.Count(x => x.Status == ServerQuest.Draft),
            Unlocked = followUps.Where(x => x.Status != ServerQuest.Draft).Select(x => new { x.Id, x.Title, x.Line }).ToList(),
            // 管理员看：完成后会开启哪些
            FollowUps = reveal ? followUps.Where(x => x.Status == ServerQuest.Draft).Select(x => new { x.Id, x.Title, x.Line }).ToList() : null,
        };
    }

    private async Task<Dictionary<int, int>> ContributorCountsAsync(List<int> questIds)
    {
        var rows = await db.ServerQuestContributions.AsNoTracking()
            .Where(c => questIds.Contains(c.QuestId) && c.Status == ServerQuestContribution.Approved)
            .Select(c => new { c.QuestId, c.UserId, c.PlayerName }).ToListAsync();
        return rows.GroupBy(r => r.QuestId).ToDictionary(g => g.Key,
            g => g.Select(r => r.UserId.HasValue ? r.UserId.Value.ToString() : "p:" + (r.PlayerName ?? "").ToLowerInvariant()).Distinct().Count());
    }

    /// <summary>任务总览：进行中的任务、已完成的任务（含解锁内容）；管理员还能看到排队中的任务和待审核数量</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Overview()
    {
        var manager = await IsManagerAsync();
        var all = await db.ServerQuests.AsNoTracking().OrderBy(q => q.SortOrder).ThenBy(q => q.Id).ToListAsync();
        var counts = await ContributorCountsAsync(all.Select(q => q.Id).ToList());
        int C(ServerQuest q) => counts.GetValueOrDefault(q.Id);
        // 进行中的：主线在前，支线按开始时间
        var actives = all.Where(q => q.Status == ServerQuest.Active).OrderBy(q => q.IsSide ? 1 : 0).ThenBy(q => q.StartedAt).ToList();
        var drafts = all.Where(q => q.Status == ServerQuest.Draft).ToList();
        var me = CurrentUserId;
        return Ok(new
        {
            canManage = manager,
            active = actives.Count == 0 ? null : View(actives[0], manager, C(actives[0]), all),
            actives = actives.Select(q => View(q, manager, C(q), all)),
            completed = all.Where(q => q.Status == ServerQuest.Completed).OrderByDescending(q => q.CompletedAt).Select(q => View(q, true, C(q), all)),
            upcomingCount = drafts.Count,
            // 排队中的任务只有管理员能看到内容，其他人只知道「后面还有几个」
            upcoming = manager ? drafts.Select(q => View(q, true, C(q), all)) : null,
            pendingCount = manager ? await db.ServerQuestContributions.CountAsync(c => c.Status == ServerQuestContribution.Pending) : (int?)null,
            myPendingCount = me is Guid uid ? await db.ServerQuestContributions.CountAsync(c => c.UserId == uid && c.Status == ServerQuestContribution.Pending) : 0,
        });
    }

    /// <summary>某个任务的贡献榜</summary>
    [HttpGet("{id:int}/leaderboard")]
    [AllowAnonymous]
    public async Task<IActionResult> Leaderboard(int id)
    {
        var rows = await db.ServerQuestContributions.AsNoTracking()
            .Where(c => c.QuestId == id && c.Status == ServerQuestContribution.Approved)
            .Select(c => new { c.UserId, c.PlayerName, c.Amount }).ToListAsync();
        var userIds = rows.Where(r => r.UserId.HasValue).Select(r => r.UserId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
        var board = rows.GroupBy(r => r.UserId.HasValue ? r.UserId.Value.ToString() : "p:" + (r.PlayerName ?? "").ToLowerInvariant())
            .Select(g =>
            {
                var uid = g.First().UserId;
                var player = g.Select(r => r.PlayerName).FirstOrDefault(p => !string.IsNullOrWhiteSpace(p));
                return new
                {
                    userId = uid,
                    username = uid.HasValue ? names.GetValueOrDefault(uid.Value) : null,
                    playerName = player,
                    amount = g.Sum(r => r.Amount),
                    times = g.Count(),
                };
            })
            .OrderByDescending(x => x.amount).Take(30).ToList();
        return Ok(board);
    }

    /// <summary>我提交过的贡献</summary>
    [HttpGet("mine")]
    [Authorize]
    public async Task<IActionResult> Mine()
    {
        var me = CurrentUserId!.Value;
        var rows = await db.ServerQuestContributions.AsNoTracking().Where(c => c.UserId == me)
            .OrderByDescending(c => c.Id).Take(30).ToListAsync();
        var questIds = rows.Select(r => r.QuestId).Distinct().ToList();
        var titles = await db.ServerQuests.AsNoTracking().Where(q => questIds.Contains(q.Id)).ToDictionaryAsync(q => q.Id, q => new { q.Title, q.Unit, q.Kind });
        return Ok(rows.Select(r => new
        {
            r.Id, r.QuestId, QuestTitle = titles.GetValueOrDefault(r.QuestId)?.Title, Unit = titles.GetValueOrDefault(r.QuestId)?.Unit,
            QuestKind = titles.GetValueOrDefault(r.QuestId)?.Kind,
            r.Amount, r.Note, r.PlayerName, Images = ReadImages(r.Images), r.PostId, r.Status, r.Source, r.ReviewNote, r.CreatedAt, r.ReviewedAt,
        }));
    }

    /// <summary>玩家提交贡献（等管理员审核）</summary>
    [HttpPost("{id:int}/contributions")]
    [Authorize]
    public async Task<IActionResult> Contribute(int id, [FromBody] ContributeDto dto)
    {
        var me = CurrentUserId!.Value;
        var quest = await db.ServerQuests.AsNoTracking().FirstOrDefaultAsync(q => q.Id == id);
        if (quest == null) return NotFound(new { message = "任务不存在" });
        if (quest.Status != ServerQuest.Active) return BadRequest(new { message = "这个任务现在不在进行中" });
        if (quest.IsGoal) dto.Amount = 1; // 一次性目标：提交的是「我做到了」的证明，不用数量
        if (dto.Amount <= 0 || dto.Amount > MaxAmount) return BadRequest(new { message = "数量不对" });
        var images = (dto.Images ?? new()).Where(u => !string.IsNullOrWhiteSpace(u) && u.Length <= 500).Take(6).ToList();
        if (dto.PostId is Guid postId)
        {
            // 报告帖：要是自己发的、已经公开的帖子，一篇帖子只能报告一次
            var post = await db.PublishedNotes.AsNoTracking().Where(p => p.Id == postId).Select(p => new { p.OriginalNoteId, p.Title }).FirstOrDefaultAsync();
            if (post?.OriginalNoteId == null) return BadRequest(new { message = "没找到这篇报告帖" });
            var authorId = await db.Notes.AsNoTracking().Where(n => n.Id == post.OriginalNoteId).Select(n => n.AuthorId).FirstOrDefaultAsync();
            if (!Guid.TryParse(authorId, out var author) || author != me) return BadRequest(new { message = "只能用自己发的帖子报告" });
            if (await db.ServerQuestContributions.AnyAsync(c => c.PostId == postId)) return BadRequest(new { message = "这篇帖子已经报告过了" });
            if (string.IsNullOrWhiteSpace(dto.Note)) dto.Note = post.Title;
        }
        else if (string.IsNullOrWhiteSpace(dto.Note) && images.Count == 0) return BadRequest(new { message = "写一句说明或者传一张截图吧，方便管理员核对" });
        var pending = await db.ServerQuestContributions.CountAsync(c => c.UserId == me && c.Status == ServerQuestContribution.Pending);
        if (pending >= 10) return BadRequest(new { message = "你还有 10 条贡献在等审核，等审核完再提交吧" });
        var row = new ServerQuestContribution
        {
            QuestId = id, UserId = me, Amount = dto.Amount, Note = ServerQuestService.Clip(dto.Note?.Trim(), 1000),
            PlayerName = ServerQuestService.Clip(dto.PlayerName?.Trim(), 50), Images = JsonSerializer.Serialize(images), PostId = dto.PostId,
            Status = ServerQuestContribution.Pending, Source = "player", CreatedAt = DateTime.UtcNow,
        };
        if (row.Note == "") row.Note = null;
        if (row.PlayerName == "") row.PlayerName = null;
        db.ServerQuestContributions.Add(row);
        await db.SaveChangesAsync();
        return Ok(new { row.Id, message = quest.IsGoal ? "已提交，管理员确认后任务就完成了" : "已提交，管理员核对后会计入进度" });
    }

    // ================= 管理员 =================

    /// <summary>待审核的贡献（最早的在前）</summary>
    [HttpGet("pending")]
    [Authorize]
    public async Task<IActionResult> Pending()
    {
        if (!await IsManagerAsync()) return Forbid();
        var rows = await db.ServerQuestContributions.AsNoTracking().Where(c => c.Status == ServerQuestContribution.Pending)
            .OrderBy(c => c.Id).Take(100).ToListAsync();
        var userIds = rows.Where(r => r.UserId.HasValue).Select(r => r.UserId!.Value).Distinct().ToList();
        var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
        var questIds = rows.Select(r => r.QuestId).Distinct().ToList();
        var titles = await db.ServerQuests.AsNoTracking().Where(q => questIds.Contains(q.Id)).ToDictionaryAsync(q => q.Id, q => new { q.Title, q.Unit, q.Status, q.Kind });
        return Ok(rows.Select(r => new
        {
            r.Id, r.QuestId, QuestTitle = titles.GetValueOrDefault(r.QuestId)?.Title, Unit = titles.GetValueOrDefault(r.QuestId)?.Unit,
            QuestStatus = titles.GetValueOrDefault(r.QuestId)?.Status, QuestKind = titles.GetValueOrDefault(r.QuestId)?.Kind,
            r.UserId, Username = r.UserId.HasValue ? names.GetValueOrDefault(r.UserId.Value) : null,
            r.PlayerName, r.Amount, r.Note, Images = ReadImages(r.Images), r.PostId, r.CreatedAt,
        }));
    }

    /// <summary>审核：通过（可以改成游戏里核对的实际数量）或驳回</summary>
    [HttpPost("contributions/{cid:long}/review")]
    [Authorize]
    public async Task<IActionResult> Review(long cid, [FromBody] ReviewDto dto)
    {
        if (!await IsManagerAsync()) return Forbid();
        var row = await db.ServerQuestContributions.FirstOrDefaultAsync(c => c.Id == cid);
        if (row == null) return NotFound(new { message = "这条贡献不存在" });
        if (row.Status != ServerQuestContribution.Pending) return BadRequest(new { message = "这条已经审核过了" });
        var quest = await db.ServerQuests.FirstOrDefaultAsync(q => q.Id == row.QuestId);
        if (quest == null) return NotFound(new { message = "任务不存在" });
        row.ReviewerId = CurrentUserId; row.ReviewedAt = DateTime.UtcNow;
        row.ReviewNote = string.IsNullOrWhiteSpace(dto.Note) ? null : ServerQuestService.Clip(dto.Note.Trim(), 300);
        var notify = new NotificationService(db);
        if (!dto.Approve)
        {
            row.Status = ServerQuestContribution.Rejected;
            if (row.UserId is Guid u) notify.Add(u, CurrentUserId, "quest", "quest", quest.Id.ToString(),
                $"你在世界任务《{ServerQuestService.Clip(quest.Title, 40)}》里提交的贡献没有通过{(row.ReviewNote != null ? "：" + row.ReviewNote : "")}", "/quests");
            await db.SaveChangesAsync();
            return Ok(new { message = "已驳回" });
        }
        if (quest.IsGoal) dto.Amount = 1;
        if (dto.Amount.HasValue)
        {
            if (dto.Amount.Value <= 0 || dto.Amount.Value > MaxAmount) return BadRequest(new { message = "数量不对" });
            row.Amount = dto.Amount.Value;
        }
        row.Status = ServerQuestContribution.Approved;
        quest.Progress = Math.Min(MaxAmount, quest.Progress + row.Amount);
        if (row.UserId is Guid uid) notify.Add(uid, CurrentUserId, "quest", "quest", quest.Id.ToString(),
            quest.IsGoal
                ? $"你在世界任务《{ServerQuestService.Clip(quest.Title, 40)}》里提交的完成证明通过了"
                : $"你在世界任务《{ServerQuestService.Clip(quest.Title, 40)}》里的贡献通过了，计入 {row.Amount:N0} {quest.Unit}", "/quests");
        await db.SaveChangesAsync();
        var completed = await quests.CheckCompletionAsync(quest, CurrentUserId);
        if (!completed) await quests.RewardLateAsync(quest, row);
        return Ok(new { message = completed ? "已通过，任务完成了！" : "已通过", progress = quest.Progress, completed });
    }

    /// <summary>管理员直接记录一笔进度（游戏里看到的，直接算数）</summary>
    [HttpPost("{id:int}/record")]
    [Authorize]
    public async Task<IActionResult> Record(int id, [FromBody] RecordDto dto)
    {
        if (!await IsManagerAsync()) return Forbid();
        var quest = await db.ServerQuests.FirstOrDefaultAsync(q => q.Id == id);
        if (quest == null) return NotFound(new { message = "任务不存在" });
        if (quest.IsGoal) dto.Amount = 1; // 一次性目标：记一笔就是「这个人做到了」
        if (dto.Amount <= 0 || dto.Amount > MaxAmount) return BadRequest(new { message = "数量不对" });
        Guid? userId = null;
        if (!string.IsNullOrWhiteSpace(dto.Username))
        {
            var name = dto.Username.Trim();
            userId = await db.Users.AsNoTracking().Where(u => u.Username == name).Select(u => (Guid?)u.Id).FirstOrDefaultAsync();
            if (userId == null) return BadRequest(new { message = $"没有找到网站用户「{name}」，可以只填游戏名" });
        }
        var row = new ServerQuestContribution
        {
            QuestId = id, UserId = userId, Amount = dto.Amount, PlayerName = string.IsNullOrWhiteSpace(dto.PlayerName) ? null : ServerQuestService.Clip(dto.PlayerName.Trim(), 50),
            Note = string.IsNullOrWhiteSpace(dto.Note) ? null : ServerQuestService.Clip(dto.Note.Trim(), 1000), Images = "[]",
            Status = ServerQuestContribution.Approved, Source = "admin", ReviewerId = CurrentUserId, CreatedAt = DateTime.UtcNow, ReviewedAt = DateTime.UtcNow,
        };
        db.ServerQuestContributions.Add(row);
        quest.Progress = Math.Min(MaxAmount, quest.Progress + dto.Amount);
        await db.SaveChangesAsync();
        var completed = await quests.CheckCompletionAsync(quest, CurrentUserId);
        if (!completed) await quests.RewardLateAsync(quest, row);
        return Ok(new { message = completed ? "已记录，任务完成了！" : "已记录", progress = quest.Progress, completed });
    }

    /// <summary>直接改进度（纠正用）</summary>
    [HttpPut("{id:int}/progress")]
    [Authorize]
    public async Task<IActionResult> SetProgress(int id, [FromBody] ProgressDto dto)
    {
        if (!await IsManagerAsync()) return Forbid();
        var quest = await db.ServerQuests.FirstOrDefaultAsync(q => q.Id == id);
        if (quest == null) return NotFound(new { message = "任务不存在" });
        quest.Progress = Math.Clamp(dto.Progress, 0, MaxAmount);
        await db.SaveChangesAsync();
        var completed = await quests.CheckCompletionAsync(quest, CurrentUserId);
        return Ok(new { progress = quest.Progress, completed });
    }

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] QuestDto dto)
    {
        if (!await IsManagerAsync()) return Forbid();
        var error = Validate(dto);
        if (error != null) return BadRequest(new { message = error });
        var maxOrder = await db.ServerQuests.Select(q => (int?)q.SortOrder).MaxAsync() ?? 0;
        var quest = new ServerQuest { CreatedAt = DateTime.UtcNow, SortOrder = maxOrder + 1 };
        Apply(quest, dto);
        var prereqError = await CheckPrereqAsync(quest, dto.PrereqId);
        if (prereqError != null) return BadRequest(new { message = prereqError });
        if (await ShouldStartNowAsync(quest)) { quest.Status = ServerQuest.Active; quest.StartedAt = DateTime.UtcNow; }
        db.ServerQuests.Add(quest);
        await db.SaveChangesAsync();
        return Ok(new { quest.Id, quest.Status });
    }

    [HttpPut("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Update(int id, [FromBody] QuestDto dto)
    {
        if (!await IsManagerAsync()) return Forbid();
        var error = Validate(dto);
        if (error != null) return BadRequest(new { message = error });
        var quest = await db.ServerQuests.FirstOrDefaultAsync(q => q.Id == id);
        if (quest == null) return NotFound(new { message = "任务不存在" });
        if (NormalizeKind(dto.Kind) != quest.Kind && (quest.Progress > 0 || quest.Status == ServerQuest.Completed))
            return BadRequest(new { message = "这个任务已经有进度了，不能再改任务类型" });
        if (quest.Status == ServerQuest.Active && NormalizeLine(dto.Line) == ServerQuest.LineMain && quest.IsSide
            && await db.ServerQuests.AnyAsync(q => q.Status == ServerQuest.Active && q.Line == ServerQuest.LineMain && q.Id != id))
            return BadRequest(new { message = "已经有进行中的主线了，这个进行中的支线不能改成主线" });
        Apply(quest, dto);
        var prereqError = await CheckPrereqAsync(quest, dto.PrereqId);
        if (prereqError != null) return BadRequest(new { message = prereqError });
        // 排队中的任务改了前置（前置已经完成了）就直接开始
        if (quest.Status == ServerQuest.Draft && quest.PrereqId != null && await ShouldStartNowAsync(quest)) { quest.Status = ServerQuest.Active; quest.StartedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync();
        var completed = await quests.CheckCompletionAsync(quest, CurrentUserId);
        return Ok(new { completed });
    }

    [HttpDelete("{id:int}")]
    [Authorize]
    public async Task<IActionResult> Delete(int id)
    {
        if (!await IsManagerAsync()) return Forbid();
        var quest = await db.ServerQuests.FirstOrDefaultAsync(q => q.Id == id);
        if (quest == null) return NotFound(new { message = "任务不存在" });
        if (quest.Status == ServerQuest.Completed) return BadRequest(new { message = "已完成的任务不能删除（奖励已经发出去了）" });
        await db.ServerQuestContributions.Where(c => c.QuestId == id).ExecuteDeleteAsync();
        // 以它为前置的任务接到它的前置上（链条不断）
        foreach (var child in await db.ServerQuests.Where(q => q.PrereqId == id).ToListAsync()) child.PrereqId = quest.PrereqId;
        db.ServerQuests.Remove(quest);
        await db.SaveChangesAsync();
        return NoContent();
    }

    /// <summary>手动开始某个排队中的任务（主线同时只能有一个；支线随时可以开）</summary>
    [HttpPost("{id:int}/start")]
    [Authorize]
    public async Task<IActionResult> Start(int id)
    {
        if (!await IsManagerAsync()) return Forbid();
        var quest = await db.ServerQuests.FirstOrDefaultAsync(q => q.Id == id);
        if (quest == null) return NotFound(new { message = "任务不存在" });
        if (quest.Status != ServerQuest.Draft) return BadRequest(new { message = "这个任务不是排队中的" });
        if (!quest.IsSide && await db.ServerQuests.AnyAsync(q => q.Status == ServerQuest.Active && q.Line == ServerQuest.LineMain && q.Id != id))
            return BadRequest(new { message = "已经有进行中的主线任务了，先完成它（支线可以随时开）" });
        quest.Status = ServerQuest.Active; quest.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok(new { message = "任务开始了" });
    }

    /// <summary>手动完成进行中的任务（进度没满也可以，比如目标改了）</summary>
    [HttpPost("{id:int}/complete")]
    [Authorize]
    public async Task<IActionResult> Complete(int id)
    {
        if (!await IsManagerAsync()) return Forbid();
        var quest = await db.ServerQuests.FirstOrDefaultAsync(q => q.Id == id);
        if (quest == null) return NotFound(new { message = "任务不存在" });
        if (quest.Status != ServerQuest.Active) return BadRequest(new { message = "只有进行中的任务能完成" });
        var started = await quests.CompleteAsync(quest, CurrentUserId);
        return Ok(new { message = "任务已完成，解锁内容已公布" + (started.Count > 0 ? "，开启了：" + string.Join("、", started.Select(q => (q.IsSide ? "支线" : "主线") + "《" + q.Title + "》")) : "") });
    }

    /// <summary>调整排队顺序</summary>
    [HttpPost("reorder")]
    [Authorize]
    public async Task<IActionResult> Reorder([FromBody] ReorderDto dto)
    {
        if (!await IsManagerAsync()) return Forbid();
        var ids = dto.Ids.Distinct().ToList();
        var rows = await db.ServerQuests.Where(q => ids.Contains(q.Id)).ToListAsync();
        foreach (var q in rows) q.SortOrder = ids.IndexOf(q.Id) + 1;
        await db.SaveChangesAsync();
        return Ok();
    }

    private static string? Validate(QuestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Title) || dto.Title.Trim().Length > 100) return "任务名称要填，最多 100 个字";
        if (NormalizeKind(dto.Kind) == ServerQuest.KindGoal) { dto.Target = 1; dto.Unit = ""; }
        if (dto.Target <= 0 || dto.Target > MaxAmount) return "目标数量要大于 0";
        if ((dto.Unit ?? "").Trim().Length > 20) return "单位最多 20 个字";
        if ((dto.RewardTeaser ?? "").Length > 500) return "预告最多 500 个字";
        if (dto.RewardExp < 0 || dto.RewardExp > 100000 || dto.RewardPotato < 0 || dto.RewardPotato > 10_000_000) return "奖励数值不对";
        return null;
    }

    private static void Apply(ServerQuest q, QuestDto dto)
    {
        q.Kind = NormalizeKind(dto.Kind);
        q.Line = NormalizeLine(dto.Line);
        q.Title = dto.Title.Trim(); q.Description = dto.Description?.Trim(); q.Unit = (dto.Unit ?? "").Trim(); q.Target = dto.Target;
        q.RewardTeaser = string.IsNullOrWhiteSpace(dto.RewardTeaser) ? null : dto.RewardTeaser.Trim();
        q.RewardText = string.IsNullOrWhiteSpace(dto.RewardText) ? null : dto.RewardText.Trim();
        q.RewardExp = dto.RewardExp; q.RewardPotato = dto.RewardPotato;
        q.CoverImage = string.IsNullOrWhiteSpace(dto.CoverImage) ? null : ServerQuestService.Clip(dto.CoverImage.Trim(), 500);
    }

    private static string NormalizeKind(string? kind) => kind == ServerQuest.KindGoal ? ServerQuest.KindGoal : ServerQuest.KindCount;
    private static string NormalizeLine(string? line) => line == ServerQuest.LineSide ? ServerQuest.LineSide : ServerQuest.LineMain;

    /// <summary>检查并设置前置任务：要存在、不能是自己、不能绕成圈</summary>
    private async Task<string?> CheckPrereqAsync(ServerQuest quest, int? prereqId)
    {
        if (prereqId is not int pid || pid <= 0) { quest.PrereqId = null; return null; }
        if (pid == quest.Id) return "前置任务不能是它自己";
        var links = await db.ServerQuests.AsNoTracking().Select(q => new { q.Id, q.PrereqId }).ToDictionaryAsync(q => q.Id, q => q.PrereqId);
        if (!links.ContainsKey(pid)) return "前置任务不存在";
        // 沿着前置往上走，走回自己就是绕圈了
        var seen = new HashSet<int>();
        for (int? cur = pid; cur is int c; cur = links.GetValueOrDefault(c))
        {
            if (quest.Id != 0 && c == quest.Id) return "前置任务绕成圈了：不能把自己的后续任务设成前置";
            if (!seen.Add(c)) break;
        }
        quest.PrereqId = pid;
        return null;
    }

    /// <summary>新建 / 改了前置之后，是不是应该马上开始</summary>
    private async Task<bool> ShouldStartNowAsync(ServerQuest quest)
    {
        if (quest.PrereqId is int pid)
            // 前置已经完成了：直接开始（主线还要看有没有别的主线在进行）
            return await db.ServerQuests.AnyAsync(q => q.Id == pid && q.Status == ServerQuest.Completed)
                && (quest.IsSide || !await db.ServerQuests.AnyAsync(q => q.Status == ServerQuest.Active && q.Line == ServerQuest.LineMain && q.Id != quest.Id));
        // 没有前置的主线：现在没有主线在进行就直接开始；没有前置的支线等管理员手动开始
        return !quest.IsSide && !await db.ServerQuests.AnyAsync(q => q.Status == ServerQuest.Active && q.Line == ServerQuest.LineMain);
    }

    private static List<string> ReadImages(string? json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? new(); }
        catch (JsonException) { return new(); }
    }
}
