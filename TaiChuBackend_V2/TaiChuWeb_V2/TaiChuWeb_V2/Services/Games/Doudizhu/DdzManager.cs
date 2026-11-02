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
    public sealed class DdzManager(IHubContext<DdzHub> hub, PotatoService potatoes, MatchRecordService records, GameSocialService social, ILogger<DdzManager> logger) : BackgroundService
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
        /// <summary>观战名单（谁在看哪桌、每桌最多 20 人、房主是否关了观战）</summary>
        private readonly SpectatorRegistry _watch = new();
        private long _lastLobbyPush;

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
                        // 观战人数、是否允许观战（不算房间人数，不影响「空几位」和快速开始）
                        spectators = _watch.Summary(r.Id).Count,
                        allowSpectate = _watch.Summary(r.Id).Allow,
                    };
            }).OrderBy(x => x.phase == "waiting" ? 0 : 1).ThenByDescending(x => x.players.Count).ToList(),
            maxSpectators = SpectatorRegistry.MaxPerRoom,
        };

        public string? RoomOf(Guid userId) => _userRoom.TryGetValue(userId, out var id) ? id : null;

        /// <summary>某位玩家视角的牌桌状态（只包含自己的手牌）；观战的人拿到的是不含任何手牌的公开状态</summary>
        public object? StateFor(Guid userId)
        {
            if (_userRoom.TryGetValue(userId, out var id) && _rooms.TryGetValue(id, out var room))
                lock (room) return BuildState(room, userId);
            if (_watch.RoomOf(userId) is string wid && _rooms.TryGetValue(wid, out var watched))
                lock (watched) return BuildState(watched, null);
            return null;
        }

        /// <summary>
        /// 牌桌状态。viewer 为空 = 观战视角：myHand 是空的，seats 里也没有手牌（只有张数、出过的牌）。
        /// 防作弊就靠这里：观战的人收到的数据里根本没有别人的牌，不是前端藏起来。
        /// 只有一局打完（ended）时，和原来给玩家看的一样，亮出每个人剩下的牌。
        /// </summary>
        private object BuildState(DdzRoom r, Guid? viewer)
        {
            var me = viewer is Guid v ? r.SeatOf(v) : null;
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
                // 服务器现在的时间：前端用它校正本机时钟偏差，倒计时才准
                serverNow = DateTime.UtcNow,
                lastPlay = r.LastPlay == null ? null : new { seat = r.LastPlay.Seat, cards = r.LastPlay.Cards, type = r.LastPlay.Type },
                multiplier = r.Multiplier,
                bombs = r.Bombs,
                hasBots = r.HasBots,
                result = r.Result == null ? null : new { r.Result.LandlordWin, r.Result.Spring, r.Result.Multiplier, r.Result.Summary, changes = r.Result.Changes.Select(kv => new { seat = kv.Key, change = kv.Value }) },
                // 观战：我是不是在观战、谁在看、最多几人、房主是否允许
                spectating = me == null,
                watch = _watch.View(r.Id),
            };
        }

        // ---------------- 推送 ----------------

        private async Task BroadcastAsync(DdzRoom room)
        {
            List<(Guid, object)> states;
            object publicState;
            List<string> watchers;
            lock (room)
            {
                states = room.Humans.Select(s => (s.UserId!.Value, BuildState(room, s.UserId!.Value))).ToList();
                // 观战的人都一样：一份不含手牌的公开状态
                publicState = BuildState(room, null);
                watchers = _watch.UserIds(room.Id);
            }
            foreach (var (uid, state) in states)
            {
                try { await hub.Clients.User(uid.ToString()).SendAsync("State", state); }
                catch (Exception ex) { logger.LogDebug(ex, "推送斗地主状态失败"); }
            }
            if (watchers.Count > 0)
            {
                try { await hub.Clients.Users(watchers).SendAsync("State", publicState); }
                catch (Exception ex) { logger.LogDebug(ex, "推送斗地主观战状态失败"); }
            }
        }

        /// <summary>
        /// 告诉在大厅的人「房间列表变了」（有人进出、观战人数变了），前端收到后重新拉一次列表。
        /// 最多一秒推一次，免得刷屏；漏掉的靠前端每 5 秒一次的刷新补上。
        /// </summary>
        private async Task NotifyLobbyAsync()
        {
            var now = Environment.TickCount64;
            var last = Interlocked.Read(ref _lastLobbyPush);
            if (now - last < 1000 || Interlocked.CompareExchange(ref _lastLobbyPush, now, last) != last) return;
            try { await hub.Clients.All.SendAsync("LobbyChanged"); }
            catch (Exception ex) { logger.LogDebug(ex, "推送斗地主大厅变化失败"); }
        }

        /// <summary>牌桌解散：把还在看的人送回大厅，并告诉他们一声</summary>
        private async Task DismissWatchersAsync(string roomId, string message)
        {
            foreach (var uid in _watch.RemoveRoom(roomId))
            {
                try
                {
                    await hub.Clients.User(uid.ToString()).SendAsync("Notice", message);
                    await SendLeftAsync(uid);
                }
                catch (Exception ex) { logger.LogDebug(ex, "通知观战的人失败"); }
            }
        }

        private Task SendLeftAsync(Guid userId) => hub.Clients.User(userId.ToString()).SendAsync("State", (object?)null);

        /// <summary>给这张牌桌上的所有真人和观战的人推一条消息（评论、点赞 / 砸番茄动画）</summary>
        private async Task SendToRoomAsync(DdzRoom room, string method, object payload)
        {
            List<string> users;
            lock (room) users = room.Humans.Select(s => s.UserId!.Value.ToString()).Concat(_watch.UserIds(room.Id)).Distinct().ToList();
            if (users.Count == 0) return;
            try { await hub.Clients.Users(users).SendAsync(method, payload); }
            catch (Exception ex) { logger.LogDebug(ex, "推送斗地主互动消息失败"); }
        }

        // ---------------- 进出房间 ----------------

        private static DdzTier? TierOf(string key) => Tiers.FirstOrDefault(t => t.Key == key);

        public async Task<string?> CreateAsync(Guid userId, string name, string tierKey, long balance)
        {
            if (RoomOf(userId) != null) return "你已经在一张牌桌上了";
            if (TierOf(tierKey) is not DdzTier tier) return "没有这个场次";
            if (balance < tier.MinBalance) return $"{tier.Name}需要至少 {tier.MinBalance} 土豆";
            await StopWatchingAsync(userId, notifySelf: false);
            var room = new DdzRoom { Id = Guid.NewGuid().ToString("N")[..6], Tier = tier, OwnerId = userId };
            lock (room) room.Sit(userId, name);
            _rooms[room.Id] = room;
            _userRoom[userId] = room.Id;
            await BroadcastAsync(room);
            await NotifyLobbyAsync();
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
                _userRoom[userId] = room.Id;
            }
            // 原来在观战（这一桌或别的桌）：坐下了就不再算观战
            var watched = _watch.Remove(userId);
            if (watched != null && watched != room.Id && _rooms.TryGetValue(watched, out var old)) await BroadcastAsync(old);
            await BroadcastAsync(room);
            await NotifyLobbyAsync();
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
            // 观战的人点「离开」：不看了，回大厅
            if (!_userRoom.ContainsKey(userId) && await StopWatchingAsync(userId, notifySelf: true)) return null;
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
            if (!room.Humans.Any())
            {
                _rooms.TryRemove(room.Id, out _);
                await DismissWatchersAsync(room.Id, "牌桌上的人都走了，这桌散了");
            }
            else await BroadcastAsync(room);
            await NotifyLobbyAsync();
            return null;
        }

        // ---------------- 观战 ----------------

        /// <summary>
        /// 进某一桌观战（等人的桌、正在打的桌都可以看）。要登录（Hub 本身就要求登录）。
        /// 正在牌桌上打牌的人不能同时观战；已经在看别的桌会自动换过来。
        /// </summary>
        public async Task<string?> WatchAsync(Guid userId, string name, string roomId)
        {
            if (RoomOf(userId) is string playing) return playing == roomId ? "你就坐在这桌上" : "你正在别的牌桌上，先离开再去观战";
            if (!_rooms.TryGetValue(roomId, out var room)) return "牌桌不存在或已解散";
            var (error, previous) = _watch.Add(roomId, userId, name);
            if (error != null) return error;
            if (previous != null && _rooms.TryGetValue(previous, out var old)) await BroadcastAsync(old);
            await BroadcastAsync(room);
            await NotifyLobbyAsync();
            return null;
        }

        /// <summary>不看了（点「离开」或断线）。观战的人走不影响牌局</summary>
        private async Task<bool> StopWatchingAsync(Guid userId, bool notifySelf)
        {
            var roomId = _watch.Remove(userId);
            if (roomId == null) return false;
            if (notifySelf) await SendLeftAsync(userId);
            if (_rooms.TryGetValue(roomId, out var room)) await BroadcastAsync(room);
            await NotifyLobbyAsync();
            return true;
        }

        /// <summary>观战的人坐下：走原来的入座流程（只有等人 / 刚打完、而且有空位时才能坐）</summary>
        public async Task<string?> SitDownAsync(Guid userId, string name, long balance)
        {
            if (_watch.RoomOf(userId) is not string roomId) return "你没有在观战";
            return await JoinAsync(userId, name, roomId, balance);
        }

        /// <summary>
        /// 玩家在两局之间「站起来观战」：让出座位，变成观战的人。
        /// 打牌中不行（和离开一样）；桌上只剩自己一个真人时也不行（站起来牌桌就散了，直接离开就好）。
        /// </summary>
        public async Task<string?> StandUpAsync(Guid userId, string name)
        {
            if (!_userRoom.TryGetValue(userId, out var id) || !_rooms.TryGetValue(id, out var room)) return "你不在牌桌上";
            lock (room)
            {
                if (room.Phase is "bidding" or "playing") return "正在打牌，打完这局才能站起来";
                if (room.Humans.Count() <= 1) return "桌上只剩你一个人，站起来这桌就散了，直接离开吧";
                if (!_watch.Allowed(room.Id)) return "这桌没开观战";
                if (_watch.Count(room.Id) >= SpectatorRegistry.MaxPerRoom) return "观战的人满了";
                var seat = room.SeatOf(userId);
                if (seat == null) return "你不在牌桌上";
                var (error, _) = _watch.Add(room.Id, userId, name);
                if (error != null) return error;
                room.Vacate(seat);
                if (room.OwnerId == userId && room.Humans.FirstOrDefault() is { } next) room.OwnerId = next.UserId!.Value;
                _userRoom.TryRemove(userId, out _);
            }
            await BroadcastAsync(room);
            await NotifyLobbyAsync();
            return null;
        }

        /// <summary>房主在等人的时候开 / 关观战；关掉时正在看的人会被送回大厅</summary>
        public async Task<string?> SetSpectateAsync(Guid userId, bool allow)
        {
            if (!_userRoom.TryGetValue(userId, out var id) || !_rooms.TryGetValue(id, out var room)) return "你不在牌桌上";
            List<Guid> kicked;
            lock (room)
            {
                if (room.OwnerId != userId) return "只有房主能设置观战";
                if (room.Phase != "waiting") return "开局前才能改观战设置";
                kicked = _watch.SetAllowed(room.Id, allow);
            }
            foreach (var uid in kicked)
            {
                try { await hub.Clients.User(uid.ToString()).SendAsync("Notice", "房主关掉了观战"); await SendLeftAsync(uid); }
                catch (Exception ex) { logger.LogDebug(ex, "通知观战的人失败"); }
            }
            await BroadcastAsync(room);
            await NotifyLobbyAsync();
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

        // ---------------- 互动：评论区、点赞 / 砸番茄 ----------------

        private static object ChatView(DdzChatLine l) => new { id = l.Id, kind = l.Kind, userId = l.UserId, name = l.Name, seat = l.Seat, spectator = l.Spectator, text = l.Text, at = l.At };

        /// <summary>进桌 / 重连时取回：最近的评论、点赞砸番茄的价格、这一局自己已经用了几次</summary>
        /// <summary>这个人所在的牌桌：坐着的优先，其次是正在观战的</summary>
        private DdzRoom? RoomForSocial(Guid userId)
        {
            if (_userRoom.TryGetValue(userId, out var id) && _rooms.TryGetValue(id, out var room)) return room;
            if (_watch.RoomOf(userId) is string wid && _rooms.TryGetValue(wid, out var watched)) return watched;
            return null;
        }

        public object? SocialFor(Guid userId)
        {
            if (RoomForSocial(userId) is not DdzRoom room) return null;
            lock (room)
                return new
                {
                    roomId = room.Id,
                    chat = room.Chat.Select(ChatView).ToList(),
                    likeCost = GameSocialService.LikeCost,
                    tomatoCost = GameSocialService.TomatoCost,
                    maxPerRound = GameSocialService.MaxReactsPerRound,
                    used = room.ReactCountOf(userId),
                };
        }

        /// <summary>在牌桌评论区说一句话：最多 100 字，每人 3 秒一条；只存在房间内存里（最近 50 条）</summary>
        public async Task<string?> ChatAsync(Guid userId, string? text)
        {
            // 换行压成空格：弹幕和小聊天框都是一行一条
            var content = string.Join(" ", (text ?? string.Empty).Split(new[] { '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (content.Length == 0) return "说点什么吧";
            if (content.Length > GameSocialService.ChatMaxLength) return $"一条最多 {GameSocialService.ChatMaxLength} 个字";
            if (RoomForSocial(userId) is not DdzRoom room) return "你不在牌桌上";
            DdzChatLine line;
            lock (room)
            {
                // 坐着的玩家用座位上的名字；观战的人用观战名单里的名字，并标上「观战」
                var seat = room.SeatOf(userId);
                var watcherName = seat == null ? _watch.NameOf(room.Id, userId) : null;
                if (seat == null && watcherName == null) return "你不在牌桌上";
                var now = DateTime.UtcNow;
                if (room.LastChatAt.TryGetValue(userId, out var last) && (now - last).TotalSeconds < GameSocialService.ChatCooldownSeconds)
                    return $"说太快啦，{GameSocialService.ChatCooldownSeconds} 秒才能发一条";
                room.LastChatAt[userId] = now;
                line = seat != null
                    ? room.AddChat("chat", userId, seat.Name, seat.Index, content, GameSocialService.ChatKeep)
                    : room.AddChat("chat", userId, watcherName!, -1, content, GameSocialService.ChatKeep, spectator: true);
            }
            await SendToRoomAsync(room, "Chat", ChatView(line));
            return null;
        }

        /// <summary>
        /// 给牌桌上另一位玩家点赞（like）或砸番茄（tomato）：
        /// 不能对自己；每人每局最多 20 次，两次至少隔 2 秒；花土豆（花掉，不给对方），余额不够就提示「土豆不够了」。
        /// 成功后记进 game_player_reactions（战绩页的被赞 / 被砸），并通知整桌播放动画。
        /// </summary>
        public async Task<string?> ReactAsync(Guid userId, int toSeat, string? kind)
        {
            if (kind == null || !GameSocialService.ValidKind(kind)) return "没有这种互动";
            if (RoomForSocial(userId) is not DdzRoom room) return "你不在牌桌上";
            string fromName, toName;
            int fromSeat, round;
            Guid? toUser;
            bool fromWatcher;
            lock (room)
            {
                // 观战的人也能点赞 / 砸番茄，次数和玩家一样按人算（每人每局 20 次）
                var me = room.SeatOf(userId);
                var watcherName = me == null ? _watch.NameOf(room.Id, userId) : null;
                if (me == null && watcherName == null) return "你不在牌桌上";
                if (toSeat is < 0 or > 2 || room.Seats[toSeat].Empty) return "这个座位没人";
                var target = room.Seats[toSeat];
                if (me != null && target.Index == me.Index) return "不能给自己点赞或砸番茄哦";
                var now = DateTime.UtcNow;
                if (room.LastReactAt.TryGetValue(userId, out var last) && (now - last).TotalSeconds < GameSocialService.ReactCooldownSeconds)
                    return "手速太快啦，歇两秒再来";
                if (room.ReactCountOf(userId) >= GameSocialService.MaxReactsPerRound)
                    return $"这一局已经互动 {GameSocialService.MaxReactsPerRound} 次了，下一局再来吧";
                // 先占上名额和冷却时间，防止连点同时进来好几次；扣土豆失败再还回去
                room.LastReactAt[userId] = now;
                room.AddReactCount(userId, 1);
                fromWatcher = me == null;
                fromName = me?.Name ?? watcherName!; fromSeat = me?.Index ?? -1;
                toName = target.Name; toUser = target.UserId;
                round = room.Round;
            }

            var cost = GameSocialService.CostOf(kind);
            var label = GameSocialService.KindLabel(kind);
            bool ok;
            try
            {
                var spent = await potatoes.SpendAsync(userId, cost, $"斗地主{label} · 给 {toName}", "ddz-react", $"ddz:{room.Id}:{round}");
                ok = spent.Ok;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "斗地主{Label}扣土豆失败", label);
                lock (room) room.AddReactCount(userId, -1);
                return "网络不稳定，没扣成土豆，再试一次吧";
            }
            if (!ok)
            {
                lock (room) room.AddReactCount(userId, -1);
                return "土豆不够了";
            }

            // 记进战绩（失败不影响动画，只记日志）
            try { await social.RecordPlayerReactionAsync("doudizhu", kind, userId, toUser, toName, room.Id, round, cost); }
            catch (Exception ex) { logger.LogError(ex, "记录斗地主{Label}失败", label); }

            DdzChatLine line;
            int used;
            lock (room)
            {
                var who = fromWatcher ? $"{fromName}（观战）" : fromName;
                var text = kind == "tomato" ? $"{who} 给 {toName} 砸了个番茄" : $"{who} 给 {toName} 点了个赞";
                line = room.AddChat(kind, userId, fromName, fromSeat, text, GameSocialService.ChatKeep, spectator: fromWatcher);
                used = room.ReactCountOf(userId);
            }
            await SendToRoomAsync(room, "Reaction", new
            {
                kind,
                // 观战的人砸的：fromSeat = -1，前端从「👁 观战」那里飞出来
                fromSeat,
                fromSpectator = fromWatcher,
                toSeat,
                fromUserId = userId,
                fromName,
                toName,
                used,
                line = ChatView(line),
            });
            return null;
        }

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
            // 观战的人所有页面都断开了：移出观战名单（不占座位，不影响牌局；重连后前端会自动再进来看）
            if (await StopWatchingAsync(userId, notifySelf: false)) return;
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
                    var dismissed = false;
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
                                if (!room.Humans.Any()) { _rooms.TryRemove(room.Id, out _); changed = false; dismissed = true; }
                            }
                        }
                        foreach (var uid in removeOffline) _userRoom.TryRemove(uid, out _);
                        if (dismissed) { await DismissWatchersAsync(room.Id, "牌桌上的人都走了，这桌散了"); await NotifyLobbyAsync(); }
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
