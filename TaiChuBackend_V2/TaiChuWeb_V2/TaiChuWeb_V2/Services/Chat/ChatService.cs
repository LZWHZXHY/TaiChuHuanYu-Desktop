using System.Threading.Channels;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Hubs;

namespace TaiChuWeb_V2.Services.Chat
{
    public sealed record ChatAuthorDto(Guid Id, string Name, string? Avatar);
    /// <summary>BotId：外部机器人发的消息才有；API 人格的为空</summary>
    public sealed record ChatAiDto(string Name, string? Avatar, string? BotId);
    public sealed record ChatReplyDto(long Id, string Name, bool IsAi, string Excerpt, string? BotId);
    public sealed record ChatMessageDto(long Id, string ChannelId, string Content, DateTime CreatedAt, bool IsAi, ChatAuthorDto Author, ChatAiDto? Ai, ChatReplyDto? ReplyTo);

    /// <summary>
    /// 需要 AI 判断是否发言的事件。Manual 表示主人点了「让 TA 说一句」，只让这一个 AI 发言。
    /// </summary>
    public sealed record ChatAiTrigger(string ChannelId, long MessageId, Guid? ManualOwnerId = null);

    /// <summary>AI 发言任务队列（单例）。满了就丢弃最旧的，避免刷屏时积压。</summary>
    public sealed class ChatAiQueue
    {
        private readonly Channel<ChatAiTrigger> _channel = Channel.CreateBounded<ChatAiTrigger>(new BoundedChannelOptions(200) { FullMode = BoundedChannelFullMode.DropOldest });
        public void Enqueue(ChatAiTrigger trigger) => _channel.Writer.TryWrite(trigger);
        public ChannelReader<ChatAiTrigger> Reader => _channel.Reader;
    }

    /// <summary>消息转换成前端格式（补上用户名、头像、回复摘要）并广播。</summary>
    public sealed class ChatService(AppDbContext db, ChatStore store, IHubContext<ChatHub> hub, BotGateway bots, ILogger<ChatService> logger)
    {
        public async Task<List<ChatMessageDto>> ToDtosAsync(List<ChatMessageRow> rows, CancellationToken ct = default)
        {
            var replyIds = rows.Where(r => r.ReplyToId.HasValue).Select(r => r.ReplyToId!.Value).ToList();
            var replies = (await store.MessagesByIdsAsync(replyIds, ct)).ToDictionary(r => r.Id);
            var userIds = rows.Select(r => r.UserId).Concat(replies.Values.Select(r => r.UserId)).Distinct().ToList();
            var users = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Username, Avatar = u.Profile != null ? u.Profile.Avatar : null }).ToListAsync(ct);
            var map = users.ToDictionary(u => u.Id);

            ChatAuthorDto Author(Guid id) => map.TryGetValue(id, out var u) ? new ChatAuthorDto(id, u.Username, u.Avatar) : new ChatAuthorDto(id, "（已注销）", null);
            ChatReplyDto? Reply(long? id)
            {
                if (id is not long rid || !replies.TryGetValue(rid, out var r)) return null;
                var name = r.IsAi ? r.AiName ?? "AI" : Author(r.UserId).Name;
                var text = r.Deleted ? "（消息已删除）" : r.Content.Length > 60 ? r.Content[..60] + "…" : r.Content;
                return new ChatReplyDto(r.Id, name, r.IsAi, text, r.BotId);
            }
            return rows.Select(r => new ChatMessageDto(r.Id, r.ChannelId, r.Content, r.CreatedAt, r.IsAi, Author(r.UserId),
                r.IsAi ? new ChatAiDto(r.AiName ?? "AI", r.AiAvatar, r.BotId) : null, Reply(r.ReplyToId))).ToList();
        }

        public async Task<ChatMessageDto> BroadcastAsync(ChatMessageRow row, CancellationToken ct = default)
        {
            var dto = (await ToDtosAsync(new List<ChatMessageRow> { row }, ct))[0];
            await hub.Clients.Group(ChatHub.Group(row.ChannelId)).SendAsync("MessageCreated", dto, ct);
            // 全站的聊天提醒要用：谁发的、说了什么（截一段）、回复的是谁。频道本来就公开，这些内容谁都能看到
            Guid? replyToUserId = null;
            if (row.ReplyToId is long replyId)
            {
                var replied = (await store.MessagesByIdsAsync(new[] { replyId }, ct)).FirstOrDefault();
                if (replied is { IsAi: false }) replyToUserId = replied.UserId;
            }
            await hub.Clients.All.SendAsync("ChannelActivity", new
            {
                channelId = row.ChannelId, messageId = row.Id,
                authorId = row.IsAi ? (Guid?)null : row.UserId,
                authorName = dto.Ai?.Name ?? dto.Author.Name,
                isAi = row.IsAi,
                preview = row.Content.Length > 120 ? row.Content[..120] + "…" : row.Content,
                replyToUserId,
            }, ct);
            // 推给接入的外部机器人；推送失败不影响消息本身
            // 放到后台做，慢的机器人不会拖慢真人发消息
            _ = Task.Run(async () =>
            {
                try { await bots.DispatchAsync(dto); } catch (Exception ex) { logger.LogWarning(ex, "推送消息给机器人失败"); }
            });
            return dto;
        }
    }
}
