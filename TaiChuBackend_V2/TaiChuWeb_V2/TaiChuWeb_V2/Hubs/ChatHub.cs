using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace TaiChuWeb_V2.Hubs;

/// <summary>
/// 聊天室实时通道：只负责进出频道、在线人数和“正在输入”。
/// 发消息走 REST 接口（ChatController），保存后由服务器广播 MessageCreated / MessageDeleted。
/// 未登录也可以连接旁观；发言需要登录。
/// </summary>
public class ChatHub : Hub
{
    // 连接 → 所在频道
    private static readonly ConcurrentDictionary<string, string> Rooms = new();

    public static string Group(string channelId) => "chat:" + channelId;

    /// <summary>每个频道当前在看的连接数</summary>
    public static Dictionary<string, int> OnlineCounts() =>
        Rooms.Values.GroupBy(v => v).ToDictionary(g => g.Key, g => g.Count());

    public async Task JoinChannel(string channelId)
    {
        if (string.IsNullOrWhiteSpace(channelId) || channelId.Length > 32) return;
        if (Rooms.TryGetValue(Context.ConnectionId, out var old) && old != channelId)
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, Group(old));
        Rooms[Context.ConnectionId] = channelId;
        await Groups.AddToGroupAsync(Context.ConnectionId, Group(channelId));
        await Clients.All.SendAsync("Presence", OnlineCounts());
    }

    /// <summary>“某某正在输入…”，只发给同频道的其他人</summary>
    public async Task Typing(string channelId, string? displayName)
    {
        if (!Rooms.TryGetValue(Context.ConnectionId, out var room) || room != channelId) return;
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return;
        var name = Context.User?.FindFirstValue(ClaimTypes.Name) ?? displayName ?? "有人";
        if (name.Length > 30) name = name[..30];
        await Clients.OthersInGroup(Group(channelId)).SendAsync("Typing", new { channelId, userId, name, isAi = false });
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (Rooms.TryRemove(Context.ConnectionId, out _))
            await Clients.All.SendAsync("Presence", OnlineCounts());
        await base.OnDisconnectedAsync(exception);
    }
}
