using System.Globalization;
using System.Net;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Email;
using TaiChuWeb_V2.Services.Notify;
using TaiChuWeb_V2.Services.Personalization;

namespace TaiChuWeb_V2.Services.Planner;

/// <summary>
/// 生活管理的提醒：每分钟看一次有没有到点的提醒。
/// · 站内通知：所有能用生活管理的人；
/// · 邮件：只有有邮件提醒特权的人（发之前再查一次，赞助过期了就不发邮件，站内照发）；
/// · 浏览器弹窗：不在这里发，前端自己拉 /api/planner/reminders/upcoming 到点弹。
/// 不会刷屏 / 补发：
/// · 到点超过 1 天还没发的（比如服务器停了很久）直接跳过；
/// · 赞助到期（只读）的人提醒暂停，到点的直接跳过，续费后从下一次开始提醒，不补发；
/// · 邮件发失败最多试 3 次（每分钟一次），还不行就放弃并记日志；站内通知只写一次，不会因为邮件重试而重复。
/// 重复事项发完这一次后，自动排好下一次。
/// </summary>
public class PlannerReminderWorker(IServiceScopeFactory scopes, ILogger<PlannerReminderWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(1);
    /// <summary>启动后等一会儿，让建表先完成</summary>
    private static readonly TimeSpan FirstDelay = TimeSpan.FromSeconds(40);
    private static readonly TimeSpan MaxLate = TimeSpan.FromDays(1);
    private const int MaxEmailTries = 3;
    public const string NotifyType = "planner";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try { await Task.Delay(FirstDelay, stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogWarning(ex, "生活管理提醒检查失败，1 分钟后再试"); }
            try { await Task.Delay(Interval, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var perks = scope.ServiceProvider.GetRequiredService<SponsorPerkService>();
        var email = scope.ServiceProvider.GetRequiredService<IEmailService>();
        var now = DateTime.UtcNow;

        var due = await PlannerStore.QueryAsync(db,
            "DeletedAt IS NULL AND NextRemindAt IS NOT NULL AND NextRemindAt <= @now ORDER BY NextRemindAt LIMIT 200",
            ct, ("@now", now));
        if (due.Count == 0) return;

        // 同一个人这一轮只查一次特权和邮箱
        var accessCache = new Dictionary<Guid, PlannerAccess.Result>();
        var emailCache = new Dictionary<Guid, string?>();
        var sent = 0;

        foreach (var row in due)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                if (!accessCache.TryGetValue(row.UserId, out var access))
                    accessCache[row.UserId] = access = await PlannerAccess.CheckAsync(db, perks, row.UserId, ct);

                // 太晚了（服务器停过）或者赞助到期了：这一次直接跳过，排下一次
                if (!access.CanEdit || row.NextRemindAt!.Value < now - MaxLate)
                {
                    await FinishAsync(db, row, now, delivered: false, ct);
                    continue;
                }

                var channels = PlannerStore.ChannelList(row.Channels);
                var delivered = new HashSet<string>(row.RemindDelivered.Split(',', StringSplitOptions.RemoveEmptyEntries));
                var (when, content) = Describe(row);

                // 1. 站内通知（只写一次）
                if (channels.Contains("site") && !delivered.Contains("site"))
                {
                    var text = $"日程提醒：{row.Title} · {when}";
                    if (text.Length > 190) text = text[..190] + "…";
                    new NotificationService(db).Add(row.UserId, null, NotifyType, "planner", row.Id.ToString(), text, LinkOf(row));
                    await db.SaveChangesAsync(ct);
                    db.ChangeTracker.Clear();
                    delivered.Add("site");
                    // 马上记下来：就算后面邮件出错，下一轮也不会再发一遍站内通知
                    row.RemindDelivered = string.Join(",", delivered);
                    await PlannerStore.ExecAsync(db, $"UPDATE {PlannerStore.Table} SET RemindDelivered = @d WHERE Id = @id", ct,
                        ("@d", row.RemindDelivered), ("@id", row.Id.ToString()));
                }

                // 2. 邮件（君者特权；发之前再确认一次，赞助过期就不发）
                if (channels.Contains("email") && !delivered.Contains("email"))
                {
                    if (!emailCache.TryGetValue(row.UserId, out var address))
                        emailCache[row.UserId] = address = await db.Users.AsNoTracking().Where(u => u.Id == row.UserId).Select(u => u.Email).FirstOrDefaultAsync(ct);
                    if (access.CanEmail && !string.IsNullOrWhiteSpace(address))
                    {
                        try
                        {
                            await email.SendHtmlEmailAsync(address, $"日程提醒：{row.Title}", content);
                            delivered.Add("email");
                        }
                        catch (Exception ex)
                        {
                            row.RemindTries++;
                            if (row.RemindTries < MaxEmailTries)
                            {
                                logger.LogWarning(ex, "生活管理提醒邮件发送失败（第 {Tries} 次），下一分钟再试：{Id}", row.RemindTries, row.Id);
                                await PlannerStore.ExecAsync(db, $"UPDATE {PlannerStore.Table} SET RemindTries = @t WHERE Id = @id", ct,
                                    ("@t", row.RemindTries), ("@id", row.Id.ToString()));
                                continue; // 这一条先不结束，下一轮重试邮件
                            }
                            logger.LogError(ex, "生活管理提醒邮件连续 {Tries} 次发送失败，这次不再发：{Id}", row.RemindTries, row.Id);
                        }
                    }
                }

                await FinishAsync(db, row, now, delivered: true, ct);
                sent++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                // 单条出错不影响别的；为了不卡住一直重试，这一次也直接结束掉
                logger.LogWarning(ex, "生活管理提醒处理失败：{Id}", row.Id);
                db.ChangeTracker.Clear();
                try { await FinishAsync(db, row, now, delivered: false, ct); } catch { /* 下一轮再说 */ }
            }
        }
        if (sent > 0) logger.LogInformation("生活管理：本轮发出 {Count} 条提醒", sent);
    }

    /// <summary>这一次结束：记下「提醒过了」，重复事项排好下一次，不重复的就不再提醒</summary>
    private static async Task FinishAsync(AppDbContext db, PlannerEventRow row, DateTime now, bool delivered, CancellationToken ct)
    {
        row.LastRemindedEventAt = row.NextEventAt;
        if (delivered) row.LastRemindedAt = now;
        PlannerStore.Reschedule(row, now);
        await PlannerStore.ExecAsync(db, $@"UPDATE {PlannerStore.Table} SET LastRemindedEventAt = @le, LastRemindedAt = @la,
NextEventAt = @ne, NextRemindAt = @nr, RemindTries = 0, RemindDelivered = '' WHERE Id = @id", ct,
            ("@le", row.LastRemindedEventAt), ("@la", row.LastRemindedAt), ("@ne", row.NextEventAt), ("@nr", row.NextRemindAt), ("@id", row.Id.ToString()));
    }

    /// <summary>点通知打开生活管理，直接定位到那一天</summary>
    private static string LinkOf(PlannerEventRow row)
    {
        var date = row.NextEventAt is DateTime ev
            ? PlannerStore.ToLocal(ev, row.TzOffset).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : row.EventDate;
        return $"/lingmai/planner?date={date}";
    }

    /// <summary>（时间文字，邮件正文）</summary>
    private static (string When, string Html) Describe(PlannerEventRow row)
    {
        var local = row.NextEventAt is DateTime ev ? PlannerStore.ToLocal(ev, row.TzOffset) : DateTime.MinValue;
        var day = local == DateTime.MinValue ? row.EventDate : local.ToString("M月d日", CultureInfo.InvariantCulture);
        var when = row.AllDay ? $"{day} 全天" : $"{day} {row.StartTime}{(string.IsNullOrEmpty(row.EndTime) ? "" : "–" + row.EndTime)}";
        var enc = (string? s) => WebUtility.HtmlEncode(s ?? "");
        var html = $@"<div style=""font-family:sans-serif;font-size:14px;color:#222;line-height:1.7"">
<p>你好，这是太初寰宇「生活管理」给你的提醒：</p>
<p style=""font-size:18px;font-weight:bold;margin:12px 0"">{enc(row.Title)}</p>
<p>时间：{enc(when)}</p>
{(string.IsNullOrWhiteSpace(row.Location) ? "" : $"<p>地点：{enc(row.Location)}</p>")}
{(string.IsNullOrWhiteSpace(row.Memo) ? "" : $"<p>备忘：{enc(row.Memo).Replace("\n", "<br>")}</p>")}
<p style=""color:#888;font-size:12px;margin-top:20px"">打开太初寰宇 → 太初灵脉 → 生活管理 可以查看和修改。不想再收邮件的话，把这条安排的「邮件」提醒去掉就行。</p>
</div>";
        return (when, html);
    }
}
