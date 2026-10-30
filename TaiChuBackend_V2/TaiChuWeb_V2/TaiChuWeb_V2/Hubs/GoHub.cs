using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Games.Go;

namespace TaiChuWeb_V2.Hubs;

/// <summary>
/// 联机围棋（/signalr/go）。需要登录。
/// 客户端调用方法后，服务器通过 "State" 推送房间状态；方法返回错误文字（null 表示成功）。
/// </summary>
[Authorize]
public class GoHub(GoManager manager, AppDbContext db) : Hub
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
    public async Task<string?> Create(string name, int size, int timeLimit) => await manager.CreateAsync(UserId, await NameAsync(), name, size, timeLimit);
    public async Task<string?> Join(string roomId) => await manager.JoinAsync(UserId, await NameAsync(), roomId);
    public async Task<string?> Quick() => await manager.QuickAsync(UserId, await NameAsync());
    public Task<string?> Leave() => manager.LeaveAsync(UserId);
    public Task<string?> Start() => manager.StartAsync(UserId);
    public Task<string?> Play(int index) => manager.PlayAsync(UserId, index);
    public Task<string?> Pass() => manager.PassAsync(UserId);
    public Task<string?> Resign() => manager.ResignAsync(UserId);
    public Task<string?> ToggleDead(int index) => manager.ToggleDeadAsync(UserId, index);
    public Task<string?> ConfirmScore() => manager.ConfirmScoreAsync(UserId);
    /// <summary>点目阶段觉得还没下完，回到对局</summary>
    public Task<string?> ResumePlay() => manager.ResumeAsync(UserId);
}
