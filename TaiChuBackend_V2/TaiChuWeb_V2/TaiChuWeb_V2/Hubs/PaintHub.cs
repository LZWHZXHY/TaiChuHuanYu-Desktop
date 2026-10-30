using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Paint;

namespace TaiChuWeb_V2.Hubs;

/// <summary>
/// 茶绘（/signalr/paint）：多人同屏绘画。需要登录。
/// 方法返回错误文字（null 表示成功）；房间里的变化通过 Members / Layers / Stroke / Live* / Undone / Baked / Cleared / Chat / Cursor 等事件推送。
/// </summary>
[Authorize]
public class PaintHub(PaintManager manager, AppDbContext db) : Hub
{
    private Guid UserId => Guid.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task<string> NameAsync()
    {
        var id = UserId;
        return await db.Users.AsNoTracking().Where(u => u.Id == id).Select(u => u.Username).FirstOrDefaultAsync() ?? "画师";
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await manager.DisconnectedAsync(UserId, Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    public object Lobby() => manager.Lobby();
    public Task<object?> Resume() => manager.ResumeAsync(UserId, Context.ConnectionId);

    public async Task<object> Create(string name, int width, int height, string? password)
    {
        var (error, room) = await manager.CreateAsync(UserId, await NameAsync(), Context.ConnectionId, name, width, height, password);
        return new { error, room };
    }

    public async Task<object> Join(string roomId, string? password)
    {
        var (error, room) = await manager.JoinAsync(UserId, await NameAsync(), Context.ConnectionId, roomId, password);
        return new { error, room };
    }

    public Task Leave() => manager.LeaveAsync(UserId);
    public Task<string?> Kick(Guid userId) => manager.KickAsync(UserId, userId);

    /// <summary>style：{ tool, color, size, opacity, brush, hardness, flags }</summary>
    public Task<string?> StrokeBegin(string key, int layerId, PaintStyleDto style, List<int>? points)
        => manager.StrokeBeginAsync(UserId, Context.ConnectionId, key, layerId, style, points);
    public Task<string?> StrokePoints(string key, List<int> points) => manager.StrokePointsAsync(UserId, Context.ConnectionId, key, points);

    public async Task<object> StrokeEnd(string key, List<int>? points)
    {
        var (error, seq) = await manager.StrokeEndAsync(UserId, Context.ConnectionId, key, points);
        return new { error, seq };
    }

    public Task StrokeCancel(string key) => manager.StrokeCancelAsync(UserId, Context.ConnectionId, key);
    public Task<string?> Undo() => manager.UndoAsync(UserId);
    public Task<string?> Redo() => manager.RedoAsync(UserId);
    public Task Cursor(double x, double y) => manager.CursorAsync(UserId, Context.ConnectionId, x, y);

    /// <summary>action：add / rename / opacity / lock / up / down / delete / clear</summary>
    public Task<string?> Layer(string action, int layerId, string? text, double value) => manager.LayerAsync(UserId, action, layerId, text, value);
    public Task<string?> Chat(string text) => manager.ChatAsync(UserId, text);
}
