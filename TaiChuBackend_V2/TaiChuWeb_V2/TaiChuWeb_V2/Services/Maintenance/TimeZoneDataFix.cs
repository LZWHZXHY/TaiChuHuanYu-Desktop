using System.Data;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.Maintenance;

/// <summary>
/// 一次性的时间数据修正（启动时执行，做过的会记在 app_data_fixes 表里，不会重复执行）。
/// · joint-dates-to-utc：联合活动的开始 / 结束时间以前是按填写人的“北京时间”原样存的，
///   现在统一存 UTC（和后端的比较逻辑、其他时间字段一致），旧数据整体减 8 小时。
/// </summary>
public class TimeZoneDataFix(IServiceScopeFactory scopes, ILogger<TimeZoneDataFix> logger) : IHostedService
{
    private static readonly (string Name, string Sql)[] Fixes =
    {
        ("joint-dates-to-utc", "UPDATE JointActivities SET StartDate = DATE_SUB(StartDate, INTERVAL 8 HOUR), EndDate = CASE WHEN EndDate IS NULL THEN NULL ELSE DATE_SUB(EndDate, INTERVAL 8 HOUR) END"),
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
                await using (var create = connection.CreateCommand())
                {
                    create.CommandText = "CREATE TABLE IF NOT EXISTS app_data_fixes (Name VARCHAR(100) NOT NULL, AppliedAt DATETIME(6) NOT NULL, PRIMARY KEY (Name)) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
                    await create.ExecuteNonQueryAsync(cancellationToken);
                }
                foreach (var (name, sql) in Fixes)
                {
                    await using var tx = await connection.BeginTransactionAsync(cancellationToken);
                    await using var mark = connection.CreateCommand();
                    mark.Transaction = tx;
                    mark.CommandText = "INSERT IGNORE INTO app_data_fixes (Name, AppliedAt) VALUES (@n, UTC_TIMESTAMP(6))";
                    var p = mark.CreateParameter(); p.ParameterName = "@n"; p.Value = name; mark.Parameters.Add(p);
                    if (await mark.ExecuteNonQueryAsync(cancellationToken) == 0) { await tx.RollbackAsync(cancellationToken); continue; } // 做过了
                    await using var fix = connection.CreateCommand();
                    fix.Transaction = tx;
                    fix.CommandText = sql;
                    var rows = await fix.ExecuteNonQueryAsync(cancellationToken);
                    await tx.CommitAsync(cancellationToken);
                    logger.LogInformation("时间数据修正 {Name} 完成，改了 {Rows} 行", name, rows);
                }
            }
            finally { if (opened) await connection.CloseAsync(); }
        }
        catch (Exception ex) { logger.LogError(ex, "时间数据修正失败（下次启动会再试）"); }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
