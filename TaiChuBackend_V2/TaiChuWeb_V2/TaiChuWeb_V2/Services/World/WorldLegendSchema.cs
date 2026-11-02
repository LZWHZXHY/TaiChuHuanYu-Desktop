using System.Data;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.World;

/// <summary>
/// 启动时创建时间线颜色图例表 WorldColorLegends（不需要 EF 迁移）。
/// 一个世界一行：Json 里是 {"items":{"red":"战争","gray":"灾难"}}，读写见 WorldLegendController。
/// 这张表只按 ProjectId 主键单独查，不和别的表关联，所以 Guid 列直接用默认字符集。
/// </summary>
public class WorldLegendSchema(IServiceScopeFactory scopes, ILogger<WorldLegendSchema> logger) : IHostedService
{
    private const string Sql = @"CREATE TABLE IF NOT EXISTS WorldColorLegends (
  ProjectId CHAR(36) NOT NULL,
  Json LONGTEXT NOT NULL,
  UpdatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (ProjectId)
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
            // 建表失败不影响网站启动：读图例会当作「没有图例」，保存会报错
            logger.LogError(ex, "时间线颜色图例表 WorldColorLegends 创建失败，图例功能不可用");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
