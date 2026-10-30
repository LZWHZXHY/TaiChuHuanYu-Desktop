using System.Data;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.Reports;

/// <summary>启动时创建举报表（不需要 EF 迁移）</summary>
public class ContentReportSchema(IServiceScopeFactory scopes, ILogger<ContentReportSchema> logger) : IHostedService
{
    private const string Sql = @"CREATE TABLE IF NOT EXISTS ContentReports (
  Id BIGINT NOT NULL AUTO_INCREMENT,
  ReporterId CHAR(36) NOT NULL,
  TargetType VARCHAR(24) NOT NULL DEFAULT 'other',
  TargetId VARCHAR(64) NOT NULL DEFAULT '',
  TargetTitle VARCHAR(200) NULL,
  TargetUrl VARCHAR(300) NULL,
  Reason VARCHAR(16) NOT NULL DEFAULT 'other',
  Detail VARCHAR(500) NULL,
  Status VARCHAR(16) NOT NULL DEFAULT 'pending',
  HandledBy CHAR(36) NULL,
  HandledAt DATETIME(6) NULL,
  HandleNote VARCHAR(300) NULL,
  CreatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_CR_Status (Status, Id),
  KEY IX_CR_Target (TargetType, TargetId),
  KEY IX_CR_Reporter (ReporterId, CreatedAt)
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
            finally { if (opened) await connection.CloseAsync(); }
        }
        catch (Exception ex) { logger.LogError(ex, "举报表创建失败，举报功能会出错"); }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
