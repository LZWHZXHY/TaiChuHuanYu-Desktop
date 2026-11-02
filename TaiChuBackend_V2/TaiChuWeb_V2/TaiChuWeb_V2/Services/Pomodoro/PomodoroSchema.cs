using System.Data;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.Pomodoro;

/// <summary>
/// 启动时创建番茄钟的两张表（不需要 EF 迁移，也不改任何已有的表）：
/// · pomodoro_state：每个人一行，存「现在在计时什么」，网页和桌面程序都读写这一行，靠 Version 防止两边互相覆盖；
/// · pomodoro_sessions：每完成 / 放弃一次专注记一条，(UserId, ClientId) 唯一，两端重复上传也只存一条。
/// Guid 列用 CHAR(36)，只用原生 SQL 读写，不和别的表做关联查询（排序规则可能不同）。
/// </summary>
public class PomodoroSchema(IServiceScopeFactory scopes, ILogger<PomodoroSchema> logger) : IHostedService
{
    private const string StateSql = @"CREATE TABLE IF NOT EXISTS pomodoro_state (
  UserId CHAR(36) NOT NULL,
  Phase VARCHAR(8) NOT NULL DEFAULT '',
  `Task` VARCHAR(120) NOT NULL DEFAULT '',
  StartedMs BIGINT NOT NULL DEFAULT 0,
  EndsMs BIGINT NOT NULL DEFAULT 0,
  PausedLeftMs BIGINT NOT NULL DEFAULT 0,
  TotalMs BIGINT NOT NULL DEFAULT 0,
  `Round` INT NOT NULL DEFAULT 0,
  NextPhase VARCHAR(8) NOT NULL DEFAULT 'focus',
  PlannerEventId CHAR(36) NULL,
  PlannerDate VARCHAR(10) NULL,
  SettingsJson TEXT NULL,
  Version BIGINT NOT NULL DEFAULT 0,
  UpdatedMs BIGINT NOT NULL DEFAULT 0,
  Source VARCHAR(16) NOT NULL DEFAULT '',
  DesktopSeenAt DATETIME NULL,
  UpdatedAt DATETIME NOT NULL,
  PRIMARY KEY (UserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    private const string SessionsSql = @"CREATE TABLE IF NOT EXISTS pomodoro_sessions (
  Id BIGINT NOT NULL AUTO_INCREMENT,
  UserId CHAR(36) NOT NULL,
  ClientId VARCHAR(40) NOT NULL,
  StartMs BIGINT NOT NULL,
  EndMs BIGINT NOT NULL,
  FocusSec INT NOT NULL DEFAULT 0,
  `Task` VARCHAR(120) NOT NULL DEFAULT '',
  Done TINYINT NOT NULL DEFAULT 1,
  PlannerEventId CHAR(36) NULL,
  PlannerDate VARCHAR(10) NULL,
  Source VARCHAR(16) NOT NULL DEFAULT '',
  CreatedAt DATETIME NOT NULL,
  DeletedAt DATETIME NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_PS_User_Client (UserId, ClientId),
  KEY IX_PS_User_Start (UserId, StartMs)
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
                foreach (var sql in new[] { StateSql, SessionsSql })
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            finally { if (opened) await connection.CloseAsync(); }
        }
        catch (Exception ex) { logger.LogError(ex, "番茄钟的表 pomodoro_state / pomodoro_sessions 创建失败，番茄钟同步和记录会用不了"); }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
