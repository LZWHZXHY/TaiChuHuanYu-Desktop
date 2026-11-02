using System.Collections.Concurrent;

namespace TaiChuWeb_V2.Services.Games
{
    /// <summary>
    /// 联机游戏的「观战」名单（斗地主、象棋、围棋、五子棋共用，每个游戏各 new 一个）。
    /// 只管：哪个房间有谁在看、最多几个人、房主有没有关掉观战、某个人正在看哪个房间。
    /// 推送用的是 SignalR 的 Clients.Users(用户 ID 列表)，不用 Hub 分组：
    /// 一个人开了几个页面、断线重连换了连接，按用户 ID 推都能收到，不用在连上 / 断开时维护分组。
    /// 线程安全：内部自己加锁，而且绝不在锁里回调外面，所以可以在 lock(room) 里面调用。
    /// </summary>
    public sealed class SpectatorRegistry
    {
        /// <summary>每个房间最多多少人观战</summary>
        public const int MaxPerRoom = 20;

        private sealed class Watchers
        {
            public readonly List<(Guid UserId, string Name)> List = new();
            public bool Allow = true;
        }

        private readonly object _lock = new();
        private readonly Dictionary<string, Watchers> _rooms = new();
        private readonly ConcurrentDictionary<Guid, string> _userRoom = new();

        private Watchers RoomLocked(string roomId)
        {
            if (!_rooms.TryGetValue(roomId, out var w)) _rooms[roomId] = w = new Watchers();
            return w;
        }

        /// <summary>这个人正在看哪个房间（没在观战为 null）</summary>
        public string? RoomOf(Guid userId) => _userRoom.TryGetValue(userId, out var id) ? id : null;

        public bool IsWatching(string roomId, Guid userId) => RoomOf(userId) == roomId;

        /// <summary>
        /// 进房间观战。已经在看别的房间会自动换过来（previous 是原来的房间，调用方要给那边推一下人数变化）。
        /// 失败返回错误文字。
        /// </summary>
        public (string? Error, string? Previous) Add(string roomId, Guid userId, string name)
        {
            lock (_lock)
            {
                var w = RoomLocked(roomId);
                if (w.List.Any(x => x.UserId == userId)) return (null, null);
                if (!w.Allow) return ("这桌的房主没开观战", null);
                if (w.List.Count >= MaxPerRoom) return ($"观战的人满了（最多 {MaxPerRoom} 人）", null);
                string? previous = null;
                if (_userRoom.TryGetValue(userId, out var old) && old != roomId)
                {
                    previous = old;
                    if (_rooms.TryGetValue(old, out var ow)) ow.List.RemoveAll(x => x.UserId == userId);
                }
                w.List.Add((userId, string.IsNullOrWhiteSpace(name) ? "观众" : name));
                _userRoom[userId] = roomId;
                return (null, previous);
            }
        }

        /// <summary>不看了（离开、断线、坐下打牌）。返回原来在看的房间</summary>
        public string? Remove(Guid userId)
        {
            lock (_lock)
            {
                if (!_userRoom.TryRemove(userId, out var roomId)) return null;
                if (_rooms.TryGetValue(roomId, out var w)) w.List.RemoveAll(x => x.UserId == userId);
                return roomId;
            }
        }

        /// <summary>房间解散：清掉整个名单，返回被请出去的人（调用方给他们推「牌桌没了」）</summary>
        public List<Guid> RemoveRoom(string roomId)
        {
            lock (_lock)
            {
                if (!_rooms.Remove(roomId, out var w)) return new List<Guid>();
                var ids = w.List.Select(x => x.UserId).ToList();
                foreach (var id in ids)
                    if (_userRoom.TryGetValue(id, out var r) && r == roomId) _userRoom.TryRemove(id, out _);
                return ids;
            }
        }

        public bool Allowed(string roomId)
        {
            lock (_lock) return !_rooms.TryGetValue(roomId, out var w) || w.Allow;
        }

        /// <summary>房主开 / 关观战。关掉时把正在看的人都请出去，返回这些人</summary>
        public List<Guid> SetAllowed(string roomId, bool allow)
        {
            lock (_lock)
            {
                var w = RoomLocked(roomId);
                w.Allow = allow;
                if (allow) return new List<Guid>();
                var ids = w.List.Select(x => x.UserId).ToList();
                w.List.Clear();
                foreach (var id in ids)
                    if (_userRoom.TryGetValue(id, out var r) && r == roomId) _userRoom.TryRemove(id, out _);
                return ids;
            }
        }

        public int Count(string roomId)
        {
            lock (_lock) return _rooms.TryGetValue(roomId, out var w) ? w.List.Count : 0;
        }

        /// <summary>观战的人的用户 ID（字符串，直接给 Clients.Users 用）</summary>
        public List<string> UserIds(string roomId)
        {
            lock (_lock) return _rooms.TryGetValue(roomId, out var w) ? w.List.Select(x => x.UserId.ToString()).ToList() : new List<string>();
        }

        public string? NameOf(string roomId, Guid userId)
        {
            lock (_lock) return _rooms.TryGetValue(roomId, out var w) ? w.List.Where(x => x.UserId == userId).Select(x => x.Name).FirstOrDefault() : null;
        }

        /// <summary>放进牌桌 / 棋盘状态里的观战信息（字段名四个游戏一样，前端共用一个小组件）</summary>
        public object View(string roomId)
        {
            lock (_lock)
            {
                var has = _rooms.TryGetValue(roomId, out var w);
                return new
                {
                    allow = !has || w!.Allow,
                    max = MaxPerRoom,
                    list = (has ? w!.List : new List<(Guid UserId, string Name)>()).Select(x => new { userId = x.UserId, name = x.Name }).ToList(),
                };
            }
        }

        /// <summary>大厅房间列表里用：人数 + 是否允许</summary>
        public (int Count, bool Allow) Summary(string roomId)
        {
            lock (_lock) return _rooms.TryGetValue(roomId, out var w) ? (w.List.Count, w.Allow) : (0, true);
        }
    }
}
