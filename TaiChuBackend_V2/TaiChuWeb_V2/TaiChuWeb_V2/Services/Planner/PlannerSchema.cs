using System.Data;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.Planner;

/// <summary>
/// 启动时创建「生活管理」的表 planner_events（不需要 EF 迁移，也不改任何已有的表）。
/// 一行 = 一条安排；提醒状态（下一次什么时候提醒、发了没有）也放在同一行里，省得两张表对不上。
/// 删除是软删除（DeletedAt 有值），可以撤销，数据不会真的丢。
/// Guid 列用 CHAR(36)，只用原生 SQL 读写，不和别的表做关联查询。
/// </summary>
public class PlannerSchema(IServiceScopeFactory scopes, ILogger<PlannerSchema> logger) : IHostedService
{
    private const string EventsSql = @"CREATE TABLE IF NOT EXISTS planner_events (
  Id CHAR(36) NOT NULL,
  UserId CHAR(36) NOT NULL,
  Title VARCHAR(200) NOT NULL DEFAULT '',
  EventDate CHAR(10) NOT NULL,
  StartTime CHAR(5) NULL,
  EndTime CHAR(5) NULL,
  AllDay TINYINT(1) NOT NULL DEFAULT 1,
  Color VARCHAR(20) NOT NULL DEFAULT '',
  Category VARCHAR(30) NOT NULL DEFAULT '',
  Memo TEXT NULL,
  Location VARCHAR(200) NOT NULL DEFAULT '',
  IsDone TINYINT(1) NOT NULL DEFAULT 0,
  DoneDates TEXT NULL,
  RepeatRule VARCHAR(10) NOT NULL DEFAULT 'none',
  RepeatUntil CHAR(10) NULL,
  RemindOffset INT NULL,
  RemindAt VARCHAR(16) NULL,
  Channels VARCHAR(40) NOT NULL DEFAULT '',
  TzOffset INT NOT NULL DEFAULT 480,
  NextEventAt DATETIME(6) NULL,
  NextRemindAt DATETIME(6) NULL,
  RemindTries INT NOT NULL DEFAULT 0,
  RemindDelivered VARCHAR(40) NOT NULL DEFAULT '',
  LastRemindedEventAt DATETIME(6) NULL,
  LastRemindedAt DATETIME(6) NULL,
  DeletedAt DATETIME(6) NULL,
  CreatedAt DATETIME(6) NOT NULL,
  UpdatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_PE_User_Date (UserId, EventDate),
  KEY IX_PE_Remind (NextRemindAt)
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
                command.CommandText = EventsSql;
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
            finally { if (opened) await connection.CloseAsync(); }
        }
        catch (Exception ex) { logger.LogError(ex, "生活管理的表 planner_events 创建失败，生活管理页面会打不开、提醒也不会发"); }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
