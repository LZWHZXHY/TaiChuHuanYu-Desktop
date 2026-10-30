using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using TaiChuWeb_V2.Hubs;

namespace TaiChuWeb_V2.Services.Games.Doudizhu
{
    /// <summary>
    /// 斗地主牌桌管理（单例）：房间、入座、叫分出牌、超时与电脑行动、土豆结算、战绩记录、向每位玩家推送各自视角的牌桌状态。
    /// 所有房间状态只在服务器上，玩家只能看到自己的手牌。房间状态修改一律在 lock(room) 内完成，数据库操作在锁外。
    /// </summary>
    public sealed class DdzManager(IHubContext<DdzHub> hub, PotatoService potatoes, MatchRecordService records, ILogger<DdzManager> logger) : BackgroundService
    {
        public static readonly DdzTier[] Tiers =
        {
            new("newbie", "新手场", 50, 0, 20),
            new("normal", "普通场", 200, 5000, 100),
            new("expert", "高手场", 1000, 30000, 500),
        };

        /// <summary>等人时掉线（网络抖一下、切到别的页面）先保留座位这么久，免得牌桌一下子就解散了</summary>
        public const int OfflineGraceSeconds = 60;

        private readonly ConcurrentDictionary<string, DdzRoom> _rooms = new();
        private readonly ConcurrentDictionary<Guid, string> _userRoom = new();
        private readonly ConcurrentDictionary<Guid, int> _connections = new();
        private readonly ConcurrentDictionary<string, int> _settledRound = new();

        // ---------------- 查询 ----------------

        public object Lobby() => new
        {
            tiers = Tiers.Select(t => new { t.Key, t.Name, @base = t.Base, t.MinBalance, fee = t.Fee }),
            rooms = _rooms.Values.Select(r =>
            {
                lock (r)
                    return new
                    {
                        id = r.Id, tier = r.Tier.Key, tierName = r.Tier.Name, @base = r.Tier.Base, phase = r.Phase,
                        players = r.Seats.Where(s => !s.Empty).Select(s => new { s.Name, s.IsBot, away = s.Human && !s.Online }).ToList(),
                        open = r.Seats.Count(s => s.Empty),
                    };
            }).OrderBy(x => x.phase == "waiting" ? 0 : 1).ThenByDescending(x => x.players.Count).ToList(),
        };

        public string? RoomOf(Guid userId) => _userRoom.TryGetValue(userId, out var id) ? id : null;

        /// <summary>某位玩家视角的牌桌状态（只包含自己的手牌）</summary>
        public object? StateFor(Guid userId)
        {
            if (!_userRoom.TryGetValue(userId, out var id) || !_rooms.TryGetValue(id, out var room)) return null;
            lock (room) return BuildState(room, userId);
        }

        private static object BuildState(DdzRoom r, Guid viewer)
        {
            var me = r.SeatOf(viewer);
            var showAll = r.Phase == "ended" && r.Result != null;
            return new
            {
                roomId = r.Id,
                tier = r.Tier.Key,
                tierName = r.Tier.Name,
                @base = r.Tier.Base,
                fee = r.Tier.Fee,
                ownerId = r.OwnerId,
                phase = r.Phase,
                round = r.Round,
                mySeat = me?.Index ?? -1,
                myHand = me?.Hand ?? new List<int>(),
                seats = r.Seats.Select(s => new
                {
                    seat = s.Index, s.UserId, s.Name, s.IsBot, s.Ready, s.Online, s.Trustee, empty = s.Empty,
                    cardCount = s.Hand.Count,
                    role = r.Landlord < 0 ? null : s.Index == r.Landlord ? "地主" : "农民",
                    recent = r.Recent[s.Index] == null ? null : new { cards = r.Recent[s.Index]!.Cards, type = r.Recent[s.Index]!.Type, label = r.Recent[s.Index]!.Type == null ? null : DdzRules.TypeLabel(r.Recent[s.Index]!.Type!), bid = r.Recent[s.Index]!.Bid },
                    hand = showAll && r.Result!.Hands.TryGetValue(s.Index, out var h) ? h : null,
                }),
                bottom = r.Landlord >= 0 ? r.Bottom : new List<int>(),
                landlord = r.Landlord,
                currentBid = r.CurrentBid,
                turn = r.Turn,
                deadline = r.Deadline,
                lastPlay = r.LastPlay == null ? null : new { seat = r.LastPlay.Seat, cards = r.LastPlay.Cards, type = r.LastPlay.Type },
                multiplier = r.Multiplier,
                bombs = r.Bombs,
                hasBots = r.HasBots,
                result = r.Result == null ? null : new { r.Result.LandlordWin, r.Result.Spring, r.Result.Multiplier, r.Result.Summary, changes = r.Result.Changes.Select(kv => new { seat = kv.Key, change = kv.Value }) },
            };
        }

        // ---------------- 推送 ----------------

        private async Task BroadcastAsync(DdzRoom room)
        {
            List<(Guid, object)> states;
            lock (room) states = room.Humans.Select(s => (s.UserId!.Value, BuildState(room, s.UserId!.Value))).ToList();
            foreach (var (uid, state) in states)
            {
                try { await hub.Clients.User(uid.ToString()).SendAsync("State", state); }
                catch (Exception ex) { logger.LogDebug(ex, "推送斗地主状态失败"); }
            }
        }

        private Task SendLeftAsync(Guid userId) => hub.Clients.User(userId.ToString()).SendAsync("State", (object?)null);

        // ---------------- 进出房间 ----------------

        private static DdzTier? TierOf(string key) => Tiers.FirstOrDefault(t => t.Key == key);

        public async Task<string?> CreateAsync(Guid userId, string name, string tierKey, long balance)
        {
            if (RoomOf(userId) != null) return "你已经在一张牌桌上了";
            if (TierOf(tierKey) is not DdzTier tier) return "没有这个场次";
            if (balance < tier.MinBalance) return $"{tier.Name}需要至少 {tier.MinBalance} 土豆";
            var room = new DdzRoom { Id = Guid.NewGuid().ToString("N")[..6], Tier = tier, OwnerId = userId };
            lock (room) room.Sit(userId, name);
            _rooms[room.Id] = room;
            _userRoom[userId] = room.Id;
            await BroadcastAsync(room);
            return null;
        }

        public async Task<string?> JoinAsync(Guid userId, string name, string roomId, long balance)
        {
            if (RoomOf(userId) is string current) return current == roomId ? null : "你已经在别的牌桌上了，先离开";
            if (!_rooms.TryGetValue(roomId, out var room)) return "牌桌不存在或已解散";
            if (balance < room.Tier.MinBalance) return $"{room.Tier.Name}需要至少 {room.Tier.MinBalance} 土豆";
            lock (room)
            {
                if (room.Sit(userId, name) == null) return room.Full ? "牌桌已满" : "这桌正在打牌，等这局结束再进";
            }
            _userRoom[userId] = room.Id;
            await BroadcastAsync(room);
            return null;
        }

        /// <summary>快速开始：找同场次有空位的牌桌，没有就新开一桌</summary>
        public async Task<string?> QuickAsync(Guid userId, string name, string tierKey, long balance)
        {
            if (RoomOf(userId) != null) return "你已经在一张牌桌上了";
            var candidate = _rooms.Values.FirstOrDefault(r => r.Tier.Key == tierKey && r.Phase == "waiting" && r.Seats.Any(s => s.Empty) && r.Humans.Any(h => h.Online));
            return candidate != null ? await JoinAsync(userId, name, candidate.Id, balance) : await CreateAsync(userId, name, tierKey, balance);
        }

        public async Task<string?> LeaveAsync(Guid userId)
        {
            if (!_userRoom.TryGetValue(userId, out var id) || !_rooms.TryGetValue(id, out var room)) { _userRoom.TryRemove(userId, out _); return null; }
            lock (room)
            {
                if (room.Phase is "bidding" or "playing") return "正在打牌，不能离开（可以开托管）";
                var seat = room.SeatOf(userId);
                if (seat != null) room.Vacate(seat);
                if (room.OwnerId == userId && room.Humans.FirstOrDefault() is { } next) room.OwnerId = next.UserId!.Value;
            }
            _userRoom.TryRemove(userId, out _);
            await SendLeftAsync(userId);
            if (!room.Humans.Any()) _rooms.TryRemove(room.Id, out _);
            else await BroadcastAsync(room);
            return null;
        }

        private async Task<string?> WithRoomAsync(Guid userId, Func<DdzRoom, DdzSeat, string?> action)
        {
            if (!_userRoom.TryGetValue(userId, out var id) || !_rooms.TryGetValue(id, out var room)) return "你不在牌桌上";
            string? error;
            lock (room)
            {
                var seat = room.SeatOf(userId);
                if (seat == null) return "你不在牌桌上";
                error = action(room, seat);
                if (error == null && room.CanStart) room.Start();
            }
            if (error == null) { await BroadcastAsync(room); await SettleIfEndedAsync(room); }
            return error;
        }

        public Task<string?> AddBotAsync(Guid userId) => WithRoomAsync(userId, (room, _) =>
            room.OwnerId != userId ? "只有房主可以添加电脑玩家" : room.AddBot() ? null : "没有空位了");

        public Task<string?> ReadyAsync(Guid userId, bool ready) => WithRoomAsync(userId, (room, seat) =>
        {
            if (room.Phase != "waiting") return "现在不能准备";
            seat.Ready = ready;
            return null;
        });

        public Task<string?> KickBotAsync(Guid userId, int seatIndex) => WithRoomAsync(userId, (room, _) =>
        {
            if (room.OwnerId != userId || room.Phase != "waiting") return "只有房主在开局前可以移除电脑";
            if (seatIndex is < 0 or > 2 || !room.Seats[seatIndex].IsBot) return "这个座位不是电脑";
            room.Vacate(room.Seats[seatIndex]);
            return null;
        });

        public Task<string?> BidAsync(Guid userId, int score) => WithRoomAsync(userId, (room, seat) =>
        {
            var e = room.Bid(seat.Index, score);
            if (e == null) { seat.Timeouts = 0; }
            return e;
        });

        public Task<string?> PlayAsync(Guid userId, int[] cards) => WithRoomAsync(userId, (room, seat) =>
        {
            var e = room.Play(seat.Index, cards);
            if (e == null) { seat.Timeouts = 0; seat.Trustee = false; }
            return e;
        });

        public Task<string?> PassAsync(Guid userId) => WithRoomAsync(userId, (room, seat) =>
        {
            var e = room.Pass(seat.Index);
            if (e == null) { seat.Timeouts = 0; seat.Trustee = false; }
            return e;
        });

        public Task<string?> TrusteeAsync(Guid userId, bool on) => WithRoomAsync(userId, (room, seat) =>
        {
            seat.Trustee = on;
            seat.Timeouts = 0;
            if (on && room.Turn == seat.Index && room.Phase is "bidding" or "playing") room.BotActAt = DateTime.UtcNow.AddMilliseconds(600);
            if (!on && room.Turn == seat.Index) room.BotActAt = null;
            return null;
        });

        // ---------------- 连接 ----------------

        public async Task ConnectedAsync(Guid userId)
        {
            _connections.AddOrUpdate(userId, 1, (_, n) => n + 1);
            if (_userRoom.TryGetValue(userId, out var id) && _rooms.TryGetValue(id, out var room))
            {
                lock (room) { if (room.SeatOf(userId) is { } s) { s.Online = true; s.OfflineSince = null; } }
                await BroadcastAsync(room);
            }
        }

        public async Task DisconnectedAsync(Guid userId)
        {
            var left = _connections.AddOrUpdate(userId, 0, (_, n) => Math.Max(0, n - 1));
            if (left > 0) return;
            if (!_userRoom.TryGetValue(userId, out var id) || !_rooms.TryGetValue(id, out var room)) return;
            lock (room)
            {
                var seat = room.SeatOf(userId);
                if (seat == null) return;
                seat.Online = false;
                seat.OfflineSince = DateTime.UtcNow;
                if (room.Phase is "bidding" or "playing")
                {
                    // 对局中掉线：保留座位，交给托管，回来后可以继续
                    seat.Trustee = true;
                    if (room.Turn == seat.Index) room.BotActAt = DateTime.UtcNow.AddSeconds(1);
                }
                else
                {
                    // 等人时掉线：先保留座位（取消准备），超过宽限时间还没回来再让出（见定时任务）。
                    // 以前是立刻让座，房主网络一抖或者切个页面，牌桌就解散了，别人就看不到这桌
                    seat.Ready = false;
                }
            }
            await BroadcastAsync(room);
        }

        // ---------------- 结算 ----------------

        private async Task SettleIfEndedAsync(DdzRoom room)
        {
            DdzResult? result;
            int round, landlord;
            List<(int Index, Guid? UserId, string Name, bool IsBot)> seats;
            bool hasBots;
            DateTime started;
            string historyJson;
            lock (room)
            {
                if (room.Phase != "ended" || room.Result == null) return;
                if (_settledRound.TryGetValue(room.Id, out var done) && done == room.Round) return;
                _settledRound[room.Id] = room.Round;
                result = room.Result; round = room.Round; landlord = room.Landlord;
                seats = room.Seats.Select(s => (s.Index, s.UserId, s.Name, s.IsBot)).ToList();
                hasBots = room.HasBots; started = room.StartedAt;
                historyJson = JsonSerializer.Serialize(new
                {
                    bottom = room.Bottom,
                    bid = room.CurrentBid,
                    landlord,
                    actions = room.History.Select(a => new { a.Seat, a.Cards, a.Type, a.Bid }),
                    hands = result.Hands,
                });
            }

            var actualBySeat = new Dictionary<int, long>();
            try
            {
                var humans = seats.Where(s => s.UserId.HasValue).ToList();
                var changes = new List<PotatoService.Change>();
                var reason = $"斗地主 · {room.Tier.Name} · {(result.LandlordWin ? "地主胜" : "农民胜")}";
                if (hasBots)
                {
                    // 有电脑的局：按一半结算；从电脑身上赢的每天最多 3000
                    foreach (var h in humans)
                    {
                        var raw = result.Changes[h.Index] / 2;
                        if (raw > 0)
                        {
                            var room0 = Math.Max(0, PotatoService.BotWinDailyCap - await potatoes.BotWinsTodayAsync(h.UserId!.Value));
                            raw = Math.Min(raw, room0);
                        }
                        changes.Add(new PotatoService.Change(h.UserId!.Value, raw, reason + "（电脑局减半）", "game-bot", $"ddz:{room.Id}:{round}"));
                    }
                }
                else
                {
                    // 全是真人：输家先按余额能付多少付多少，赢家拿走输家实际付出的
                    var balances = await potatoes.BalancesAsync(humans.Select(h => h.UserId!.Value));
                    var losers = humans.Where(h => result.Changes[h.Index] < 0).ToList();
                    var winners = humans.Where(h => result.Changes[h.Index] > 0).ToList();
                    long pot = 0;
                    foreach (var l in losers)
                    {
                        var pay = Math.Min(-result.Changes[l.Index], balances[l.UserId!.Value]);
                        pot += pay;
                        changes.Add(new PotatoService.Change(l.UserId!.Value, -pay, reason, "game", $"ddz:{room.Id}:{round}"));
                    }
                    for (var i = 0; i < winners.Count; i++)
                    {
                        var share = pot / winners.Count + (i == 0 ? pot % winners.Count : 0);
                        changes.Add(new PotatoService.Change(winners[i].UserId!.Value, share, reason, "game", $"ddz:{room.Id}:{round}"));
                    }
                }
                // 台费：每局每位真人都扣（在输赢之后扣，余额不够就扣到 0）
                if (room.Tier.Fee > 0)
                    foreach (var h in humans)
                        changes.Add(new PotatoService.Change(h.UserId!.Value, -room.Tier.Fee, $"斗地主 · {room.Tier.Name} · 台费", "fee", $"ddz:{room.Id}:{round}"));
                var actual = await potatoes.ApplyAsync(changes);
                foreach (var s in seats) actualBySeat[s.Index] = s.UserId.HasValue && actual.TryGetValue(s.UserId.Value, out var v) ? v : 0;

                await records.SaveAsync(new MatchRecord
                {
                    Game = "doudizhu",
                    Mode = $"{room.Tier.Name} · 底分 {room.Tier.Base} · 台费 {room.Tier.Fee}{(hasBots ? " · 有电脑" : "")}",
                    Summary = result.Summary,
                    DetailJson = historyJson,
                    HasBots = hasBots,
                    StartedAt = started,
                    EndedAt = DateTime.UtcNow,
                    Players = seats.Select(s => new MatchPlayer
                    {
                        Seat = s.Index, UserId = s.UserId, Name = s.Name, IsBot = s.IsBot,
                        Role = s.Index == landlord ? "地主" : "农民",
                        Result = result.Changes[s.Index] > 0 ? "win" : "lose",
                        PotatoChange = actualBySeat[s.Index],
                    }).ToList(),
                });
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "斗地主结算失败 room={Room} round={Round}", room.Id, round);
            }
            lock (room) { if (room.Round == round) room.ApplyActualChanges(actualBySeat); }
            await BroadcastAsync(room);
        }

        // ---------------- 定时：电脑行动、超时、结算后回到等人 ----------------

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(300));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                foreach (var room in _rooms.Values)
                {
                    var changed = false;
                    var removeOffline = new List<Guid>();
                    try
                    {
                        lock (room)
                        {
                            var now = DateTime.UtcNow;
                            if (room.Phase is "bidding" or "playing")
                            {
                                var seat = room.Seats[room.Turn];
                                if ((seat.IsBot || seat.Trustee) && room.BotActAt is DateTime at && now >= at) { room.AutoAct(); changed = true; }
                                else if (room.Deadline is DateTime dl && now >= dl)
                                {
                                    if (seat.IsBot || seat.Trustee) room.AutoAct(); else room.Timeout();
                                    changed = true;
                                }
                            }
                            else if (room.Phase == "ended" && room.Deadline is DateTime end && now >= end && _settledRound.GetValueOrDefault(room.Id) == room.Round)
                            {
                                room.BackToWaiting();
                                foreach (var s in room.Seats.Where(s => s.Human && !s.Online).ToList()) { removeOffline.Add(s.UserId!.Value); room.Vacate(s); }
                                changed = true;
                            }
                            else if (room.Phase == "waiting")
                            {
                                // 掉线超过宽限时间的人让出座位
                                foreach (var s in room.Seats.Where(s => s.Human && !s.Online && s.OfflineSince is DateTime since && (now - since).TotalSeconds >= OfflineGraceSeconds).ToList())
                                {
                                    removeOffline.Add(s.UserId!.Value);
                                    room.Vacate(s);
                                    changed = true;
                                }
                                if (changed && !room.Humans.Any(h => h.UserId == room.OwnerId) && room.Humans.FirstOrDefault() is { } next) room.OwnerId = next.UserId!.Value;
                                if (!room.Humans.Any()) { _rooms.TryRemove(room.Id, out _); changed = false; }
                            }
                        }
                        foreach (var uid in removeOffline) _userRoom.TryRemove(uid, out _);
                        if (changed)
                        {
                            await BroadcastAsync(room);
                            await SettleIfEndedAsync(room);
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "斗地主牌桌 {Room} 处理出错", room.Id);
                    }
                }
            }
        }
    }
}
