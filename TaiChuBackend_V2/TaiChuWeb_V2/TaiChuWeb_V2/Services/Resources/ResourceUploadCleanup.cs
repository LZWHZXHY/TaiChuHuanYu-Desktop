namespace TaiChuWeb_V2.Services.Resources;

/// <summary>
/// 后台清理放弃的上传：开始超过 24 小时还没传完的（或者用户已经取消 / 删除、但当时没清理成功的），
/// 让 MinIO 删掉已经传上去的碎片（AbortMultipartUpload），再把这条记录标记为删除（数据保留，不真删）。
/// 每小时检查一次。
/// </summary>
public sealed class ResourceUploadCleanup(CommunityResourceStore store, ResourceStorage storage, ILogger<ResourceUploadCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // 启动后等一会儿，让建表先完成、也不和启动时的其他任务挤在一起
            await Task.Delay(TimeSpan.FromMinutes(3), stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
            do
            {
                try { await RunOnceAsync(stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
                catch (Exception ex) { logger.LogError(ex, "清理放弃的资源上传失败，一小时后再试"); }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) { /* 网站关闭 */ }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        var stale = await store.StaleUploadsAsync(DateTime.UtcNow - ResourceRules.StaleUploadAge, ct);
        foreach (var row in stale)
        {
            try
            {
                if (!string.IsNullOrEmpty(row.UploadId)) await storage.For(row.Storage).AbortAsync(row.ObjectKey, row.UploadId, ct);
                await store.MarkAbandonedAsync(row.Id, ct);
                await store.ClearPartsAsync(row.Id, ct);
                logger.LogInformation("已清理放弃的资源上传 {Id}（{File}）", row.Id, row.FileName);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // NAS 暂时连不上：这条留着，下次再试
                logger.LogWarning(ex, "清理资源上传 {Id} 失败，下次再试", row.Id);
            }
        }
    }
}
