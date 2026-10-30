using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using TaiChuWeb_V2.Hubs;

namespace TaiChuWeb_V2.Services.Games.Go
{
    public sealed class GoPlayer
    {
        public Guid UserId { get; init; }
        public string Name { get; set; } = "玩家";
        public bool Online { get; set; } = true;
        public DateTime? OfflineSince { get; set; }
    }

    /// <summary>一手棋；Index 为 -1 表示停一手</summary>
    public sealed record GoMove(int Index, char Color, int Captured);

    public sealed class GoRoom
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = "围棋房间";
        public int Size { get; set; } = 19;
        public int TimeLimit { get; set; } = 60;
        public Guid OwnerId { get; set; }
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public GoPlayer? Black { get; set; }
        public GoPlayer? White { get; set; }
        /// <summary>waiting 等人 / playing 对局中 / scoring 点目 / ended 已结束</summary>
        public string Phase { get; set; } = "waiting";
        public int Round { get; set; }
        public string Board { get; set; } = "";
        public char Turn { get; set; } = 'b';
        public int Ko { get; set; } = -1;
        public int Passes { get; set; }
        public List<GoMove> Moves { get; } = new();
        public Dictionary<char, int> Captures { get; } = new() { ['b'] = 0, ['w'] = 0 };
        public HashSet<int> Dead { get; } = new();
        public HashSet<char> Confirmed { get; } = new();
        public DateTime? Deadline { get; set; }
        public DateTime? StartedAt { get; set; }
        public char? Winner { get; set; }
        public string? Reason { get; set; }
        public (int Black, double White)? FinalScore { get; set; }

        public IEnumerable<GoPlayer> Players => new[] { Black, White }.Where(p => p != null)!;
        public GoPlayer? Of(Guid u) => Black?.UserId == u ? Black : White?.UserId == u ? White : null;
        public char? ColorOf(Guid u) => Black?.UserId == u ? 'b' : White?.UserId == u ? 'w' : null;
        public GoPlayer? PlayerOf(char color) => color == 'b' ? Black : White;
        public bool Active => Phase is "playing" or "scoring";
    }

    /// <summary>
    /// 联机围棋：房间、落子校验（提子 / 禁着点 / 打劫）、停一手、双方确认的点目、中国规则数子、写战绩。
    /// 状态通过 GoHub 的 "State" 事件推给两位玩家。
    /// </summary>
    public sealed class GoManager(IHubContext<GoHub> hub, MatchRecordService records, ILogger<GoManager> logger) : BackgroundService
    {
        public static readonly int[] TimeLimits = { 30, 60, 120, 180 };
        public const int OfflineGraceSeconds = 60;
        /// <summary>点目阶段的时限：到时按当前标记自动结算</summary>
        public const int ScoringSeconds = 180;

        private readonly ConcurrentDictionary<string, GoRoom> _rooms = new();
        private readonly ConcurrentDictionary<Guid, string> _userRoom = new();
        private readonly ConcurrentDictionary<Guid, int> _connections = new();

        private GoRoom? RoomOf(Guid u) => _userRoom.TryGetValue(u, out var id) && _rooms.TryGetValue(id, out var r) ? r : null;

        // ================= 查询 =================

        public object Lobby() => _rooms.Values
            .Select(r => { lock (r) return new { id = r.Id, name = r.Name, size = r.Size, timeLimit = r.TimeLimit, phase = r.Phase, players = r.Players.Select(p => p.Name).ToList(), open = r.Players.Count() < 2, createdAt = r.CreatedAt }; })
            .OrderBy(x => x.phase == "waiting" && x.open ? 0 : 1).ThenByDescending(x => x.createdAt)
            .Take(60).ToList();

        public object? StateFor(Guid u)
        {
            var room = RoomOf(u);
            if (room == null) return null;
            lock (room) return BuildState(room, u);
        }

        private static object? PlayerView(GoPlayer? p) => p == null ? null : new { userId = p.UserId, name = p.Name, online = p.Online };

        private static object BuildState(GoRoom r, Guid viewer)
        {
            var scoring = r.Board.Length > 0 && (r.Phase == "scoring" || (r.Phase == "ended" && r.FinalScore != null));
            var score = scoring ? GoRules.Score(r.Board, r.Size, r.Dead) : default;
            return new
            {
                roomId = r.Id,
                roomName = r.Name,
                size = r.Size,
                timeLimit = r.TimeLimit,
                ownerId = r.OwnerId,
                phase = r.Phase,
                round = r.Round,
                board = r.Board,
                turn = r.Turn.ToString(),
                passes = r.Passes,
                moves = r.Moves.Select(m => new { index = m.Index, color = m.Color.ToString(), captured = m.Captured }),
                captures = new { b = r.Captures['b'], w = r.Captures['w'] },
                deadline = r.Deadline,
                black = PlayerView(r.Black),
                white = PlayerView(r.White),
                myColor = r.ColorOf(viewer)?.ToString(),
                dead = r.Dead.OrderBy(i => i).ToList(),
                confirmed = new { b = r.Confirmed.Contains('b'), w = r.Confirmed.Contains('w') },
                score = scoring ? new { black = score.Black, white = score.White, owner = score.Owner, komi = GoRules.Komi } : null,
                result = r.Phase == "ended" ? new { winner = r.Winner?.ToString(), reason = r.Reason } : null,
            };
        }

        // ================= 推送 =================

        private async Task PushAsync(GoRoom room)
        {
            List<(Guid, object)> states;
            lock (room) states = room.Players.Select(p => (p.UserId, BuildState(room, p.UserId))).ToList();
            foreach (var (uid, state) in states)
            {
                try { await hub.Clients.User(uid.ToString()).SendAsync("State", state); }
                catch (Exception ex) { logger.LogDebug(ex, "推送围棋状态失败"); }
            }
        }

        private Task SendLeftAsync(Guid u) => hub.Clients.User(u.ToString()).SendAsync("State", (object?)null);

        // ================= 进出房间 =================

        public async Task<string?> CreateAsync(Guid u, string name, string roomName, int size, int timeLimit)
        {
            if (RoomOf(u) is { } old)
            {
                if (old.Active) return "你还有一盘棋没下完";
                await LeaveAsync(u);
            }
            if (!GoRules.Sizes.Contains(size)) size = 19;
            if (!TimeLimits.Contains(timeLimit)) timeLimit = 60;
            var title = string.IsNullOrWhiteSpace(roomName) ? $"{name} 的棋局" : roomName.Trim();
            if (title.Length > 30) title = title[..30];
            var room = new GoRoom { Name = title, Size = size, TimeLimit = timeLimit, OwnerId = u, Black = new GoPlayer { UserId = u, Name = name, Online = IsOnline(u) } };
            _rooms[room.Id] = room;
            _userRoom[u] = room.Id;
            await PushAsync(room);
            return null;
        }

        public async Task<string?> JoinAsync(Guid u, string name, string roomId)
        {
            if (!_rooms.TryGetValue((roomId ?? "").Trim(), out var room)) return "房间不存在，可能已经解散了";
            if (RoomOf(u) is { } old)
            {
                if (old.Id == room.Id) { await PushAsync(room); return null; }
                if (old.Active) return "你还有一盘棋没下完";
                await LeaveAsync(u);
            }
            lock (room)
            {
                if (room.Active) return "这盘棋已经开始了";
                var p = new GoPlayer { UserId = u, Name = name, Online = IsOnline(u) };
                if (room.Black == null) room.Black = p;
                else if (room.White == null) room.White = p;
                else return "房间已满";
                _userRoom[u] = room.Id;
            }
            await PushAsync(room);
            return null;
        }

        /// <summary>快速匹配：有人在等就加入（任意路数），没有就开一个 19 路、每步 60 秒的房间</summary>
        public async Task<string?> QuickAsync(Guid u, string name)
        {
            var waiting = _rooms.Values
                .Where(r => r.Phase == "waiting" && r.Players.Count() == 1 && r.Of(u) == null && r.Players.All(p => p.Online))
                .OrderBy(r => r.CreatedAt).FirstOrDefault();
            if (waiting != null) return await JoinAsync(u, name, waiting.Id);
            return await CreateAsync(u, name, $"{name} 的快速匹配", 19, 60);
        }

        public async Task<string?> LeaveAsync(Guid u)
        {
            var room = RoomOf(u);
            if (room == null) { await SendLeftAsync(u); return null; }
            Finished? finished = null;
            var empty = false;
            lock (room)
            {
                if (room.Active && room.ColorOf(u) is char c)
                    finished = Finish(room, GoRules.Other(c), $"{room.Of(u)?.Name ?? "玩家"} 离开了对局");
                if (room.Black?.UserId == u) room.Black = null;
                if (room.White?.UserId == u) room.White = null;
                if (room.Black == null && room.White != null) { room.Black = room.White; room.White = null; }
                if (room.Black != null && room.OwnerId == u) room.OwnerId = room.Black.UserId;
                empty = room.Black == null && room.White == null;
                if (empty) _rooms.TryRemove(room.Id, out _);
                else if (room.Phase == "ended") room.Phase = "waiting";
            }
            _userRoom.TryRemove(u, out _);
            if (finished != null) await RecordAsync(finished);
            await SendLeftAsync(u);
            if (!empty) await PushAsync(room);
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
                if (room.Active) return "对局已经开始了";
                if (room.Black == null || room.White == null) return "还差一位对手";
                if (!room.Black.Online || !room.White.Online) return "对手暂时掉线了";
                if (room.Round > 0) (room.Black, room.White) = (room.White, room.Black); // 再来一局交换黑白
                room.Round++;
                room.Phase = "playing";
                room.Board = new string('.', room.Size * room.Size);
                room.Turn = 'b';
                room.Ko = -1;
                room.Passes = 0;
                room.Moves.Clear();
                room.Captures['b'] = 0; room.Captures['w'] = 0;
                room.Dead.Clear();
                room.Confirmed.Clear();
                room.Winner = null; room.Reason = null; room.FinalScore = null;
                room.StartedAt = DateTime.UtcNow;
                room.Deadline = DateTime.UtcNow.AddSeconds(room.TimeLimit);
            }
            await PushAsync(room);
            return null;
        }

        public async Task<string?> PlayAsync(Guid u, int index)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在房间里";
            lock (room)
            {
                if (room.Phase != "playing") return room.Phase == "scoring" ? "正在点目，点「继续下」才能落子" : "对局还没开始或已经结束";
                if (room.ColorOf(u) != room.Turn) return "还没轮到你";
                var (error, board, captured, ko) = GoRules.Play(room.Board, room.Size, index, room.Turn, room.Ko);
                if (error != null) return error;
                room.Board = board;
                room.Ko = ko;
                room.Captures[room.Turn] += captured;
                room.Moves.Add(new GoMove(index, room.Turn, captured));
                room.Passes = 0;
                room.Turn = GoRules.Other(room.Turn);
                room.Deadline = DateTime.UtcNow.AddSeconds(room.TimeLimit);
            }
            await PushAsync(room);
            return null;
        }

        public async Task<string?> PassAsync(Guid u)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在房间里";
            lock (room)
            {
                if (room.Phase != "playing") return "现在不能停一手";
                if (room.ColorOf(u) != room.Turn) return "还没轮到你";
                room.Moves.Add(new GoMove(-1, room.Turn, 0));
                room.Passes++;
                room.Ko = -1;
                room.Turn = GoRules.Other(room.Turn);
                if (room.Passes >= 2) EnterScoring(room);
                else room.Deadline = DateTime.UtcNow.AddSeconds(room.TimeLimit);
            }
            await PushAsync(room);
            return null;
        }

        private static void EnterScoring(GoRoom room)
        {
            room.Phase = "scoring";
            room.Dead.Clear();
            room.Confirmed.Clear();
            room.Deadline = DateTime.UtcNow.AddSeconds(ScoringSeconds);
        }

        /// <summary>点目：切换一整块棋的死活（任何一方改动都会取消双方的确认）</summary>
        public async Task<string?> ToggleDeadAsync(Guid u, int index)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在房间里";
            lock (room)
            {
                if (room.Phase != "scoring" || room.ColorOf(u) == null) return "现在不是点目阶段";
                if (index < 0 || index >= room.Board.Length || room.Board[index] == '.') return null;
                var group = GoRules.Group(room.Board.ToCharArray(), room.Size, index).Stones;
                var nowDead = !room.Dead.Contains(index);
                foreach (var s in group) { if (nowDead) room.Dead.Add(s); else room.Dead.Remove(s); }
                room.Confirmed.Clear();
            }
            await PushAsync(room);
            return null;
        }

        /// <summary>点目：确认结果。双方都确认后按中国规则数子结算</summary>
        public async Task<string?> ConfirmScoreAsync(Guid u)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在房间里";
            Finished? finished = null;
            lock (room)
            {
                if (room.Phase != "scoring" || room.ColorOf(u) is not char c) return "现在不是点目阶段";
                room.Confirmed.Add(c);
                if (room.Confirmed.Count == 2) finished = FinishByScore(room, "双方确认点目");
            }
            if (finished != null) await RecordAsync(finished);
            await PushAsync(room);
            return null;
        }

        /// <summary>点目时觉得还没下完：回到对局，轮到停最后一手之后的那一方</summary>
        public async Task<string?> ResumeAsync(Guid u)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在房间里";
            lock (room)
            {
                if (room.Phase != "scoring" || room.ColorOf(u) == null) return "现在不能继续下";
                room.Phase = "playing";
                room.Passes = 0;
                room.Dead.Clear();
                room.Confirmed.Clear();
                room.Deadline = DateTime.UtcNow.AddSeconds(room.TimeLimit);
            }
            await PushAsync(room);
            return null;
        }

        public async Task<string?> ResignAsync(Guid u)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在房间里";
            Finished finished;
            lock (room)
            {
                if (!room.Active || room.ColorOf(u) is not char c) return "现在不能认输";
                finished = Finish(room, GoRules.Other(c), $"{room.Of(u)?.Name ?? "玩家"} 认输，中盘负");
            }
            await RecordAsync(finished);
            await PushAsync(room);
            return null;
        }

        private sealed record Finished(GoRoom Room, char? Winner, string Reason, List<GoPlayer> Players);

        private static Finished Finish(GoRoom room, char? winner, string reason)
        {
            room.Phase = "ended";
            room.Winner = winner;
            room.Reason = reason;
            room.Deadline = null;
            return new Finished(room, winner, reason, new List<GoPlayer?> { room.Black, room.White }.Where(p => p != null).Select(p => p!).ToList());
        }

        private static Finished FinishByScore(GoRoom room, string how)
        {
            var (black, white, _) = GoRules.Score(room.Board, room.Size, room.Dead);
            room.FinalScore = (black, white);
            var winner = black > white ? 'b' : 'w'; // 贴 7.5 目不会平局
            var diff = Math.Abs(black - white);
            return Finish(room, winner, $"{how} · 黑 {black} : 白 {white}（含贴 {GoRules.Komi} 目），{(winner == 'b' ? "黑" : "白")}胜 {diff} 子");
        }

        private async Task RecordAsync(Finished f)
        {
            try
            {
                if (f.Players.Count != 2) return;
                var room = f.Room;
                var stones = room.Moves.Count(m => m.Index >= 0);
                await records.SaveAsync(new MatchRecord
                {
                    Game = "go",
                    Mode = $"联机 · {room.Size} 路 · 每步 {room.TimeLimit} 秒",
                    Summary = $"{f.Reason} · {stones} 手",
                    HasBots = false,
                    StartedAt = room.StartedAt ?? DateTime.UtcNow,
                    EndedAt = DateTime.UtcNow,
                    Players = f.Players.Select((p, i) => new MatchPlayer
                    {
                        Seat = i,
                        UserId = p.UserId,
                        Name = p.Name,
                        Role = i == 0 ? "黑方" : "白方",
                        Result = f.Winner == null ? "draw" : (i == 0 ? 'b' : 'w') == f.Winner ? "win" : "lose",
                    }).ToList(),
                });
            }
            catch (Exception ex) { logger.LogWarning(ex, "围棋战绩记录失败"); }
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
            if (left > 0 || RoomOf(u) is not { } room) return;
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

        // ================= 定时：超时、掉线、点目到时 =================

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(500));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var room in _rooms.Values)
                {
                    Finished? finished = null;
                    try
                    {
                        List<Guid>? leave = null;
                        lock (room)
                        {
                            if (!room.Active)
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
                            if (!room.Active) continue;
                            var now = DateTime.UtcNow;
                            var offline = room.Players.FirstOrDefault(p => !p.Online && p.OfflineSince is DateTime since && (now - since).TotalSeconds >= OfflineGraceSeconds);
                            if (offline != null && room.ColorOf(offline.UserId) is char oc)
                                finished = Finish(room, GoRules.Other(oc), $"{offline.Name} 掉线超过 {OfflineGraceSeconds} 秒");
                            else if (room.Deadline is DateTime dl && now >= dl)
                            {
                                if (room.Phase == "scoring") finished = FinishByScore(room, "点目时间到，按当前标记结算");
                                else finished = Finish(room, GoRules.Other(room.Turn), $"{room.PlayerOf(room.Turn)?.Name ?? "玩家"} 超时（每步 {room.TimeLimit} 秒）");
                            }
                        }
                        if (finished != null)
                        {
                            await RecordAsync(finished);
                            await PushAsync(room);
                        }
                    }
                    catch (Exception ex) { logger.LogWarning(ex, "围棋定时任务出错"); }
                }
            }
        }
    }
}
