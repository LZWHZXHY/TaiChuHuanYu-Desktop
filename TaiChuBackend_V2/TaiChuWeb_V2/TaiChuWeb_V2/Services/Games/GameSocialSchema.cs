namespace TaiChuWeb_V2.Services.Games
{
    /// <summary>
    /// 启动时创建「点赞 / 砸番茄」记录表和游戏留言板的表（不需要 EF 迁移，表已存在就跳过）。
    /// 建表语句在 GameSocialService 里，这里只是开机时先建好，免得第一个人用的时候才去建。
    /// </summary>
    public class GameSocialSchema(GameSocialService social, ILogger<GameSocialSchema> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try { await social.EnsureSchemaAsync(cancellationToken); }
            catch (Exception ex) { logger.LogError(ex, "游戏留言板 / 点赞表创建失败，留言板和砸番茄会出错（第一次用的时候还会再试）"); }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
