using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.CommunityHistory;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services.Analytics;
using TaiChuWeb_V2.Services.Notify;

namespace TaiChuWeb_V2.Controllers;

/// <summary>
/// 岁月史书：社区的日常——群聊名场面和截图、生活里的事、大家的成果进步。
/// · 任何人（包括未登录访客）都能看；
/// · 登录用户都能上传，马上就能看到，记下上传时间；自己只能删除自己上传的，别人上传的动不了；
/// · 管理员（超级管理员或「太初官方」权限）可以下架 / 恢复 / 编辑任何一段，编辑会记下是谁、什么时候改的；
/// · 不发经验和土豆，靠表情回应和评论互动；评论也是自己删自己的，管理员能删任何一条。
/// </summary>
[ApiController]
[Route("api/history")]
public class HistoryController(AppDbContext db) : ControllerBase
{
    public sealed class EntryDto
    {
        public string? Kind { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public List<string>? Images { get; set; }
        /// <summary>发生在哪天，yyyy-MM-dd</summary>
        public string? HappenedOn { get; set; }
        public string? Precision { get; set; }
        /// <summary>@ 了谁（用户编号，最多 20 个）。编辑时传 null 表示不改</summary>
        public List<Guid>? Mentions { get; set; }
    }
    public sealed class HideDto { public string? Reason { get; set; } }
    public sealed class ReactDto { public string? Kind { get; set; } }
    public sealed class CommentDto { public string? Content { get; set; } public long? ReplyToId { get; set; } public List<string>? Images { get; set; } }

    private const int PageSize = 20;
    private const int DailyLimit = 30;
    private const int MaxImages = 12;
    private const int DailyCommentLimit = 200;
    /// <summary>列表里每段先带最近几条评论</summary>
    private const int PreviewComments = 2;
    private const int MaxMentions = 20;
    private const int MaxCommentImages = 4;

    private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private async Task<bool> IsManagerAsync()
    {
        if (CurrentUserId is not Guid me) return false;
        return await db.UserPermissions.AsNoTracking().AnyAsync(p => p.UserId == me
            && (p.Permission == AdminPermission.SuperAdmin || p.Permission == AdminPermission.TaiChu));
    }

    // ================= 看 =================

    /// <summary>
    /// 一页（20 段），按发生时间从新到旧。kind 类型、year 哪一年、q 搜标题和内容、mine 只看我上传的、author 只看某个人记的、
    /// person 和某个人有关的（他记的，或者 @ 了他的）。
    /// 第一页不带筛选时顺便给「那年今日」（往年的今天发生的事）。
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(string? kind, int? year, string? q, bool mine = false, Guid? author = null, Guid? person = null, int page = 1)
    {
        page = Math.Max(1, page);
        var me = CurrentUserId;
        var manager = await IsManagerAsync();
        var visible = Visible(me, manager);

        var query = visible;
        if (CommunityHistoryEntry.Kinds.Contains(kind)) query = query.Where(e => e.Kind == kind);
        if (year is int y && y is > 1900 and < 3000)
        {
            var from = new DateTime(y, 1, 1);
            var to = from.AddYears(1);
            query = query.Where(e => e.HappenedOn >= from && e.HappenedOn < to);
        }
        var keyword = (q ?? "").Trim();
        if (keyword.Length > 50) keyword = keyword[..50];
        if (keyword.Length > 0) query = query.Where(e => e.Title.Contains(keyword) || (e.Content != null && e.Content.Contains(keyword)));
        if (mine && me is Guid mineId) query = query.Where(e => e.UserId == mineId);
        // 某个人记的（个人主页「岁月史书」点进来）
        if (author is Guid authorId) query = query.Where(e => e.UserId == authorId);
        // 和某个人有关的：他记的 + @ 了他的（先查出 @ 他的段落编号，不做跨表子查询）
        string? personName = null;
        if (person is Guid personId)
        {
            var mentioned = await db.CommunityHistoryMentions.AsNoTracking().Where(m => m.UserId == personId).Select(m => m.EntryId).Take(5000).ToListAsync();
            query = query.Where(e => e.UserId == personId || mentioned.Contains(e.Id));
            personName = await db.Users.AsNoTracking().Where(u => u.Id == personId).Select(u => u.Username).FirstOrDefaultAsync();
        }

        var rows = await query.OrderByDescending(e => e.HappenedOn).ThenByDescending(e => e.Id)
            .Skip((page - 1) * PageSize).Take(PageSize + 1).ToListAsync();
        var hasMore = rows.Count > PageSize;
        rows = rows.Take(PageSize).ToList();

        // 年份目录（只按可见性，不受筛选影响）
        var years = await visible.GroupBy(e => e.HappenedOn.Year).Select(g => new { Year = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Year).ToListAsync();

        // 那年今日
        var onThisDay = new List<CommunityHistoryEntry>();
        var noFilter = page == 1 && kind == null && year == null && keyword.Length == 0 && !mine && author == null && person == null;
        if (noFilter)
        {
            var today = ChinaClock.Today;
            onThisDay = await visible.Where(e => e.DatePrecision == "day" && e.HappenedOn.Month == today.Month && e.HappenedOn.Day == today.Day && e.HappenedOn.Year < today.Year)
                .OrderByDescending(e => e.HappenedOn).Take(6).ToListAsync();
        }

        var views = await ViewsAsync(rows.Concat(onThisDay).DistinctBy(e => e.Id).ToList(), me, manager);
        return Ok(new
        {
            items = rows.Select(r => views[r.Id]),
            hasMore,
            years,
            onThisDay = onThisDay.Select(r => views[r.Id]),
            canPost = me != null,
            canManage = manager,
            dailyLimit = DailyLimit,
            person = person is Guid pid ? new { Id = pid, Username = personName ?? "已注销的用户" } : null,
        });
    }

    /// <summary>
    /// 首页「那年今日」：往年的今天（按北京时间）发生的事，最多 6 段；今天没有就退一步给「往年的这个月」。
    /// 只给公开的（下架的不出现），没有就返回空列表，首页不显示这一块。
    /// </summary>
    [HttpGet("on-this-day")]
    [AllowAnonymous]
    public async Task<IActionResult> OnThisDay()
    {
        var today = ChinaClock.Today;
        var visible = Visible(null, false);
        var mode = "day";
        var rows = await visible.Where(e => e.DatePrecision == "day" && e.HappenedOn.Month == today.Month && e.HappenedOn.Day == today.Day && e.HappenedOn.Year < today.Year)
            .OrderByDescending(e => e.HappenedOn).ThenByDescending(e => e.Id).Take(6).ToListAsync();
        if (rows.Count == 0)
        {
            mode = "month";
            rows = await visible.Where(e => e.DatePrecision != "year" && e.HappenedOn.Month == today.Month && e.HappenedOn.Year < today.Year)
                .OrderByDescending(e => e.HappenedOn).ThenByDescending(e => e.Id).Take(6).ToListAsync();
        }
        var views = await ViewsAsync(rows, CurrentUserId, false);
        return Ok(new { mode, today = today.ToString("yyyy-MM-dd"), items = rows.Select(r => views[r.Id]) });
    }

    /// <summary>
    /// 年鉴：某一年的全部（公开的）段落，按发生时间从早到晚，给前端排版打印 / 存成 PDF。最多 2000 段。
    /// </summary>
    [HttpGet("yearbook")]
    [AllowAnonymous]
    public async Task<IActionResult> Yearbook(int year)
    {
        if (year is < 1900 or > 3000) return BadRequest(new { message = "年份不对" });
        var from = new DateTime(year, 1, 1);
        var to = from.AddYears(1);
        var rows = await Visible(null, false).Where(e => e.HappenedOn >= from && e.HappenedOn < to)
            .OrderBy(e => e.HappenedOn).ThenBy(e => e.Id).Take(2000).ToListAsync();
        var views = await ViewsAsync(rows, CurrentUserId, false);
        var years = await Visible(null, false).GroupBy(e => e.HappenedOn.Year).Select(g => new { Year = g.Key, Count = g.Count() })
            .OrderByDescending(g => g.Year).ToListAsync();
        return Ok(new { year, items = rows.Select(r => views[r.Id]), years, contributors = rows.Select(r => r.UserId).Distinct().Count() });
    }

    [HttpGet("{id:long}")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(long id)
    {
        var me = CurrentUserId;
        var manager = await IsManagerAsync();
        var row = await Visible(me, manager).FirstOrDefaultAsync(e => e.Id == id);
        if (row == null) return NotFound(new { message = "这一段不存在，或者已经被删除 / 下架了" });
        return Ok((await ViewsAsync(new() { row }, me, manager))[row.Id]);
    }

    // ================= 上传 / 删除 =================

    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] EntryDto dto)
    {
        if (CurrentUserId is not Guid me) return Unauthorized();
        var dayStart = ChinaClock.DayStartUtc(ChinaClock.Today);
        if (await db.CommunityHistoryEntries.CountAsync(e => e.UserId == me && e.CreatedAt >= dayStart) >= DailyLimit)
            return BadRequest(new { message = $"今天已经记了 {DailyLimit} 段了，明天再来吧" });
        var entry = new CommunityHistoryEntry { UserId = me, CreatedAt = DateTime.UtcNow };
        var error = Apply(entry, dto);
        if (error != null) return BadRequest(new { message = error });
        db.CommunityHistoryEntries.Add(entry);
        await db.SaveChangesAsync();
        await SaveMentionsAsync(entry, dto.Mentions, me);
        return Ok((await ViewsAsync(new() { entry }, me, await IsManagerAsync()))[entry.Id]);
    }

    /// <summary>删除：上传的人删自己的，管理员能删任何一段</summary>
    [HttpDelete("{id:long}")]
    [Authorize]
    public async Task<IActionResult> Delete(long id)
    {
        if (CurrentUserId is not Guid me) return Unauthorized();
        var entry = await db.CommunityHistoryEntries.FirstOrDefaultAsync(e => e.Id == id);
        if (entry == null) return NotFound(new { message = "这一段不存在" });
        if (entry.UserId != me && !await IsManagerAsync()) return StatusCode(403, new { message = "只能删除自己上传的" });
        await db.CommunityHistoryReactions.Where(r => r.EntryId == id).ExecuteDeleteAsync();
        await db.CommunityHistoryComments.Where(c => c.EntryId == id).ExecuteDeleteAsync();
        await db.CommunityHistoryMentions.Where(m => m.EntryId == id).ExecuteDeleteAsync();
        db.CommunityHistoryEntries.Remove(entry);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // ================= 管理员：编辑 / 下架 / 恢复 =================

    [HttpPut("{id:long}")]
    [Authorize]
    public async Task<IActionResult> Edit(long id, [FromBody] EntryDto dto)
    {
        if (!await IsManagerAsync()) return StatusCode(403, new { message = "只有管理员能编辑别人上传的内容" });
        var entry = await db.CommunityHistoryEntries.FirstOrDefaultAsync(e => e.Id == id);
        if (entry == null) return NotFound(new { message = "这一段不存在" });
        var error = Apply(entry, dto);
        if (error != null) return BadRequest(new { message = error });
        entry.EditedBy = CurrentUserId;
        entry.EditedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        if (dto.Mentions != null) await SaveMentionsAsync(entry, dto.Mentions, CurrentUserId);
        return Ok((await ViewsAsync(new() { entry }, CurrentUserId, true))[entry.Id]);
    }

    [HttpPost("{id:long}/hide")]
    [Authorize]
    public async Task<IActionResult> Hide(long id, [FromBody] HideDto? dto)
    {
        if (!await IsManagerAsync()) return StatusCode(403, new { message = "只有管理员能下架" });
        var entry = await db.CommunityHistoryEntries.FirstOrDefaultAsync(e => e.Id == id);
        if (entry == null) return NotFound(new { message = "这一段不存在" });
        entry.Hidden = true;
        entry.HiddenReason = string.IsNullOrWhiteSpace(dto?.Reason) ? null : Clip(dto.Reason.Trim(), 200);
        entry.HiddenBy = CurrentUserId;
        entry.HiddenAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return Ok((await ViewsAsync(new() { entry }, CurrentUserId, true))[entry.Id]);
    }

    [HttpPost("{id:long}/unhide")]
    [Authorize]
    public async Task<IActionResult> Unhide(long id)
    {
        if (!await IsManagerAsync()) return StatusCode(403, new { message = "只有管理员能恢复" });
        var entry = await db.CommunityHistoryEntries.FirstOrDefaultAsync(e => e.Id == id);
        if (entry == null) return NotFound(new { message = "这一段不存在" });
        entry.Hidden = false;
        entry.HiddenReason = null;
        entry.HiddenBy = null;
        entry.HiddenAt = null;
        await db.SaveChangesAsync();
        return Ok((await ViewsAsync(new() { entry }, CurrentUserId, true))[entry.Id]);
    }

    // ================= 表情回应 =================

    /// <summary>点一下加上，再点一下取消</summary>
    [HttpPost("{id:long}/react")]
    [Authorize]
    public async Task<IActionResult> React(long id, [FromBody] ReactDto dto)
    {
        if (CurrentUserId is not Guid me) return Unauthorized();
        if (!CommunityHistoryReaction.Kinds.Contains(dto.Kind)) return BadRequest(new { message = "不认识这个表情" });
        if (!await Visible(me, await IsManagerAsync()).AnyAsync(e => e.Id == id)) return NotFound(new { message = "这一段不存在" });
        var existing = await db.CommunityHistoryReactions.FirstOrDefaultAsync(r => r.EntryId == id && r.UserId == me && r.Kind == dto.Kind);
        if (existing != null) db.CommunityHistoryReactions.Remove(existing);
        else db.CommunityHistoryReactions.Add(new CommunityHistoryReaction { EntryId = id, UserId = me, Kind = dto.Kind!, CreatedAt = DateTime.UtcNow });
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException) { /* 同时点了两下：以数据库里为准 */ }
        var (counts, mine) = await ReactionsAsync(new List<long> { id }, me);
        return Ok(new { reactions = counts.GetValueOrDefault(id) ?? new(), myReactions = mine.GetValueOrDefault(id) ?? new() });
    }

    // ================= 评论 =================

    /// <summary>一段的全部评论（从早到晚）</summary>
    [HttpGet("{id:long}/comments")]
    [AllowAnonymous]
    public async Task<IActionResult> Comments(long id)
    {
        var me = CurrentUserId;
        var manager = await IsManagerAsync();
        if (!await Visible(me, manager).AnyAsync(e => e.Id == id)) return NotFound(new { message = "这一段不存在，或者已经被删除 / 下架了" });
        var rows = await db.CommunityHistoryComments.AsNoTracking().Where(c => c.EntryId == id).OrderBy(c => c.Id).Take(1000).ToListAsync();
        return Ok(await CommentViewsAsync(rows, me, manager));
    }

    [HttpPost("{id:long}/comments")]
    [Authorize]
    public async Task<IActionResult> AddComment(long id, [FromBody] CommentDto dto)
    {
        if (CurrentUserId is not Guid me) return Unauthorized();
        var content = (dto.Content ?? "").Trim();
        var images = (dto.Images ?? new()).Select(u => (u ?? "").Trim())
            .Where(u => u.Length is > 0 and <= 500 && (u.StartsWith("https://") || u.StartsWith("http://"))).Distinct().ToList();
        if (images.Count > MaxCommentImages) return BadRequest(new { message = $"评论里最多放 {MaxCommentImages} 张图" });
        if (content.Length == 0 && images.Count == 0) return BadRequest(new { message = "写点什么或者发张图再发吧" });
        if (content.Length > 500) return BadRequest(new { message = "评论最多 500 个字" });
        var manager = await IsManagerAsync();
        var entry = await Visible(me, manager).FirstOrDefaultAsync(e => e.Id == id);
        if (entry == null) return NotFound(new { message = "这一段不存在，或者已经被删除 / 下架了" });
        var dayStart = ChinaClock.DayStartUtc(ChinaClock.Today);
        if (await db.CommunityHistoryComments.CountAsync(c => c.UserId == me && c.CreatedAt >= dayStart) >= DailyCommentLimit)
            return BadRequest(new { message = "今天评论得太多了，明天再来吧" });
        CommunityHistoryComment? replyTo = null;
        if (dto.ReplyToId is long rid)
        {
            replyTo = await db.CommunityHistoryComments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == rid && c.EntryId == id);
            if (replyTo == null) return BadRequest(new { message = "回复的那条评论已经被删除了" });
        }
        var comment = new CommunityHistoryComment { EntryId = id, UserId = me, Content = content, ReplyToId = replyTo?.Id, CreatedAt = DateTime.UtcNow,
            Images = images.Count == 0 ? null : JsonSerializer.Serialize(images) };
        db.CommunityHistoryComments.Add(comment);
        // 通知：这一段的上传者；回复了谁就通知谁（同一个人只通知一次，自己不通知自己）
        var notify = new NotificationService(db);
        var link = $"/history?entry={id}";
        var label = string.IsNullOrWhiteSpace(entry.Title) ? "你在岁月史书里记的一段" : $"你在岁月史书里记的「{Clip(entry.Title, 30)}」";
        var preview = content.Length > 0 ? Clip(content, 60) : "[图片]";
        if (replyTo != null && replyTo.UserId != entry.UserId)
            notify.Add(replyTo.UserId, me, "comment", "history", id.ToString(), $"在岁月史书里回复了你：{preview}", link);
        notify.Add(entry.UserId, me, "comment", "history", id.ToString(), replyTo != null && replyTo.UserId == entry.UserId ? $"回复了你：{preview}" : $"评论了{label}：{preview}", link);
        await db.SaveChangesAsync();
        return Ok((await CommentViewsAsync(new() { comment }, me, manager))[0]);
    }

    /// <summary>删除评论：自己删自己的，管理员能删任何一条</summary>
    [HttpDelete("comments/{commentId:long}")]
    [Authorize]
    public async Task<IActionResult> DeleteComment(long commentId)
    {
        if (CurrentUserId is not Guid me) return Unauthorized();
        var comment = await db.CommunityHistoryComments.FirstOrDefaultAsync(c => c.Id == commentId);
        if (comment == null) return NotFound(new { message = "这条评论不存在" });
        if (comment.UserId != me && !await IsManagerAsync()) return StatusCode(403, new { message = "只能删除自己的评论" });
        db.CommunityHistoryComments.Remove(comment);
        await db.SaveChangesAsync();
        return NoContent();
    }

    private async Task<List<object>> CommentViewsAsync(List<CommunityHistoryComment> rows, Guid? me, bool manager)
    {
        if (rows.Count == 0) return new();
        var replyIds = rows.Where(r => r.ReplyToId != null).Select(r => r.ReplyToId!.Value).Distinct().ToList();
        var replies = replyIds.Count == 0 ? new Dictionary<long, Guid>() : await db.CommunityHistoryComments.AsNoTracking()
            .Where(c => replyIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.UserId);
        var userIds = rows.Select(r => r.UserId).Concat(replies.Values).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, Avatar = u.Profile != null ? u.Profile.Avatar : null }).ToDictionaryAsync(u => u.Id);
        return rows.Select(r =>
        {
            var author = users.GetValueOrDefault(r.UserId);
            Guid? replyUser = r.ReplyToId is long rid && replies.TryGetValue(rid, out var ru) ? ru : null;
            return (object)new
            {
                r.Id,
                r.EntryId,
                r.Content,
                Images = r.Images == null ? new List<string>() : ReadImages(r.Images),
                CreatedAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc),
                Author = new { Id = r.UserId, Username = author?.Username ?? "已注销的用户", author?.Avatar },
                r.ReplyToId,
                ReplyTo = replyUser is Guid ruid ? new { Id = ruid, Username = users.GetValueOrDefault(ruid)?.Username ?? "已注销的用户" } : null,
                CanDelete = manager || (me is Guid id && id == r.UserId),
            };
        }).ToList();
    }

    // ================= 工具 =================

    /// <summary>能看到的：没下架的；下架的只有管理员和上传的人自己能看到</summary>
    private IQueryable<CommunityHistoryEntry> Visible(Guid? me, bool manager)
    {
        var q = db.CommunityHistoryEntries.AsNoTracking();
        if (manager) return q;
        if (me is Guid id) return q.Where(e => !e.Hidden || e.UserId == id);
        return q.Where(e => !e.Hidden);
    }

    private static string? Apply(CommunityHistoryEntry entry, EntryDto dto)
    {
        var title = (dto.Title ?? "").Trim();
        var content = (dto.Content ?? "").Trim();
        if (title.Length > 100) return "标题最多 100 个字";
        if (content.Length > 5000) return "内容最多 5000 个字";
        var images = (dto.Images ?? new()).Select(u => (u ?? "").Trim())
            .Where(u => u.Length is > 0 and <= 500 && (u.StartsWith("https://") || u.StartsWith("http://"))).Distinct().ToList();
        if (images.Count > MaxImages) return $"图片最多 {MaxImages} 张";
        if (title.Length == 0 && content.Length == 0 && images.Count == 0) return "写点什么，或者传张截图吧";
        if (!DateTime.TryParseExact(dto.HappenedOn ?? "", "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var happened))
            return "发生的日期不对";
        if (happened.Year < 1900) return "日期太早了";
        if (happened.Date > ChinaClock.Today.AddDays(1)) return "还没发生的事不能记哦";
        var precision = CommunityHistoryEntry.Precisions.Contains(dto.Precision) ? dto.Precision! : "day";
        // 只记得月 / 年：日期放到那个月 / 那年的第一天，排序时排在那个月 / 那年的最后
        if (precision == "month") happened = new DateTime(happened.Year, happened.Month, 1);
        if (precision == "year") happened = new DateTime(happened.Year, 1, 1);

        entry.Kind = CommunityHistoryEntry.Kinds.Contains(dto.Kind) ? dto.Kind! : "other";
        entry.Title = title;
        entry.Content = content.Length == 0 ? null : content;
        entry.Images = JsonSerializer.Serialize(images);
        entry.HappenedOn = DateTime.SpecifyKind(happened.Date, DateTimeKind.Unspecified);
        entry.DatePrecision = precision;
        return null;
    }

    private async Task<(Dictionary<long, Dictionary<string, int>> Counts, Dictionary<long, List<string>> Mine)> ReactionsAsync(List<long> ids, Guid? me)
    {
        var counts = new Dictionary<long, Dictionary<string, int>>();
        var mine = new Dictionary<long, List<string>>();
        if (ids.Count == 0) return (counts, mine);
        var rows = await db.CommunityHistoryReactions.AsNoTracking().Where(r => ids.Contains(r.EntryId))
            .Select(r => new { r.EntryId, r.UserId, r.Kind }).ToListAsync();
        foreach (var r in rows)
        {
            if (!counts.TryGetValue(r.EntryId, out var c)) counts[r.EntryId] = c = new();
            c[r.Kind] = c.GetValueOrDefault(r.Kind) + 1;
            if (me is Guid id && r.UserId == id)
            {
                if (!mine.TryGetValue(r.EntryId, out var m)) mine[r.EntryId] = m = new();
                m.Add(r.Kind);
            }
        }
        return (counts, mine);
    }

    private async Task<Dictionary<long, object>> ViewsAsync(List<CommunityHistoryEntry> rows, Guid? me, bool manager)
    {
        var result = new Dictionary<long, object>();
        if (rows.Count == 0) return result;
        var userIds = rows.Select(r => r.UserId).Concat(rows.Where(r => r.EditedBy != null).Select(r => r.EditedBy!.Value)).Distinct().ToList();
        var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, Avatar = u.Profile != null ? u.Profile.Avatar : null }).ToDictionaryAsync(u => u.Id);
        var ids = rows.Select(r => r.Id).ToList();
        var (counts, mine) = await ReactionsAsync(ids, me);
        // 评论数 + 每段最近两条：先只取编号（很轻），算出条数、挑出每段最后两条，再一次读出这些评论
        var commentIds = await db.CommunityHistoryComments.AsNoTracking().Where(c => ids.Contains(c.EntryId))
            .Select(c => new { c.Id, c.EntryId }).ToListAsync();
        var commentCounts = commentIds.GroupBy(c => c.EntryId).ToDictionary(g => g.Key, g => g.Count());
        var recentIds = commentIds.GroupBy(c => c.EntryId).SelectMany(g => g.OrderByDescending(c => c.Id).Take(PreviewComments)).Select(c => c.Id).ToList();
        var recentRows = recentIds.Count == 0 ? new List<CommunityHistoryComment>() : await db.CommunityHistoryComments.AsNoTracking()
            .Where(c => recentIds.Contains(c.Id)).OrderBy(c => c.Id).ToListAsync();
        var recentViews = await CommentViewsAsync(recentRows, me, manager);
        var recentByEntry = recentRows.Select((c, i) => (c.EntryId, View: recentViews[i])).GroupBy(x => x.EntryId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.View).ToList());
        // @ 了谁
        var mentionRows = await db.CommunityHistoryMentions.AsNoTracking().Where(m => ids.Contains(m.EntryId)).Select(m => new { m.EntryId, m.UserId }).ToListAsync();
        var mentionUserIds = mentionRows.Select(m => m.UserId).Where(u => !users.ContainsKey(u)).Distinct().ToList();
        var mentionNames = users.ToDictionary(kv => kv.Key, kv => kv.Value.Username);
        if (mentionUserIds.Count > 0)
            foreach (var u in await db.Users.AsNoTracking().Where(u => mentionUserIds.Contains(u.Id)).Select(u => new { u.Id, u.Username }).ToListAsync())
                mentionNames[u.Id] = u.Username;
        var mentionsByEntry = mentionRows.GroupBy(m => m.EntryId).ToDictionary(g => g.Key,
            g => g.Select(m => new { Id = m.UserId, Username = mentionNames.GetValueOrDefault(m.UserId) ?? "已注销的用户" }).ToList());
        foreach (var r in rows)
        {
            var author = users.GetValueOrDefault(r.UserId);
            result[r.Id] = new
            {
                r.Id,
                r.Kind,
                r.Title,
                r.Content,
                Images = ReadImages(r.Images),
                HappenedOn = r.HappenedOn.ToString("yyyy-MM-dd"),
                Precision = r.DatePrecision,
                CreatedAt = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc),
                Author = new { Id = r.UserId, Username = author?.Username ?? "已注销的用户", author?.Avatar },
                r.Hidden,
                HiddenReason = r.Hidden ? r.HiddenReason : null,
                EditedAt = r.EditedAt == null ? (DateTime?)null : DateTime.SpecifyKind(r.EditedAt.Value, DateTimeKind.Utc),
                EditedByName = r.EditedBy is Guid e ? users.GetValueOrDefault(e)?.Username ?? "管理员" : null,
                Reactions = counts.GetValueOrDefault(r.Id) ?? new(),
                MyReactions = mine.GetValueOrDefault(r.Id) ?? new(),
                CanDelete = manager || (me is Guid id && id == r.UserId),
                CommentCount = commentCounts.GetValueOrDefault(r.Id),
                Mentions = (object?)mentionsByEntry.GetValueOrDefault(r.Id) ?? Array.Empty<object>(),
                RecentComments = recentByEntry.GetValueOrDefault(r.Id) ?? new List<object>(),
            };
        }
        return result;
    }

    /// <summary>
    /// 保存一段里 @ 了谁（整组替换），新 @ 到的人收到通知（自己不通知自己）。只认真实存在的用户，最多 20 个。
    /// </summary>
    private async Task SaveMentionsAsync(CommunityHistoryEntry entry, List<Guid>? wanted, Guid? actor)
    {
        var ids = (wanted ?? new()).Where(g => g != Guid.Empty).Distinct().Take(MaxMentions).ToList();
        if (ids.Count > 0) ids = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => u.Id).ToListAsync();
        var existing = await db.CommunityHistoryMentions.Where(m => m.EntryId == entry.Id).ToListAsync();
        db.CommunityHistoryMentions.RemoveRange(existing.Where(m => !ids.Contains(m.UserId)));
        var added = ids.Where(id => existing.All(m => m.UserId != id)).ToList();
        foreach (var id in added) db.CommunityHistoryMentions.Add(new CommunityHistoryMention { EntryId = entry.Id, UserId = id, CreatedAt = DateTime.UtcNow });
        var notify = new NotificationService(db);
        var what = string.IsNullOrWhiteSpace(entry.Title) ? "一段事" : $"「{Clip(entry.Title, 30)}」";
        foreach (var id in added) notify.Add(id, actor, "mention", "history", entry.Id.ToString(), $"在岁月史书里记{what}时提到了你", $"/history?entry={entry.Id}");
        await db.SaveChangesAsync();
    }

    private static List<string> ReadImages(string? json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? new(); }
        catch (JsonException) { return new(); }
    }

    private static string Clip(string s, int max) => s.Length > max ? s[..max] : s;
}
