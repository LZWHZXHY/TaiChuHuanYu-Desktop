using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using TaiChuWeb_V2.Hubs;

namespace TaiChuWeb_V2.Services.Games.Xiangqi
{
    public sealed class XqPlayer
    {
        public Guid UserId { get; init; }
        public string Name { get; set; } = "玩家";
        public bool Online { get; set; } = true;
        public DateTime? OfflineSince { get; set; }
    }

    public sealed record XqMove(int From, int To, char Piece, char Captured);

    public sealed class XqRoom
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = "象棋房间";
        public int TimeLimit { get; set; } = 60;
        public Guid OwnerId { get; set; }
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public XqPlayer? Red { get; set; }
        public XqPlayer? Black { get; set; }
        /// <summary>waiting 等人 / playing 对局中 / ended 已结束（可以再来一局）</summary>
        public string Phase { get; set; } = "waiting";
        public int Round { get; set; }
        public string Board { get; set; } = XqRules.Initial;
        public char Turn { get; set; } = 'r';
        public List<XqMove> Moves { get; } = new();
        public Dictionary<string, int> Seen { get; } = new();
        public int SinceCapture { get; set; }
        public DateTime? Deadline { get; set; }
        public DateTime? StartedAt { get; set; }
        public Guid? DrawOfferBy { get; set; }
        public char? Winner { get; set; }
        public string? Reason { get; set; }
        /// <summary>
        /// 上一局的结果（谁赢、赢家名字、原因）。一局结束后有人点「返回大厅」，房间会回到等人状态，
        /// 以前结果就跟着没了（赢的一方看不到自己赢了），现在留在这里，等人界面也能看到。
        /// </summary>
        public (char? Winner, string? WinnerName, string Reason)? LastResult { get; set; }

        public IEnumerable<XqPlayer> Players => new[] { Red, Black }.Where(p => p != null)!;
        public XqPlayer? Of(Guid u) => Red?.UserId == u ? Red : Black?.UserId == u ? Black : null;
        public char? ColorOf(Guid u) => Red?.UserId == u ? 'r' : Black?.UserId == u ? 'b' : null;
        public XqPlayer? PlayerOf(char color) => color == 'r' ? Red : Black;
    }

    /// <summary>
    /// 联机象棋：房间、走子校验、胜负判定（将死 / 困毙 / 超时 / 认输 / 掉线 / 和棋）、写战绩。
    /// 状态通过 XiangqiHub 的 "State" 事件推给房间里的两位玩家。
    /// </summary>
    public sealed class XqManager(IHubContext<XiangqiHub> hub, MatchRecordService records, ILogger<XqManager> logger) : BackgroundService
    {
        public static readonly int[] TimeLimits = { 30, 60, 120, 180 };
        /// <summary>对局中掉线超过这么久判负</summary>
        public const int OfflineGraceSeconds = 60;
        /// <summary>连续这么多步没有吃子判和（60 回合）</summary>
        public const int NoCaptureDrawPlies = 120;

        private readonly ConcurrentDictionary<string, XqRoom> _rooms = new();
        private readonly ConcurrentDictionary<Guid, string> _userRoom = new();
        private readonly ConcurrentDictionary<Guid, int> _connections = new();
        /// <summary>观战名单（谁在看哪个房间、每间最多 20 人、房主是否关了观战）</summary>
        private readonly SpectatorRegistry _watch = new();
        private long _lastLobbyPush;

        private XqRoom? RoomOf(Guid u) => _userRoom.TryGetValue(u, out var id) && _rooms.TryGetValue(id, out var r) ? r : null;

        // ================= 查询 =================

        public object Lobby() => _rooms.Values
            .Select(r =>
            {
                // 观战人数、是否允许观战（观战的人不算房间人数，不影响「有空位」和快速匹配）
                var (watchers, allow) = _watch.Summary(r.Id);
                lock (r) return new { id = r.Id, name = r.Name, timeLimit = r.TimeLimit, phase = r.Phase, players = r.Players.Select(p => p.Name).ToList(), open = r.Players.Count() < 2, createdAt = r.CreatedAt, spectators = watchers, allowSpectate = allow, maxSpectators = SpectatorRegistry.MaxPerRoom };
            })
            .OrderBy(x => x.phase == "waiting" && x.open ? 0 : 1).ThenByDescending(x => x.createdAt)
            .Take(60).ToList();

        public object? StateFor(Guid u)
        {
            var room = RoomOf(u);
            if (room == null)
            {
                // 观战的人：同样的棋盘状态（象棋没有隐藏信息），只是 myColor 为空
                if (_watch.RoomOf(u) is string wid && _rooms.TryGetValue(wid, out var watched))
                    lock (watched) return BuildState(watched, Guid.Empty);
                return null;
            }
            lock (room) return BuildState(room, u);
        }

        private static object? PlayerView(XqPlayer? p) => p == null ? null : new { userId = p.UserId, name = p.Name, online = p.Online };

        /// <summary>viewer 为 Guid.Empty 或不在棋局里 = 观战视角（myColor 为空，前端只读）</summary>
        private object BuildState(XqRoom r, Guid viewer) => new
        {
            roomId = r.Id,
            roomName = r.Name,
            timeLimit = r.TimeLimit,
            ownerId = r.OwnerId,
            phase = r.Phase,
            round = r.Round,
            board = r.Board,
            turn = r.Turn.ToString(),
            moves = r.Moves.Select(m => new { from = m.From, to = m.To, piece = m.Piece.ToString(), captured = m.Captured == '.' ? null : m.Captured.ToString() }),
            deadline = r.Deadline,
            // 服务器现在的时间：前端用它校正本机时钟偏差，倒计时才准（本机时间快了慢了都不影响）
            serverNow = DateTime.UtcNow,
            inCheck = r.Phase == "playing" && XqRules.InCheck(r.Board, r.Turn),
            red = PlayerView(r.Red),
            black = PlayerView(r.Black),
            myColor = r.ColorOf(viewer)?.ToString(),
            drawOfferBy = r.DrawOfferBy,
            result = r.Phase == "ended" ? new { winner = r.Winner?.ToString(), winnerName = r.LastResult?.WinnerName, reason = r.Reason } : null,
            // 上一局结果：房间回到等人状态后也还在（前端在等人界面显示「上一局：红方胜（将死）· 某某」）
            lastResult = r.LastResult is { } lr ? new { winner = lr.Winner?.ToString(), winnerName = lr.WinnerName, reason = lr.Reason } : null,
            spectating = r.ColorOf(viewer) == null,
            watch = _watch.View(r.Id),
        };

        // ================= 推送 =================

        private async Task PushAsync(XqRoom room)
        {
            List<(Guid, object)> states;
            object publicState;
            List<string> watchers;
            lock (room)
            {
                states = room.Players.Select(p => (p.UserId, BuildState(room, p.UserId))).ToList();
                publicState = BuildState(room, Guid.Empty);
                watchers = _watch.UserIds(room.Id);
            }
            foreach (var (uid, state) in states)
            {
                try { await hub.Clients.User(uid.ToString()).SendAsync("State", state); }
                catch (Exception ex) { logger.LogDebug(ex, "推送象棋状态失败"); }
            }
            // 观战的人：一份状态发给所有人（按用户 ID 推，不用 Hub 分组，重连换了连接也收得到）
            if (watchers.Count > 0)
            {
                try { await hub.Clients.Users(watchers).SendAsync("State", publicState); }
                catch (Exception ex) { logger.LogDebug(ex, "推送象棋观战状态失败"); }
            }
        }

        /// <summary>告诉在大厅的人房间列表变了（最多一秒一次；漏掉的靠前端定时刷新）</summary>
        private async Task NotifyLobbyAsync()
        {
            var now = Environment.TickCount64;
            var last = Interlocked.Read(ref _lastLobbyPush);
            if (now - last < 1000 || Interlocked.CompareExchange(ref _lastLobbyPush, now, last) != last) return;
            try { await hub.Clients.All.SendAsync("LobbyChanged"); }
            catch (Exception ex) { logger.LogDebug(ex, "推送象棋大厅变化失败"); }
        }

        // ================= 观战 =================

        /// <summary>进房间观战（等人的、正在下的、刚下完的都能看）。在下棋的人不能同时观战</summary>
        public async Task<string?> WatchAsync(Guid u, string name, string roomId)
        {
            if (RoomOf(u) is { } mine) return mine.Id == roomId ? "你就在这个房间里" : "你正在别的房间下棋，先离开再去观战";
            if (!_rooms.TryGetValue((roomId ?? "").Trim(), out var room)) return "房间不存在，可能已经解散了";
            var (error, previous) = _watch.Add(room.Id, u, name);
            if (error != null) return error;
            if (previous != null && _rooms.TryGetValue(previous, out var old)) await PushAsync(old);
            await PushAsync(room);
            await NotifyLobbyAsync();
            return null;
        }

        /// <summary>不看了（离开或断线）；观战的人走了不影响棋局</summary>
        private async Task<bool> StopWatchingAsync(Guid u, bool notifySelf)
        {
            var roomId = _watch.Remove(u);
            if (roomId == null) return false;
            if (notifySelf) await SendLeftAsync(u);
            if (_rooms.TryGetValue(roomId, out var room)) await PushAsync(room);
            await NotifyLobbyAsync();
            return true;
        }

        /// <summary>观战的人坐下（房间在等人、有空位时；走原来的加入流程）</summary>
        public async Task<string?> SitDownAsync(Guid u, string name)
        {
            if (_watch.RoomOf(u) is not string roomId) return "你没有在观战";
            return await JoinAsync(u, name, roomId);
        }

        /// <summary>房主在没开局时开 / 关观战；关掉时把正在看的人送回大厅</summary>
        public async Task<string?> SetSpectateAsync(Guid u, bool allow)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在房间里";
            List<Guid> kicked;
            lock (room)
            {
                if (room.OwnerId != u) return "只有房主能设置观战";
                if (room.Phase == "playing") return "下棋的时候不能改观战设置";
                kicked = _watch.SetAllowed(room.Id, allow);
            }
            await KickWatchersAsync(kicked, "房主关掉了观战");
            await PushAsync(room);
            await NotifyLobbyAsync();
            return null;
        }

        private async Task KickWatchersAsync(List<Guid> users, string message)
        {
            foreach (var uid in users)
            {
                try { await hub.Clients.User(uid.ToString()).SendAsync("Notice", message); await SendLeftAsync(uid); }
                catch (Exception ex) { logger.LogDebug(ex, "通知观战的人失败"); }
            }
        }

        private Task SendLeftAsync(Guid u) => hub.Clients.User(u.ToString()).SendAsync("State", (object?)null);

        // ================= 进出房间 =================

        public async Task<string?> CreateAsync(Guid u, string name, string roomName, int timeLimit)
        {
            if (RoomOf(u) is { } old)
            {
                if (old.Phase == "playing") return "你还有一盘棋没下完";
                await LeaveAsync(u);
            }
            await StopWatchingAsync(u, notifySelf: false);
            if (!TimeLimits.Contains(timeLimit)) timeLimit = 60;
            var title = string.IsNullOrWhiteSpace(roomName) ? $"{name} 的棋局" : roomName.Trim();
            if (title.Length > 30) title = title[..30];
            var room = new XqRoom { Name = title, TimeLimit = timeLimit, OwnerId = u, Red = new XqPlayer { UserId = u, Name = name, Online = IsOnline(u) } };
            _rooms[room.Id] = room;
            _userRoom[u] = room.Id;
            await PushAsync(room);
            await NotifyLobbyAsync();
            return null;
        }

        public async Task<string?> JoinAsync(Guid u, string name, string roomId)
        {
            if (!_rooms.TryGetValue((roomId ?? "").Trim(), out var room)) return "房间不存在，可能已经解散了";
            if (RoomOf(u) is { } old)
            {
                if (old.Id == room.Id) { await PushAsync(room); return null; }
                if (old.Phase == "playing") return "你还有一盘棋没下完";
                await LeaveAsync(u);
            }
            lock (room)
            {
                if (room.Phase == "playing") return "这盘棋已经开始了";
                var p = new XqPlayer { UserId = u, Name = name, Online = IsOnline(u) };
                if (room.Red == null) room.Red = p;
                else if (room.Black == null) room.Black = p;
                else return "房间已满";
                _userRoom[u] = room.Id;
            }
            // 原来在观战（这一间或别的房间）：坐下了就不再算观战
            var watched = _watch.Remove(u);
            if (watched != null && watched != room.Id && _rooms.TryGetValue(watched, out var oldWatch)) await PushAsync(oldWatch);
            await PushAsync(room);
            await NotifyLobbyAsync();
            return null;
        }

        /// <summary>快速匹配：有人在等就加入，没有就开一个新房间（每步 60 秒）</summary>
        public async Task<string?> QuickAsync(Guid u, string name)
        {
            var waiting = _rooms.Values
                .Where(r => r.Phase == "waiting" && r.Players.Count() == 1 && r.Of(u) == null && r.Players.All(p => p.Online))
                .OrderBy(r => r.CreatedAt).FirstOrDefault();
            if (waiting != null) return await JoinAsync(u, name, waiting.Id);
            return await CreateAsync(u, name, $"{name} 的快速匹配", 60);
        }

        public async Task<string?> LeaveAsync(Guid u)
        {
            var room = RoomOf(u);
            if (room == null)
            {
                // 观战的人点「离开」：不看了
                if (!await StopWatchingAsync(u, notifySelf: true)) await SendLeftAsync(u);
                return null;
            }
            (XqRoom Room, char? Winner, string Reason, List<XqPlayer> Players)? finished = null;
            var empty = false;
            lock (room)
            {
                var color = room.ColorOf(u);
                if (room.Phase == "playing" && color is char c)
                {
                    var name = room.Of(u)?.Name ?? "玩家";
                    finished = Finish(room, XqRules.Other(c), $"{name} 离开了对局");
                }
                if (room.Red?.UserId == u) room.Red = null;
                if (room.Black?.UserId == u) room.Black = null;
                // 让留下的人坐到红方（房主执红）
                if (room.Red == null && room.Black != null) { room.Red = room.Black; room.Black = null; }
                if (room.Red != null && room.OwnerId == u) room.OwnerId = room.Red.UserId;
                empty = room.Red == null && room.Black == null;
                if (empty) _rooms.TryRemove(room.Id, out _);
                else if (room.Phase == "ended") { room.Phase = "waiting"; room.DrawOfferBy = null; }
            }
            _userRoom.TryRemove(u, out _);
            if (finished is { } f) await RecordAsync(f.Room, f.Winner, f.Reason, f.Players);
            await SendLeftAsync(u);
            if (!empty) await PushAsync(room);
            // 房间没人了：观战的人也送回大厅
            else await KickWatchersAsync(_watch.RemoveRoom(room.Id), "下棋的人都走了，房间解散了");
            await NotifyLobbyAsync();
            return null;
        }

        // ================= 对局 =================

        public async Task<string?> StartAsync(Guid u)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在房间里";
            lock (room)
            {
                if (room.OwnerId != u) return "只有房主可以开始";
                if (room.Phase == "playing") return "对局已经开始了";
                if (room.Red == null || room.Black == null) return "还差一位对手";
                if (!room.Red.Online || !room.Black.Online) return "对手暂时掉线了";
                // 再来一局时交换先后手
                if (room.Round > 0) (room.Red, room.Black) = (room.Black, room.Red);
                room.Round++;
                room.Phase = "playing";
                room.Board = XqRules.Initial;
                room.Turn = 'r';
                room.Moves.Clear();
                room.Seen.Clear();
                room.Seen[room.Board + room.Turn] = 1;
                room.SinceCapture = 0;
                room.DrawOfferBy = null;
                room.Winner = null;
                room.Reason = null;
                room.StartedAt = DateTime.UtcNow;
                room.Deadline = DateTime.UtcNow.AddSeconds(room.TimeLimit);
            }
            await PushAsync(room);
            return null;
        }

        public async Task<string?> MoveAsync(Guid u, int from, int to)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在房间里";
            (XqRoom Room, char? Winner, string Reason, List<XqPlayer> Players)? finished = null;
            lock (room)
            {
                if (room.Phase != "playing") return "对局还没开始或已经结束";
                if (room.ColorOf(u) != room.Turn) return "还没轮到你";
                if (!XqRules.IsLegal(room.Board, room.Turn, from, to)) return "这步棋不合规则";

                var piece = room.Board[from];
                var captured = room.Board[to];
                room.Board = XqRules.Apply(room.Board, from, to);
                room.Moves.Add(new XqMove(from, to, piece, captured));
                room.SinceCapture = captured == '.' ? room.SinceCapture + 1 : 0;
                room.DrawOfferBy = null;
                var mover = room.Turn;
                room.Turn = XqRules.Other(mover);
                var key = room.Board + room.Turn;
                room.Seen[key] = room.Seen.GetValueOrDefault(key) + 1;

                if (!XqRules.HasAnyLegal(room.Board, room.Turn))
                    finished = Finish(room, mover, XqRules.InCheck(room.Board, room.Turn) ? "将死" : "困毙");
                else if (room.SinceCapture >= NoCaptureDrawPlies)
                    finished = Finish(room, null, "60 回合没有吃子，和棋");
                else if (room.Seen[key] >= 3)
                    finished = Finish(room, null, "同一局面重复三次，和棋");
                else
                    room.Deadline = DateTime.UtcNow.AddSeconds(room.TimeLimit);
            }
            if (finished is { } f) await RecordAsync(f.Room, f.Winner, f.Reason, f.Players);
            await PushAsync(room);
            return null;
        }

        public async Task<string?> ResignAsync(Guid u)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在房间里";
            (XqRoom Room, char? Winner, string Reason, List<XqPlayer> Players)? finished;
            lock (room)
            {
                if (room.Phase != "playing" || room.ColorOf(u) is not char c) return "现在不能认输";
                finished = Finish(room, XqRules.Other(c), $"{room.Of(u)?.Name ?? "玩家"} 认输");
            }
            await RecordAsync(finished.Value.Room, finished.Value.Winner, finished.Value.Reason, finished.Value.Players);
            await PushAsync(room);
            return null;
        }

        public async Task<string?> OfferDrawAsync(Guid u)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在房间里";
            lock (room)
            {
                if (room.Phase != "playing" || room.ColorOf(u) == null) return "现在不能提和";
                if (room.Moves.Count < 10) return "至少走满 10 步才能提和";
                room.DrawOfferBy = u;
            }
            await PushAsync(room);
            return null;
        }

        public async Task<string?> AnswerDrawAsync(Guid u, bool accept)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在房间里";
            (XqRoom Room, char? Winner, string Reason, List<XqPlayer> Players)? finished = null;
            lock (room)
            {
                if (room.Phase != "playing" || room.DrawOfferBy == null || room.DrawOfferBy == u || room.ColorOf(u) == null) return "对方没有提和";
                if (accept) finished = Finish(room, null, "双方同意和棋");
                else room.DrawOfferBy = null;
            }
            if (finished is { } f) await RecordAsync(f.Room, f.Winner, f.Reason, f.Players);
            await PushAsync(room);
            return null;
        }

        /// <summary>结束对局（在锁内调用），返回写战绩需要的信息</summary>
        private static (XqRoom, char?, string, List<XqPlayer>) Finish(XqRoom room, char? winner, string reason)
        {
            // 双方都还没走过棋就结束（有人离开 / 掉线 / 超时 / 认输）：算「对局取消」，不分胜负、不记战绩。
            // 以前会记成「红方胜（某某 离开了对局）· 0 回合」，常见于「再来一局」刚开、对方正要回大厅或已经走开
            if (winner != null && room.Moves.Count < 2)
            {
                winner = null;
                reason = $"对局取消：{reason}（双方都还没走棋，不计胜负）";
            }
            room.Phase = "ended";
            room.Winner = winner;
            room.Reason = reason;
            room.Deadline = null;
            room.DrawOfferBy = null;
            room.LastResult = (winner, winner is char w ? room.PlayerOf(w)?.Name : null, reason);
            return (room, winner, reason, new List<XqPlayer> { room.Red!, room.Black! }.Where(p => p != null).ToList());
        }

        private async Task RecordAsync(XqRoom room, char? winner, string reason, List<XqPlayer> players)
        {
            try
            {
                var moves = room.Moves.Count;
                // 对局取消（双方都还没走棋）不记战绩
                if (players.Count != 2 || moves < 2) return;
                await records.SaveAsync(new MatchRecord
                {
                    Game = "xiangqi",
                    Mode = $"联机 · 每步 {room.TimeLimit} 秒",
                    // 战绩里写清楚哪方赢、怎么赢的，例如「红方胜（将死）· 23 回合」
                    Summary = $"{(winner == null ? reason : $"{(winner == 'r' ? "红方" : "黑方")}胜（{reason}）")} · {(moves + 1) / 2} 回合",
                    HasBots = false,
                    StartedAt = room.StartedAt ?? DateTime.UtcNow,
                    EndedAt = DateTime.UtcNow,
                    Players = players.Select((p, i) => new MatchPlayer
                    {
                        Seat = i,
                        UserId = p.UserId,
                        Name = p.Name,
                        Role = i == 0 ? "红方" : "黑方",
                        Result = winner == null ? "draw" : (i == 0 ? 'r' : 'b') == winner ? "win" : "lose",
                    }).ToList(),
                });
            }
            catch (Exception ex) { logger.LogWarning(ex, "象棋战绩记录失败"); }
        }

        // ================= 连接 =================

        private bool IsOnline(Guid u) => _connections.GetValueOrDefault(u) > 0;

        public async Task ConnectedAsync(Guid u)
        {
            _connections.AddOrUpdate(u, 1, (_, n) => n + 1);
            if (RoomOf(u) is not { } room) return;
            lock (room) { if (room.Of(u) is { } p) { p.Online = true; p.OfflineSince = null; } }
            await PushAsync(room);
        }

        public async Task DisconnectedAsync(Guid u)
        {
            var left = _connections.AddOrUpdate(u, 0, (_, n) => Math.Max(0, n - 1));
            if (left > 0) return;
            // 观战的人所有页面都断了：移出名单（不占位置；前端重连后会自动回来接着看）
            if (RoomOf(u) == null && await StopWatchingAsync(u, notifySelf: false)) return;
            if (RoomOf(u) is not { } room) return;
            lock (room)
            {
                if (room.Of(u) is not { } p) return;
                // 掉线先保留 OfflineGraceSeconds 秒：对局中等重连；等人时也不马上离开（网络抖一下、切个页面房间不会马上没了），
                // 超时由定时任务处理（对局中判负，等人时让出位置）
                p.Online = false;
                p.OfflineSince = DateTime.UtcNow;
            }
            await PushAsync(room);
        }

        // ================= 定时：超时、掉线 =================

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var room in _rooms.Values)
                {
                    (XqRoom Room, char? Winner, string Reason, List<XqPlayer> Players)? finished = null;
                    try
                    {
                        List<Guid>? leave = null;
                        lock (room)
                        {
                            if (room.Phase != "playing")
                            {
                                // 等人 / 结束后：掉线超过宽限时间的人离开房间
                                var stamp = DateTime.UtcNow;
                                leave = room.Players.Where(p => !p.Online && p.OfflineSince is DateTime since && (stamp - since).TotalSeconds >= OfflineGraceSeconds).Select(p => p.UserId).ToList();
                            }
                        }
                        if (leave != null)
                        {
                            foreach (var uid in leave)
                            {
                                bool stillOffline;
                                lock (room) stillOffline = room.Of(uid) is { Online: false };
                                if (stillOffline) await LeaveAsync(uid);
                            }
                            continue;
                        }
                        lock (room)
                        {
                            if (room.Phase != "playing") continue;
                            var now = DateTime.UtcNow;
                            var offline = room.Players.FirstOrDefault(p => !p.Online && p.OfflineSince is DateTime since && (now - since).TotalSeconds >= OfflineGraceSeconds);
                            if (offline != null && room.ColorOf(offline.UserId) is char oc)
                                finished = Finish(room, XqRules.Other(oc), $"{offline.Name} 掉线超过 {OfflineGraceSeconds} 秒");
                            else if (room.Deadline is DateTime dl && now >= dl)
                                finished = Finish(room, XqRules.Other(room.Turn), $"{room.PlayerOf(room.Turn)?.Name ?? "玩家"} 超时（每步 {room.TimeLimit} 秒）");
                        }
                        if (finished is { } f)
                        {
                            await RecordAsync(f.Room, f.Winner, f.Reason, f.Players);
                            await PushAsync(room);
                        }
                    }
                    catch (Exception ex) { logger.LogWarning(ex, "象棋定时任务出错"); }
                }
            }
        }
    }
}
