using System.Data;
using System.Data.Common;
using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.ChaiCommunity;

namespace TaiChuWeb_V2.Services.ChaiCommunity;

/// <summary>
/// 柴圈 OC ↔ 世界观 联动用到的三张新表（启动时由 StickmanWorldSchema 建，不动任何已有的表）：
///   stickman_world_links       OC 关联到哪些世界观（世界观首页的「柴圈人设」列表）
///   stickman_world_card_links  OC 和世界观条目的对应关系（复制 / 发布时记下，「从另一边更新」用）
///   stickman_backups           OC 的备份：从条目更新、从备份恢复之前，把 OC 当时的样子整份存下来
/// 都用原生 SQL 读写，不需要改 AppDbContext；只按主键 / 编号查，不和别的表关联（不同表的排序规则可能不一样）。
/// 编号存成 36 位字符串，读的时候统一 Guid.TryParse（驱动返回 Guid 还是字符串都能认）。
/// 正在用 EF 事务时，命令自动挂到同一个事务里（同步 OC 时备份和修改要么都成功、要么都不算）。
/// </summary>
public static class StickmanWorldStore
{
    public sealed record WorldLink(Guid CharacterId, Guid ProjectId, Guid LinkedBy, DateTime CreatedAt);
    public sealed record CardLink(Guid CharacterId, Guid CardId, Guid ProjectId, Guid CreatedBy, DateTime CreatedAt, DateTime? LastSyncAt, string? LastSyncDirection);
    public sealed record Backup(long Id, Guid CharacterId, string Json, string Reason, Guid? CreatedBy, DateTime CreatedAt);

    // ===================== 关联到世界观 =====================

    public static Task<List<WorldLink>> LinksOfCharacterAsync(AppDbContext db, Guid characterId) =>
        QueryAsync(db, "SELECT CharacterId, ProjectId, LinkedBy, CreatedAt FROM stickman_world_links WHERE CharacterId = @c ORDER BY CreatedAt",
            ReadLink, ("@c", characterId.ToString()));

    public static Task<List<WorldLink>> LinksOfProjectAsync(AppDbContext db, Guid projectId) =>
        QueryAsync(db, "SELECT CharacterId, ProjectId, LinkedBy, CreatedAt FROM stickman_world_links WHERE ProjectId = @p ORDER BY CreatedAt DESC LIMIT 500",
            ReadLink, ("@p", projectId.ToString()));

    /// <summary>关联（已经关联过就什么都不做）；返回是不是这次新加的</summary>
    public static async Task<bool> AddLinkAsync(AppDbContext db, Guid characterId, Guid projectId, Guid userId) =>
        await ExecuteAsync(db, "INSERT IGNORE INTO stickman_world_links (CharacterId, ProjectId, LinkedBy, CreatedAt) VALUES (@c, @p, @u, @t)",
            ("@c", characterId.ToString()), ("@p", projectId.ToString()), ("@u", userId.ToString()), ("@t", DateTime.UtcNow)) > 0;

    /// <summary>只删这一条关联，OC 和世界观本身都不动</summary>
    public static async Task<bool> RemoveLinkAsync(AppDbContext db, Guid characterId, Guid projectId) =>
        await ExecuteAsync(db, "DELETE FROM stickman_world_links WHERE CharacterId = @c AND ProjectId = @p",
            ("@c", characterId.ToString()), ("@p", projectId.ToString())) > 0;

    // ===================== OC ↔ 条目 对应关系 =====================

    private const string CardLinkColumns = "CharacterId, CardId, ProjectId, CreatedBy, CreatedAt, LastSyncAt, LastSyncDirection";

    public static Task<List<CardLink>> CardLinksOfCharacterAsync(AppDbContext db, Guid characterId) =>
        QueryAsync(db, $"SELECT {CardLinkColumns} FROM stickman_world_card_links WHERE CharacterId = @c ORDER BY CreatedAt",
            ReadCardLink, ("@c", characterId.ToString()));

    public static Task<List<CardLink>> CardLinksOfCardAsync(AppDbContext db, Guid cardId) =>
        QueryAsync(db, $"SELECT {CardLinkColumns} FROM stickman_world_card_links WHERE CardId = @k ORDER BY CreatedAt",
            ReadCardLink, ("@k", cardId.ToString()));

    public static async Task<CardLink?> CardLinkAsync(AppDbContext db, Guid characterId, Guid cardId) =>
        (await QueryAsync(db, $"SELECT {CardLinkColumns} FROM stickman_world_card_links WHERE CharacterId = @c AND CardId = @k",
            ReadCardLink, ("@c", characterId.ToString()), ("@k", cardId.ToString()))).FirstOrDefault();

    public static async Task AddCardLinkAsync(AppDbContext db, Guid characterId, Guid cardId, Guid projectId, Guid userId, string direction)
    {
        var now = DateTime.UtcNow;
        await ExecuteAsync(db, @"INSERT IGNORE INTO stickman_world_card_links (CharacterId, CardId, ProjectId, CreatedBy, CreatedAt, LastSyncAt, LastSyncDirection)
VALUES (@c, @k, @p, @u, @t, @t, @d)",
            ("@c", characterId.ToString()), ("@k", cardId.ToString()), ("@p", projectId.ToString()), ("@u", userId.ToString()), ("@t", now), ("@d", direction));
    }

    /// <summary>同步完记一下时间和方向</summary>
    public static Task TouchCardLinkAsync(AppDbContext db, Guid characterId, Guid cardId, string direction) =>
        ExecuteAsync(db, "UPDATE stickman_world_card_links SET LastSyncAt = @t, LastSyncDirection = @d WHERE CharacterId = @c AND CardId = @k",
            ("@t", DateTime.UtcNow), ("@d", direction), ("@c", characterId.ToString()), ("@k", cardId.ToString()));

    /// <summary>断开对应关系：只删这一条记录，OC 和条目的内容都不动</summary>
    public static async Task<bool> RemoveCardLinkAsync(AppDbContext db, Guid characterId, Guid cardId) =>
        await ExecuteAsync(db, "DELETE FROM stickman_world_card_links WHERE CharacterId = @c AND CardId = @k",
            ("@c", characterId.ToString()), ("@k", cardId.ToString())) > 0;

    // ===================== OC 备份 =====================

    /// <summary>OC 某一时刻的完整内容（标题、简介、封面、状态、全部属性、图库、两份面板原样 JSON）</summary>
    public sealed class Snapshot
    {
        public string Title { get; set; } = "";
        public string? Description { get; set; }
        public string? CoverUrl { get; set; }
        public string? Status { get; set; }
        public List<SnapshotAttribute> Attributes { get; set; } = new();
        public List<SnapshotImage> Images { get; set; } = new();
        /// <summary>展示面板原样 JSON（没有 = null）</summary>
        public string? ShowPanel { get; set; }
        /// <summary>约战面板原样 JSON（没有 = null）</summary>
        public string? BattlePanel { get; set; }
    }
    public sealed class SnapshotAttribute
    {
        public string Key { get; set; } = "";
        public string? Value { get; set; }
        public string Type { get; set; } = "short";
        public int SortOrder { get; set; }
    }
    public sealed class SnapshotImage
    {
        public string Url { get; set; } = "";
        public string? Alt { get; set; }
        public int SortOrder { get; set; }
    }

    public static readonly JsonSerializerOptions SnapshotJson = new(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public static Snapshot Snap(StickmanCharacter oc, IEnumerable<StickmanAttribute> attributes, IEnumerable<StickmanImage> images, string? showPanel, string? battlePanel) => new()
    {
        Title = oc.Title, Description = oc.Description, CoverUrl = oc.CoverUrl, Status = oc.Status,
        Attributes = attributes.OrderBy(a => a.SortOrder).Select(a => new SnapshotAttribute { Key = a.Key, Value = a.Value, Type = a.Type, SortOrder = a.SortOrder }).ToList(),
        Images = images.OrderBy(i => i.SortOrder).Select(i => new SnapshotImage { Url = i.Url, Alt = i.Alt, SortOrder = i.SortOrder }).ToList(),
        ShowPanel = showPanel, BattlePanel = battlePanel,
    };

    public static Snapshot? ParseSnapshot(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<Snapshot>(json, SnapshotJson); }
        catch (JsonException) { return null; }
    }

    public static Task AddBackupAsync(AppDbContext db, Guid characterId, Snapshot snapshot, string reason, Guid userId) =>
        ExecuteAsync(db, "INSERT INTO stickman_backups (CharacterId, Snapshot, Reason, CreatedBy, CreatedAt) VALUES (@c, @s, @r, @u, @t)",
            ("@c", characterId.ToString()), ("@s", JsonSerializer.Serialize(snapshot, SnapshotJson)),
            ("@r", reason.Length > 200 ? reason[..200] : reason), ("@u", userId.ToString()), ("@t", DateTime.UtcNow));

    public static Task<List<Backup>> BackupsAsync(AppDbContext db, Guid characterId, int take = 50) =>
        QueryAsync(db, "SELECT Id, CharacterId, Snapshot, Reason, CreatedBy, CreatedAt FROM stickman_backups WHERE CharacterId = @c ORDER BY Id DESC LIMIT " + Math.Clamp(take, 1, 200),
            ReadBackup, ("@c", characterId.ToString()));

    public static async Task<Backup?> BackupAsync(AppDbContext db, Guid characterId, long id) =>
        (await QueryAsync(db, "SELECT Id, CharacterId, Snapshot, Reason, CreatedBy, CreatedAt FROM stickman_backups WHERE CharacterId = @c AND Id = @i",
            ReadBackup, ("@c", characterId.ToString()), ("@i", id))).FirstOrDefault();

    // ===================== OC 能力面板（表 stickman_panels，见 StickmanPanelSchema） =====================

    public const string ShowPanel = "show", BattlePanel = "battle";

    public static async Task<string?> PanelAsync(AppDbContext db, Guid characterId, string kind) =>
        (await QueryAsync(db, "SELECT Json FROM stickman_panels WHERE CharacterId = @c AND Kind = @k",
            r => r.IsDBNull(0) ? null : r.GetString(0), ("@c", characterId.ToString()), ("@k", kind))).FirstOrDefault();

    /// <summary>写面板（和 StickmanPanelController 的写法一样：有就覆盖，没有就新建）</summary>
    public static Task WritePanelAsync(AppDbContext db, Guid characterId, string kind, string json) =>
        ExecuteAsync(db, @"INSERT INTO stickman_panels (CharacterId, Kind, Json, UpdatedAt) VALUES (@c, @k, @j, @t)
ON DUPLICATE KEY UPDATE Json = VALUES(Json), UpdatedAt = VALUES(UpdatedAt)",
            ("@c", characterId.ToString()), ("@k", kind), ("@j", json), ("@t", DateTime.UtcNow));

    public static Task DeletePanelAsync(AppDbContext db, Guid characterId, string kind) =>
        ExecuteAsync(db, "DELETE FROM stickman_panels WHERE CharacterId = @c AND Kind = @k", ("@c", characterId.ToString()), ("@k", kind));

    // ===================== 底层 =====================

    private static WorldLink ReadLink(DbDataReader r) =>
        new(ReadGuid(r, 0), ReadGuid(r, 1), ReadGuid(r, 2), Utc(r.GetDateTime(3)));

    private static CardLink ReadCardLink(DbDataReader r) =>
        new(ReadGuid(r, 0), ReadGuid(r, 1), ReadGuid(r, 2), ReadGuid(r, 3), Utc(r.GetDateTime(4)),
            r.IsDBNull(5) ? null : Utc(r.GetDateTime(5)), r.IsDBNull(6) ? null : r.GetString(6));

    private static Backup ReadBackup(DbDataReader r) =>
        new(Convert.ToInt64(r.GetValue(0)), ReadGuid(r, 1), r.GetString(2), r.IsDBNull(3) ? "" : r.GetString(3),
            r.IsDBNull(4) ? null : ReadGuid(r, 4), Utc(r.GetDateTime(5)));

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
/// 启动时建 OC ↔ 世界观 联动的三张新表（CREATE TABLE IF NOT EXISTS，不改任何已有的表和列）。
/// 建表失败不影响网站启动：联动功能会报错，其他功能照常。
/// </summary>
public sealed class StickmanWorldSchema(IServiceScopeFactory scopes, ILogger<StickmanWorldSchema> logger) : IHostedService
{
    private static readonly string[] Sql =
    {
        @"CREATE TABLE IF NOT EXISTS stickman_world_links (
  CharacterId CHAR(36) NOT NULL,
  ProjectId CHAR(36) NOT NULL,
  LinkedBy CHAR(36) NOT NULL,
  CreatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (CharacterId, ProjectId),
  KEY IX_stickman_world_links_project (ProjectId, CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
        @"CREATE TABLE IF NOT EXISTS stickman_world_card_links (
  CharacterId CHAR(36) NOT NULL,
  CardId CHAR(36) NOT NULL,
  ProjectId CHAR(36) NOT NULL,
  CreatedBy CHAR(36) NOT NULL,
  CreatedAt DATETIME(6) NOT NULL,
  LastSyncAt DATETIME(6) NULL,
  LastSyncDirection VARCHAR(16) NULL,
  PRIMARY KEY (CharacterId, CardId),
  KEY IX_stickman_world_card_links_card (CardId),
  KEY IX_stickman_world_card_links_project (ProjectId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
        @"CREATE TABLE IF NOT EXISTS stickman_backups (
  Id BIGINT NOT NULL AUTO_INCREMENT,
  CharacterId CHAR(36) NOT NULL,
  Snapshot LONGTEXT NOT NULL,
  Reason VARCHAR(200) NOT NULL DEFAULT '',
  CreatedBy CHAR(36) NULL,
  CreatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_stickman_backups_character (CharacterId, Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4",
    };

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
                foreach (var sql in Sql)
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            finally
            {
                if (opened) await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "柴圈 OC ↔ 世界观 联动的表（stickman_world_links / stickman_world_card_links / stickman_backups）创建失败，联动功能不可用");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
