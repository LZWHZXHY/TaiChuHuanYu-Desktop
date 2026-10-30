using System.Data;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.CommunityHistory;

/// <summary>启动时创建岁月史书的三张表（内容、表情回应、评论）（不需要 EF 迁移）</summary>
public class CommunityHistorySchema(IServiceScopeFactory scopes, ILogger<CommunityHistorySchema> logger) : IHostedService
{
    private const string EntriesSql = @"CREATE TABLE IF NOT EXISTS CommunityHistoryEntries (
  Id BIGINT NOT NULL AUTO_INCREMENT,
  UserId CHAR(36) NOT NULL,
  Kind VARCHAR(16) NOT NULL DEFAULT 'funny',
  Title VARCHAR(100) NOT NULL DEFAULT '',
  Content LONGTEXT NULL,
  Images LONGTEXT NOT NULL,
  HappenedOn DATETIME(6) NOT NULL,
  DatePrecision VARCHAR(8) NOT NULL DEFAULT 'day',
  Hidden TINYINT(1) NOT NULL DEFAULT 0,
  HiddenReason VARCHAR(200) NULL,
  HiddenBy CHAR(36) NULL,
  HiddenAt DATETIME(6) NULL,
  EditedBy CHAR(36) NULL,
  EditedAt DATETIME(6) NULL,
  CreatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_CHE_Happened (HappenedOn, Id),
  KEY IX_CHE_User (UserId, CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    private const string ReactionsSql = @"CREATE TABLE IF NOT EXISTS CommunityHistoryReactions (
  EntryId BIGINT NOT NULL,
  UserId CHAR(36) NOT NULL,
  Kind VARCHAR(16) NOT NULL,
  CreatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (EntryId, UserId, Kind),
  KEY IX_CHR_Entry (EntryId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    private const string CommentsSql = @"CREATE TABLE IF NOT EXISTS CommunityHistoryComments (
  Id BIGINT NOT NULL AUTO_INCREMENT,
  EntryId BIGINT NOT NULL,
  UserId CHAR(36) NOT NULL,
  Content VARCHAR(500) NOT NULL DEFAULT '',
  ReplyToId BIGINT NULL,
  CreatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_CHC_Entry (EntryId, Id),
  KEY IX_CHC_User (UserId, CreatedAt)
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
                foreach (var sql in new[] { EntriesSql, ReactionsSql, CommentsSql })
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            finally { if (opened) await connection.CloseAsync(); }
        }
        catch (Exception ex) { logger.LogError(ex, "岁月史书表创建失败，岁月史书页面会出错"); }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
