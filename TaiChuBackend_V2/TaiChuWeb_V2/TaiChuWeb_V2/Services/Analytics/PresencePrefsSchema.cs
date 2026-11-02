using System.Data;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.Analytics;

/// <summary>启动时创建「在线状态设置」表（目前只有隐身开关），并把隐身的人读进内存</summary>
public class PresencePrefsSchema(IServiceScopeFactory scopes, PresenceTracker tracker, ILogger<PresencePrefsSchema> logger) : IHostedService
{
    private const string Sql = @"CREATE TABLE IF NOT EXISTS user_presence_prefs (
  UserId CHAR(36) NOT NULL,
  Invisible TINYINT(1) NOT NULL DEFAULT 0,
  UpdatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (UserId)
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
                await using (var create = connection.CreateCommand())
                {
                    create.CommandText = Sql;
                    await create.ExecuteNonQueryAsync(cancellationToken);
                }
                var ids = new List<Guid>();
                await using (var read = connection.CreateCommand())
                {
                    read.CommandText = "SELECT UserId FROM user_presence_prefs WHERE Invisible = 1";
                    await using var reader = await read.ExecuteReaderAsync(cancellationToken);
                    while (await reader.ReadAsync(cancellationToken))
                        if (Guid.TryParse(reader.GetValue(0)?.ToString(), out var id)) ids.Add(id);
                }
                tracker.LoadInvisible(ids);
            }
            finally { if (opened) await connection.CloseAsync(); }
        }
        catch (Exception ex) { logger.LogError(ex, "在线状态设置表创建 / 读取失败，隐身设置暂时不生效"); }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
