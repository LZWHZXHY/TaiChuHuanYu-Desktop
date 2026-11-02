using System.Globalization;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Personalization;
using TaiChuWeb_V2.Services.Planner;

namespace TaiChuWeb_V2.Controllers.LingMai
{
    /// <summary>太初灵脉 · 生活管理（每个人一本自己的日历）。只看得到、只改得了自己的安排。</summary>
    public class PlannerEventInput
    {
        public string? Title { get; set; }
        /// <summary>"YYYY-MM-DD"</summary>
        public string? Date { get; set; }
        /// <summary>"HH:mm"，全天事项不填</summary>
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public bool AllDay { get; set; }
        public string? Color { get; set; }
        public string? Category { get; set; }
        public string? Memo { get; set; }
        public string? Location { get; set; }
        public bool? IsDone { get; set; }
        /// <summary>none / daily / weekly / monthly</summary>
        public string? Repeat { get; set; }
        public string? RepeatUntil { get; set; }
        /// <summary>提前多少分钟提醒；空 = 不提醒</summary>
        public int? RemindOffset { get; set; }
        /// <summary>自定义提醒时间（本地 "YYYY-MM-DDTHH:mm"），填了就以它为准</summary>
        public string? RemindAt { get; set; }
        public List<string>? Channels { get; set; }
        /// <summary>浏览器所在时区比 UTC 快多少分钟（北京时间 480）</summary>
        public int? TzOffset { get; set; }
    }

    public class PlannerDoneInput
    {
        public bool Done { get; set; }
        /// <summary>重复事项：勾的是哪一天（不填 = 整个事项）</summary>
        public string? Date { get; set; }
    }

    [ApiController]
    [Route("api/planner")]
    [Authorize]
    public class PlannerController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly SponsorPerkService _perks;
        private static readonly Regex ColorPattern = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

        public PlannerController(AppDbContext context, SponsorPerkService perks)
        {
            _context = context;
            _perks = perks;
        }

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        private const string ExpiredMessage = "赞助已到期，续费后恢复编辑和提醒，你的安排都还在";
        private static string NoAccessMessage => $"生活管理是{PlannerAccess.NeedTierName}及以上赞助专享";

        private IActionResult NoEdit(bool hasData) => StatusCode(403, new { message = hasData ? ExpiredMessage : NoAccessMessage, readOnly = hasData });

        // ---------------- 权限 ----------------

        /// <summary>前端进页面先问这个：能不能用、能不能改、能不能用邮件提醒</summary>
        [HttpGet("access")]
        public async Task<IActionResult> Access(CancellationToken ct)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var access = await PlannerAccess.CheckAsync(_context, _perks, uid, ct);
            bool hasData;
            try { hasData = await PlannerStore.HasDataAsync(_context, uid, ct); }
            catch { hasData = false; } // 表还没建好时当没有数据
            var email = await _context.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.Email).FirstOrDefaultAsync(ct);
            return Ok(new
            {
                canUse = access.CanEdit || hasData,
                canEdit = access.CanEdit,
                canEmail = access.CanEmail,
                readOnly = !access.CanEdit && hasData,
                needTier = PlannerAccess.NeedTierName,
                emailTier = PlannerAccess.EmailTierName,
                until = access.Until,
                tier = access.Tier,
                isAdmin = access.IsAdmin,
                hasData,
                hasEmailAddress = !string.IsNullOrWhiteSpace(email),
            });
        }

        // ---------------- 查看 ----------------

        /// <summary>
        /// 某段日期里的安排（from / to 都是 "YYYY-MM-DD"，都不填 = 全部，用于导出）。
        /// 重复事项只要在这段时间里可能出现就一起返回，具体哪几天由前端展开。
        /// 赞助到期但有数据的人也能看（只读）。
        /// </summary>
        [HttpGet("events")]
        public async Task<IActionResult> List([FromQuery] string? from, [FromQuery] string? to, CancellationToken ct)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var access = await PlannerAccess.CheckAsync(_context, _perks, uid, ct);
            if (!access.CanEdit && !await PlannerStore.HasDataAsync(_context, uid, ct))
                return StatusCode(403, new { message = NoAccessMessage });

            var fromDate = PlannerStore.ParseDate(from);
            var toDate = PlannerStore.ParseDate(to);
            var where = "UserId = @u AND DeletedAt IS NULL";
            var ps = new List<(string, object?)> { ("@u", uid.ToString()) };
            if (toDate != null)
            {
                where += " AND EventDate <= @to";
                ps.Add(("@to", toDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            }
            if (fromDate != null)
            {
                where += " AND (RepeatRule <> 'none' OR EventDate >= @from) AND (RepeatUntil IS NULL OR RepeatUntil >= @from)";
                ps.Add(("@from", fromDate.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            }
            where += $" ORDER BY EventDate, StartTime LIMIT {PlannerStore.MaxEventsPerUser}";
            var rows = await PlannerStore.QueryAsync(_context, where, ct, ps.ToArray());
            return Ok(rows.Select(ToDto));
        }

        // ---------------- 新建 / 修改 ----------------

        [HttpPost("events")]
        public async Task<IActionResult> Create([FromBody] PlannerEventInput input, CancellationToken ct)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var access = await PlannerAccess.CheckAsync(_context, _perks, uid, ct);
            if (!access.CanEdit) return NoEdit(await PlannerStore.HasDataAsync(_context, uid, ct));
            if (await PlannerStore.CountAsync(_context, uid, ct) >= PlannerStore.MaxEventsPerUser)
                return BadRequest(new { message = $"安排太多了（上限 {PlannerStore.MaxEventsPerUser} 条），先删掉一些旧的吧" });

            var now = DateTime.UtcNow;
            var row = new PlannerEventRow { Id = Guid.NewGuid(), UserId = uid, CreatedAt = now, UpdatedAt = now };
            var (error, notice) = Apply(row, input, access.CanEmail);
            if (error != null) return BadRequest(new { message = error });
            PlannerStore.Reschedule(row, now);
            await PlannerStore.InsertAsync(_context, row, ct);
            return Ok(new { item = ToDto(row), notice });
        }

        [HttpPut("events/{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] PlannerEventInput input, CancellationToken ct)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var access = await PlannerAccess.CheckAsync(_context, _perks, uid, ct);
            if (!access.CanEdit) return NoEdit(await PlannerStore.HasDataAsync(_context, uid, ct));
            var row = await PlannerStore.FindAsync(_context, uid, id, ct);
            if (row == null || row.DeletedAt != null) return NotFound(new { message = "没找到这条安排，可能已经删掉了" });

            // 时间或提醒设置变了 = 相当于新的一次安排，之前「已经提醒过」的记录作废
            var before = ScheduleKey(row);
            var (error, notice) = Apply(row, input, access.CanEmail);
            if (error != null) return BadRequest(new { message = error });
            if (ScheduleKey(row) != before) row.LastRemindedEventAt = null;
            row.UpdatedAt = DateTime.UtcNow;
            PlannerStore.Reschedule(row, row.UpdatedAt);
            await PlannerStore.UpdateAsync(_context, row, ct);
            return Ok(new { item = ToDto(row), notice });
        }

        /// <summary>勾选 / 取消完成。重复事项带 date = 只勾那一天。</summary>
        [HttpPatch("events/{id:guid}/done")]
        public async Task<IActionResult> Done(Guid id, [FromBody] PlannerDoneInput input, CancellationToken ct)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var access = await PlannerAccess.CheckAsync(_context, _perks, uid, ct);
            if (!access.CanEdit) return NoEdit(await PlannerStore.HasDataAsync(_context, uid, ct));
            var row = await PlannerStore.FindAsync(_context, uid, id, ct);
            if (row == null || row.DeletedAt != null) return NotFound(new { message = "没找到这条安排，可能已经删掉了" });

            var day = PlannerStore.ParseDate(input.Date);
            if (row.RepeatRule != "none" && day != null)
            {
                var set = PlannerStore.DoneSet(row.DoneDates);
                var key = day.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (input.Done) set.Add(key); else set.Remove(key);
                // 只留最近的 500 个，免得字段无限变长
                row.DoneDates = string.Join(",", set.OrderByDescending(x => x, StringComparer.Ordinal).Take(500).OrderBy(x => x, StringComparer.Ordinal));
            }
            else row.IsDone = input.Done;
            row.UpdatedAt = DateTime.UtcNow;
            PlannerStore.Reschedule(row, row.UpdatedAt);
            await PlannerStore.UpdateAsync(_context, row, ct);
            return Ok(new { item = ToDto(row) });
        }

        /// <summary>删除 = 软删除（只做标记，数据还在），前端可以马上「撤销」</summary>
        [HttpDelete("events/{id:guid}")]
        public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var access = await PlannerAccess.CheckAsync(_context, _perks, uid, ct);
            if (!access.CanEdit) return NoEdit(await PlannerStore.HasDataAsync(_context, uid, ct));
            var row = await PlannerStore.FindAsync(_context, uid, id, ct);
            if (row == null) return NotFound(new { message = "没找到这条安排" });
            if (row.DeletedAt != null) return Ok(new { ok = true });
            row.DeletedAt = row.UpdatedAt = DateTime.UtcNow;
            PlannerStore.Reschedule(row, row.UpdatedAt); // 删掉后不再提醒
            await PlannerStore.UpdateAsync(_context, row, ct);
            return Ok(new { ok = true });
        }

        /// <summary>撤销删除</summary>
        [HttpPost("events/{id:guid}/restore")]
        public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var access = await PlannerAccess.CheckAsync(_context, _perks, uid, ct);
            if (!access.CanEdit) return NoEdit(await PlannerStore.HasDataAsync(_context, uid, ct));
            var row = await PlannerStore.FindAsync(_context, uid, id, ct);
            if (row == null) return NotFound(new { message = "没找到这条安排" });
            row.DeletedAt = null;
            row.UpdatedAt = DateTime.UtcNow;
            PlannerStore.Reschedule(row, row.UpdatedAt);
            await PlannerStore.UpdateAsync(_context, row, ct);
            return Ok(new { item = ToDto(row) });
        }

        // ---------------- 浏览器弹窗提醒 ----------------

        /// <summary>
        /// 接下来十几分钟要弹的浏览器提醒（勾了「浏览器」的）。前端每分钟拉一次，到点自己弹。
        /// 赞助到期（只读）时提醒暂停，返回空列表。
        /// desktop=true 是桌面程序来拉：勾了「浏览器」或「站内」的都给它，由桌面弹系统通知（网页照旧只拿「浏览器」的）。
        /// </summary>
        [HttpGet("reminders/upcoming")]
        public async Task<IActionResult> Upcoming(CancellationToken ct, [FromQuery] bool desktop = false)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var access = await PlannerAccess.CheckAsync(_context, _perks, uid, ct);
            if (!access.CanEdit) return Ok(Array.Empty<object>());
            var now = DateTime.UtcNow;
            // 渠道条件是固定写死的两种，不拼用户输入
            var channelWhere = desktop ? "(Channels LIKE '%browser%' OR Channels LIKE '%site%')" : "Channels LIKE '%browser%'";
            var rows = await PlannerStore.QueryAsync(_context,
                $"UserId = @u AND DeletedAt IS NULL AND NextRemindAt IS NOT NULL AND NextRemindAt >= @a AND NextRemindAt <= @b AND {channelWhere} ORDER BY NextRemindAt LIMIT 50",
                ct, ("@u", uid.ToString()), ("@a", now.AddMinutes(-3)), ("@b", now.AddMinutes(15)));
            return Ok(rows.Select(r => new
            {
                id = r.Id,
                title = r.Title,
                location = r.Location,
                allDay = r.AllDay,
                eventAt = r.NextEventAt,
                remindAt = r.NextRemindAt,
                date = r.NextEventAt is DateTime ev ? PlannerStore.ToLocal(ev, r.TzOffset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : r.EventDate,
            }));
        }

        // ---------------- 工具 ----------------

        private static string ScheduleKey(PlannerEventRow r) =>
            $"{r.EventDate}|{r.StartTime}|{r.AllDay}|{r.RepeatRule}|{r.RepeatUntil}|{r.RemindOffset}|{r.RemindAt}|{r.TzOffset}";

        private static string Cut(string? s, int max)
        {
            var t = (s ?? "").Trim();
            return t.Length > max ? t[..max] : t;
        }

        /// <summary>把前端填的内容检查一遍再写进 row。返回（错误，提示）。</summary>
        private static (string? Error, string? Notice) Apply(PlannerEventRow row, PlannerEventInput input, bool canEmail)
        {
            var title = Cut(input.Title, 100);
            if (title.Length == 0) return ("标题不能为空", null);
            var date = PlannerStore.ParseDate(input.Date);
            if (date == null) return ("日期格式不对", null);

            string? start = null, end = null;
            if (!input.AllDay)
            {
                var s = PlannerStore.ParseTime(input.StartTime);
                if (s == null) return ("请填开始时间，或者勾选「全天」", null);
                start = s.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
                if (!string.IsNullOrWhiteSpace(input.EndTime))
                {
                    var e = PlannerStore.ParseTime(input.EndTime);
                    if (e == null) return ("结束时间格式不对", null);
                    if (e.Value < s.Value) return ("结束时间不能早于开始时间", null);
                    end = e.Value.ToString("HH:mm", CultureInfo.InvariantCulture);
                }
            }

            var repeat = (input.Repeat ?? "none").Trim().ToLowerInvariant();
            if (!PlannerStore.Repeats.Contains(repeat)) repeat = "none";
            var until = repeat == "none" ? null : PlannerStore.ParseDate(input.RepeatUntil);
            if (until != null && until.Value < date.Value) until = null;

            int? offset = null;
            string? remindAt = null;
            if (!string.IsNullOrWhiteSpace(input.RemindAt))
            {
                var custom = PlannerStore.ParseLocalDateTime(input.RemindAt);
                if (custom == null) return ("提醒时间格式不对", null);
                remindAt = custom.Value.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
            }
            else if (input.RemindOffset != null)
            {
                if (input.RemindOffset.Value < 0 || input.RemindOffset.Value > 60 * 24 * 30) return ("提醒最多提前 30 天", null);
                offset = input.RemindOffset.Value;
            }

            // 提醒方式：只认这三种；没有邮件特权的人选了邮件就去掉（不报错，告诉前端一声）
            string? notice = null;
            var channels = (input.Channels ?? new List<string>()).Select(c => (c ?? "").Trim().ToLowerInvariant())
                .Where(c => PlannerStore.AllChannels.Contains(c)).Distinct().ToList();
            if (channels.Contains("email") && !canEmail)
            {
                channels.Remove("email");
                notice = $"邮件提醒是{PlannerAccess.EmailTierName}赞助专属，这次先去掉了邮件，站内和浏览器提醒照常";
            }
            if (offset == null && remindAt == null) channels.Clear();

            var color = (input.Color ?? "").Trim();
            row.Title = title;
            row.EventDate = date.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            row.AllDay = input.AllDay;
            row.StartTime = start;
            row.EndTime = end;
            row.Color = ColorPattern.IsMatch(color) ? color : "";
            row.Category = Cut(input.Category, 20);
            var memo = (input.Memo ?? "").Trim();
            row.Memo = memo.Length == 0 ? null : (memo.Length > 5000 ? memo[..5000] : memo);
            row.Location = Cut(input.Location, 100);
            if (input.IsDone != null) row.IsDone = input.IsDone.Value;
            row.RepeatRule = repeat;
            row.RepeatUntil = until?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            // 重复规则改成「不重复」时，按天勾的完成记录留着也没用，但不删（改回来还能用）
            row.RemindOffset = offset;
            row.RemindAt = remindAt;
            row.Channels = string.Join(",", channels);
            if (input.TzOffset != null) row.TzOffset = Math.Clamp(input.TzOffset.Value, -720, 840);
            return (null, notice);
        }

        private static object ToDto(PlannerEventRow r) => new
        {
            id = r.Id,
            title = r.Title,
            date = r.EventDate,
            startTime = r.StartTime,
            endTime = r.EndTime,
            allDay = r.AllDay,
            color = r.Color,
            category = r.Category,
            memo = r.Memo ?? "",
            location = r.Location,
            isDone = r.IsDone,
            doneDates = PlannerStore.DoneSet(r.DoneDates).OrderBy(x => x, StringComparer.Ordinal).ToList(),
            repeat = r.RepeatRule,
            repeatUntil = r.RepeatUntil,
            remindOffset = r.RemindOffset,
            remindAt = r.RemindAt,
            channels = PlannerStore.ChannelList(r.Channels),
            nextRemindAt = r.NextRemindAt,
            createdAt = r.CreatedAt,
            updatedAt = r.UpdatedAt,
        };
    }
}
