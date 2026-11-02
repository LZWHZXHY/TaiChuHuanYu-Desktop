using System.Data;
using System.Data.Common;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.Planner;

/// <summary>
/// 生活管理（个人日历）的一条安排。和表 planner_events 一一对应。
/// 日期、时间存的是用户自己那边的「本地」字符串（"2026-11-01"、"09:30"），和用户看到的一样，不做时区换算；
/// 只有「下一次什么时候提醒」这种要和服务器时钟比较的字段才换算成 UTC 存（NextEventAt / NextRemindAt）。
/// </summary>
public sealed class PlannerEventRow
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Title { get; set; } = "";
    /// <summary>日期 "YYYY-MM-DD"（重复事项 = 第一次的日期）</summary>
    public string EventDate { get; set; } = "";
    /// <summary>开始 / 结束时间 "HH:mm"；全天事项为空</summary>
    public string? StartTime { get; set; }
    public string? EndTime { get; set; }
    public bool AllDay { get; set; } = true;
    public string Color { get; set; } = "";
    public string Category { get; set; } = "";
    public string? Memo { get; set; }
    public string Location { get; set; } = "";
    public bool IsDone { get; set; }
    /// <summary>重复事项里已经勾完成的那几天，逗号分隔</summary>
    public string? DoneDates { get; set; }
    /// <summary>none / daily / weekly / monthly</summary>
    public string RepeatRule { get; set; } = "none";
    public string? RepeatUntil { get; set; }
    /// <summary>提前多少分钟提醒；空 = 不提醒（RemindAt 有值时以 RemindAt 为准）</summary>
    public int? RemindOffset { get; set; }
    /// <summary>自定义提醒时间（用户本地 "YYYY-MM-DDTHH:mm"）</summary>
    public string? RemindAt { get; set; }
    /// <summary>提醒方式：site,browser,email 逗号分隔</summary>
    public string Channels { get; set; } = "";
    /// <summary>用户时区比 UTC 快多少分钟（北京时间 = 480），保存时由浏览器带上来</summary>
    public int TzOffset { get; set; } = 480;
    public DateTime? NextEventAt { get; set; }
    public DateTime? NextRemindAt { get; set; }
    public int RemindTries { get; set; }
    public string RemindDelivered { get; set; } = "";
    public DateTime? LastRemindedEventAt { get; set; }
    public DateTime? LastRemindedAt { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>
/// planner_events 的读写（原生 SQL，不走 EF 映射，所以不用改 AppDbContext）。
/// 参数统一用 DbParameter 传，空值写成 DBNull，避免拼接 SQL。
/// </summary>
public static class PlannerStore
{
    public const string Table = "planner_events";
    public static readonly string[] Repeats = ["none", "daily", "weekly", "monthly"];
    public static readonly string[] AllChannels = ["site", "browser", "email"];
    /// <summary>全天事项的提醒按当天早上 9 点算（「提前 1 天」= 前一天早上 9 点）</summary>
    public static readonly TimeOnly AllDayRemindTime = new(9, 0);
    /// <summary>每个人最多存多少条（不含已删除）</summary>
    public const int MaxEventsPerUser = 5000;

    private const string Columns = "Id, UserId, Title, EventDate, StartTime, EndTime, AllDay, Color, Category, Memo, Location, IsDone, DoneDates, " +
        "RepeatRule, RepeatUntil, RemindOffset, RemindAt, Channels, TzOffset, NextEventAt, NextRemindAt, RemindTries, RemindDelivered, " +
        "LastRemindedEventAt, LastRemindedAt, DeletedAt, CreatedAt, UpdatedAt";

    // ---------------- 底层：开连接、执行 ----------------

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
        // 如果 EF 正开着事务，命令要挂到同一个事务上
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

    public static Task<int> ExecAsync(AppDbContext db, string sql, CancellationToken ct, params (string Name, object? Value)[] ps) =>
        WithConnectionAsync(db, async connection =>
        {
            await using var command = Command(connection, db, sql, ps);
            return await command.ExecuteNonQueryAsync(ct);
        }, ct);

    public static Task<long> ScalarAsync(AppDbContext db, string sql, CancellationToken ct, params (string Name, object? Value)[] ps) =>
        WithConnectionAsync(db, async connection =>
        {
            await using var command = Command(connection, db, sql, ps);
            var value = await command.ExecuteScalarAsync(ct);
            return value == null || value is DBNull ? 0L : Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }, ct);

    /// <summary>按条件查安排。where 里用 @参数名。</summary>
    public static Task<List<PlannerEventRow>> QueryAsync(AppDbContext db, string where, CancellationToken ct, params (string Name, object? Value)[] ps) =>
        WithConnectionAsync(db, async connection =>
        {
            await using var command = Command(connection, db, $"SELECT {Columns} FROM {Table} WHERE {where}", ps);
            await using var reader = await command.ExecuteReaderAsync(ct);
            var list = new List<PlannerEventRow>();
            while (await reader.ReadAsync(ct)) list.Add(Read(reader));
            return list;
        }, ct);

    public static async Task<PlannerEventRow?> FindAsync(AppDbContext db, Guid userId, Guid id, CancellationToken ct) =>
        (await QueryAsync(db, "Id = @id AND UserId = @u LIMIT 1", ct, ("@id", id.ToString()), ("@u", userId.ToString()))).FirstOrDefault();

    public static async Task<bool> HasDataAsync(AppDbContext db, Guid userId, CancellationToken ct) =>
        await ScalarAsync(db, $"SELECT COUNT(*) FROM {Table} WHERE UserId = @u AND DeletedAt IS NULL", ct, ("@u", userId.ToString())) > 0;

    public static Task<long> CountAsync(AppDbContext db, Guid userId, CancellationToken ct) =>
        ScalarAsync(db, $"SELECT COUNT(*) FROM {Table} WHERE UserId = @u AND DeletedAt IS NULL", ct, ("@u", userId.ToString()));

    // CHAR(36) 列 MySqlConnector 默认按 Guid 读出来；这里两种情况都兼容
    private static Guid ReadGuid(DbDataReader r, int i)
    {
        var v = r.GetValue(i);
        return v is Guid g ? g : Guid.Parse(Convert.ToString(v, CultureInfo.InvariantCulture) ?? "");
    }
    private static string? Str(DbDataReader r, int i) => r.IsDBNull(i) ? null : Convert.ToString(r.GetValue(i), CultureInfo.InvariantCulture);
    private static DateTime? Time(DbDataReader r, int i) => r.IsDBNull(i) ? null : DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc);
    private static bool Bool(DbDataReader r, int i) => !r.IsDBNull(i) && Convert.ToInt32(r.GetValue(i), CultureInfo.InvariantCulture) != 0;
    private static int? Int(DbDataReader r, int i) => r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i), CultureInfo.InvariantCulture);

    private static PlannerEventRow Read(DbDataReader r) => new()
    {
        Id = ReadGuid(r, 0),
        UserId = ReadGuid(r, 1),
        Title = Str(r, 2) ?? "",
        EventDate = Str(r, 3) ?? "",
        StartTime = Str(r, 4),
        EndTime = Str(r, 5),
        AllDay = Bool(r, 6),
        Color = Str(r, 7) ?? "",
        Category = Str(r, 8) ?? "",
        Memo = Str(r, 9),
        Location = Str(r, 10) ?? "",
        IsDone = Bool(r, 11),
        DoneDates = Str(r, 12),
        RepeatRule = Str(r, 13) ?? "none",
        RepeatUntil = Str(r, 14),
        RemindOffset = Int(r, 15),
        RemindAt = Str(r, 16),
        Channels = Str(r, 17) ?? "",
        TzOffset = Int(r, 18) ?? 480,
        NextEventAt = Time(r, 19),
        NextRemindAt = Time(r, 20),
        RemindTries = Int(r, 21) ?? 0,
        RemindDelivered = Str(r, 22) ?? "",
        LastRemindedEventAt = Time(r, 23),
        LastRemindedAt = Time(r, 24),
        DeletedAt = Time(r, 25),
        CreatedAt = Time(r, 26) ?? DateTime.UtcNow,
        UpdatedAt = Time(r, 27) ?? DateTime.UtcNow,
    };

    private static (string, object?)[] AllParams(PlannerEventRow e) =>
    [
        ("@Id", e.Id.ToString()), ("@UserId", e.UserId.ToString()), ("@Title", e.Title), ("@EventDate", e.EventDate),
        ("@StartTime", e.StartTime), ("@EndTime", e.EndTime), ("@AllDay", e.AllDay), ("@Color", e.Color), ("@Category", e.Category),
        ("@Memo", e.Memo), ("@Location", e.Location), ("@IsDone", e.IsDone), ("@DoneDates", e.DoneDates), ("@RepeatRule", e.RepeatRule),
        ("@RepeatUntil", e.RepeatUntil), ("@RemindOffset", e.RemindOffset), ("@RemindAt", e.RemindAt), ("@Channels", e.Channels),
        ("@TzOffset", e.TzOffset), ("@NextEventAt", e.NextEventAt), ("@NextRemindAt", e.NextRemindAt), ("@RemindTries", e.RemindTries),
        ("@RemindDelivered", e.RemindDelivered), ("@LastRemindedEventAt", e.LastRemindedEventAt), ("@LastRemindedAt", e.LastRemindedAt),
        ("@DeletedAt", e.DeletedAt), ("@CreatedAt", e.CreatedAt), ("@UpdatedAt", e.UpdatedAt),
    ];

    public static Task<int> InsertAsync(AppDbContext db, PlannerEventRow e, CancellationToken ct) =>
        ExecAsync(db, $@"INSERT INTO {Table} ({Columns}) VALUES (@Id, @UserId, @Title, @EventDate, @StartTime, @EndTime, @AllDay, @Color, @Category,
@Memo, @Location, @IsDone, @DoneDates, @RepeatRule, @RepeatUntil, @RemindOffset, @RemindAt, @Channels, @TzOffset, @NextEventAt, @NextRemindAt,
@RemindTries, @RemindDelivered, @LastRemindedEventAt, @LastRemindedAt, @DeletedAt, @CreatedAt, @UpdatedAt)", ct, AllParams(e));

    /// <summary>整行写回（Id、UserId、CreatedAt 不变）。带上 UserId 条件，防止改到别人的。</summary>
    public static Task<int> UpdateAsync(AppDbContext db, PlannerEventRow e, CancellationToken ct) =>
        ExecAsync(db, $@"UPDATE {Table} SET Title = @Title, EventDate = @EventDate, StartTime = @StartTime, EndTime = @EndTime, AllDay = @AllDay,
Color = @Color, Category = @Category, Memo = @Memo, Location = @Location, IsDone = @IsDone, DoneDates = @DoneDates, RepeatRule = @RepeatRule,
RepeatUntil = @RepeatUntil, RemindOffset = @RemindOffset, RemindAt = @RemindAt, Channels = @Channels, TzOffset = @TzOffset,
NextEventAt = @NextEventAt, NextRemindAt = @NextRemindAt, RemindTries = @RemindTries, RemindDelivered = @RemindDelivered,
LastRemindedEventAt = @LastRemindedEventAt, LastRemindedAt = @LastRemindedAt, DeletedAt = @DeletedAt, UpdatedAt = @UpdatedAt
WHERE Id = @Id AND UserId = @UserId", ct, AllParams(e));

    // ---------------- 日期 / 重复 / 提醒时间计算 ----------------

    public static DateOnly? ParseDate(string? s) =>
        DateOnly.TryParseExact(s ?? "", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;
    public static TimeOnly? ParseTime(string? s) =>
        TimeOnly.TryParseExact(s ?? "", "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var t) ? t : null;
    public static DateTime? ParseLocalDateTime(string? s) =>
        DateTime.TryParseExact(s ?? "", "yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    public static HashSet<string> DoneSet(string? doneDates) =>
        new((doneDates ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries), StringComparer.Ordinal);

    public static List<string> ChannelList(string? channels) =>
        (channels ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(c => AllChannels.Contains(c)).Distinct().ToList();

    /// <summary>第 k 次发生的日期。按月重复遇到小月会落在月底（1 月 31 日 → 2 月 28 日），和前端算法一致。</summary>
    public static DateOnly Occurrence(DateOnly anchor, string rule, int k) => rule switch
    {
        "daily" => anchor.AddDays(k),
        "weekly" => anchor.AddDays(7 * k),
        "monthly" => anchor.AddMonths(k),
        _ => anchor,
    };

    /// <summary>
    /// 算「下一次提醒」：返回（这一次的开始时间 UTC，提醒时间 UTC）；不需要提醒就返回两个 null。
    /// notBefore：提醒时间早于它的不要（已经过去的不补发）。
    /// after：这一次的开始时间必须晚于它（已经提醒过的那一次不再提醒）。
    /// </summary>
    public static (DateTime? EventAt, DateTime? RemindAt) ComputeNext(PlannerEventRow e, DateTime notBefore, DateTime? after)
    {
        if (e.DeletedAt != null || e.IsDone) return (null, null);
        if (ChannelList(e.Channels).Count == 0) return (null, null);
        var anchor = ParseDate(e.EventDate);
        if (anchor == null) return (null, null);
        var time = (e.AllDay ? null : ParseTime(e.StartTime)) ?? AllDayRemindTime;

        // 提前量（分钟）：自定义时间 = 第一次的开始时间 - 自定义时间
        double lead;
        var custom = ParseLocalDateTime(e.RemindAt);
        if (custom != null) lead = (anchor.Value.ToDateTime(time) - custom.Value).TotalMinutes;
        else if (e.RemindOffset != null) lead = e.RemindOffset.Value;
        else return (null, null);

        var tz = TimeSpan.FromMinutes(e.TzOffset);
        var rule = Repeats.Contains(e.RepeatRule) ? e.RepeatRule : "none";
        var until = ParseDate(e.RepeatUntil);
        var done = DoneSet(e.DoneDates);

        // 本地时间 → UTC：减去时区偏移
        DateTime ToUtc(DateOnly d) => DateTime.SpecifyKind(d.ToDateTime(time) - tz, DateTimeKind.Utc);

        if (rule == "none")
        {
            var ev = ToUtc(anchor.Value);
            var rm = ev.AddMinutes(-lead);
            if (rm < notBefore || (after != null && ev <= after.Value)) return (null, null);
            return (ev, rm);
        }

        // 重复事项：先粗略跳到「现在」附近，再一个个往后找，最多找 1000 次
        var start = 0;
        var anchorUtc = ToUtc(anchor.Value);
        var from = after != null && after.Value > notBefore ? after.Value : notBefore;
        if (from > anchorUtc)
        {
            var days = (from - anchorUtc).TotalDays;
            start = rule switch
            {
                "daily" => (int)Math.Max(0, Math.Floor(days) - 2),
                "weekly" => (int)Math.Max(0, Math.Floor(days / 7) - 2),
                _ => (int)Math.Max(0, Math.Floor(days / 31) - 2),
            };
        }
        for (var k = start; k < start + 1000; k++)
        {
            var d = Occurrence(anchor.Value, rule, k);
            if (until != null && d > until.Value) break;
            if (done.Contains(d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))) continue;
            var ev = ToUtc(d);
            if (after != null && ev <= after.Value) continue;
            var rm = ev.AddMinutes(-lead);
            if (rm < notBefore) continue;
            return (ev, rm);
        }
        return (null, null);
    }

    /// <summary>按当前设置重新排好下一次提醒（保存、勾完成、发完提醒后都调用）</summary>
    public static void Reschedule(PlannerEventRow e, DateTime now)
    {
        var (ev, rm) = ComputeNext(e, now.AddMinutes(-2), e.LastRemindedEventAt);
        e.NextEventAt = ev;
        e.NextRemindAt = rm;
        e.RemindTries = 0;
        e.RemindDelivered = "";
    }

    /// <summary>用户本地时间（给通知、邮件里显示用）</summary>
    public static DateTime ToLocal(DateTime utc, int tzOffset) => utc.AddMinutes(tzOffset);
}
