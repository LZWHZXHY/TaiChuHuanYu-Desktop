using System.Security.Claims;

namespace TaiChuWeb_V2.Services.Analytics
{
    /// <summary>
    /// 后台任务：启动时建表；每分钟把活跃记录写入数据库；每 5 分钟记一次在线人数；每天清理一次过期快照。
    /// 统计出错只记日志，不影响网站本身。
    /// </summary>
    public sealed class AnalyticsWorker(PresenceTracker tracker, AnalyticsStore store, ILogger<AnalyticsWorker> logger) : BackgroundService
    {
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var ready = false;
            DateTime lastSample = DateTime.MinValue, lastCleanup = DateTime.MinValue;
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
            do
            {
                try
                {
                    if (!ready) { await store.EnsureSchemaAsync(stoppingToken); ready = true; }
                    await store.FlushAsync(tracker, stoppingToken);

                    // 对齐到整 5 分钟，便于画曲线
                    var now = DateTime.UtcNow;
                    var slot = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute / 5 * 5, 0, DateTimeKind.Utc);
                    if (slot > lastSample)
                    {
                        var (members, guests) = tracker.Snapshot();
                        await store.InsertSampleAsync(slot, members.Count, guests, stoppingToken);
                        lastSample = slot;
                    }
                    if (now - lastCleanup > TimeSpan.FromDays(1))
                    {
                        await store.CleanupAsync(stoppingToken);
                        lastCleanup = now;
                    }
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "社区统计写入失败，稍后重试");
                }
            } while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));

            // 关闭前把最后一批写进去
            try { if (ready) await store.FlushAsync(tracker, CancellationToken.None); } catch { /* 忽略 */ }
        }
    }

    /// <summary>
    /// 记录登录用户的“出现”：每个带有效登录凭证的请求都会更新在线状态和当日活跃。
    /// 只做内存操作，不访问数据库。
    /// </summary>
    public static class ActivityTrackingExtensions
    {
        public static IApplicationBuilder UseActivityTracking(this IApplicationBuilder app)
        {
            var tracker = app.ApplicationServices.GetRequiredService<PresenceTracker>();
            return app.Use(async (context, next) =>
            {
                if (context.User.Identity?.IsAuthenticated == true
                    && Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                    && !HttpMethods.IsOptions(context.Request.Method))
                {
                    tracker.TouchMember(userId, context.Request.Headers["X-Visitor-Id"].FirstOrDefault());
                }
                await next(context);
            });
        }
    }
}
