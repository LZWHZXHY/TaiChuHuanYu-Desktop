using System.Data;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.Quest;

/// <summary>启动时创建世界任务的两张表（不需要 EF 迁移）</summary>
public class ServerQuestSchema(IServiceScopeFactory scopes, ILogger<ServerQuestSchema> logger) : IHostedService
{
    private const string QuestsSql = @"CREATE TABLE IF NOT EXISTS ServerQuests (
  Id INT NOT NULL AUTO_INCREMENT,
  Title VARCHAR(100) NOT NULL DEFAULT '',
  Kind VARCHAR(16) NOT NULL DEFAULT 'count',
  Line VARCHAR(16) NOT NULL DEFAULT 'main',
  PrereqId INT NULL,
  Description LONGTEXT NULL,
  Unit VARCHAR(20) NOT NULL DEFAULT '',
  Target BIGINT NOT NULL DEFAULT 0,
  Progress BIGINT NOT NULL DEFAULT 0,
  Status VARCHAR(16) NOT NULL DEFAULT 'draft',
  SortOrder INT NOT NULL DEFAULT 0,
  RewardTeaser VARCHAR(500) NULL,
  RewardText LONGTEXT NULL,
  RewardExp INT NOT NULL DEFAULT 0,
  RewardPotato INT NOT NULL DEFAULT 0,
  CoverImage VARCHAR(500) NULL,
  StartedAt DATETIME(6) NULL,
  CompletedAt DATETIME(6) NULL,
  CreatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_ServerQuests_Status (Status, SortOrder)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    private const string ContributionsSql = @"CREATE TABLE IF NOT EXISTS ServerQuestContributions (
  Id BIGINT NOT NULL AUTO_INCREMENT,
  QuestId INT NOT NULL,
  UserId CHAR(36) NULL,
  PlayerName VARCHAR(50) NULL,
  Amount BIGINT NOT NULL DEFAULT 0,
  Note VARCHAR(1000) NULL,
  Images LONGTEXT NOT NULL,
  PostId CHAR(36) NULL,
  Status VARCHAR(16) NOT NULL DEFAULT 'pending',
  Source VARCHAR(16) NOT NULL DEFAULT 'player',
  ReviewerId CHAR(36) NULL,
  ReviewNote VARCHAR(300) NULL,
  CreatedAt DATETIME(6) NOT NULL,
  ReviewedAt DATETIME(6) NULL,
  PRIMARY KEY (Id),
  KEY IX_SQC_Quest (QuestId, Status),
  KEY IX_SQC_Status (Status, Id),
  KEY IX_SQC_User (UserId)
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
                foreach (var sql in new[] { QuestsSql, ContributionsSql })
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                // 后来加的列：任务类型（数量 / 一次性）、主线支线、前置任务。表已经建好的话补上
                foreach (var (table, column, definition) in new[]
                {
                    ("ServerQuests", "Kind", "VARCHAR(16) NOT NULL DEFAULT 'count' AFTER Title"),
                    ("ServerQuests", "Line", "VARCHAR(16) NOT NULL DEFAULT 'main' AFTER Kind"),
                    ("ServerQuests", "PrereqId", "INT NULL AFTER Line"),
                    ("ServerQuestContributions", "PostId", "CHAR(36) NULL AFTER Images"),
                })
                {
                    await using var check = connection.CreateCommand();
                    check.CommandText = $"SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'";
                    if (Convert.ToInt64(await check.ExecuteScalarAsync(cancellationToken)) > 0) continue;
                    await using var alter = connection.CreateCommand();
                    alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
                    await alter.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            finally { if (opened) await connection.CloseAsync(); }
        }
        catch (Exception ex) { logger.LogError(ex, "世界任务表创建失败，世界任务页面会出错"); }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
