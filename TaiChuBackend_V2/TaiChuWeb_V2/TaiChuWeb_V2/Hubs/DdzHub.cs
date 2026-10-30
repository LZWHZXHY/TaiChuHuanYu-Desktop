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
}
