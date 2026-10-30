using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Security.Claims;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Games;
using Timer = System.Timers.Timer;

namespace TaiChuWeb_V2.Hubs;

/// <summary>
/// 联机五子棋。房间、棋盘、轮次、胜负都由服务器维护，结果写进战绩（不依赖客户端上报）。
/// 事件：RoomCreated / PlayerJoined / PlayerLeft / GameStarted / MoveMade / TurnChanged / GameOver / Error
/// </summary>
[Authorize]
public class GameHub(MatchRecordService records, IServiceScopeFactory scopes, IHubContext<GameHub> hubContext) : Hub
{
    private static readonly ConcurrentDictionary<string, string> _playerRooms = new();
    private static readonly ConcurrentDictionary<string, Room> _rooms = new();

    private string? CurrentUserId => Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    private string CurrentName => Context.User?.FindFirst(ClaimTypes.Name)?.Value is { Length: > 0 } n ? n : "玩家";

    private static object PlayersView(Room room) => room.Players.Select(id => new
    {
        id,
        name = room.Names.TryGetValue(id, out var n) ? n : "玩家",
        isOwner = id == room.OwnerId,
    }).ToList();

    private static object RoomView(Room r) => new
    {
        roomId = r.RoomId,
        roomName = r.RoomName,
        mode = r.Mode,
        boardSize = r.BoardSize,
        timeLimit = r.TimeLimit,
        ownerName = r.Names.TryGetValue(r.OwnerId, out var n) ? n : "玩家",
        playerCount = r.Players.Count,
        started = r.Started,
        createdAt = r.CreatedAt,
    };

    // ===== 0. 房间列表（大厅里显示等待对手的房间） =====
    public object[] ListRooms() => _rooms.Values
        .Where(r => !r.Started && r.Players.Count == 1)
        .OrderByDescending(r => r.CreatedAt)
        .Take(50)
        .Select(RoomView)
        .ToArray();

    // ===== 1. 创建房间 =====
    public async Task CreateRoom(string roomName, string mode, int boardSize, int timeLimit = 30)
    {
        try
        {
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
            // 已经在别的房间里：先离开，避免卡在旧房间
            await LeaveCurrentRoomAsync(userId, Context.ConnectionId, "离开了房间");

            var roomId = Guid.NewGuid().ToString("N")[..8];
            var name = roomName.Trim();
            if (name.Length > 30) name = name[..30];
            var room = new Room
            {
                RoomId = roomId,
                RoomName = name,
                Mode = string.IsNullOrWhiteSpace(mode) ? "classic" : mode,
                BoardSize = boardSize,
                OwnerId = userId,
                Players = new List<string> { userId },
                TimeLimit = timeLimit,
                CreatedAt = DateTime.UtcNow,
            };
            room.Names[userId] = CurrentName;

            if (!_rooms.TryAdd(roomId, room))
            {
                await Clients.Caller.SendAsync("Error", new { message = "创建房间失败，请重试" });
                return;
            }

            _playerRooms[userId] = roomId;
            await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

            await Clients.Caller.SendAsync("RoomCreated", new
            {
                roomId,
                roomName = room.RoomName,
                mode = room.Mode,
                boardSize,
                timeLimit,
                ownerId = userId,
                players = PlayersView(room),
            });
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
        roomId = (roomId ?? string.Empty).Trim();

        if (!_rooms.TryGetValue(roomId, out var room))
        {
            await Clients.Caller.SendAsync("Error", new { message = "房间不存在，可能已经解散了" });
            return;
        }
        if (room.Players.Contains(userId))
        {
            await Clients.Caller.SendAsync("Error", new { message = "你已在房间中" });
            return;
        }
        if (room.Players.Count >= 2)
        {
            await Clients.Caller.SendAsync("Error", new { message = "房间已满" });
            return;
        }

        await LeaveCurrentRoomAsync(userId, Context.ConnectionId, "离开了房间");

        room.Players.Add(userId);
        room.Names[userId] = CurrentName;
        _playerRooms[userId] = roomId;
        await Groups.AddToGroupAsync(Context.ConnectionId, roomId);

        await Clients.Group(roomId).SendAsync("PlayerJoined", new
        {
            roomId,
            roomName = room.RoomName,
            mode = room.Mode,
            boardSize = room.BoardSize,
            timeLimit = room.TimeLimit,
            ownerId = room.OwnerId,
            playerCount = room.Players.Count,
            players = PlayersView(room),
        });
    }

    // ===== 2.5 快速匹配：有人在等就加入，没有就开一个新房间等人 =====
    public async Task<object> QuickMatch()
    {
        var userId = CurrentUserId ?? Context.ConnectionId;
        var waiting = _rooms.Values
            .Where(r => !r.Started && r.Players.Count == 1 && !r.Players.Contains(userId) && r.BoardSize == 15)
            .OrderBy(r => r.CreatedAt)
            .FirstOrDefault();
        if (waiting != null)
        {
            await JoinRoom(waiting.RoomId);
            return new { joined = true, roomId = waiting.RoomId };
        }
        await CreateRoom($"{CurrentName} 的快速匹配", "classic", 15, 30);
        return new { joined = false, roomId = _playerRooms.TryGetValue(userId, out var id) ? id : null };
    }

    // ===== 2.6 离开房间（返回大厅时调用；对局中离开算认输） =====
    public async Task LeaveRoom()
    {
        var userId = CurrentUserId ?? Context.ConnectionId;
        await LeaveCurrentRoomAsync(userId, Context.ConnectionId, "离开了对局");
    }

    // ===== 2.7 认输 =====
    public async Task Resign(string roomId)
    {
        var userId = CurrentUserId ?? Context.ConnectionId;
        if (!_rooms.TryGetValue(roomId, out var room) || !room.Players.Contains(userId)) return;
        string? winner;
        List<string> players;
        lock (room)
        {
            if (room.Board == null) return;
            winner = room.Players.FirstOrDefault(p => p != userId);
            players = room.Players.ToList();
            EndGame(room);
        }
        var name = room.Names.TryGetValue(userId, out var n) ? n : "玩家";
        if (winner != null) _ = RecordGobangAsync(room, players, winner, "认输");
        await Clients.Group(roomId).SendAsync("GameOver", new { winner, reason = $"{name} 认输", resignPlayer = userId });
    }

    // ===== 3. 落子（服务器校验轮次、位置并判断胜负） =====
    public async Task MakeMove(string roomId, int row, int col)
    {
        var userId = CurrentUserId ?? Context.ConnectionId;
        if (!_rooms.TryGetValue(roomId, out var room))
        {
            await Clients.Caller.SendAsync("Error", new { message = "房间不存在" });
            return;
        }

        string? nextPlayer;
        List<(int Row, int Col)>? winLine = null;
        var draw = false;
        List<string> players;
        lock (room)
        {
            if (room.Board == null || room.CurrentTurn == null)
            {
                _ = Clients.Caller.SendAsync("Error", new { message = "对局还没开始或已经结束" });
                return;
            }
            if (room.CurrentTurn != userId)
            {
                _ = Clients.Caller.SendAsync("Error", new { message = "还没轮到你下棋" });
                return;
            }
            nextPlayer = room.Players.FirstOrDefault(p => p != userId);
            if (nextPlayer == null)
            {
                _ = Clients.Caller.SendAsync("Error", new { message = "没有对手" });
                return;
            }
            var size = room.Board.GetLength(0);
            if (row < 0 || col < 0 || row >= size || col >= size || room.Board[row, col] != 0)
            {
                _ = Clients.Caller.SendAsync("Error", new { message = "这里不能落子" });
                return;
            }
            var color = userId == room.OwnerId ? 1 : 2;
            room.Board[row, col] = color;
            room.MoveCount++;
            players = room.Players.ToList();
            winLine = FiveInRow(room.Board, row, col, color);
            draw = winLine == null && room.MoveCount >= size * size;
            if (winLine != null || draw) EndGame(room);
            else
            {
                room.CurrentTurn = nextPlayer;
                StartTurnTimer(room);
            }
        }

        await Clients.Group(roomId).SendAsync("MoveMade", new
        {
            row,
            col,
            playerId = userId,
            color = userId == room.OwnerId ? 1 : 2,
            currentTurn = winLine != null || draw ? null : nextPlayer,
            remainingTime = room.TimeLimit,
        });

        if (winLine != null)
        {
            _ = RecordGobangAsync(room, players, userId, "五子连珠");
            await Clients.Group(roomId).SendAsync("GameOver", new { winner = userId, reason = "五子连珠", line = winLine.Select(p => new[] { p.Row, p.Col }) });
        }
        else if (draw)
        {
            _ = RecordGobangAsync(room, players, null, "棋盘下满");
            await Clients.Group(roomId).SendAsync("GameOver", new { winner = (string?)null, reason = "棋盘下满，和棋" });
        }
    }

    // ===== 4. 开始游戏（房主操作；对局结束后可以再来一局） =====
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

        lock (room)
        {
            room.Started = true;
            room.CurrentTurn = room.OwnerId;
            room.Board = new int[room.BoardSize, room.BoardSize];
            room.MoveCount = 0;
            room.StartedAt = DateTime.UtcNow;
            StartTurnTimer(room);
        }

        await Clients.Group(roomId).SendAsync("GameStarted", new
        {
            roomId,
            boardSize = room.BoardSize,
            mode = room.Mode,
            players = room.Players,
            playerList = PlayersView(room),
            ownerId = room.OwnerId,
            currentTurn = room.CurrentTurn,
            timeLimit = room.TimeLimit,
        });
    }

    // ===== 5. 计时器 =====
    private void StartTurnTimer(Room room)
    {
        StopTimer(room);
        var roomId = room.RoomId;
        var timer = new Timer(room.TimeLimit * 1000) { AutoReset = false };
        timer.Elapsed += (_, _) => HandleTimeout(roomId);
        timer.Start();
        room.TurnTimer = timer;
        room.TurnStartTime = DateTime.Now;
    }

    private static void StopTimer(Room room)
    {
        if (room.TurnTimer == null) return;
        room.TurnTimer.Stop();
        room.TurnTimer.Dispose();
        room.TurnTimer = null;
    }

    /// <summary>对局结束：清理棋盘和计时器，房间保留（可以再来一局）</summary>
    private static void EndGame(Room room)
    {
        room.CurrentTurn = null;
        room.Board = null;
        room.Started = false;
        StopTimer(room);
    }

    private void HandleTimeout(string roomId)
    {
        if (!_rooms.TryGetValue(roomId, out var room)) return;
        string? timeoutPlayer, nextPlayer;
        List<string> players;
        lock (room)
        {
            if (room.CurrentTurn == null || room.Board == null) return;
            timeoutPlayer = room.CurrentTurn;
            nextPlayer = room.Players.FirstOrDefault(p => p != timeoutPlayer);
            players = room.Players.ToList();
            EndGame(room);
        }
        if (nextPlayer != null) _ = RecordGobangAsync(room, players, nextPlayer, "对手超时");
        var name = room.Names.TryGetValue(timeoutPlayer!, out var n) ? n : "玩家";

        // 计时器回调发生在 Hub 实例释放之后，必须用 IHubContext 推送
        _ = Task.Run(async () =>
        {
            try
            {
                await hubContext.Clients.Group(roomId).SendAsync("GameOver", new
                {
                    winner = nextPlayer,
                    reason = $"{name} 超时（{room.TimeLimit} 秒）",
                    timeoutPlayer,
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[{DateTime.Now}] 超时发送失败: {ex.Message}");
            }
        });
    }

    /// <summary>判断五连，返回连成的那几个点（没有则 null）</summary>
    private static List<(int Row, int Col)>? FiveInRow(int[,] board, int row, int col, int color)
    {
        var size = board.GetLength(0);
        foreach (var (dr, dc) in new[] { (0, 1), (1, 0), (1, 1), (1, -1) })
        {
            var line = new List<(int Row, int Col)> { (row, col) };
            for (var k = 1; k < size; k++) { int r = row + dr * k, c = col + dc * k; if (r < 0 || c < 0 || r >= size || c >= size || board[r, c] != color) break; line.Add((r, c)); }
            for (var k = 1; k < size; k++) { int r = row - dr * k, c = col - dc * k; if (r < 0 || c < 0 || r >= size || c >= size || board[r, c] != color) break; line.Insert(0, (r, c)); }
            if (line.Count >= 5) return line;
        }
        return null;
    }

    /// <summary>把联机五子棋的结果写进战绩（winner 为空表示和棋）</summary>
    private async Task RecordGobangAsync(Room room, List<string> players, string? winner, string reason)
    {
        try
        {
            if (players.Count != 2) return;
            var ids = players.Select(p => Guid.TryParse(p, out var g) ? g : (Guid?)null).ToList();
            var known = ids.Where(i => i.HasValue).Select(i => i!.Value).ToList();
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var names = await db.Users.AsNoTracking().Where(u => known.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
            await records.SaveAsync(new MatchRecord
            {
                Game = "gobang",
                Mode = $"联机 · {room.BoardSize} 路",
                Summary = $"{reason} · {room.MoveCount} 手",
                HasBots = false,
                StartedAt = room.StartedAt ?? DateTime.UtcNow,
                EndedAt = DateTime.UtcNow,
                Players = players.Select((p, i) => new MatchPlayer
                {
                    Seat = i,
                    UserId = ids[i],
                    Name = ids[i] is Guid g && names.TryGetValue(g, out var n) ? n : "游客",
                    Role = p == room.OwnerId ? "黑方" : "白方",
                    Result = winner == null ? "draw" : p == winner ? "win" : "lose",
                }).ToList(),
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[{DateTime.Now}] 五子棋战绩记录失败: {ex.Message}");
        }
    }

    /// <summary>离开当前房间：对局中离开判负；房主离开时把房主交给对手；没人了就解散</summary>
    private async Task LeaveCurrentRoomAsync(string userId, string connectionId, string reason)
    {
        if (!_playerRooms.TryRemove(userId, out var roomId)) return;
        await Groups.RemoveFromGroupAsync(connectionId, roomId);
        if (!_rooms.TryGetValue(roomId, out var room)) return;

        string? winner = null;
        var wasPlaying = false;
        List<string> players;
        lock (room)
        {
            players = room.Players.ToList();
            if (room.Board != null)
            {
                wasPlaying = true;
                winner = room.Players.FirstOrDefault(p => p != userId);
                EndGame(room);
            }
            room.Players.Remove(userId);
            if (room.Players.Count > 0 && room.OwnerId == userId) room.OwnerId = room.Players[0];
        }
        var name = room.Names.TryGetValue(userId, out var n) ? n : "玩家";

        if (wasPlaying && winner != null)
        {
            _ = RecordGobangAsync(room, players, winner, "对手离开");
            await Clients.Group(roomId).SendAsync("GameOver", new { winner, reason = $"{name} {reason}，判负", leftPlayer = userId });
        }

        if (room.Players.Count == 0)
        {
            StopTimer(room);
            _rooms.TryRemove(roomId, out _);
            return;
        }
        room.Names.TryRemove(userId, out _);
        await Clients.Group(roomId).SendAsync("PlayerLeft", new { playerId = userId, name, ownerId = room.OwnerId, players = PlayersView(room) });
    }

    // ===== 6. 断开连接 =====
    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        var userId = CurrentUserId ?? Context.ConnectionId;
        await LeaveCurrentRoomAsync(userId, Context.ConnectionId, "掉线了");
        await base.OnDisconnectedAsync(exception);
    }
}

// ===== 房间模型 =====
public class Room
{
    public string RoomId { get; set; } = "";
    public string RoomName { get; set; } = "";
    public string Mode { get; set; } = "classic";
    public int BoardSize { get; set; } = 15;
    public string OwnerId { get; set; } = "";
    public List<string> Players { get; set; } = new();
    /// <summary>玩家 Id → 显示名（来自登录凭证）</summary>
    public ConcurrentDictionary<string, string> Names { get; } = new();
    public string? CurrentTurn { get; set; }
    public int TimeLimit { get; set; } = 30;
    public DateTime? TurnStartTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>对局进行中</summary>
    public bool Started { get; set; }

    /// <summary>服务器端棋盘（0 空 / 1 黑 / 2 白），null 表示没有进行中的对局</summary>
    [JsonIgnore]
    public int[,]? Board { get; set; }
    public int MoveCount { get; set; }
    public DateTime? StartedAt { get; set; }

    [JsonIgnore]
    public Timer? TurnTimer { get; set; }
}
