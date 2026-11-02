using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Games.Xiangqi;

namespace TaiChuWeb_V2.Hubs;

/// <summary>
/// 联机象棋（/signalr/xiangqi）。需要登录。
/// 客户端调用方法后，服务器通过 "State" 推送房间状态；方法返回错误文字（null 表示成功）。
/// 观战：Watch 进来后同样收 "State"（spectating 为真、myColor 为空）；被请出去时收 "Notice"；大厅列表变了推 "LobbyChanged"。
/// </summary>
[Authorize]
public class XiangqiHub(XqManager manager, AppDbContext db) : Hub
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
    /// <summary>重新打开页面时取回自己所在房间的状态（没有则为 null）</summary>
    public object? Resume() => manager.StateFor(UserId);
    public async Task<string?> Create(string name, int timeLimit) => await manager.CreateAsync(UserId, await NameAsync(), name, timeLimit);
    public async Task<string?> Join(string roomId) => await manager.JoinAsync(UserId, await NameAsync(), roomId);
    public async Task<string?> Quick() => await manager.QuickAsync(UserId, await NameAsync());
    public Task<string?> Leave() => manager.LeaveAsync(UserId);
    public Task<string?> Start() => manager.StartAsync(UserId);
    public Task<string?> Move(int from, int to) => manager.MoveAsync(UserId, from, to);
    public Task<string?> Resign() => manager.ResignAsync(UserId);
    public Task<string?> OfferDraw() => manager.OfferDrawAsync(UserId);
    public Task<string?> AnswerDraw(bool accept) => manager.AnswerDrawAsync(UserId, accept);

    // ---------- 观战（只读：观战的人调用 Move / Resign / OfferDraw 等都会因为「不在房间里」被拒绝） ----------
    /// <summary>进房间观战</summary>
    public async Task<string?> Watch(string roomId) => await manager.WatchAsync(UserId, await NameAsync(), roomId);
    /// <summary>观战的人坐下（有空位、还没开局时）</summary>
    public async Task<string?> SitDown() => await manager.SitDownAsync(UserId, await NameAsync());
    /// <summary>房主开 / 关观战</summary>
    public Task<string?> SetSpectate(bool allow) => manager.SetSpectateAsync(UserId, allow);
}
