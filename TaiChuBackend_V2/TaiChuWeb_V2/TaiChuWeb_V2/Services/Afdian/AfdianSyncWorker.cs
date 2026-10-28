namespace TaiChuWeb_V2.Services.Afdian
{
    /// <summary>后台定时同步爱发电订单/赞助者（默认每 10 分钟）。</summary>
    public class AfdianSyncWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AfdianSyncWorker> _logger;
        private readonly IConfiguration _config;

        public AfdianSyncWorker(IServiceScopeFactory scopeFactory, ILogger<AfdianSyncWorker> logger, IConfiguration config)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
            _config = config;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var minutes = _config.GetValue<int?>("Afdian:SyncIntervalMinutes") ?? 10;
            if (minutes < 1) minutes = 10;

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(minutes));
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var sync = scope.ServiceProvider.GetRequiredService<AfdianSyncService>();
                    var (orders, sponsors) = await sync.SyncAsync(stoppingToken);
                    if (orders > 0 || sponsors > 0)
                        _logger.LogInformation("爱发电同步完成：新增财政收入 {Orders} 条，赞助者 {Sponsors} 位", orders, sponsors);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "爱发电自动同步失败");
                }

                try
                {
                    await timer.WaitForNextTickAsync(stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
