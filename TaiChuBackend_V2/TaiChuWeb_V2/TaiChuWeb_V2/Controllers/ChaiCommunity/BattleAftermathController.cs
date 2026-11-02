using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.ChaiCommunity;
using TaiChuWeb_V2.Models.ChaiCommunity.Battle;
using TaiChuWeb_V2.Models.World;
using TaiChuWeb_V2.Services.ChaiCommunity;
using TaiChuWeb_V2.Services.Notify;
using TaiChuWeb_V2.Services.World;
using static TaiChuWeb_V2.Services.ChaiCommunity.BattleAftermathStore;

namespace TaiChuWeb_V2.Controllers.ChaiCommunity;

/// <summary>
/// 约战「战后记录」（单独一个控制器，约战、OC、编年史原来的接口都不改）：
///   1. 约战结束后，参赛者给自己出战的每个 OC 写一条：战后状态（毫发无伤 / 轻伤 / 重伤 / 阵亡 / 觉醒 / 突破 / 自定义）+ 说明 + 视频链接。
///      只有 OC 作者本人、并且确实带这个 OC 参加了这场约战才能写、改、删。删除只删这条记录。
///   2. 写的时候可以顺便写进世界观编年史：世界观要是这个 OC 已关联、自己能编辑的；
///      事件照编年史原来的规矩建（同样的权限检查、同样记修改记录，可以在编年史「修改记录」里撤销），事件 id 记回战后记录。
///      编年史事件没有视频字段：视频链接写在事件内容里，前端在编年史里看到有对应战后记录的事件时，另外显示一张「约战记录」卡片。
///   3. 读：约战页（所有参赛 OC 的战后记录）、OC 详情页（战斗经历 + 当前状态）、编年史事件（约战记录卡片）。
/// 表结构见 Services/ChaiCommunity/BattleAftermathStore.cs。
/// </summary>
[Route("api/battle-aftermath")]
[ApiController]
[Authorize]
public class BattleAftermathController(AppDbContext context, ILogger<BattleAftermathController> logger) : ControllerBase
{
    public sealed class SaveRequest
    {
        public string StatusTag { get; set; } = "";
        public string? StatusNote { get; set; }
        public string? VideoUrl { get; set; }
        public string? VideoTitle { get; set; }
        public string? VideoCover { get; set; }
        /// <summary>要顺便写进世界观编年史时填；不写就是 null</summary>
        public ChronicleRequest? Chronicle { get; set; }
    }

    public sealed class ChronicleRequest
    {
        public Guid ProjectId { get; set; }
        /// <summary>写到哪条时间线；不填 = 这个世界的第一条线（一条线都没有就自动建一条「约战记录」）</summary>
        public Guid? TimelineId { get; set; }
        /// <summary>事件标题；不填 = 「约战《xxx》：A vs B」</summary>
        public string? Title { get; set; }
        /// <summary>发生在哪一年（世界自己的历法，可以不填）</summary>
        public long? Year { get; set; }
        /// <summary>模糊的时间写法（「第三纪元末」），可以不填</summary>
        public string? DateText { get; set; }
        /// <summary>网站地址（前端传首页地址，比如 https://xx.com/#/），事件内容里链回约战页、OC 页用</summary>
        public string? LinkBase { get; set; }
    }

    private const string Finished = "finished";
    private const int MaxTag = 10, MaxNote = 500, MaxUrl = 1000, MaxVideoTitle = 200;
    private const long MaxYear = 1_000_000_000_000;
    /// <summary>站内作品页地址里的作品编号（哈希路由 /#/works/xxx 和 /works/xxx 都认）</summary>
    private static readonly Regex WorkLink = new(@"/works/([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})(?:[/?#]|$)", RegexOptions.Compiled);

    private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    // ======================================================================
    //  读
    // ======================================================================

    /// <summary>约战页：所有参赛 OC 和它们的战后记录；参赛者本人的 OC 带 canEdit，另外给出自己交的作品链接（写记录时默认带上）</summary>
    [HttpGet("battle/{battleId}")]
    [AllowAnonymous]
    public async Task<IActionResult> OfBattle(string battleId)
    {
        var me = Me;
        var battle = await context.Battles.AsNoTracking().Include(b => b.Participants).FirstOrDefaultAsync(b => b.Id == battleId);
        if (battle == null) return NotFound(new { message = "约战不存在" });
        var finished = battle.Status == Finished;

        var records = await StoreSafeAsync(() => OfBattleAsync(context, battleId));
        var participants = battle.Participants.OrderBy(p => p.JoinedAt).ToList();
        var ocIds = participants.SelectMany(p => OcIdsOf(p)).Distinct().ToList();
        var ocs = (await context.StickmanCharacters.AsNoTracking().Where(c => ocIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Title, c.CoverUrl, c.AuthorId, c.Status }).ToListAsync()).ToDictionary(c => c.Id);
        var look = await LookupAsync(records, new List<string> { battleId }, me);
        var byOc = records.ToDictionary(r => r.OcId);

        // 自己交的作品（最新一件）：写战后记录时视频默认用它
        var mine = participants.FirstOrDefault(p => me != Guid.Empty && p.UserId == me);
        object? myDefaultVideo = null;
        if (mine != null)
        {
            var sub = await context.BattleSubmissions.AsNoTracking().Where(s => s.BattleId == battleId && s.ParticipantId == mine.Id)
                .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt).Select(s => new { s.Id, s.Title, s.ContentUrl }).FirstOrDefaultAsync();
            if (sub != null && IsWebLink(sub.ContentUrl))
                myDefaultVideo = new { url = sub.ContentUrl.Trim(), title = sub.Title, pending = look.Unapproved.Contains(Key(battleId, sub.ContentUrl)) };
        }

        var items = new List<object>();
        foreach (var p in participants)
        {
            var names = OcNamesOf(p);
            var ids = OcIdsOf(p);
            for (var i = 0; i < ids.Count; i++)
            {
                var id = ids[i];
                ocs.TryGetValue(id, out var oc);
                // 草稿 OC（作者后来又改回草稿）只给作者看名字
                var ocVisible = oc != null && (oc.Status != "draft" || oc.AuthorId == me);
                items.Add(new
                {
                    participantId = p.Id,
                    userId = p.UserId,
                    userName = p.UserName,
                    teamName = p.TeamName,
                    teamNumber = p.TeamNumber,
                    result = p.Result,
                    ocId = id,
                    ocName = ocVisible ? oc!.Title : i < names.Count ? names[i] : "OC",
                    ocCover = ocVisible ? oc!.CoverUrl : null,
                    ocExists = ocVisible,
                    // 能写：约战结束了、自己是这个参赛者、而且是这个 OC 的作者
                    canEdit = finished && me != Guid.Empty && p.UserId == me && oc != null && oc.AuthorId == me,
                    @record = byOc.TryGetValue(id, out var r) ? View(r, look, me) : null,
                });
            }
        }
        return Ok(new { battleId, title = battle.Title, status = battle.Status, finished, outcome = OutcomeOf(battle), myDefaultVideo, items });
    }

    /// <summary>OC 详情页：这个 OC 打过的已结束约战（对手、结果、战后记录）+ 当前状态（最近一场约战的战后状态）</summary>
    [HttpGet("oc/{ocId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> OfOc(Guid ocId)
    {
        var me = Me;
        var oc = await context.StickmanCharacters.AsNoTracking().Where(c => c.Id == ocId)
            .Select(c => new { c.Id, c.Title, c.AuthorId, c.Status }).FirstOrDefaultAsync();
        if (oc == null || (oc.Status == "draft" && oc.AuthorId != me)) return NotFound(new { message = "OC 角色不存在" });

        // 带过这个 OC 的参赛记录：先用 SQL 粗筛（OcIdsJson 里有这个编号），再在内存里精确核对
        var idText = ocId.ToString();
        List<BattleParticipant> mineRows;
        try
        {
            mineRows = await context.BattleParticipants.AsNoTracking().Where(p => p.OcIdsJson.Contains(idText)).ToListAsync();
        }
        catch (Exception ex)
        {
            // 万一数据库不支持在 json 列上这样查：退回到读出已结束约战的全部参赛记录再筛（慢一点，但不会读不出来）
            logger.LogWarning(ex, "按 OC 查约战参赛记录失败，改为内存筛选");
            var finishedIds = await context.Battles.AsNoTracking().Where(b => b.Status == Finished).Select(b => b.Id).ToListAsync();
            mineRows = await context.BattleParticipants.AsNoTracking().Where(p => finishedIds.Contains(p.BattleId)).ToListAsync();
        }
        mineRows = mineRows.Where(p => OcIdsOf(p).Contains(ocId)).ToList();

        var battleIds = mineRows.Select(p => p.BattleId).Distinct().ToList();
        var battles = (await context.Battles.AsNoTracking().Where(b => battleIds.Contains(b.Id) && b.Status == Finished)
            .Select(b => new { b.Id, b.Title, b.FinishedAt, b.CreatedAt, b.Result, b.BattleConfigJson, b.Status }).ToListAsync()).ToDictionary(b => b.Id);
        battleIds = battleIds.Where(battles.ContainsKey).ToList();
        var everyone = battleIds.Count == 0 ? new List<BattleParticipant>()
            : await context.BattleParticipants.AsNoTracking().Where(p => battleIds.Contains(p.BattleId)).ToListAsync();

        var records = (await StoreSafeAsync(() => OfOcAsync(context, ocId))).Where(r => battles.ContainsKey(r.BattleId)).ToList();
        var look = await LookupAsync(records, battleIds, me);
        var byBattle = records.GroupBy(r => r.BattleId).ToDictionary(g => g.Key, g => g.First());

        var items = battleIds
            .Select(id => new { battle = battles[id], row = mineRows.First(p => p.BattleId == id) })
            .OrderByDescending(x => x.battle.FinishedAt ?? x.battle.CreatedAt)
            .Select(x =>
            {
                var b = x.battle;
                var outcome = OutcomeOf(b.Result, b.BattleConfigJson);
                var others = everyone.Where(p => p.BattleId == b.Id && p.Id != x.row.Id).OrderBy(p => p.JoinedAt).ToList();
                return new
                {
                    battleId = b.Id,
                    title = b.Title,
                    finishedAt = b.FinishedAt,
                    outcome,
                    result = x.row.Result,
                    teamName = x.row.TeamName,
                    // 同队的是队友，其他都是对手（单挑就是对面那个人）
                    teammates = others.Where(p => SameTeam(p, x.row)).Select(p => new { userName = p.UserName, ocNames = OcNamesOf(p) }).ToList(),
                    opponents = others.Where(p => !SameTeam(p, x.row)).Select(p => new { userName = p.UserName, ocNames = OcNamesOf(p) }).ToList(),
                    @record = byBattle.TryGetValue(b.Id, out var r) ? View(r, look, me) : null,
                    at = b.FinishedAt ?? b.CreatedAt,
                };
            }).ToList();

        // 当前状态 = 最近一场（按约战结束时间）写了战后记录的那条
        var latest = items.FirstOrDefault(i => i.@record != null);
        var current = latest == null ? null : new
        {
            statusTag = byBattle[latest.battleId].StatusTag,
            statusNote = byBattle[latest.battleId].StatusNote,
            battleId = latest.battleId,
            battleTitle = latest.title,
            at = latest.at,
        };
        return Ok(new { ocId, isAuthor = me != Guid.Empty && oc.AuthorId == me, current, items });
    }

    /// <summary>编年史事件对应的战后记录（编年史里显示「约战记录」卡片）。看不到这个事件的人拿到空列表。</summary>
    [HttpGet("event/{eventId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> OfEvent(Guid eventId)
    {
        var me = Me;
        var empty = Ok(new { items = Array.Empty<object>() });
        var ev = await context.WorldTimelineEvents.AsNoTracking().Where(e => e.Id == eventId)
            .Select(e => new { e.Id, e.ProjectId, e.TimelineId, e.Hidden }).FirstOrDefaultAsync();
        if (ev == null) return empty;
        // 和编年史本身一样的可见规则：能读这个世界；隐藏的事件、隐藏的线只给创建者和协作者看
        if (!await context.WorldProjects.AsNoTracking().ReadableBy(me).AnyAsync(p => p.Id == ev.ProjectId)) return empty;
        var lineHidden = await context.WorldTimelines.AsNoTracking().Where(t => t.Id == ev.TimelineId).Select(t => t.Hidden).FirstOrDefaultAsync();
        if ((ev.Hidden || lineHidden) && !(me != Guid.Empty && await context.WorldProjects.AsNoTracking().MemberOf(me).AnyAsync(p => p.Id == ev.ProjectId)))
            return empty;

        var records = await StoreSafeAsync(() => OfEventAsync(context, eventId));
        if (records.Count == 0) return empty;
        var battleIds = records.Select(r => r.BattleId).Distinct().ToList();
        var battles = (await context.Battles.AsNoTracking().Where(b => battleIds.Contains(b.Id))
            .Select(b => new { b.Id, b.Title, b.Result, b.BattleConfigJson, b.FinishedAt }).ToListAsync()).ToDictionary(b => b.Id);
        var parts = await context.BattleParticipants.AsNoTracking().Where(p => battleIds.Contains(p.BattleId)).ToListAsync();
        var ocIds = records.Select(r => r.OcId).Distinct().ToList();
        var ocs = (await context.StickmanCharacters.AsNoTracking().Where(c => ocIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Title, c.CoverUrl, c.Status, c.AuthorId }).ToListAsync()).ToDictionary(c => c.Id);
        var look = await LookupAsync(records, battleIds, me);

        return Ok(new
        {
            items = records.Where(r => battles.ContainsKey(r.BattleId)).Select(r =>
            {
                var b = battles[r.BattleId];
                var row = parts.FirstOrDefault(p => p.BattleId == r.BattleId && OcIdsOf(p).Contains(r.OcId));
                ocs.TryGetValue(r.OcId, out var oc);
                var ocVisible = oc != null && (oc.Status != "draft" || oc.AuthorId == me);
                return new
                {
                    battleId = b.Id,
                    battleTitle = b.Title,
                    finishedAt = b.FinishedAt,
                    outcome = OutcomeOf(b.Result, b.BattleConfigJson),
                    result = row?.Result,
                    userName = row?.UserName,
                    ocId = r.OcId,
                    ocName = ocVisible ? oc!.Title : "OC",
                    ocCover = ocVisible ? oc!.CoverUrl : null,
                    ocExists = ocVisible,
                    opponents = row == null ? new List<object>() : parts.Where(p => p.BattleId == r.BattleId && p.Id != row.Id && !SameTeam(p, row))
                        .Select(p => (object)new { userName = p.UserName, ocNames = OcNamesOf(p) }).ToList(),
                    @record = View(r, look, me),
                };
            }).ToList(),
        });
    }

    /// <summary>我发布过的站内视频作品（柴圈作品里类型是视频的），写战后记录时可以直接选</summary>
    [HttpGet("my-videos")]
    public async Task<IActionResult> MyVideos()
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized(new { message = "请先登录" });
        var items = await context.ChaiWorks.AsNoTracking()
            .Where(w => w.AuthorId == me && w.MediaType == "video" && w.Status == "published")
            .OrderByDescending(w => w.CreatedAt).Take(50)
            .Select(w => new { w.Id, w.Title, w.CoverUrl, w.MediaUrl, w.CreatedAt }).ToListAsync();
        return Ok(new { items });
    }

    // ======================================================================
    //  写
    // ======================================================================

    /// <summary>写 / 修改某个 OC 在这场约战的战后记录；可以顺便写进世界观编年史</summary>
    [HttpPut("battle/{battleId}/oc/{ocId:guid}")]
    public async Task<IActionResult> Save(string battleId, Guid ocId, [FromBody] SaveRequest body)
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized(new { message = "请先登录" });
        var check = await CheckWriterAsync(battleId, ocId, me);
        if (check.Error != null) return check.Error;
        var (battle, participant, oc) = (check.Battle!, check.Participant!, check.Oc!);

        // ---- 检查填的内容 ----
        var tag = OneLine(body.StatusTag);
        if (tag.Length == 0) return BadRequest(new { message = "请选一个战后状态（或者自己写一个）" });
        if (tag.Length > MaxTag) return BadRequest(new { message = $"战后状态最多 {MaxTag} 个字" });
        var note = string.IsNullOrWhiteSpace(body.StatusNote) ? null : body.StatusNote.Trim().Replace("\r\n", "\n");
        if (note != null && note.Length > MaxNote) return BadRequest(new { message = $"说明最多 {MaxNote} 个字" });
        string? videoUrl = null, videoTitle = null, videoCover = null;
        if (!string.IsNullOrWhiteSpace(body.VideoUrl))
        {
            videoUrl = body.VideoUrl.Trim();
            // 只收 http / https，防止 javascript: 之类的链接被别人点到
            if (!IsWebLink(videoUrl) || videoUrl.Length > MaxUrl) return BadRequest(new { message = "视频链接要以 http:// 或 https:// 开头" });
            videoTitle = string.IsNullOrWhiteSpace(body.VideoTitle) ? null : Clip(OneLine(body.VideoTitle), MaxVideoTitle);
            // 封面拿不到就不存（前端会显示占位图），存了也只收网页地址
            videoCover = IsWebLink(body.VideoCover) && body.VideoCover!.Trim().Length <= MaxUrl ? body.VideoCover.Trim() : null;
        }

        await UpsertAsync(context, battleId, ocId, me, tag, note, videoUrl, videoTitle, videoCover);
        var saved = (await GetAsync(context, battleId, ocId))!;

        // ---- 顺便写进编年史（可选）：失败不影响战后记录本身，已经存好了 ----
        string? warning = null, written = null;
        if (body.Chronicle != null)
        {
            if (saved.ChronicleEventId is Guid old && await context.WorldTimelineEvents.AsNoTracking().AnyAsync(e => e.Id == old))
                warning = "这条记录之前已经写进过编年史了，这次没有重复写（要改内容请到编年史里改）";
            else
            {
                try
                {
                    var result = await WriteChronicleAsync(battle, participant, oc.Id, oc.Title, saved, body.Chronicle, me);
                    if (result.Error != null) warning = "战后记录已保存，但没能写进编年史：" + result.Error;
                    else written = result.ProjectName;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "战后记录写进编年史失败 battle={Battle} oc={Oc}", battleId, ocId);
                    warning = "战后记录已保存，但写进编年史时出错了，可以稍后修改记录再勾选一次";
                }
                saved = (await GetAsync(context, battleId, ocId))!;
            }
        }

        var look = await LookupAsync(new List<Aftermath> { saved }, new List<string> { battleId }, me);
        return Ok(new
        {
            message = written != null ? $"已保存，并写进了世界观《{Clip(written, 40)}》的编年史" : "战后记录已保存",
            warning,
            @record = View(saved, look, me),
        });
    }

    /// <summary>删除战后记录：只删这一条；已经写进编年史的事件不会被删（提示用户去编年史里自己删）</summary>
    [HttpDelete("battle/{battleId}/oc/{ocId:guid}")]
    public async Task<IActionResult> Delete(string battleId, Guid ocId)
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized(new { message = "请先登录" });
        var existing = await GetAsync(context, battleId, ocId);
        if (existing == null) return NotFound(new { message = "这条战后记录已经不存在了" });
        var authorId = await context.StickmanCharacters.AsNoTracking().Where(c => c.Id == ocId).Select(c => (Guid?)c.AuthorId).FirstOrDefaultAsync();
        // OC 已经被删了的话，写记录的人自己也能删
        if (authorId != me && !(authorId == null && existing.UserId == me))
            return StatusCode(403, new { message = "只有 OC 的作者能删除它的战后记录" });

        await DeleteAsync(context, battleId, ocId);
        var eventKept = existing.ChronicleEventId is Guid e && await context.WorldTimelineEvents.AsNoTracking().AnyAsync(x => x.Id == e);
        return Ok(new
        {
            ok = true,
            message = eventKept ? "战后记录已删除。写进编年史的那件事还在，不需要的话请到世界观编年史里删除。" : "战后记录已删除",
        });
    }

    // ======================================================================
    //  写进编年史
    // ======================================================================

    private sealed record ChronicleResult(string? Error, string? ProjectName);

    /// <summary>
    /// 在世界观编年史里建一个事件。和编年史自己的「新建事件」一样：要有编辑权限、每个世界最多 5000 件事、记修改记录（可撤销）。
    /// 另外要求这个 OC 已经关联到这个世界观（在 OC 页「世界观」一栏关联）。
    /// </summary>
    private async Task<ChronicleResult> WriteChronicleAsync(Battle battle, BattleParticipant mine, Guid ocId, string ocTitle, Aftermath rec, ChronicleRequest req, Guid me)
    {
        var projectId = req.ProjectId;
        var project = await context.WorldProjects.AsNoTracking().Where(p => p.Id == projectId).Select(p => new { p.Id, p.Name, p.OwnerId }).FirstOrDefaultAsync();
        if (project == null) return new("世界观不存在", null);
        if (!await WorldAccess.CanEditProjectAsync(context, projectId, me)) return new("你对这个世界观没有编辑权限（要是创建者或编写者）", null);
        var links = await StickmanWorldStore.LinksOfCharacterAsync(context, ocId);
        if (!links.Any(l => l.ProjectId == projectId)) return new("这个 OC 还没有关联到这个世界观，先在 OC 页「世界观」一栏关联", null);
        if (await context.WorldTimelineEvents.CountAsync(e => e.ProjectId == projectId) >= 5000) return new("这个世界的编年史事件太多了（最多 5000 个）", null);

        var history = new WorldChronicleHistoryService(context, logger);

        // ---- 时间线：选了就用选的；没选用第一条；一条都没有就建一条「约战记录」 ----
        Guid timelineId;
        if (req.TimelineId is Guid chosen)
        {
            if (!await context.WorldTimelines.AsNoTracking().AnyAsync(t => t.Id == chosen && t.ProjectId == projectId))
                return new("选的时间线不存在了，请刷新后重新选", null);
            timelineId = chosen;
        }
        else
        {
            var first = await context.WorldTimelines.AsNoTracking().Where(t => t.ProjectId == projectId)
                .OrderBy(t => t.SortOrder).ThenBy(t => t.CreatedAt).Select(t => (Guid?)t.Id).FirstOrDefaultAsync();
            if (first is Guid f) timelineId = f;
            else
            {
                var line = new WorldTimeline { ProjectId = projectId, Name = "约战记录", Description = "柴圈约战的战后记录写进来时自动建的时间线", Status = WorldTimeline.StatusCanon, SortOrder = 1 };
                context.WorldTimelines.Add(line);
                await context.SaveChangesAsync();
                await history.RecordAsync(projectId, WorldChronicleHistoryService.TargetTimeline, line.Id, me, WorldChronicleHistoryService.Create,
                    line.Name, null, WorldChronicleHistoryService.ToJson(WorldChronicleHistoryService.Snap(line)), "写约战战后记录时自动新建");
                timelineId = line.Id;
            }
        }

        // ---- 标题、时间 ----
        var participants = await context.BattleParticipants.AsNoTracking().Where(p => p.BattleId == battle.Id).OrderBy(p => p.JoinedAt).ToListAsync();
        var title = Clip(OneLine(string.IsNullOrWhiteSpace(req.Title) ? DefaultTitle(battle.Title, participants) : req.Title), 200);
        if (title.Length == 0) title = Clip($"约战《{battle.Title}》", 200);
        if (req.Year is long y && Math.Abs(y) > MaxYear) return new("年份太大了", null);
        var dateText = string.IsNullOrWhiteSpace(req.DateText) ? null : Clip(OneLine(req.DateText), 100);

        // ---- 相关条目：这个 OC 在这个世界里对应的人物条目（复制 / 发布时记下的） ----
        var cardIds = new List<string>();
        try
        {
            var pairCards = (await StickmanWorldStore.CardLinksOfCharacterAsync(context, ocId)).Where(c => c.ProjectId == projectId).Select(c => c.CardId).ToList();
            if (pairCards.Count > 0)
                cardIds = (await context.WorldCards.AsNoTracking().Where(c => c.ProjectId == projectId && pairCards.Contains(c.Id)).Select(c => c.Id).ToListAsync())
                    .Select(g => g.ToString()).ToList();
        }
        catch (Exception ex) { logger.LogWarning(ex, "读取 OC 对应条目失败，事件不带相关条目"); }

        var ev = new WorldTimelineEvent
        {
            ProjectId = projectId,
            TimelineId = timelineId,
            Kind = WorldTimelineEvent.KindEvent,
            Title = title,
            Description = await DescribeAsync(battle, mine, participants, ocId, ocTitle, rec, req.LinkBase),
            Year = req.Year,
            DateText = dateText,
            Tags = JsonSerializer.Serialize(new List<string> { "约战", Clip(rec.StatusTag, 30) }.Distinct().ToList()),
            CardIds = JsonSerializer.Serialize(cardIds),
            Images = "[]",
            CreatedBy = me,
        };
        context.WorldTimelineEvents.Add(ev);
        await context.SaveChangesAsync();
        await history.RecordAsync(projectId, WorldChronicleHistoryService.TargetEvent, ev.Id, me, WorldChronicleHistoryService.Create,
            ev.Title, null, WorldChronicleHistoryService.ToJson(WorldChronicleHistoryService.Snap(ev)), "从约战战后记录写入");
        await SetEventAsync(context, rec.Id, projectId, ev.Id);

        // 世界观不是自己建的：告诉创建者一声（和 OC 关联世界观时的通知同一类）
        if (project.OwnerId != me)
        {
            try
            {
                if (new NotificationService(context).Add(project.OwnerId, me, "world_oc", "world", projectId.ToString(),
                        $"把约战《{Clip(battle.Title, 30)}》的战后记录（{Clip(ocTitle, 20)}：{rec.StatusTag}）写进了世界观《{Clip(project.Name, 30)}》的编年史",
                        $"/world/project/{projectId}?view=timeline&event={ev.Id}"))
                    await context.SaveChangesAsync();
            }
            catch (Exception ex) { logger.LogWarning(ex, "发送战后记录写入编年史的通知失败"); }
        }
        return new(null, project.Name);
    }

    /// <summary>事件内容：结果、战后状态、说明、视频链接、约战页链接（编年史事件没有视频字段，链接写在这里）</summary>
    private async Task<string> DescribeAsync(Battle battle, BattleParticipant mine, List<BattleParticipant> participants, Guid ocId, string ocTitle, Aftermath rec, string? linkBase)
    {
        var outcome = OutcomeOf(battle);
        var sb = new StringBuilder();
        sb.Append($"柴圈约战《{battle.Title}》已结束");
        if (OutcomeText(outcome) is { } o) sb.Append($"，结果：{o}");
        sb.Append("。\n");
        sb.Append($"出战：{ocTitle}（{mine.UserName}）— {ResultText(outcome, mine.Result)}\n");
        var others = participants.Where(p => p.Id != mine.Id && !SameTeam(p, mine)).ToList();
        if (others.Count > 0) sb.Append("对手：").Append(string.Join("、", others.Select(SideLabel))).Append('\n');
        sb.Append($"战后状态：{rec.StatusTag}\n");
        if (!string.IsNullOrWhiteSpace(rec.StatusNote)) sb.Append($"说明：{rec.StatusNote}\n");
        if (!string.IsNullOrWhiteSpace(rec.VideoUrl))
        {
            // 还没审核通过的约战作品链接不写进去（编年史谁都能看，不能绕过审核）
            var pending = (await UnapprovedAsync(new List<string> { battle.Id })).Contains(Key(battle.Id, rec.VideoUrl));
            sb.Append(pending ? "视频：作品还在审核，通过后可以在约战页面观看\n"
                : $"视频：{(string.IsNullOrWhiteSpace(rec.VideoTitle) ? "" : rec.VideoTitle + " ")}{rec.VideoUrl}\n");
        }
        if (SafeBase(linkBase) is { } site)
        {
            sb.Append($"约战页面：{site}battles/{battle.Id}\n");
            sb.Append($"OC 页面：{site}ocs/{ocId}\n");
        }
        return sb.ToString().TrimEnd();
    }

    // ======================================================================
    //  工具
    // ======================================================================

    private sealed record WriterCheck(IActionResult? Error, Battle? Battle = null, BattleParticipant? Participant = null, StickmanCharacter? Oc = null);

    /// <summary>写记录前的检查：约战已结束、当前用户是这个 OC 的作者、并且确实带这个 OC 参加了这场约战</summary>
    private async Task<WriterCheck> CheckWriterAsync(string battleId, Guid ocId, Guid me)
    {
        var battle = await context.Battles.AsNoTracking().Include(b => b.Participants).FirstOrDefaultAsync(b => b.Id == battleId);
        if (battle == null) return new(NotFound(new { message = "约战不存在" }));
        if (battle.Status != Finished) return new(BadRequest(new { message = "约战结束（公布结果）以后才能写战后记录" }));
        var oc = await context.StickmanCharacters.AsNoTracking().FirstOrDefaultAsync(c => c.Id == ocId);
        if (oc == null) return new(NotFound(new { message = "OC 角色不存在" }));
        if (oc.AuthorId != me) return new(StatusCode(403, new { message = "只有 OC 的作者能写它的战后记录" }));
        var participant = battle.Participants.FirstOrDefault(p => p.UserId == me && OcIdsOf(p).Contains(ocId));
        if (participant == null) return new(StatusCode(403, new { message = "这个 OC 没有参加这场约战" }));
        return new(null, battle, participant, oc);
    }

    /// <summary>读战后记录时顺便要用的东西：世界观名字、事件还在不在、站内视频作品、没审核通过的作品链接</summary>
    private sealed class Lookup
    {
        public Dictionary<Guid, string> ReadableProjects { get; init; } = new();
        public HashSet<Guid> VisibleEvents { get; init; } = new();
        public HashSet<Guid> ExistingEvents { get; init; } = new();
        public Dictionary<Guid, SiteWork> Works { get; init; } = new();
        public HashSet<string> Unapproved { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record SiteWork(Guid Id, string Title, string MediaUrl, string? CoverUrl, string MediaType);

    private async Task<Lookup> LookupAsync(List<Aftermath> records, List<string> battleIds, Guid me)
    {
        var projectIds = records.Where(r => r.ProjectId.HasValue).Select(r => r.ProjectId!.Value).Distinct().ToList();
        var readable = projectIds.Count == 0 ? new Dictionary<Guid, string>()
            : await context.WorldProjects.AsNoTracking().ReadableBy(me).Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);
        var members = projectIds.Count == 0 || me == Guid.Empty ? new HashSet<Guid>()
            : (await context.WorldProjects.AsNoTracking().MemberOf(me).Where(p => projectIds.Contains(p.Id)).Select(p => p.Id).ToListAsync()).ToHashSet();

        var eventIds = records.Where(r => r.ChronicleEventId.HasValue).Select(r => r.ChronicleEventId!.Value).Distinct().ToList();
        var events = await context.WorldTimelineEvents.AsNoTracking().Where(e => eventIds.Contains(e.Id))
            .Select(e => new { e.Id, e.ProjectId, e.TimelineId, e.Hidden }).ToListAsync();
        var lineIds = events.Select(e => e.TimelineId).Distinct().ToList();
        var hiddenLines = lineIds.Count == 0 ? new HashSet<Guid>()
            : (await context.WorldTimelines.AsNoTracking().Where(t => lineIds.Contains(t.Id) && t.Hidden).Select(t => t.Id).ToListAsync()).ToHashSet();
        // 和编年史自己一样：能读这个世界才看得到；隐藏的事件 / 线只给创建者和协作者
        var visible = events.Where(e => readable.ContainsKey(e.ProjectId) && (members.Contains(e.ProjectId) || (!e.Hidden && !hiddenLines.Contains(e.TimelineId))))
            .Select(e => e.Id).ToHashSet();

        var workIds = records.Select(r => WorkIdOf(r.VideoUrl)).Where(g => g.HasValue).Select(g => g!.Value).Distinct().ToList();
        var works = workIds.Count == 0 ? new Dictionary<Guid, SiteWork>()
            : (await context.ChaiWorks.AsNoTracking().Where(w => workIds.Contains(w.Id) && (w.Status == "published" || w.AuthorId == me))
                .Select(w => new { w.Id, w.Title, w.MediaUrl, w.CoverUrl, w.MediaType }).ToListAsync())
                .ToDictionary(w => w.Id, w => new SiteWork(w.Id, w.Title, w.MediaUrl, w.CoverUrl, w.MediaType));

        return new Lookup
        {
            ReadableProjects = readable,
            VisibleEvents = visible,
            ExistingEvents = events.Select(e => e.Id).ToHashSet(),
            Works = works,
            Unapproved = await UnapprovedAsync(battleIds),
        };
    }

    /// <summary>这些约战里还没审核通过的作品链接（「约战编号 + 链接」）：这些链接只给作者本人看，和约战页的规矩一样</summary>
    private async Task<HashSet<string>> UnapprovedAsync(List<string> battleIds)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (battleIds.Count == 0) return set;
        var subs = await context.BattleSubmissions.AsNoTracking().Where(s => battleIds.Contains(s.BattleId))
            .Select(s => new { s.Id, s.BattleId, s.ContentUrl }).ToListAsync();
        if (subs.Count == 0) return set;
        Dictionary<string, BattleReviewStore.Review> reviews;
        try { reviews = await BattleReviewStore.GetAsync(context, subs.Select(s => s.Id).ToList()); }
        catch (Exception) { reviews = new(); } // 审核表读不出来：都按待审核处理（宁可多藏）
        foreach (var s in subs)
            if (reviews.GetValueOrDefault(s.Id)?.Status != BattleReviewStore.Approved)
                set.Add(Key(s.BattleId, s.ContentUrl));
        return set;
    }

    /// <summary>给前端的一条战后记录。没审核通过的约战作品链接，除了写记录的人自己，别人看不到。</summary>
    private static object View(Aftermath r, Lookup look, Guid me)
    {
        var mine = me != Guid.Empty && r.UserId == me;
        var pending = !string.IsNullOrWhiteSpace(r.VideoUrl) && look.Unapproved.Contains(Key(r.BattleId, r.VideoUrl));
        var hideVideo = pending && !mine;
        var workId = hideVideo ? null : WorkIdOf(r.VideoUrl);
        var work = workId is Guid w && look.Works.TryGetValue(w, out var sw) ? sw : null;
        var projectVisible = r.ProjectId is Guid p && look.ReadableProjects.ContainsKey(p);
        var eventVisible = r.ChronicleEventId is Guid e && look.VisibleEvents.Contains(e);
        return new
        {
            id = r.Id,
            battleId = r.BattleId,
            ocId = r.OcId,
            userId = r.UserId,
            statusTag = r.StatusTag,
            statusNote = r.StatusNote,
            videoUrl = hideVideo ? null : r.VideoUrl,
            videoTitle = hideVideo ? null : r.VideoTitle,
            videoCover = hideVideo ? null : r.VideoCover,
            // 别人看：有视频但还在审核（链接藏起来）；作者看：自己的视频别人暂时看不到
            videoHidden = hideVideo,
            videoPending = pending && mine,
            siteWork = work == null ? null : new { id = work.Id, title = work.Title, mediaUrl = work.MediaUrl, coverUrl = work.CoverUrl, mediaType = work.MediaType },
            projectId = projectVisible || mine ? r.ProjectId : null,
            projectName = r.ProjectId is Guid pid && look.ReadableProjects.TryGetValue(pid, out var name) ? name : null,
            chronicleEventId = eventVisible || (mine && r.ChronicleEventId is Guid ce && look.ExistingEvents.Contains(ce)) ? r.ChronicleEventId : null,
            // 写过但事件已经被删了（作者可以重新写一次）
            eventDeleted = mine && r.ChronicleEventId is Guid de && !look.ExistingEvents.Contains(de),
            mine,
            createdAt = r.CreatedAt,
            updatedAt = r.UpdatedAt,
        };
    }

    /// <summary>表还没建好（启动建表失败）时当作没有记录，不让约战页、OC 页整个报错</summary>
    private async Task<List<Aftermath>> StoreSafeAsync(Func<Task<List<Aftermath>>> read)
    {
        try { return await read(); }
        catch (Exception ex) { logger.LogWarning(ex, "读取约战战后记录失败（battle_aftermath 表可能还没建好）"); return new(); }
    }

    private static string Key(string battleId, string? url) => battleId + "\n" + (url ?? "").Trim();

    private static Guid? WorkIdOf(string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return null;
        var m = WorkLink.Match(url);
        return m.Success && Guid.TryParse(m.Groups[1].Value, out var id) ? id : null;
    }

    private static List<Guid> OcIdsOf(BattleParticipant p)
    {
        try { return JsonSerializer.Deserialize<List<Guid>>(string.IsNullOrWhiteSpace(p.OcIdsJson) ? "[]" : p.OcIdsJson) ?? new(); }
        catch (JsonException) { return new(); }
    }

    private static List<string> OcNamesOf(BattleParticipant p)
    {
        try { return JsonSerializer.Deserialize<List<string>>(string.IsNullOrWhiteSpace(p.OcNamesJson) ? "[]" : p.OcNamesJson) ?? new(); }
        catch (JsonException) { return new(); }
    }

    /// <summary>团队战里同一队（队伍编号相同）算队友；没有队伍编号的都算各自一方</summary>
    private static bool SameTeam(BattleParticipant a, BattleParticipant b) => a.TeamNumber.HasValue && a.TeamNumber == b.TeamNumber;

    private static string SideLabel(BattleParticipant p)
    {
        var names = OcNamesOf(p).Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        return names.Count > 0 ? string.Join("、", names) : p.UserName;
    }

    /// <summary>默认事件标题：约战《xxx》：A vs B（团队战同队的用顿号连起来）</summary>
    private static string DefaultTitle(string battleTitle, List<BattleParticipant> participants)
    {
        var sides = participants.GroupBy(p => p.TeamNumber.HasValue ? "t" + p.TeamNumber.Value : "p" + p.Id)
            .Select(g => string.Join("、", g.Select(SideLabel))).Where(s => s.Length > 0).ToList();
        return sides.Count >= 2 ? $"约战《{battleTitle}》：{string.Join(" vs ", sides)}" : $"约战《{battleTitle}》";
    }

    // 结果种类：和 BattleController 的规矩一样（存在 BattleConfigJson.outcome；老约战按 Result 推断）
    private static readonly List<string> Outcomes = new() { "win", "pyrrhic", "draw", "mutual", "nocontest" };

    private static string OutcomeOf(Battle battle) => OutcomeOf(battle.Result, battle.BattleConfigJson);

    private static string OutcomeOf(string? result, string? configJson)
    {
        try
        {
            using var config = JsonDocument.Parse(string.IsNullOrWhiteSpace(configJson) ? "{}" : configJson);
            if (config.RootElement.ValueKind == JsonValueKind.Object && config.RootElement.TryGetProperty("outcome", out var v)
                && v.ValueKind == JsonValueKind.String && Outcomes.Contains(v.GetString()!))
                return v.GetString()!;
        }
        catch (JsonException) { }
        return result switch { "draw" => "draw", "completed" => "win", "nocontest" => "nocontest", _ => "" };
    }

    private static string? OutcomeText(string outcome) => outcome switch
    {
        "win" => "分出胜负", "pyrrhic" => "惨胜", "draw" => "平手", "mutual" => "同归于尽", "nocontest" => "无结果（中止）", _ => null,
    };

    private static string ResultText(string outcome, string? result) => result switch
    {
        "win" => outcome == "pyrrhic" ? "惨胜" : "胜",
        "lose" => "负",
        "draw" => outcome == "mutual" ? "同归于尽" : "平",
        _ => "不计胜负",
    };

    /// <summary>事件内容里链接用的网站地址：只认 http / https；前端传的是首页地址（哈希路由，比如 https://xx.com/#/），保证以 / 结尾</summary>
    private static string? SafeBase(string? linkBase)
    {
        if (string.IsNullOrWhiteSpace(linkBase) || linkBase.Length > 200) return null;
        var text = linkBase.Trim();
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return null;
        return text.EndsWith('/') ? text : text + "/";
    }

    private static bool IsWebLink(string? url)
        => Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var u) && (u.Scheme == Uri.UriSchemeHttp || u.Scheme == Uri.UriSchemeHttps);

    private static string OneLine(string? text) => Regex.Replace(text ?? "", @"\s*[\r\n]+\s*", " ").Trim();

    private static string Clip(string text, int max) => text.Length > max ? text[..max] : text;
}
