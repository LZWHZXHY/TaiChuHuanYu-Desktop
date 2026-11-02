using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.ChaiCommunity;

/// <summary>
/// 约战「战后记录」：约战结束后，参赛者给自己出战的每个 OC 写一条——战后状态（轻伤 / 重伤 / 觉醒……）、一段说明、一个视频链接，
/// 可选地写进世界观编年史（ChronicleEventId 记下建出来的事件）。
/// 单独一张新表 battle_aftermath（启动时由 BattleAftermathSchema 建），不改 OC、约战、编年史原有的任何表和列：
/// OC 的「当前状态」不存在 OC 表里，而是取这张表里这个 OC 最近一场约战的那条记录。
/// 都用原生 SQL 读写（和 StickmanWorldStore 一样），不需要改 AppDbContext；只按自己的列查，不和别的表 join（排序规则可能不同）。
/// </summary>
public static class BattleAftermathStore
{
    public sealed record Aftermath(
        long Id, string BattleId, Guid OcId, Guid UserId, string StatusTag, string? StatusNote,
        string? VideoUrl, string? VideoTitle, string? VideoCover, Guid? ProjectId, Guid? ChronicleEventId,
        DateTime CreatedAt, DateTime UpdatedAt);

    private const string Columns = "Id, BattleId, OcId, UserId, StatusTag, StatusNote, VideoUrl, VideoTitle, VideoCover, ProjectId, ChronicleEventId, CreatedAt, UpdatedAt";

    public static Task<List<Aftermath>> OfBattleAsync(AppDbContext db, string battleId) =>
        QueryAsync(db, $"SELECT {Columns} FROM battle_aftermath WHERE BattleId = @b ORDER BY Id", Read, ("@b", battleId));

    /// <summary>一个 OC 的全部战后记录（OC 详情页「战斗经历」用；一个 OC 打的约战有限，最多取 500 条）</summary>
    public static Task<List<Aftermath>> OfOcAsync(AppDbContext db, Guid ocId) =>
        QueryAsync(db, $"SELECT {Columns} FROM battle_aftermath WHERE OcId = @o ORDER BY Id DESC LIMIT 500", Read, ("@o", ocId.ToString()));

    /// <summary>写进了某个编年史事件的战后记录（编年史里显示「约战记录」卡片用）</summary>
    public static Task<List<Aftermath>> OfEventAsync(AppDbContext db, Guid eventId) =>
        QueryAsync(db, $"SELECT {Columns} FROM battle_aftermath WHERE ChronicleEventId = @e ORDER BY Id LIMIT 50", Read, ("@e", eventId.ToString()));

    public static async Task<Aftermath?> GetAsync(AppDbContext db, string battleId, Guid ocId) =>
        (await QueryAsync(db, $"SELECT {Columns} FROM battle_aftermath WHERE BattleId = @b AND OcId = @o", Read,
            ("@b", battleId), ("@o", ocId.ToString()))).FirstOrDefault();

    /// <summary>
    /// 新建或修改（BattleId + OcId 唯一）。只改状态、说明、视频这几列；
    /// 写进了哪个世界观 / 哪个事件由 SetEventAsync 单独记，改记录时不会把它清掉。
    /// </summary>
    public static Task UpsertAsync(AppDbContext db, string battleId, Guid ocId, Guid userId, string statusTag, string? statusNote,
        string? videoUrl, string? videoTitle, string? videoCover)
    {
        var now = DateTime.UtcNow;
        return ExecuteAsync(db, @"INSERT INTO battle_aftermath (BattleId, OcId, UserId, StatusTag, StatusNote, VideoUrl, VideoTitle, VideoCover, CreatedAt, UpdatedAt)
VALUES (@b, @o, @u, @s, @n, @v, @vt, @vc, @t, @t)
ON DUPLICATE KEY UPDATE UserId = VALUES(UserId), StatusTag = VALUES(StatusTag), StatusNote = VALUES(StatusNote),
  VideoUrl = VALUES(VideoUrl), VideoTitle = VALUES(VideoTitle), VideoCover = VALUES(VideoCover), UpdatedAt = VALUES(UpdatedAt)",
            ("@b", battleId), ("@o", ocId.ToString()), ("@u", userId.ToString()), ("@s", statusTag), ("@n", statusNote),
            ("@v", videoUrl), ("@vt", videoTitle), ("@vc", videoCover), ("@t", now));
    }

    /// <summary>记下这条战后记录写进了哪个世界观的哪个编年史事件</summary>
    public static Task SetEventAsync(AppDbContext db, long id, Guid projectId, Guid eventId) =>
        ExecuteAsync(db, "UPDATE battle_aftermath SET ProjectId = @p, ChronicleEventId = @e, UpdatedAt = @t WHERE Id = @i",
            ("@p", projectId.ToString()), ("@e", eventId.ToString()), ("@t", DateTime.UtcNow), ("@i", id));

    /// <summary>只删这一条战后记录；已经写进编年史的事件不动（要删请到编年史里删，那边有修改记录可以撤销）</summary>
    public static async Task<bool> DeleteAsync(AppDbContext db, string battleId, Guid ocId) =>
        await ExecuteAsync(db, "DELETE FROM battle_aftermath WHERE BattleId = @b AND OcId = @o", ("@b", battleId), ("@o", ocId.ToString())) > 0;

    // ===================== 底层（照 StickmanWorldStore 的写法） =====================

    private static Aftermath Read(DbDataReader r) => new(
        Convert.ToInt64(r.GetValue(0)), Convert.ToString(r.GetValue(1)) ?? "", ReadGuid(r, 2), ReadGuid(r, 3),
        r.IsDBNull(4) ? "" : r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5),
        r.IsDBNull(6) ? null : r.GetString(6), r.IsDBNull(7) ? null : r.GetString(7), r.IsDBNull(8) ? null : r.GetString(8),
        r.IsDBNull(9) ? null : ReadGuid(r, 9), r.IsDBNull(10) ? null : ReadGuid(r, 10),
        Utc(r.GetDateTime(11)), Utc(r.GetDateTime(12)));

    private static Guid ReadGuid(DbDataReader r, int i) => Guid.TryParse(Convert.ToString(r.GetValue(i)), out var id) ? id : Guid.Empty;
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static async Task<DbCommand> CommandAsync(AppDbContext db, string sql, (string Name, object? Value)[] args)
    {
        var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open) await db.Database.OpenConnectionAsync();
        var command = connection.CreateCommand();
        command.CommandText = sql;
        // EF 开着事务时挂到同一个事务上（MySQL 驱动要求命令和连接上的事务一致）
        command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        foreach (var (name, value) in args)
        {
            var p = command.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            command.Parameters.Add(p);
        }
        return command;
    }

    private static async Task<List<T>> QueryAsync<T>(AppDbContext db, string sql, Func<DbDataReader, T> read, params (string Name, object? Value)[] args)
    {
        await using var command = await CommandAsync(db, sql, args);
        var list = new List<T>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) list.Add(read(reader));
        return list;
    }

    private static async Task<int> ExecuteAsync(AppDbContext db, string sql, params (string Name, object? Value)[] args)
    {
        await using var command = await CommandAsync(db, sql, args);
        return await command.ExecuteNonQueryAsync();
    }
}

/// <summary>
/// 启动时建战后记录表 battle_aftermath（CREATE TABLE IF NOT EXISTS，不改任何已有的表和列）。
/// 建表失败不影响网站启动：只是战后记录功能报错，其他功能照常。
/// </summary>
public sealed class BattleAftermathSchema(IServiceScopeFactory scopes, ILogger<BattleAftermathSchema> logger) : IHostedService
{
    private const string Sql = @"CREATE TABLE IF NOT EXISTS battle_aftermath (
  Id BIGINT NOT NULL AUTO_INCREMENT,
  BattleId VARCHAR(64) NOT NULL,
  OcId CHAR(36) NOT NULL,
  UserId CHAR(36) NOT NULL,
  StatusTag VARCHAR(20) NOT NULL,
  StatusNote VARCHAR(1000) NULL,
  VideoUrl VARCHAR(1000) NULL,
  VideoTitle VARCHAR(200) NULL,
  VideoCover VARCHAR(1000) NULL,
  ProjectId CHAR(36) NULL,
  ChronicleEventId CHAR(36) NULL,
  CreatedAt DATETIME(6) NOT NULL,
  UpdatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_battle_aftermath_battle_oc (BattleId, OcId),
  KEY IX_battle_aftermath_oc (OcId, UpdatedAt),
  KEY IX_battle_aftermath_event (ChronicleEventId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var connection = db.Database.GetDbConnection();
            var opened = connection.State != ConnectionState.Open;
            if (opened) await connection.OpenAsync(cancellationToken);
            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = Sql;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            finally
            {
                if (opened) await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "约战战后记录表 battle_aftermath 创建失败，战后记录功能不可用");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
