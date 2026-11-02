using System.Data;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.World;

/// <summary>
/// 启动时创建创作里程碑的三张表（不需要 EF 迁移）：
/// - WorldMilestones：每个世界达成了哪些里程碑（主键 = 世界 + 里程碑，天然保证每个只记一次、只发一次通知）
/// - WorldMilestoneScans：后台上次检测这个世界是什么时候、当时的「数据指纹」（没变化的世界下次就跳过不算）
/// - WorldMilestoneKeys：每种里程碑第一次被检测到的时间（用来判断「上线前就达成了」→ 静默补记，不发通知）
/// 这几张表只用原生 SQL 读写，不和别的表做关联查询，所以 Guid 列直接用 CHAR(36)。
/// </summary>
public class WorldMilestoneSchema(IServiceScopeFactory scopes, ILogger<WorldMilestoneSchema> logger) : IHostedService
{
    private const string MilestonesSql = @"CREATE TABLE IF NOT EXISTS WorldMilestones (
  ProjectId CHAR(36) NOT NULL,
  MilestoneKey VARCHAR(40) NOT NULL,
  ReachedValue BIGINT NOT NULL DEFAULT 0,
  AchievedAt DATETIME(6) NULL,
  Silent TINYINT(1) NOT NULL DEFAULT 0,
  CreatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (ProjectId, MilestoneKey)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    private const string ScansSql = @"CREATE TABLE IF NOT EXISTS WorldMilestoneScans (
  ProjectId CHAR(36) NOT NULL,
  Fingerprint VARCHAR(200) NOT NULL DEFAULT '',
  ScannedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (ProjectId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    private const string KeysSql = @"CREATE TABLE IF NOT EXISTS WorldMilestoneKeys (
  MilestoneKey VARCHAR(40) NOT NULL,
  AddedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (MilestoneKey)
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
                foreach (var sql in new[] { MilestonesSql, ScansSql, KeysSql })
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            finally { if (opened) await connection.CloseAsync(); }
        }
        catch (Exception ex) { logger.LogError(ex, "创作里程碑表创建失败，统计页的里程碑墙会一直是灰的，也不会发里程碑通知"); }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
