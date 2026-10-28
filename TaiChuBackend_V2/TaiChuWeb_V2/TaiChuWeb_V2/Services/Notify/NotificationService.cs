using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Notify;

namespace TaiChuWeb_V2.Services.Notify;

/// <summary>站内通知写入助手：只把通知加入上下文，由调用方统一 SaveChanges。</summary>
public class NotificationService(AppDbContext context)
{
    /// <summary>添加一条通知；接收者为空或与操作者相同则跳过。返回是否真的写入。</summary>
    public bool Add(Guid recipientId, Guid? actorId, string type, string targetType, string targetId, string content, string? link)
    {
        if (recipientId == Guid.Empty) return false;
        if (actorId.HasValue && actorId.Value == recipientId) return false; // 自己操作自己不通知

        context.Notifications.Add(new Notification
        {
            UserId = recipientId,
            ActorId = actorId,
            Type = type,
            TargetType = targetType,
            TargetId = targetId,
            Content = content,
            Link = link,
            CreatedAt = DateTime.UtcNow
        });
        return true;
    }
}
