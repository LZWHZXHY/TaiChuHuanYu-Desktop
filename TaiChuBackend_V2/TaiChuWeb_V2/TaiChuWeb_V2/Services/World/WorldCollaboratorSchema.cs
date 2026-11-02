using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.World;

/// <summary>
/// 启动时创建世界观协作者表 WorldCollaborators、条目修改记录表 WorldCardRevisions、编年史的表（含修改记录 WorldChronicleRevisions），
/// 并给 WorldCards 补上 Layout 列（不需要 EF 迁移）。
/// Guid 列的字符集 / 排序规则照抄 WorldProjects.Id（Pomelo 通常是 ascii 字符集），
/// 避免两表关联查询时出现「排序规则混用」错误。照抄失败时退回默认写法，保证表一定能建出来。
/// </summary>
public class WorldCollaboratorSchema(IServiceScopeFactory scopes, ILogger<WorldCollaboratorSchema> logger) : IHostedService
{
    private static readonly Regex SafeName = new("^[A-Za-z0-9_]+$");

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
                string? charset = null, collation = null;
                await using (var query = connection.CreateCommand())
                {
                    query.CommandText = @"SELECT CHARACTER_SET_NAME, COLLATION_NAME FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'WorldProjects' AND COLUMN_NAME = 'Id' LIMIT 1";
                    await using var reader = await query.ExecuteReaderAsync(cancellationToken);
                    if (await reader.ReadAsync(cancellationToken))
                    {
                        charset = reader.IsDBNull(0) ? null : reader.GetString(0);
                        collation = reader.IsDBNull(1) ? null : reader.GetString(1);
                    }
                }

                var guidType = "CHAR(36)";
                if (charset != null && collation != null && SafeName.IsMatch(charset) && SafeName.IsMatch(collation))
                    guidType = $"CHAR(36) CHARACTER SET {charset} COLLATE {collation}";

                await EnsureCardLayoutColumnAsync(connection, cancellationToken);

                foreach (var sql in new Func<string, string>[] { Sql, RevisionSql, TimelineSql, TimelineEventSql, CalendarSql, ChronicleRevisionSql })
                {
                    try
                    {
                        await Execute(connection, sql(guidType), cancellationToken);
                    }
                    catch (DbException ex) when (guidType != "CHAR(36)")
                    {
                        logger.LogWarning(ex, "按 WorldProjects 的字符集建表失败，改用默认字符集重试");
                        await Execute(connection, sql("CHAR(36)"), cancellationToken);
                    }
                }
            }
            finally
            {
                if (opened) await connection.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "世界观协作者表创建失败，协作功能和世界观读取会出错");
        }
    }

    private static string Sql(string guidType) => $@"CREATE TABLE IF NOT EXISTS WorldCollaborators (
  ProjectId {guidType} NOT NULL,
  UserId {guidType} NOT NULL,
  Role VARCHAR(16) NOT NULL DEFAULT 'editor',
  AddedBy {guidType} NULL,
  CreatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (ProjectId, UserId),
  KEY IX_WorldCollaborators_UserId (UserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    // 条目修改记录：谁、什么时候、改成了什么（完整快照），用来查看和撤销
    private static string RevisionSql(string guidType) => $@"CREATE TABLE IF NOT EXISTS WorldCardRevisions (
  Id BIGINT NOT NULL AUTO_INCREMENT,
  ProjectId {guidType} NOT NULL,
  CardId {guidType} NOT NULL,
  UserId {guidType} NULL,
  Action VARCHAR(16) NOT NULL,
  Title VARCHAR(200) NOT NULL DEFAULT '',
  Snapshot LONGTEXT NOT NULL,
  Note VARCHAR(200) NULL,
  CreatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_WorldCardRevisions_Project (ProjectId, Id),
  KEY IX_WorldCardRevisions_Card (CardId, Id),
  KEY IX_WorldCardRevisions_User (UserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    // 编年史（fix102）：时间线、事件、历法
    private static string TimelineSql(string guidType) => $@"CREATE TABLE IF NOT EXISTS WorldTimelines (
  Id {guidType} NOT NULL,
  ProjectId {guidType} NOT NULL,
  Name VARCHAR(80) NOT NULL DEFAULT '',
  Description VARCHAR(1000) NULL,
  Color VARCHAR(16) NULL,
  Status VARCHAR(16) NOT NULL DEFAULT 'canon',
  ParentId {guidType} NULL,
  ForkEventId {guidType} NULL,
  MergeEventId {guidType} NULL,
  CardId {guidType} NULL,
  SortOrder INT NOT NULL DEFAULT 0,
  Hidden TINYINT(1) NOT NULL DEFAULT 0,
  CreatedAt DATETIME(6) NOT NULL,
  UpdatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_WorldTimelines_Project (ProjectId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    private static string TimelineEventSql(string guidType) => $@"CREATE TABLE IF NOT EXISTS WorldTimelineEvents (
  Id {guidType} NOT NULL,
  ProjectId {guidType} NOT NULL,
  TimelineId {guidType} NOT NULL,
  Kind VARCHAR(16) NOT NULL DEFAULT 'event',
  Title VARCHAR(500) NOT NULL DEFAULT '',
  Description LONGTEXT NULL,
  EraId VARCHAR(40) NULL,
  Year BIGINT NULL,
  Month INT NULL,
  Day INT NULL,
  EndEraId VARCHAR(40) NULL,
  EndYear BIGINT NULL,
  EndMonth INT NULL,
  EndDay INT NULL,
  DateText VARCHAR(100) NULL,
  Color VARCHAR(16) NULL,
  Images LONGTEXT NOT NULL,
  Tags LONGTEXT NOT NULL,
  CardIds LONGTEXT NOT NULL,
  LocationCardId {guidType} NULL,
  Hidden TINYINT(1) NOT NULL DEFAULT 0,
  SortOrder INT NOT NULL DEFAULT 0,
  CreatedBy {guidType} NULL,
  CreatedAt DATETIME(6) NOT NULL,
  UpdatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_WorldTimelineEvents_Project (ProjectId),
  KEY IX_WorldTimelineEvents_Timeline (TimelineId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    private static string CalendarSql(string guidType) => $@"CREATE TABLE IF NOT EXISTS WorldCalendars (
  ProjectId {guidType} NOT NULL,
  Json LONGTEXT NOT NULL,
  UpdatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (ProjectId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    // 编年史修改记录（fix121）：时间线 / 事件 / 历法改之前和改之后的完整内容，用来查看和撤销
    // （列名不用 Before / After：BEFORE 是 MySQL 保留字）
    private static string ChronicleRevisionSql(string guidType) => $@"CREATE TABLE IF NOT EXISTS WorldChronicleRevisions (
  Id BIGINT NOT NULL AUTO_INCREMENT,
  ProjectId {guidType} NOT NULL,
  TargetType VARCHAR(16) NOT NULL,
  TargetId {guidType} NOT NULL,
  UserId {guidType} NULL,
  Action VARCHAR(16) NOT NULL,
  Title VARCHAR(200) NOT NULL DEFAULT '',
  BeforeJson LONGTEXT NULL,
  AfterJson LONGTEXT NULL,
  ChangedFields VARCHAR(500) NULL,
  Note VARCHAR(200) NULL,
  CreatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_WorldChronicleRevisions_Project (ProjectId, Id),
  KEY IX_WorldChronicleRevisions_Target (TargetId, Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    /// <summary>条目的区块排列列 WorldCards.Layout（fix80 新增）：没有就加上</summary>
    private async Task EnsureCardLayoutColumnAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        try
        {
            await using var query = connection.CreateCommand();
            query.CommandText = @"SELECT COUNT(*) FROM information_schema.COLUMNS
WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'WorldCards' AND COLUMN_NAME = 'Layout'";
            var exists = Convert.ToInt64(await query.ExecuteScalarAsync(cancellationToken)) > 0;
            if (!exists)
            {
                await Execute(connection, "ALTER TABLE WorldCards ADD COLUMN Layout LONGTEXT NULL", cancellationToken);
                logger.LogInformation("已给 WorldCards 加上 Layout 列（条目区块排列）");
            }
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "给 WorldCards 加 Layout 列失败，世界观条目会读不出来。请手动执行：ALTER TABLE WorldCards ADD COLUMN Layout LONGTEXT NULL");
        }
    }

    private static async Task Execute(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
