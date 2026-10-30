using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.SignalR;
using TaiChuWeb_V2.Hubs;

namespace TaiChuWeb_V2.Services.Paint
{
    public sealed class PaintLayer
    {
        public int Id { get; init; }
        public string Name { get; set; } = "图层";
        public Guid CreatorId { get; init; }
        public string CreatorName { get; set; } = "";
        public double Opacity { get; set; } = 1;
        /// <summary>锁定后只有创建者能在上面画</summary>
        public bool Locked { get; set; }
        /// <summary>底图（合并掉的旧笔画），PNG；为 null 表示透明</summary>
        public byte[]? BaseImage { get; set; }
        /// <summary>底图每变一次加一，客户端用它判断要不要重新取图</summary>
        public int BaseVersion { get; set; }
    }

    public sealed class PaintStroke
    {
        public long Seq { get; set; }
        public int LayerId { get; init; }
        public Guid UserId { get; init; }
        public string Tool { get; init; } = "brush";
        public string Color { get; init; } = "#000000";
        public double Size { get; init; } = 4;
        public double Opacity { get; init; } = 1;
        /// <summary>扁平的整数数组：x*10, y*10, 压感*100, …</summary>
        public List<int> Points { get; } = new();
        public bool Undone { get; set; }
        /// <summary>笔刷：pen / ink / pencil / marker / soft / airbrush</summary>
        public string Brush { get; init; } = "pen";
        /// <summary>硬度 0~1（柔边程度）</summary>
        public double Hardness { get; init; } = 1;
        /// <summary>1 = 压感控制粗细，2 = 压感控制浓淡</summary>
        public int Flags { get; init; } = 1;
        /// <summary>所在图层被清空过（回放时不画）</summary>
        public bool Cleared { get; set; }
        /// <summary>自制笔刷（Brush = custom）的参数，规范化后随笔画保存</summary>
        public PaintBrushParams? Custom { get; init; }
        /// <summary>选区：只画在这个多边形里（x*10, y*10, …）</summary>
        public List<int>? Clip { get; init; }
        /// <summary>xform（移动 / 翻转选区）的仿射矩阵 a, b, c, d, e, f</summary>
        public double[]? Matrix { get; init; }
        /// <summary>渐变终点颜色（null = 渐变到透明）</summary>
        public string? Color2 { get; init; }
        /// <summary>patch（油漆桶结果）的图片哈希，图片见 PaintRoom.Patches</summary>
        public string? Patch { get; init; }
    }

    /// <summary>落笔时的笔刷设置（客户端传上来的对象）</summary>
    public sealed class PaintStyleDto
    {
        public string Tool { get; set; } = "brush";
        public string Color { get; set; } = "#000000";
        public double Size { get; set; } = 4;
        public double Opacity { get; set; } = 1;
        public string? Brush { get; set; }
        public double Hardness { get; set; } = 1;
        public int Flags { get; set; } = 1;
        /// <summary>Brush = custom 时的自制笔刷参数</summary>
        public PaintBrushParams? Custom { get; set; }
        public List<int>? Clip { get; set; }
        public double[]? Matrix { get; set; }
        public string? Color2 { get; set; }
        public string? Patch { get; set; }
    }

    public sealed class PaintMember
    {
        public Guid UserId { get; init; }
        public string Name { get; set; } = "画师";
        public string Color { get; init; } = "#2dd4bf";
        public DateTime? OfflineSince { get; set; }
        public DateTime LastChatAt { get; set; }
    }

    public sealed class PaintRoom
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("N")[..8];
        public string Name { get; set; } = "茶绘画室";
        public string? Password { get; set; }
        public int Width { get; init; } = 1600;
        public int Height { get; init; } = 1200;
        public Guid OwnerId { get; set; }
        public DateTime CreatedAt { get; } = DateTime.UtcNow;
        public DateTime? EmptySince { get; set; }
        public long Seq { get; set; }
        public int NextLayerId { get; set; } = 1;
        public List<PaintLayer> Layers { get; } = new();
        public List<PaintStroke> Strokes { get; } = new();
        /// <summary>画室里画过的每一笔（合并底图后也保留），用来回放绘画过程</summary>
        public List<PaintStroke> History { get; } = new();
        public long HistoryPoints { get; set; }
        public bool HistoryTruncated { get; set; }
        public List<PaintMember> Members { get; } = new();
        /// <summary>连接 → 用户</summary>
        public Dictionary<string, Guid> Connections { get; } = new();
        /// <summary>正在画的笔画：用户 + 客户端笔画编号</summary>
        public Dictionary<(Guid, string), PaintStroke> Live { get; } = new();
        public Dictionary<Guid, Stack<long>> Redo { get; } = new();
        /// <summary>等待某个客户端上传合并底图：图层 → (合并到哪一笔, 请求时间)</summary>
        public Dictionary<int, (long Upto, DateTime At)> PendingBake { get; } = new();
        public List<object> Chat { get; } = new();
        /// <summary>贴图（油漆桶的结果，PNG）：哈希 → 图片。笔画和回放都按哈希引用，房间解散时一起释放</summary>
        public ConcurrentDictionary<string, byte[]> Patches { get; } = new();
        public long PatchBytes { get; set; }
        /// <summary>房间内所有改动和推送都在这把异步锁里做，保证每个人收到的笔画顺序一致</summary>
        public SemaphoreSlim Gate { get; } = new(1, 1);

        public string Group => $"paint:{Id}";
        public PaintMember? Member(Guid u) => Members.FirstOrDefault(m => m.UserId == u);
        public PaintLayer? Layer(int id) => Layers.FirstOrDefault(l => l.Id == id);
    }

    /// <summary>
    /// 茶绘（多人同屏绘画）。房间最多 4 人，共用一张画布，有图层。
    /// · 笔画边画边转发（Live*），抬笔后由服务器编号（Seq）再发给所有人，大家按同样顺序画进图层；
    /// · 撤销 / 重做只针对自己的笔画；
    /// · 一个图层的笔画太多时，请刚落笔的那个客户端把旧笔画合成一张 PNG 底图上传（服务器不画图），
    ///   其他人在本地做同样的合并，后进来的人直接取底图，避免回放几千笔。
    /// 房间只在内存里；所有人离开 30 分钟后解散（服务重启也会清空），画完请导出或发到画廊。
    /// </summary>
    public sealed class PaintManager(IHubContext<PaintHub> hub, ILogger<PaintManager> logger) : BackgroundService
    {
        public const int MaxMembers = 4;
        public const int MaxRooms = 40;
        public const int MaxLayers = 16;
        public const int OfflineGraceSeconds = 90;
        public const int EmptyRoomMinutes = 30;
        /// <summary>一个图层超过这么多笔就合并旧笔画，只保留最近 KeepAfterBake 笔可以撤销</summary>
        public const int BakeThreshold = 240;
        public const int KeepAfterBake = 80;
        public const int MaxPointsPerChunk = 900;
        public const int MaxPointsPerStroke = 60000;
        public const int MaxBaseBytes = 16 * 1024 * 1024;
        /// <summary>回放记录最多保存这么多个点数（约 16MB），超过后不再记录</summary>
        public const long MaxHistoryPoints = 4_000_000;
        /// <summary>单张贴图上限</summary>
        public const int MaxPatchBytes = 16 * 1024 * 1024;
        /// <summary>一间画室所有贴图加起来的上限</summary>
        public const long MaxRoomPatchBytes = 160L * 1024 * 1024;
        /// <summary>选区多边形最多这么多个数（x, y 两个一组）</summary>
        public const int MaxClipValues = 6000;
        public static readonly string[] BrushKinds = { "pen", "ink", "pencil", "marker", "soft", "airbrush", "custom" };
        /// <summary>画笔、橡皮、模糊、涂抹、多边形填充、变换、渐变、贴图</summary>
        public static readonly string[] Tools = { "brush", "eraser", "blur", "smudge", "polyfill", "xform", "gradient", "patch" };
        private static readonly Regex HashRe = new("^[0-9a-f]{32}$", RegexOptions.Compiled);

        public static readonly (int W, int H)[] Sizes = { (1600, 1200), (1920, 1080), (1080, 1920), (1600, 1600), (1200, 900) };
        private static readonly string[] MemberColors = { "#2dd4bf", "#f59e0b", "#a855f7", "#ef4444", "#3b82f6", "#22c55e" };
        private static readonly Regex ColorRe = new("^#[0-9a-fA-F]{6}$", RegexOptions.Compiled);

        private readonly ConcurrentDictionary<string, PaintRoom> _rooms = new();
        private readonly ConcurrentDictionary<Guid, string> _userRoom = new();

        private PaintRoom? RoomOf(Guid u) => _userRoom.TryGetValue(u, out var id) && _rooms.TryGetValue(id, out var r) ? r : null;
        public PaintRoom? Find(string roomId) => _rooms.TryGetValue(roomId ?? "", out var r) ? r : null;

        // ================= 视图 =================

        public object Lobby()
        {
            var list = new List<(string Id, string Name, int W, int H, bool Pwd, List<string> Members, int Layers, DateTime At)>();
            foreach (var r in _rooms.Values)
            {
                // 不进 Gate，读的时候房间可能正在变，读失败就跳过这一间
                try { list.Add((r.Id, r.Name, r.Width, r.Height, r.Password != null, r.Members.Select(m => m.Name).ToList(), r.Layers.Count, r.CreatedAt)); }
                catch (InvalidOperationException) { }
            }
            return list
                .OrderBy(x => x.Members.Count >= MaxMembers ? 1 : 0).ThenByDescending(x => x.Members.Count).ThenByDescending(x => x.At)
                .Take(60)
                .Select(x => new { id = x.Id, name = x.Name, width = x.W, height = x.H, hasPassword = x.Pwd, members = x.Members, full = x.Members.Count >= MaxMembers, layers = x.Layers, createdAt = x.At })
                .ToList();
        }

        private static object LayerView(PaintLayer l) => new
        {
            id = l.Id, name = l.Name, creatorId = l.CreatorId, creatorName = l.CreatorName,
            opacity = l.Opacity, locked = l.Locked, hasBase = l.BaseImage != null, baseVersion = l.BaseVersion,
        };
        private static object StrokeView(PaintStroke s) => new
        {
            seq = s.Seq, layerId = s.LayerId, userId = s.UserId, tool = s.Tool, color = s.Color,
            size = s.Size, opacity = s.Opacity, points = s.Points, undone = s.Undone,
            brush = s.Brush, hardness = s.Hardness, flags = s.Flags, custom = s.Custom,
            clip = s.Clip, matrix = s.Matrix, color2 = s.Color2, patch = s.Patch,
        };
        private static object MembersView(PaintRoom r) => r.Members.Select(m => new { userId = m.UserId, name = m.Name, color = m.Color, online = m.OfflineSince == null }).ToList();
        private static object LayersView(PaintRoom r) => r.Layers.Select(LayerView).ToList();

        private static object RoomView(PaintRoom r) => new
        {
            roomId = r.Id, name = r.Name, ownerId = r.OwnerId, width = r.Width, height = r.Height,
            hasPassword = r.Password != null, maxMembers = MaxMembers, seq = r.Seq,
            members = MembersView(r), layers = LayersView(r),
            strokes = r.Strokes.Select(StrokeView).ToList(),
            chat = r.Chat.ToList(),
        };

        // ================= 进出房间 =================

        /// <summary>重新连上（或刷新页面）时取回房间；不在房间里返回 null</summary>
        public async Task<object?> ResumeAsync(Guid u, string connectionId)
        {
            var room = RoomOf(u);
            if (room == null) return null;
            await room.Gate.WaitAsync();
            try
            {
                var m = room.Member(u);
                if (m == null) { _userRoom.TryRemove(u, out _); return null; }
                await AttachAsync(room, m, connectionId);
                return RoomView(room);
            }
            finally { room.Gate.Release(); }
        }

        public async Task<(string? Error, object? Room)> CreateAsync(Guid u, string name, string connectionId, string? title, int width, int height, string? password)
        {
            if (_rooms.Count >= MaxRooms) return ("画室太多了，先去别人的画室玩玩吧", null);
            if (!Sizes.Contains((width, height))) (width, height) = Sizes[0];
            var pwd = string.IsNullOrWhiteSpace(password) ? null : password.Trim();
            if (pwd is { Length: > 16 }) return ("密码最多 16 个字", null);
            var roomName = string.IsNullOrWhiteSpace(title) ? $"{name} 的画室" : title.Trim();
            if (roomName.Length > 30) roomName = roomName[..30];
            await LeaveAsync(u);
            var room = new PaintRoom { Name = roomName, Password = pwd, Width = width, Height = height, OwnerId = u };
            room.Layers.Add(new PaintLayer { Id = room.NextLayerId++, Name = "背景", CreatorId = u, CreatorName = name });
            _rooms[room.Id] = room;
            return await JoinAsync(u, name, connectionId, room.Id, pwd);
        }

        public async Task<(string? Error, object? Room)> JoinAsync(Guid u, string name, string connectionId, string roomId, string? password)
        {
            var room = Find((roomId ?? "").Trim());
            if (room == null) return ("画室不存在，可能已经解散了", null);
            if (RoomOf(u) is { } old && old.Id != room.Id) await LeaveAsync(u);
            await room.Gate.WaitAsync();
            try
            {
                var m = room.Member(u);
                if (m == null)
                {
                    if (room.Password != null && room.Password != (password ?? "").Trim() && room.OwnerId != u) return ("密码不对", null);
                    if (room.Members.Count >= MaxMembers) return ($"画室已满（最多 {MaxMembers} 人）", null);
                    var color = MemberColors.FirstOrDefault(c => room.Members.All(x => x.Color != c)) ?? MemberColors[0];
                    m = new PaintMember { UserId = u, Name = name, Color = color };
                    room.Members.Add(m);
                    // 每个人进来自动有一个自己的图层（之前建过的就不再建）
                    if (room.Layers.All(l => l.CreatorId != u) && room.Layers.Count < MaxLayers)
                        room.Layers.Add(new PaintLayer { Id = room.NextLayerId++, Name = $"{name} 的图层", CreatorId = u, CreatorName = name });
                    await hub.Clients.Group(room.Group).SendAsync("Layers", LayersView(room));
                }
                m.Name = name;
                room.EmptySince = null;
                _userRoom[u] = room.Id;
                await AttachAsync(room, m, connectionId);
                return (null, RoomView(room));
            }
            finally { room.Gate.Release(); }
        }

        private async Task AttachAsync(PaintRoom room, PaintMember m, string connectionId)
        {
            room.Connections[connectionId] = m.UserId;
            m.OfflineSince = null;
            await hub.Groups.AddToGroupAsync(connectionId, room.Group);
            await hub.Clients.Group(room.Group).SendAsync("Members", MembersView(room));
        }

        public async Task LeaveAsync(Guid u)
        {
            var room = RoomOf(u);
            _userRoom.TryRemove(u, out _);
            if (room == null) return;
            await room.Gate.WaitAsync();
            try { await RemoveMemberAsync(room, u, "left"); }
            finally { room.Gate.Release(); }
        }

        /// <summary>在 Gate 里调用</summary>
        private async Task RemoveMemberAsync(PaintRoom room, Guid u, string reason)
        {
            room.Members.RemoveAll(m => m.UserId == u);
            foreach (var key in room.Live.Keys.Where(k => k.Item1 == u).ToList()) room.Live.Remove(key);
            room.Redo.Remove(u);
            foreach (var (conn, _) in room.Connections.Where(kv => kv.Value == u).ToList())
            {
                room.Connections.Remove(conn);
                try
                {
                    await hub.Groups.RemoveFromGroupAsync(conn, room.Group);
                    await hub.Clients.Client(conn).SendAsync("Left", reason);
                }
                catch (Exception ex) { logger.LogDebug(ex, "移出画室分组失败"); }
            }
            if (_userRoom.TryGetValue(u, out var rid) && rid == room.Id) _userRoom.TryRemove(u, out _);
            if (room.Members.Count == 0) room.EmptySince = DateTime.UtcNow;
            else if (room.OwnerId == u) room.OwnerId = room.Members[0].UserId;
            await hub.Clients.Group(room.Group).SendAsync("Members", MembersView(room));
            await hub.Clients.Group(room.Group).SendAsync("Owner", room.OwnerId);
        }

        public async Task<string?> KickAsync(Guid u, Guid target)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在画室里";
            await room.Gate.WaitAsync();
            try
            {
                if (room.OwnerId != u) return "只有房主可以请人离开";
                if (target == u || room.Member(target) == null) return "这个人不在画室里";
                await RemoveMemberAsync(room, target, "kicked");
                return null;
            }
            finally { room.Gate.Release(); }
        }

        // ================= 连接 =================

        public async Task DisconnectedAsync(Guid u, string connectionId)
        {
            var room = RoomOf(u);
            if (room == null) return;
            await room.Gate.WaitAsync();
            try
            {
                room.Connections.Remove(connectionId);
                // 这个连接画到一半的笔画作废
                var m = room.Member(u);
                if (m == null || room.Connections.ContainsValue(u)) return;
                m.OfflineSince = DateTime.UtcNow;
                foreach (var key in room.Live.Keys.Where(k => k.Item1 == u).ToList())
                {
                    room.Live.Remove(key);
                    await hub.Clients.Group(room.Group).SendAsync("LiveCancel", new { userId = u, key = key.Item2 });
                }
                await hub.Clients.Group(room.Group).SendAsync("Members", MembersView(room));
            }
            finally { room.Gate.Release(); }
        }

        // ================= 画笔 =================

        private static bool InCanvas(PaintRoom r, List<int> pts)
        {
            for (var i = 0; i < pts.Count; i += 3)
            {
                if (pts[i] < -2000 || pts[i] > r.Width * 10 + 2000) return false;
                if (pts[i + 1] < -2000 || pts[i + 1] > r.Height * 10 + 2000) return false;
                if (pts[i + 2] < 0 || pts[i + 2] > 100) return false;
            }
            return true;
        }

        /// <summary>x, y 两个一组（选区）</summary>
        private static bool InCanvasXY(PaintRoom r, List<int> pts)
        {
            for (var i = 0; i + 1 < pts.Count; i += 2)
            {
                if (pts[i] < -2000 || pts[i] > r.Width * 10 + 2000) return false;
                if (pts[i + 1] < -2000 || pts[i + 1] > r.Height * 10 + 2000) return false;
            }
            return true;
        }

        private static string? CanDraw(PaintRoom room, Guid u, PaintLayer? layer)
        {
            if (room.Member(u) == null) return "你不在画室里";
            if (layer == null) return "图层不存在";
            if (layer.Locked && layer.CreatorId != u) return $"「{layer.Name}」被 {layer.CreatorName} 锁定了";
            return null;
        }

        public async Task<string?> StrokeBeginAsync(Guid u, string connectionId, string key, int layerId, PaintStyleDto? style, List<int>? points)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在画室里";
            if (style == null) return "笔刷设置不对";
            var tool = style.Tool;
            var color = style.Color;
            var size = style.Size;
            var opacity = style.Opacity;
            var brush = BrushKinds.Contains(style.Brush) ? style.Brush! : "pen";
            var custom = brush == "custom" ? style.Custom?.Normalized() : null;
            if (brush == "custom" && custom == null) brush = "pen";
            var hardness = double.IsNaN(style.Hardness) ? 1 : Math.Round(Math.Clamp(style.Hardness, 0, 1), 3);
            var flags = style.Flags & 31;
            if (string.IsNullOrEmpty(key) || key.Length > 40) return "笔画编号不对";
            if (!Tools.Contains(tool)) return "不支持的工具";
            if (!ColorRe.IsMatch(color ?? "")) return "颜色格式不对";
            if (double.IsNaN(size) || size < 0.5 || size > 300) return "笔刷大小不对";
            if (double.IsNaN(opacity) || opacity < 0.02 || opacity > 1) return "不透明度不对";
            points ??= new List<int>();
            if (points.Count % 3 != 0 || points.Count > MaxPointsPerChunk) return "笔画数据不对";
            // 选区
            var clip = style.Clip is { Count: > 0 } clipIn ? clipIn : null;
            if (clip != null && (clip.Count % 2 != 0 || clip.Count < 6 || clip.Count > MaxClipValues)) return "选区数据不对";
            // 变换矩阵：只有 xform 用
            double[]? matrix = null;
            if (tool == "xform")
            {
                if (style.Matrix is not { Length: 6 } m || m.Any(v => !double.IsFinite(v))) return "变换数据不对";
                if (m.Take(4).Any(v => Math.Abs(v) > 16)) return "缩放太大了";
                matrix = m.Select(v => Math.Round(v, 4)).ToArray();
            }
            string? color2 = null;
            if (tool == "gradient" && !string.IsNullOrEmpty(style.Color2))
            {
                if (!ColorRe.IsMatch(style.Color2)) return "颜色格式不对";
                color2 = style.Color2.ToLowerInvariant();
            }
            string? patch = null;
            if (tool == "patch")
            {
                if (!HashRe.IsMatch(style.Patch ?? "")) return "贴图数据不对";
                patch = style.Patch;
            }
            await room.Gate.WaitAsync();
            try
            {
                if (CanDraw(room, u, room.Layer(layerId)) is { } err) return err;
                if (!InCanvas(room, points)) return "笔画超出画布";
                if (clip != null && !InCanvasXY(room, clip)) return "选区超出画布";
                if (matrix != null && (Math.Abs(matrix[4]) > room.Width * 4 || Math.Abs(matrix[5]) > room.Height * 4)) return "移动得太远了";
                if (patch != null && !room.Patches.ContainsKey(patch)) return "贴图还没上传成功，再试一次";
                if (room.Live.Count(kv => kv.Key.Item1 == u) >= 4) return "笔画太快了";
                var stroke = new PaintStroke
                {
                    LayerId = layerId, UserId = u, Tool = tool, Color = color!.ToLowerInvariant(), Size = Math.Round(size, 2), Opacity = Math.Round(opacity, 3),
                    Brush = brush, Hardness = hardness, Flags = flags, Custom = custom, Clip = clip, Matrix = matrix, Color2 = color2, Patch = patch,
                };
                stroke.Points.AddRange(points);
                room.Live[(u, key)] = stroke;
                await hub.Clients.GroupExcept(room.Group, connectionId).SendAsync("LiveBegin", new { userId = u, key, layerId, tool = stroke.Tool, color = stroke.Color, size = stroke.Size, opacity = stroke.Opacity, brush, hardness, flags, custom, clip, matrix, color2, patch, points });
                return null;
            }
            finally { room.Gate.Release(); }
        }

        public async Task<string?> StrokePointsAsync(Guid u, string connectionId, string key, List<int> points)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在画室里";
            if (points == null || points.Count == 0) return null;
            if (points.Count % 3 != 0 || points.Count > MaxPointsPerChunk) return "笔画数据不对";
            await room.Gate.WaitAsync();
            try
            {
                if (!room.Live.TryGetValue((u, key), out var stroke)) return null;
                if (!InCanvas(room, points)) return "笔画超出画布";
                if (stroke.Points.Count + points.Count > MaxPointsPerStroke) return "这一笔太长了，抬笔再继续画";
                stroke.Points.AddRange(points);
                await hub.Clients.GroupExcept(room.Group, connectionId).SendAsync("LivePoints", new { userId = u, key, points });
                return null;
            }
            finally { room.Gate.Release(); }
        }

        /// <summary>抬笔：编号入库并发给所有人。返回给自己的编号（0 表示这笔作废）</summary>
        public async Task<(string? Error, long Seq)> StrokeEndAsync(Guid u, string connectionId, string key, List<int>? points)
        {
            var room = RoomOf(u);
            if (room == null) return ("你不在画室里", 0);
            points ??= new List<int>();
            if (points.Count % 3 != 0 || points.Count > MaxPointsPerChunk) return ("笔画数据不对", 0);
            await room.Gate.WaitAsync();
            try
            {
                if (!room.Live.Remove((u, key), out var stroke)) return (null, 0);
                if (!InCanvas(room, points) || stroke.Points.Count + points.Count > MaxPointsPerStroke)
                {
                    await hub.Clients.GroupExcept(room.Group, connectionId).SendAsync("LiveCancel", new { userId = u, key });
                    return ("笔画数据不对", 0);
                }
                stroke.Points.AddRange(points);
                var layer = room.Layer(stroke.LayerId);
                var minPoints = stroke.Tool switch { "polyfill" or "xform" => 9, "gradient" => 6, _ => 3 };
                if (stroke.Points.Count < minPoints || CanDraw(room, u, layer) is not null)
                {
                    await hub.Clients.GroupExcept(room.Group, connectionId).SendAsync("LiveCancel", new { userId = u, key });
                    return (layer == null ? "图层已经被删掉了" : null, 0);
                }
                stroke.Seq = ++room.Seq;
                room.Strokes.Add(stroke);
                if (room.HistoryPoints + stroke.Points.Count <= MaxHistoryPoints) { room.History.Add(stroke); room.HistoryPoints += stroke.Points.Count; }
                else room.HistoryTruncated = true;
                room.Redo.Remove(u); // 画了新的一笔，重做记录清空
                await hub.Clients.GroupExcept(room.Group, connectionId).SendAsync("Stroke", new { key, stroke = StrokeView(stroke) });
                await MaybeRequestBakeAsync(room, stroke.LayerId, connectionId);
                return (null, stroke.Seq);
            }
            finally { room.Gate.Release(); }
        }

        public async Task StrokeCancelAsync(Guid u, string connectionId, string key)
        {
            var room = RoomOf(u);
            if (room == null) return;
            await room.Gate.WaitAsync();
            try
            {
                if (room.Live.Remove((u, key)))
                    await hub.Clients.GroupExcept(room.Group, connectionId).SendAsync("LiveCancel", new { userId = u, key });
            }
            finally { room.Gate.Release(); }
        }

        public async Task<string?> UndoAsync(Guid u)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在画室里";
            await room.Gate.WaitAsync();
            try
            {
                var s = room.Strokes.LastOrDefault(x => x.UserId == u && !x.Undone);
                if (s == null) return "没有可以撤销的笔画了";
                s.Undone = true;
                if (!room.Redo.TryGetValue(u, out var stack)) room.Redo[u] = stack = new Stack<long>();
                stack.Push(s.Seq);
                await hub.Clients.Group(room.Group).SendAsync("Undone", new { seq = s.Seq, layerId = s.LayerId, undone = true });
                return null;
            }
            finally { room.Gate.Release(); }
        }

        public async Task<string?> RedoAsync(Guid u)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在画室里";
            await room.Gate.WaitAsync();
            try
            {
                if (!room.Redo.TryGetValue(u, out var stack)) return "没有可以重做的笔画";
                while (stack.Count > 0)
                {
                    var seq = stack.Pop();
                    var s = room.Strokes.FirstOrDefault(x => x.Seq == seq);
                    if (s == null || !s.Undone) continue;
                    s.Undone = false;
                    await hub.Clients.Group(room.Group).SendAsync("Undone", new { seq = s.Seq, layerId = s.LayerId, undone = false });
                    return null;
                }
                return "没有可以重做的笔画";
            }
            finally { room.Gate.Release(); }
        }

        public async Task CursorAsync(Guid u, string connectionId, double x, double y)
        {
            var room = RoomOf(u);
            if (room == null || double.IsNaN(x) || double.IsNaN(y)) return;
            try { await hub.Clients.GroupExcept(room.Group, connectionId).SendAsync("Cursor", new { userId = u, x = Math.Round(x, 1), y = Math.Round(y, 1) }); }
            catch (Exception ex) { logger.LogDebug(ex, "转发光标失败"); }
        }

        // ================= 合并旧笔画（底图） =================

        /// <summary>在 Gate 里调用：图层笔画太多时，请 connectionId 这个客户端把旧笔画合成底图上传</summary>
        private async Task MaybeRequestBakeAsync(PaintRoom room, int layerId, string connectionId)
        {
            if (room.PendingBake.ContainsKey(layerId)) return;
            var strokes = room.Strokes.Where(s => s.LayerId == layerId).ToList();
            if (strokes.Count <= BakeThreshold) return;
            var upto = strokes[strokes.Count - KeepAfterBake - 1].Seq;
            room.PendingBake[layerId] = (upto, DateTime.UtcNow);
            await hub.Clients.Client(connectionId).SendAsync("BakeRequest", new { layerId, upto });
        }

        /// <summary>客户端上传了合并好的底图（HTTP，见 PaintController）</summary>
        public async Task<string?> AcceptBakeAsync(Guid u, string roomId, int layerId, long upto, byte[] png)
        {
            var room = Find(roomId);
            if (room == null) return "画室不存在";
            if (png.Length == 0 || png.Length > MaxBaseBytes) return "图片太大";
            await room.Gate.WaitAsync();
            try
            {
                if (room.Member(u) == null) return "你不在画室里";
                if (!room.PendingBake.TryGetValue(layerId, out var pending) || pending.Upto != upto) return "这次合并已经过期了";
                var layer = room.Layer(layerId);
                room.PendingBake.Remove(layerId);
                if (layer == null) return "图层不存在";
                layer.BaseImage = png;
                layer.BaseVersion++;
                room.Strokes.RemoveAll(s => s.LayerId == layerId && s.Seq <= upto);
                PruneRedo(room);
                await hub.Clients.Group(room.Group).SendAsync("Baked", new { layerId, upto, baseVersion = layer.BaseVersion });
                return null;
            }
            finally { room.Gate.Release(); }
        }

        private static void PruneRedo(PaintRoom room)
        {
            var alive = room.Strokes.Select(s => s.Seq).ToHashSet();
            foreach (var u in room.Redo.Keys.ToList())
                room.Redo[u] = new Stack<long>(room.Redo[u].Where(alive.Contains).Reverse());
        }

        /// <summary>取底图。version 和当前版本不一致时返回 Stale = true（客户端应重新载入整个画室）</summary>
        public (byte[]? Png, string? Error, bool Stale) GetBase(Guid u, string roomId, int layerId, int? version)
        {
            var room = Find(roomId);
            if (room == null) return (null, "画室不存在", false);
            if (room.Member(u) == null) return (null, "你不在画室里", false);
            var layer = room.Layer(layerId);
            if (layer == null) return (null, "图层不存在", true);
            if (version != null && version != layer.BaseVersion) return (null, "底图已经更新", true);
            var png = layer.BaseImage;
            return png == null ? (null, "没有底图", false) : (png, null, false);
        }

        // ================= 贴图（油漆桶的结果） =================

        /// <summary>客户端上传贴图（HTTP，见 PaintController）。返回哈希，之后用 tool = patch 的笔画引用它</summary>
        public async Task<(string? Hash, string? Error)> AddPatchAsync(Guid u, string roomId, byte[] png)
        {
            var room = Find(roomId);
            if (room == null) return (null, "画室不存在");
            if (png.Length < 33 || png.Length > MaxPatchBytes) return (null, "图片太大");
            // PNG 文件头 + IHDR 里的宽高：不能比画布还大
            if (png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4E || png[3] != 0x47) return (null, "只接受 PNG");
            var w = (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
            var h = (png[20] << 24) | (png[21] << 16) | (png[22] << 8) | png[23];
            if (w <= 0 || h <= 0 || w > room.Width || h > room.Height) return (null, "图片尺寸不对");
            var hash = Convert.ToHexString(SHA256.HashData(png)).ToLowerInvariant()[..32];
            await room.Gate.WaitAsync();
            try
            {
                if (room.Member(u) == null) return (null, "你不在画室里");
                if (room.Patches.ContainsKey(hash)) return (hash, null);
                if (room.PatchBytes + png.Length > MaxRoomPatchBytes) return (null, "这间画室的填充数据太多了，先导出保存，或者开一间新画室");
                room.Patches[hash] = png;
                room.PatchBytes += png.Length;
                return (hash, null);
            }
            finally { room.Gate.Release(); }
        }

        public (byte[]? Png, string? Error) GetPatch(Guid u, string roomId, string hash)
        {
            var room = Find(roomId);
            if (room == null) return (null, "画室不存在");
            if (room.Member(u) == null) return (null, "你不在画室里");
            return room.Patches.TryGetValue(hash ?? "", out var png) ? (png, null) : (null, "贴图不存在");
        }

        // ================= 回放 =================

        /// <summary>
        /// 绘画过程：按落笔顺序列出最终还在画面上的笔画（撤销的、被清空的、所在图层被删的都不算），
        /// 图层按现在的顺序和不透明度。画室成员才能取。
        /// </summary>
        public async Task<(object? Data, string? Error)> HistoryAsync(Guid u, string roomId)
        {
            var room = Find(roomId);
            if (room == null) return (null, "画室不存在");
            await room.Gate.WaitAsync();
            try
            {
                if (room.Member(u) == null) return (null, "你不在画室里");
                var layerIds = room.Layers.Select(l => l.Id).ToHashSet();
                var names = room.Members.ToDictionary(m => m.UserId, m => m.Name);
                var strokes = room.History
                    .Where(s => !s.Undone && !s.Cleared && layerIds.Contains(s.LayerId))
                    .Select(s => new
                    {
                        layerId = s.LayerId, userId = s.UserId, tool = s.Tool, color = s.Color, size = s.Size, opacity = s.Opacity,
                        brush = s.Brush, hardness = s.Hardness, flags = s.Flags, custom = s.Custom, points = s.Points.ToArray(),
                        clip = s.Clip, matrix = s.Matrix, color2 = s.Color2, patch = s.Patch,
                    })
                    .ToList();
                return (new
                {
                    name = room.Name, width = room.Width, height = room.Height, truncated = room.HistoryTruncated,
                    layers = room.Layers.Select(l => new { id = l.Id, name = l.Name, opacity = l.Opacity }).ToList(),
                    painters = names.Select(kv => new { userId = kv.Key, name = kv.Value }).ToList(),
                    strokes,
                }, null);
            }
            finally { room.Gate.Release(); }
        }

        // ================= 图层 =================

        private static bool CanManage(PaintRoom room, Guid u, PaintLayer layer) => layer.CreatorId == u || room.OwnerId == u;

        public async Task<string?> LayerAsync(Guid u, string action, int layerId, string? text, double value)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在画室里";
            await room.Gate.WaitAsync();
            try
            {
                var member = room.Member(u);
                if (member == null) return "你不在画室里";
                if (action == "add")
                {
                    if (room.Layers.Count >= MaxLayers) return $"最多 {MaxLayers} 个图层";
                    var name = string.IsNullOrWhiteSpace(text) ? $"图层 {room.NextLayerId}" : text.Trim();
                    if (name.Length > 20) name = name[..20];
                    // 新图层放在当前图层上面
                    var at = room.Layers.FindIndex(l => l.Id == layerId);
                    var layer = new PaintLayer { Id = room.NextLayerId++, Name = name, CreatorId = u, CreatorName = member.Name };
                    room.Layers.Insert(at < 0 ? room.Layers.Count : at + 1, layer);
                    await hub.Clients.Group(room.Group).SendAsync("Layers", LayersView(room));
                    return null;
                }
                var target = room.Layer(layerId);
                if (target == null) return "图层不存在";
                if (!CanManage(room, u, target)) return $"只有 {target.CreatorName} 或房主能改这个图层";
                switch (action)
                {
                    case "rename":
                        var n = (text ?? "").Trim();
                        if (n.Length == 0) return "名字不能为空";
                        target.Name = n.Length > 20 ? n[..20] : n;
                        break;
                    case "opacity":
                        if (double.IsNaN(value)) return "不透明度不对";
                        target.Opacity = Math.Round(Math.Clamp(value, 0, 1), 3);
                        break;
                    case "lock":
                        target.Locked = value > 0;
                        break;
                    case "up":
                    case "down":
                        var i = room.Layers.IndexOf(target);
                        var j = action == "up" ? i + 1 : i - 1;
                        if (j < 0 || j >= room.Layers.Count) return null;
                        (room.Layers[i], room.Layers[j]) = (room.Layers[j], room.Layers[i]);
                        break;
                    case "delete":
                        if (room.Layers.Count <= 1) return "至少要留一个图层";
                        room.Layers.Remove(target);
                        room.Strokes.RemoveAll(s => s.LayerId == target.Id);
                        foreach (var key in room.Live.Where(kv => kv.Value.LayerId == target.Id).Select(kv => kv.Key).ToList()) room.Live.Remove(key);
                        room.PendingBake.Remove(target.Id);
                        PruneRedo(room);
                        break;
                    case "clear":
                        foreach (var h in room.History) if (h.LayerId == target.Id) h.Cleared = true;
                        room.Strokes.RemoveAll(s => s.LayerId == target.Id);
                        room.PendingBake.Remove(target.Id);
                        target.BaseImage = null;
                        target.BaseVersion++;
                        PruneRedo(room);
                        await hub.Clients.Group(room.Group).SendAsync("Cleared", new { layerId = target.Id, baseVersion = target.BaseVersion });
                        break;
                    default:
                        return "不支持的操作";
                }
                await hub.Clients.Group(room.Group).SendAsync("Layers", LayersView(room));
                return null;
            }
            finally { room.Gate.Release(); }
        }

        // ================= 聊天 =================

        public async Task<string?> ChatAsync(Guid u, string text)
        {
            var room = RoomOf(u);
            if (room == null) return "你不在画室里";
            text = (text ?? "").Trim();
            if (text.Length == 0) return null;
            if (text.Length > 200) text = text[..200];
            await room.Gate.WaitAsync();
            try
            {
                var m = room.Member(u);
                if (m == null) return "你不在画室里";
                if ((DateTime.UtcNow - m.LastChatAt).TotalMilliseconds < 600) return "说得太快了";
                m.LastChatAt = DateTime.UtcNow;
                var msg = new { userId = u, name = m.Name, color = m.Color, text, at = DateTime.UtcNow };
                room.Chat.Add(msg);
                if (room.Chat.Count > 80) room.Chat.RemoveAt(0);
                await hub.Clients.Group(room.Group).SendAsync("Chat", msg);
                return null;
            }
            finally { room.Gate.Release(); }
        }

        // ================= 定时清理 =================

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                var now = DateTime.UtcNow;
                foreach (var room in _rooms.Values)
                {
                    try
                    {
                        await room.Gate.WaitAsync(stoppingToken);
                        try
                        {
                            foreach (var m in room.Members.Where(m => m.OfflineSince is DateTime t && (now - t).TotalSeconds >= OfflineGraceSeconds).ToList())
                                await RemoveMemberAsync(room, m.UserId, "offline");
                            foreach (var (layerId, p) in room.PendingBake.ToList())
                                if ((now - p.At).TotalSeconds > 30) room.PendingBake.Remove(layerId); // 下一笔会重新请求
                            if (room.Members.Count == 0 && room.EmptySince is DateTime e && (now - e).TotalMinutes >= EmptyRoomMinutes)
                                _rooms.TryRemove(room.Id, out _);
                        }
                        finally { room.Gate.Release(); }
                    }
                    catch (OperationCanceledException) { return; }
                    catch (Exception ex) { logger.LogWarning(ex, "茶绘定时清理出错"); }
                }
            }
        }
    }
}
