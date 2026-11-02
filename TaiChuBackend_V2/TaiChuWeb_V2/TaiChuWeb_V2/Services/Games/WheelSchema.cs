namespace TaiChuWeb_V2.Services.Games
{
    /// <summary>
    /// 启动时创建转盘的两张表 wheel_wheels（转盘）和 wheel_official_spins（官方转盘抽奖记录），不需要 EF 迁移，表已存在就跳过。
    /// 建表语句在 WheelService 里，这里只是开机时先建好，免得第一个人用的时候才去建。
    /// </summary>
    public class WheelSchema(WheelService wheels, ILogger<WheelSchema> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try { await wheels.EnsureSchemaAsync(cancellationToken); }
            catch (Exception ex) { logger.LogError(ex, "转盘数据表创建失败，转盘功能会出错（第一次用的时候还会再试）"); }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
