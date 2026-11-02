using System.Data;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.Panels;

/// <summary>
/// 启动时创建柴圈 OC 能力面板表 stickman_panels（不需要 EF 迁移，不动任何已有的表）。
/// 一个 OC 最多两行，按 Kind 分开存、互不覆盖：
///   show   = 展示面板（自由设计：自定义维度和刻度，格式见 StatPanelRules）
///   battle = 约战面板（官方约战模板，按维度 Key 存等级，格式见 BattlePanelTemplate）
/// 面板和 OC 的属性、图片分开存，OC 的保存接口完全不碰这张表，读写见 StickmanPanelController。
/// 这张表只按主键单独查，不和别的表关联（不同表的排序规则可能不同），所以 Guid 列直接用默认字符集。
/// </summary>
public class StickmanPanelSchema(IServiceScopeFactory scopes, ILogger<StickmanPanelSchema> logger) : IHostedService
{
    private const string Sql = @"CREATE TABLE IF NOT EXISTS stickman_panels (
  CharacterId CHAR(36) NOT NULL,
  Kind VARCHAR(16) NOT NULL,
  Json LONGTEXT NOT NULL,
  UpdatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (CharacterId, Kind)
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
            // 建表失败不影响网站启动：读面板会当作「没有面板」，保存会报错
            logger.LogError(ex, "OC 能力面板表 stickman_panels 创建失败，能力面板功能不可用");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
