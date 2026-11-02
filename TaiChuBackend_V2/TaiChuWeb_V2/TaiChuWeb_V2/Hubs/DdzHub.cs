using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Games;
using TaiChuWeb_V2.Services.Games.Doudizhu;

namespace TaiChuWeb_V2.Hubs;

/// <summary>
/// 斗地主实时通道（/signalr/ddz）。需要登录。
/// 客户端调用方法后，服务器通过 "State" 推送各自视角的牌桌状态；出错时返回错误文字（null 表示成功）。
/// 牌桌互动：评论推 "Chat"（一行评论），点赞 / 砸番茄推 "Reaction"（谁砸谁，前端播动画）。
/// </summary>
[Authorize]
public class DdzHub(DdzManager manager, PotatoService potatoes, AppDbContext db) : Hub
{
    private Guid UserId => Guid.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<string> NameAsync()
    {
        var id = UserId;
        return await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.Username).FirstOrDefaultAsync() ?? "玩家";
    }

    public override async Task OnConnectedAsync()
    {
        await manager.ConnectedAsync(UserId);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await manager.DisconnectedAsync(UserId);
        await base.OnDisconnectedAsync(exception);
    }

    public object Lobby() => manager.Lobby();

    /// <summary>重新打开页面时取回自己所在牌桌的状态（没有则为 null）</summary>
    public object? Resume() => manager.StateFor(UserId);

    public async Task<string?> Create(string tier) => await manager.CreateAsync(UserId, await NameAsync(), tier, (await potatoes.GetAsync(UserId)).Balance);
    public async Task<string?> Join(string roomId) => await manager.JoinAsync(UserId, await NameAsync(), roomId, (await potatoes.GetAsync(UserId)).Balance);
    public async Task<string?> Quick(string tier) => await manager.QuickAsync(UserId, await NameAsync(), tier, (await potatoes.GetAsync(UserId)).Balance);
    public Task<string?> Leave() => manager.LeaveAsync(UserId);
    public Task<string?> AddBot() => manager.AddBotAsync(UserId);
    public Task<string?> KickBot(int seat) => manager.KickBotAsync(UserId, seat);
    public Task<string?> Ready(bool ready) => manager.ReadyAsync(UserId, ready);
    public Task<string?> Bid(int score) => manager.BidAsync(UserId, score);
    public Task<string?> Play(int[] cards) => manager.PlayAsync(UserId, cards ?? Array.Empty<int>());
    public Task<string?> Pass() => manager.PassAsync(UserId);
    public Task<string?> Trustee(bool on) => manager.TrusteeAsync(UserId, on);

    // ---------- 互动：评论区、点赞 / 砸番茄（服务器推送 "Chat"、"Reaction"） ----------

    /// <summary>进桌 / 重连时取回最近的评论、价格和这一局已用次数（不在牌桌上为 null）</summary>
    public object? Social() => manager.SocialFor(UserId);
    /// <summary>在牌桌评论区说一句话</summary>
    public Task<string?> SendChat(string text) => manager.ChatAsync(UserId, text);
    /// <summary>给某个座位的玩家点赞（like）或砸番茄（tomato）</summary>
    public Task<string?> React(int seat, string kind) => manager.ReactAsync(UserId, seat, kind);

    // ---------- 观战（服务器推 "State"：观战的人拿到不含手牌的公开状态；"Notice"：被请出去时的提示；"LobbyChanged"：大厅列表变了） ----------
    // 不用 Hub 分组：谁在看哪桌记在服务器的观战名单里，推送按用户 ID 发，重连换了连接也能收到。

    /// <summary>进某一桌观战</summary>
    public async Task<string?> Watch(string roomId) => await manager.WatchAsync(UserId, await NameAsync(), roomId);
    /// <summary>观战的人坐下（有空位、还没开局时）</summary>
    public async Task<string?> SitDown() => await manager.SitDownAsync(UserId, await NameAsync(), (await potatoes.GetAsync(UserId)).Balance);
    /// <summary>玩家两局之间站起来观战</summary>
    public async Task<string?> StandUp() => await manager.StandUpAsync(UserId, await NameAsync());
    /// <summary>房主开 / 关观战（开局前）</summary>
    public Task<string?> SetSpectate(bool allow) => manager.SetSpectateAsync(UserId, allow);
}
