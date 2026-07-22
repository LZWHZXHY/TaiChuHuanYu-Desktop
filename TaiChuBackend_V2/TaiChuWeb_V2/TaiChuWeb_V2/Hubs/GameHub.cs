using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Timers;
using Timer = System.Timers.Timer;

namespace TaiChuWeb_V2.Hubs;

[Authorize]
public class GameHub : Hub
{
    private static readonly ConcurrentDictionary<string, string> _playerRooms = new();
    private static readonly ConcurrentDictionary<string, Room> _rooms = new();

    private string? CurrentUserId => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

    // ===== 1. 创建房间（带参数校验和异常处理） =====
    public async Task CreateRoom(string roomName, string mode, int boardSize, int timeLimit = 30)
    {
        try
        {
            // 参数校验
            if (string.IsNullOrWhiteSpace(roomName))
            {
                await Clients.Caller.SendAsync("Error", new { message = "房间名称不能为空" });
                return;
            }

            if (boardSize < 5 || boardSize > 25)
            {
                await Clients.Caller.SendAsync("Error", new { message = "棋盘大小必须在 5 到 25 之间" });
                return;
            }

            if (timeLimit < 10 || timeLimit > 300)
            {
                await Clients.Caller.SendAsync("Error", new { message = "时间限制必须在 10 到 300 秒之间" });
                return;
            }

            var userId = CurrentUserId ?? Context.ConnectionId;
            if (string.IsNullOrEmpty(userId))
            {
                await Clients.Caller.SendAsync("Error", new { message = "无法识别用户身份" });
                return;
            }

            var roomId = Guid.NewGuid().ToString("N").Substring(0, 8);

            var room = new Room
            {
                RoomId = roomId,
                RoomName = roomName.Trim(),
                Mode = mode ?? "classic",
                BoardSize = boardSize,
                OwnerId = userId,
                Players = new List<string> { userId },
                TimeLimit = timeLimit
            };

            if (!_rooms.TryAdd(roomId, room))
            {
                await Clients.Caller.SendAsync("Error", new { message = "创建房间失败，请重试" });
                return;
            }

            _playerRooms.TryAdd(userId, roomId);
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

            await Clients.Caller.SendAsync("RoomCreated", new
            {
                roomId,
                roomName = roomName.Trim(),
                mode = room.Mode,
                boardSize,
                timeLimit
            });

            Console.WriteLine($"[{DateTime.Now}] 房间创建成功: {roomId} by {userId}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now}] CreateRoom 异常: {ex.Message}");
            await Clients.Caller.SendAsync("Error", new { message = $"创建房间失败: {ex.Message}" });
        }
    }

    // ===== 2. 加入房间 =====
    public async Task JoinRoom(string roomId)
    {
        var userId = CurrentUserId ?? Context.ConnectionId;

        if (!_rooms.TryGetValue(roomId, out var room))
        {
            await Clients.Caller.SendAsync("Error", new { message = "房间不存在" });
            return;
        }

        if (room.Players.Count >= 2)
        {
            await Clients.Caller.SendAsync("Error", new { message = "房间已满" });
            return;
        }

        if (room.Players.Contains(userId))
        {
            await Clients.Caller.SendAsync("Error", new { message = "你已在房间中" });
            return;
        }

        room.Players.Add(userId);
        _playerRooms.TryAdd(userId, roomId);
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        var players = room.Players.Select((id, idx) => new
        {
            name = id == userId ? "我" : $"玩家{idx + 1}",
            id = id
        });

        await Clients.Group(roomId).SendAsync("PlayerJoined", new
        {
            playerCount = room.Players.Count,
            players = players
        });
    }

    // ===== 3. 落子（带回合校验） =====
    public async Task MakeMove(string roomId, int row, int col)
    {
        var userId = CurrentUserId ?? Context.ConnectionId;
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            await Clients.Caller.SendAsync("Error", new { message = "房间不存在" });
            return;
        }

        if (room.CurrentTurn != userId)
        {
            await Clients.Caller.SendAsync("Error", new { message = "还没轮到你下棋" });
            return;
        }

        var nextPlayer = room.Players.FirstOrDefault(p => p != userId);
        if (nextPlayer == null)
        {
            await Clients.Caller.SendAsync("Error", new { message = "没有对手" });
            return;
        }

        room.CurrentTurn = nextPlayer;
        room.TurnStartTime = DateTime.Now;

        StartTurnTimer(room);

        await Clients.Group(roomId).SendAsync("MoveMade", new
        {
            row,
            col,
            playerId = userId,
            currentTurn = room.CurrentTurn,
            remainingTime = room.TimeLimit
        });
    }

    // ===== 4. 开始游戏 =====
    public async Task StartGame(string roomId)
    {
        var userId = CurrentUserId ?? Context.ConnectionId;
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            await Clients.Caller.SendAsync("Error", new { message = "房间不存在" });
            return;
        }

        if (room.OwnerId != userId)
        {
            await Clients.Caller.SendAsync("Error", new { message = "只有房主可以开始游戏" });
            return;
        }

        if (room.Players.Count < 2)
        {
            await Clients.Caller.SendAsync("Error", new { message = "至少需要 2 名玩家" });
            return;
        }

        room.CurrentTurn = room.OwnerId;
        room.TurnStartTime = DateTime.Now;

        StartTurnTimer(room);

        await Clients.Group(roomId).SendAsync("GameStarted", new
        {
            boardSize = room.BoardSize,
            mode = room.Mode,
            players = room.Players,
            currentTurn = room.CurrentTurn,
            timeLimit = room.TimeLimit
        });
    }

    // ===== 5. 计时器管理 =====
    private void StartTurnTimer(Room room)
    {
        if (room.TurnTimer != null)
        {
            room.TurnTimer.Stop();
            room.TurnTimer.Dispose();
            room.TurnTimer = null;
        }

        var timer = new Timer(room.TimeLimit * 1000);
        timer.Elapsed += (sender, e) => HandleTimeout(room.RoomId);
        timer.AutoReset = false;
        timer.Start();
        room.TurnTimer = timer;
        room.TurnStartTime = DateTime.Now;
    }

    private void HandleTimeout(string roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room))
            return;

        if (room.CurrentTurn == null)
            return;

        var timeoutPlayer = room.CurrentTurn;
        var nextPlayer = room.Players.FirstOrDefault(p => p != timeoutPlayer);

        room.CurrentTurn = null;

        if (room.TurnTimer != null)
        {
            room.TurnTimer.Stop();
            room.TurnTimer.Dispose();
            room.TurnTimer = null;
        }

        // 异步发送，避免阻塞 Timer 线程
        _ = Task.Run(async () =>
        {
            try
            {
                await Clients.Group(roomId).SendAsync("GameOver", new
                {
                    winner = nextPlayer,
                    reason = $"玩家 {timeoutPlayer} 超时（{room.TimeLimit}秒）",
                    timeoutPlayer = timeoutPlayer
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now}] 超时发送失败: {ex.Message}");
            }
        });
    }

    // ===== 6. 断开连接 =====
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CurrentUserId ?? Context.ConnectionId;
        if (_playerRooms.TryRemove(userId, out var roomId))
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomId);
            if (_rooms.TryGetValue(roomId, out var room))
            {
                room.Players.Remove(userId);

                if (room.Players.Count == 0)
                {
                    if (room.TurnTimer != null)
                    {
                        room.TurnTimer.Stop();
                        room.TurnTimer.Dispose();
                        room.TurnTimer = null;
                    }
                    _rooms.TryRemove(roomId, out _);
                }
                else
                {
                    if (room.CurrentTurn == userId)
                    {
                        var nextPlayer = room.Players.FirstOrDefault();
                        if (nextPlayer != null)
                        {
                            room.CurrentTurn = nextPlayer;
                            StartTurnTimer(room);
                            await Clients.Group(roomId).SendAsync("TurnChanged", new { currentTurn = room.CurrentTurn });
                        }
                    }
                    await Clients.Group(roomId).SendAsync("PlayerLeft", new { playerId = userId });
                }
            }
        }
        await base.OnDisconnectedAsync(exception);
    }
}

// ===== 房间模型（标记 Timer 忽略序列化） =====
public class Room
{
    public string RoomId { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string Mode { get; set; } = "classic";
    public int BoardSize { get; set; } = 15;
    public string OwnerId { get; set; } = "";
    public List<string> Players { get; set; } = new();
    public string? CurrentTurn { get; set; }
    public int TimeLimit { get; set; } = 30;
    public DateTime? TurnStartTime { get; set; }

    [JsonIgnore]  // 忽略 Timer，避免序列化问题
    public Timer? TurnTimer { get; set; }
}