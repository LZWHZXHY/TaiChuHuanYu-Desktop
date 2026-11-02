using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.Pomodoro;

/// <summary>
/// 番茄钟两张表的读写（原生 SQL + 参数，不走 EF 映射，所以不用改 AppDbContext）。
/// 列名 Task、Round 加反引号，免得和 MySQL 的关键字 / 函数名撞上。
/// </summary>
public static class PomodoroStore
{
    public const string StateTable = "pomodoro_state";
    public const string SessionsTable = "pomodoro_sessions";

    public static readonly string[] Phases = ["", "focus", "short", "long"];
    /// <summary>毫秒时间戳的上限（2100 年），防止乱传超大的数</summary>
    public const long MaxMs = 4102444800000L;
    /// <summary>一段计时最长一天</summary>
    public const long MaxSegmentMs = 86_400_000L;
    public const int MaxTaskLength = 60;
    public const int MaxClientIdLength = 40;
    /// <summary>桌面程序多久没来问过就算「不在线」</summary>
    public static readonly TimeSpan DesktopOnlineWindow = TimeSpan.FromSeconds(90);

    /// <summary>设置存成 JSON 时用 camelCase，和接口里的字段名一致</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static readonly Regex DatePattern = new(@"^\d{4}-\d{2}-\d{2}$", RegexOptions.Compiled);

    // ---------------- 小工具：校验、换算 ----------------

    public static long ToMs(DateTime utc) => new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)).ToUnixTimeMilliseconds();

    /// <summary>截到 max 个字；不把 emoji 这种「两个 char 组成的字」切成半个</summary>
    public static string Cut(string? s, int max)
    {
        var t = (s ?? "").Trim();
        if (t.Length <= max) return t;
        var len = char.IsHighSurrogate(t[max - 1]) ? max - 1 : max;
        return t[..len];
    }

    /// <summary>生活管理事项 id：必须是 Guid，统一成小写带横线的格式；不是就当没关联</summary>
    public static string? NormGuid(string? s) =>
        Guid.TryParse((s ?? "").Trim(), out var g) ? g.ToString("D") : null;

    /// <summary>"YYYY-MM-DD"，而且得是真实存在的日期；不是就当没填</summary>
    public static string? NormDate(string? s)
    {
        var t = (s ?? "").Trim();
        return DatePattern.IsMatch(t) && DateOnly.TryParseExact(t, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? t : null;
    }

    public static string NormSource(string? s) =>
        string.Equals((s ?? "").Trim(), "desktop", StringComparison.OrdinalIgnoreCase) ? "desktop" : "web";

    public static PomoSettingsDto NormSettings(PomoSettingsDto s) => new()
    {
        FocusMin = Math.Clamp(s.FocusMin, 1, 240),
        ShortMin = Math.Clamp(s.ShortMin, 1, 120),
        LongMin = Math.Clamp(s.LongMin, 1, 240),
        LongEvery = Math.Clamp(s.LongEvery, 0, 12),
        AutoBreak = s.AutoBreak,
        AutoFocus = s.AutoFocus,
    };

    public static PomoSettingsDto? ParseSettings(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var s = JsonSerializer.Deserialize<PomoSettingsDto>(json, Json);
            return s == null ? null : NormSettings(s);
        }
        catch (JsonException) { return null; } // 存坏了就当没设置过，客户端用默认值
    }

    /// <summary>没有记录时的「待命」状态</summary>
    public static PomoStateDto IdleState() => new()
    {
        Phase = "", Task = "", StartedMs = 0, EndsMs = 0, PausedLeftMs = 0, TotalMs = 0, Round = 0,
        Next = "focus", PlannerEventId = null, PlannerDate = null, UpdatedMs = 0, Source = "",
    };

    // ---------------- 底层：开连接、执行（照 PlannerStore 的写法） ----------------

    private static async Task<T> WithConnectionAsync<T>(AppDbContext db, Func<DbConnection, Task<T>> work, CancellationToken ct)
    {
        var connection = db.Database.GetDbConnection();
        var opened = connection.State != ConnectionState.Open;
        if (opened) await connection.OpenAsync(ct);
        try { return await work(connection); }
        finally { if (opened) await connection.CloseAsync(); }
    }

    private static DbCommand Command(DbConnection connection, AppDbContext db, string sql, IEnumerable<(string Name, object? Value)> ps)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        // 如果 EF 正开着事务，命令要挂到同一个事务上，否则 MySqlConnector 会报错
        var tx = db.Database.CurrentTransaction?.GetDbTransaction();
        if (tx != null) command.Transaction = tx;
        foreach (var (name, value) in ps)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            command.Parameters.Add(p);
        }
        return command;
    }

    private static Task<int> ExecAsync(AppDbContext db, string sql, CancellationToken ct, IEnumerable<(string Name, object? Value)> ps) =>
        WithConnectionAsync(db, async connection =>
        {
            await using var command = Command(connection, db, sql, ps);
            return await command.ExecuteNonQueryAsync(ct);
        }, ct);

    // CHAR(36) 列 MySqlConnector 默认按 Guid 读出来；两种情况都兼容，统一成字符串
    private static string? GuidStr(DbDataReader r, int i)
    {
        if (r.IsDBNull(i)) return null;
        var v = r.GetValue(i);
        return v is Guid g ? g.ToString("D") : NormGuid(Convert.ToString(v, CultureInfo.InvariantCulture));
    }
    private static string? Str(DbDataReader r, int i) => r.IsDBNull(i) ? null : Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture);
    private static long Long(DbDataReader r, int i) => r.IsDBNull(i) ? 0L : Convert.ToInt64(r.GetValue(i), CultureInfo.InvariantCulture);
    private static int Int(DbDataReader r, int i) => r.IsDBNull(i) ? 0 : Convert.ToInt32(r.GetValue(i), CultureInfo.InvariantCulture);
    private static DateTime? Time(DbDataReader r, int i) => r.IsDBNull(i) ? null : DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc);

    // ---------------- 计时状态 ----------------

    private const string StateColumns = "Phase, `Task`, StartedMs, EndsMs, PausedLeftMs, TotalMs, `Round`, NextPhase, PlannerEventId, PlannerDate, " +
        "SettingsJson, Version, UpdatedMs, Source, DesktopSeenAt";

    public static Task<PomoStateRow?> GetStateAsync(AppDbContext db, Guid userId, CancellationToken ct) =>
        WithConnectionAsync(db, async connection =>
        {
            await using var command = Command(connection, db, $"SELECT {StateColumns} FROM {StateTable} WHERE UserId = @u LIMIT 1",
                [("@u", userId.ToString())]);
            await using var r = await command.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return (PomoStateRow?)null;
            return new PomoStateRow
            {
                State = new PomoStateDto
                {
                    Phase = Str(r, 0) ?? "",
                    Task = Str(r, 1) ?? "",
                    StartedMs = Long(r, 2),
                    EndsMs = Long(r, 3),
                    PausedLeftMs = Long(r, 4),
                    TotalMs = Long(r, 5),
                    Round = Int(r, 6),
                    Next = Str(r, 7) ?? "focus",
                    PlannerEventId = GuidStr(r, 8),
                    PlannerDate = Str(r, 9),
                    UpdatedMs = Long(r, 12),
                    Source = Str(r, 13) ?? "",
                },
                SettingsJson = Str(r, 10),
                Version = Long(r, 11),
                DesktopSeenAt = Time(r, 14),
            };
        }, ct);

    /// <summary>桌面程序来问了一次：记下时间（没有这一行就先建一行待命的，Version 还是 0）</summary>
    public static Task<int> TouchDesktopAsync(AppDbContext db, Guid userId, DateTime nowUtc, CancellationToken ct) =>
        ExecAsync(db, $@"INSERT INTO {StateTable} (UserId, Phase, `Task`, StartedMs, EndsMs, PausedLeftMs, TotalMs, `Round`, NextPhase,
Version, UpdatedMs, Source, DesktopSeenAt, UpdatedAt)
VALUES (@u, '', '', 0, 0, 0, 0, 0, 'focus', 0, 0, '', @now, @now)
ON DUPLICATE KEY UPDATE DesktopSeenAt = @now", ct, [("@u", userId.ToString()), ("@now", nowUtc)]);

    private static List<(string, object?)> StateParams(Guid userId, PomoStateDto s, string? settingsJson, string source, DateTime nowUtc, bool fromDesktop) =>
    [
        ("@u", userId.ToString()), ("@phase", s.Phase ?? ""), ("@task", s.Task ?? ""), ("@started", s.StartedMs), ("@ends", s.EndsMs),
        ("@paused", s.PausedLeftMs), ("@total", s.TotalMs), ("@round", s.Round), ("@next", s.Next ?? ""),
        ("@pe", s.PlannerEventId), ("@pd", s.PlannerDate), ("@settings", settingsJson), ("@ms", s.UpdatedMs), ("@src", source),
        ("@now", nowUtc), ("@desk", fromDesktop ? 1 : 0),
    ];

    /// <summary>
    /// 只有服务器上的版本号还是 baseVersion 时才写进去（同时版本号 +1）。返回改了几行：0 = 另一端抢先改过了。
    /// settingsJson 为 null 时保留原来的设置。桌面程序写的那次顺便算它「在线」。
    /// </summary>
    public static Task<int> UpdateStateAsync(AppDbContext db, Guid userId, long baseVersion, PomoStateDto s, string? settingsJson,
        string source, DateTime nowUtc, CancellationToken ct)
    {
        var ps = StateParams(userId, s, settingsJson, source, nowUtc, source == "desktop");
        ps.Add(("@base", baseVersion));
        return ExecAsync(db, $@"UPDATE {StateTable} SET Phase = @phase, `Task` = @task, StartedMs = @started, EndsMs = @ends,
PausedLeftMs = @paused, TotalMs = @total, `Round` = @round, NextPhase = @next, PlannerEventId = @pe, PlannerDate = @pd,
SettingsJson = COALESCE(@settings, SettingsJson), Version = Version + 1, UpdatedMs = @ms, Source = @src, UpdatedAt = @now,
DesktopSeenAt = CASE WHEN @desk = 1 THEN @now ELSE DesktopSeenAt END
WHERE UserId = @u AND Version = @base", ct, ps);
    }

    /// <summary>
    /// 这个人还没有记录时新建一行（版本号直接是 1）。两端同时第一次写的话，后到的那个撞主键，
    /// INSERT IGNORE 返回 0 行，按「冲突」处理。
    /// </summary>
    public static Task<int> InsertStateAsync(AppDbContext db, Guid userId, PomoStateDto s, string? settingsJson,
        string source, DateTime nowUtc, CancellationToken ct)
    {
        var ps = StateParams(userId, s, settingsJson, source, nowUtc, source == "desktop");
        return ExecAsync(db, $@"INSERT IGNORE INTO {StateTable} (UserId, Phase, `Task`, StartedMs, EndsMs, PausedLeftMs, TotalMs, `Round`, NextPhase,
PlannerEventId, PlannerDate, SettingsJson, Version, UpdatedMs, Source, DesktopSeenAt, UpdatedAt)
VALUES (@u, @phase, @task, @started, @ends, @paused, @total, @round, @next, @pe, @pd, @settings, 1, @ms, @src,
CASE WHEN @desk = 1 THEN @now ELSE NULL END, @now)", ct, ps);
    }

    // ---------------- 专注记录 ----------------

    /// <summary>
    /// 一次插入多条。(UserId, ClientId) 已经有了的跳过（INSERT IGNORE），所以不会覆盖原来的，
    /// 原来被删掉的也还是删掉的状态。返回这次真正新存了几条。
    /// </summary>
    public static Task<int> InsertSessionsAsync(AppDbContext db, Guid userId, IReadOnlyList<PomoSessionDto> list, DateTime nowUtc, CancellationToken ct)
    {
        if (list.Count == 0) return Task.FromResult(0);
        var sql = new StringBuilder($"INSERT IGNORE INTO {SessionsTable} (UserId, ClientId, StartMs, EndMs, FocusSec, `Task`, Done, PlannerEventId, PlannerDate, Source, CreatedAt) VALUES ");
        var ps = new List<(string, object?)> { ("@u", userId.ToString()), ("@now", nowUtc) };
        for (var i = 0; i < list.Count; i++)
        {
            var s = list[i];
            if (i > 0) sql.Append(", ");
            sql.Append($"(@u, @c{i}, @s{i}, @e{i}, @f{i}, @t{i}, @d{i}, @pe{i}, @pd{i}, @src{i}, @now)");
            ps.Add(($"@c{i}", s.ClientId));
            ps.Add(($"@s{i}", s.StartMs));
            ps.Add(($"@e{i}", s.EndMs));
            ps.Add(($"@f{i}", s.FocusSec));
            ps.Add(($"@t{i}", s.Task ?? ""));
            ps.Add(($"@d{i}", s.Done ? 1 : 0));
            ps.Add(($"@pe{i}", s.PlannerEventId));
            ps.Add(($"@pd{i}", s.PlannerDate));
            ps.Add(($"@src{i}", s.Source ?? "web"));
        }
        return ExecAsync(db, sql.ToString(), ct, ps);
    }

    /// <summary>这些 clientId 里，服务器上已经有的（包括被删掉的）——客户端据此把它们标成「已同步」</summary>
    public static Task<List<string>> ExistingClientIdsAsync(AppDbContext db, Guid userId, IReadOnlyList<string> clientIds, CancellationToken ct) =>
        WithConnectionAsync(db, async connection =>
        {
            var result = new List<string>();
            if (clientIds.Count == 0) return result;
            var ps = new List<(string, object?)> { ("@u", userId.ToString()) };
            var names = new List<string>();
            for (var i = 0; i < clientIds.Count; i++)
            {
                names.Add($"@c{i}");
                ps.Add(($"@c{i}", clientIds[i]));
            }
            await using var command = Command(connection, db,
                $"SELECT ClientId FROM {SessionsTable} WHERE UserId = @u AND ClientId IN ({string.Join(", ", names)})", ps);
            await using var r = await command.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var id = Str(r, 0);
                if (id != null) result.Add(id);
            }
            return result;
        }, ct);

    /// <summary>一段时间里（按开始时间）没删的记录，开始时间从早到晚</summary>
    public static Task<List<PomoSessionDto>> ListSessionsAsync(AppDbContext db, Guid userId, long fromMs, long toMs, int limit, CancellationToken ct) =>
        WithConnectionAsync(db, async connection =>
        {
            await using var command = Command(connection, db,
                $@"SELECT ClientId, StartMs, EndMs, FocusSec, `Task`, Done, PlannerEventId, PlannerDate, Source FROM {SessionsTable}
WHERE UserId = @u AND DeletedAt IS NULL AND StartMs >= @from AND StartMs < @to ORDER BY StartMs, Id LIMIT {limit}",
                [("@u", userId.ToString()), ("@from", fromMs), ("@to", toMs)]);
            await using var r = await command.ExecuteReaderAsync(ct);
            var list = new List<PomoSessionDto>();
            while (await r.ReadAsync(ct))
            {
                list.Add(new PomoSessionDto
                {
                    ClientId = Str(r, 0) ?? "",
                    StartMs = Long(r, 1),
                    EndMs = Long(r, 2),
                    FocusSec = Int(r, 3),
                    Task = Str(r, 4) ?? "",
                    Done = Int(r, 5) != 0,
                    PlannerEventId = GuidStr(r, 6),
                    PlannerDate = Str(r, 7),
                    Source = Str(r, 8) ?? "",
                });
            }
            return list;
        }, ct);

    /// <summary>软删除（只打标记）。已经删过的不再改删除时间。</summary>
    public static Task<int> SoftDeleteSessionAsync(AppDbContext db, Guid userId, string clientId, DateTime nowUtc, CancellationToken ct) =>
        ExecAsync(db, $"UPDATE {SessionsTable} SET DeletedAt = @now WHERE UserId = @u AND ClientId = @c AND DeletedAt IS NULL", ct,
            [("@u", userId.ToString()), ("@c", clientId), ("@now", nowUtc)]);
}
