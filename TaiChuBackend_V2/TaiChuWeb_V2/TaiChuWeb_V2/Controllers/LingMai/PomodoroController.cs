using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Pomodoro;

namespace TaiChuWeb_V2.Controllers.LingMai
{
    /// <summary>
    /// 太初灵脉 · 番茄钟：网页版和桌面程序共用一份「现在在计时什么」，专注记录也存在账号上。
    /// 所有登录用户都能用（不是赞助特权）。只看得到、只改得了自己的。
    /// 字段名和桌面程序约好了，不要改；时间都是毫秒时间戳（UTC）。
    /// </summary>
    [ApiController]
    [Route("api/pomodoro")]
    [Authorize]
    public class PomodoroController : ControllerBase
    {
        private readonly AppDbContext _context;
        /// <summary>一次最多上传多少条记录</summary>
        private const int MaxUpload = 200;
        /// <summary>一次最多返回多少条记录</summary>
        private const int MaxList = 3000;
        private const long DayMs = 86_400_000L;

        public PomodoroController(AppDbContext context)
        {
            _context = context;
        }

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        // ---------------- 计时状态 ----------------

        /// <summary>
        /// 现在在计时什么。client=desktop 时顺便记下「桌面程序刚来过」，网页据此判断桌面在不在线
        /// （在线的话到点由桌面提醒，网页不重复响）。
        /// </summary>
        [HttpGet("state")]
        public async Task<IActionResult> GetState([FromQuery] string? client, CancellationToken ct)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var now = DateTime.UtcNow;
            if (string.Equals(client, "desktop", StringComparison.OrdinalIgnoreCase))
                await PomodoroStore.TouchDesktopAsync(_context, uid, now, ct);
            var row = await PomodoroStore.GetStateAsync(_context, uid, ct);
            return Ok(Snapshot(row, now));
        }

        /// <summary>
        /// 改计时状态。带上改之前看到的版本号 baseVersion：服务器上的版本号已经变了（另一端刚改过）就返回 409，
        /// 内容是服务器现在的状态，客户端照着它更新界面；没变就保存、版本号 +1。
        /// </summary>
        [HttpPut("state")]
        public async Task<IActionResult> PutState([FromBody] PomoStatePutInput input, CancellationToken ct)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            if (input?.State == null) return BadRequest(new { message = "缺少计时状态" });

            var source = PomodoroStore.NormSource(input.Source ?? input.State.Source);
            var (state, error) = NormalizeState(input.State, source);
            if (state == null) return BadRequest(new { message = error ?? "计时状态不对" });
            var settingsJson = input.Settings == null ? null : JsonSerializer.Serialize(PomodoroStore.NormSettings(input.Settings), PomodoroStore.Json);

            var now = DateTime.UtcNow;
            state.UpdatedMs = PomodoroStore.ToMs(now); // 以服务器时间为准，两端各自的时钟可能不准
            var changed = await PomodoroStore.UpdateStateAsync(_context, uid, input.BaseVersion, state, settingsJson, source, now, ct);
            // 版本号是 0 而且还没有这一行：第一次用，新建（撞主键 = 另一端刚好也在新建，也算冲突）
            if (changed == 0 && input.BaseVersion == 0)
                changed = await PomodoroStore.InsertStateAsync(_context, uid, state, settingsJson, source, now, ct);

            var row = await PomodoroStore.GetStateAsync(_context, uid, ct);
            if (changed == 0) return StatusCode(409, ConflictSnapshot(row, now));
            return Ok(Snapshot(row, now));
        }

        // ---------------- 专注记录 ----------------

        /// <summary>
        /// 上传专注记录（一次最多 200 条，多的这次先不收，客户端下次再传）。
        /// 同一个 clientId 已经有了就不覆盖（删掉的也保持删掉）。
        /// 返回的 clientIds = 这次传的里面服务器上已经有的全部（包括原来就有的），客户端据此标记「已同步」。
        /// </summary>
        [HttpPost("sessions")]
        public async Task<IActionResult> PostSessions([FromBody] PomoSessionsInput input, CancellationToken ct)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var list = new List<PomoSessionDto>();
            // 同一批里重复的 clientId 只留第一条（数据库的排序规则不分大小写，这里也不分）
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var raw in (input?.Sessions ?? new List<PomoSessionDto>()).Take(MaxUpload))
            {
                if (raw == null) continue;
                var s = NormalizeSession(raw);
                if (s?.ClientId == null || !seen.Add(s.ClientId)) continue;
                list.Add(s);
            }
            if (list.Count == 0) return Ok(new { saved = 0, clientIds = Array.Empty<string>() });

            var saved = await PomodoroStore.InsertSessionsAsync(_context, uid, list, DateTime.UtcNow, ct);
            var ids = await PomodoroStore.ExistingClientIdsAsync(_context, uid, list.Select(s => s.ClientId!).ToList(), ct);
            return Ok(new { saved, clientIds = ids });
        }

        /// <summary>
        /// 一段时间里的专注记录（按开始时间，毫秒时间戳，包含 fromMs、不包含 toMs）。都不传 = 最近 30 天。
        /// 从早到晚排，最多 3000 条。
        /// </summary>
        [HttpGet("sessions")]
        public async Task<IActionResult> ListSessions([FromQuery] long? fromMs, [FromQuery] long? toMs, CancellationToken ct)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var nowMs = PomodoroStore.ToMs(DateTime.UtcNow);
            long from, to;
            if (fromMs == null && toMs == null)
            {
                from = nowMs - 30 * DayMs;
                to = nowMs + DayMs; // 往后多放一天，客户端时钟快一点的记录也能查到
            }
            else
            {
                to = toMs ?? PomodoroStore.MaxMs;
                from = fromMs ?? to - 30 * DayMs;
            }
            if (from >= to) return Ok(Array.Empty<PomoSessionDto>());
            var list = await PomodoroStore.ListSessionsAsync(_context, uid, Math.Max(0, from), Math.Min(PomodoroStore.MaxMs + 1, to), MaxList, ct);
            return Ok(list);
        }

        /// <summary>删除一条记录（软删除，只打标记）。没有这一条也返回成功，重复点不会报错。</summary>
        [HttpDelete("sessions/{clientId}")]
        public async Task<IActionResult> DeleteSession(string clientId, CancellationToken ct)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var id = (clientId ?? "").Trim();
            if (id.Length == 0 || id.Length > PomodoroStore.MaxClientIdLength) return BadRequest(new { message = "记录编号不对" });
            await PomodoroStore.SoftDeleteSessionAsync(_context, uid, id, DateTime.UtcNow, ct);
            return Ok(new { ok = true });
        }

        // ---------------- 工具 ----------------

        /// <summary>GET / PUT 返回的统一结构</summary>
        private static object Snapshot(PomoStateRow? row, DateTime nowUtc) => new
        {
            state = row?.State ?? PomodoroStore.IdleState(),
            settings = PomodoroStore.ParseSettings(row?.SettingsJson),
            version = row?.Version ?? 0L,
            serverNowMs = PomodoroStore.ToMs(nowUtc),
            desktopOnline = IsDesktopOnline(row, nowUtc),
        };

        /// <summary>409 时多一个 conflict: true</summary>
        private static object ConflictSnapshot(PomoStateRow? row, DateTime nowUtc) => new
        {
            state = row?.State ?? PomodoroStore.IdleState(),
            settings = PomodoroStore.ParseSettings(row?.SettingsJson),
            version = row?.Version ?? 0L,
            serverNowMs = PomodoroStore.ToMs(nowUtc),
            desktopOnline = IsDesktopOnline(row, nowUtc),
            conflict = true,
        };

        private static bool IsDesktopOnline(PomoStateRow? row, DateTime nowUtc) =>
            row?.DesktopSeenAt is DateTime seen && nowUtc - seen <= PomodoroStore.DesktopOnlineWindow;

        /// <summary>检查客户端传来的状态：阶段只认那几个值，数字限制在合理范围，任务截 60 字，关联的事项 id / 日期格式不对就当没关联</summary>
        private static (PomoStateDto? State, string? Error) NormalizeState(PomoStateDto s, string source)
        {
            var phase = (s.Phase ?? "").Trim().ToLowerInvariant();
            if (!PomodoroStore.Phases.Contains(phase)) return (null, "阶段只能是 focus / short / long 或者空（待命）");
            var next = (s.Next ?? "").Trim().ToLowerInvariant();
            if (!PomodoroStore.Phases.Contains(next)) return (null, "下一段只能是 focus / short / long 或者空");
            return (new PomoStateDto
            {
                Phase = phase,
                Task = PomodoroStore.Cut(s.Task, PomodoroStore.MaxTaskLength),
                StartedMs = Math.Clamp(s.StartedMs, 0, PomodoroStore.MaxMs),
                EndsMs = Math.Clamp(s.EndsMs, 0, PomodoroStore.MaxMs),
                PausedLeftMs = Math.Clamp(s.PausedLeftMs, 0, PomodoroStore.MaxSegmentMs),
                TotalMs = Math.Clamp(s.TotalMs, 0, PomodoroStore.MaxSegmentMs),
                Round = Math.Clamp(s.Round, 0, 999),
                Next = next,
                PlannerEventId = PomodoroStore.NormGuid(s.PlannerEventId),
                PlannerDate = PomodoroStore.NormDate(s.PlannerDate),
                Source = source,
            }, null);
        }

        /// <summary>检查一条记录；clientId 为空或太长的没法去重，直接跳过（返回 null）</summary>
        private static PomoSessionDto? NormalizeSession(PomoSessionDto s)
        {
            var clientId = (s.ClientId ?? "").Trim();
            if (clientId.Length == 0 || clientId.Length > PomodoroStore.MaxClientIdLength) return null;
            var start = Math.Clamp(s.StartMs, 0, PomodoroStore.MaxMs);
            return new PomoSessionDto
            {
                ClientId = clientId,
                StartMs = start,
                // 结束不能早于开始
                EndMs = Math.Clamp(s.EndMs, start, PomodoroStore.MaxMs),
                FocusSec = Math.Clamp(s.FocusSec, 0, 86400),
                Task = PomodoroStore.Cut(s.Task, PomodoroStore.MaxTaskLength),
                Done = s.Done,
                PlannerEventId = PomodoroStore.NormGuid(s.PlannerEventId),
                PlannerDate = PomodoroStore.NormDate(s.PlannerDate),
                Source = PomodoroStore.NormSource(s.Source),
            };
        }
    }
}
