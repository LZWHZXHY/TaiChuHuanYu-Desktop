using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using TaiChuWeb_V2.Hubs;

namespace TaiChuWeb_V2.Services.Chat
{
    /// <summary>
    /// 外部机器人接入网关（太初机器人协议 v1）。
    /// 机器人程序（例如本地 Python + Ollama）主动用 WebSocket 连到 /signalr/bot，凭令牌登录：
    ///   ws(s)://站点/signalr/bot?token=tcb_xxx    或请求头 X-Bot-Token: tcb_xxx
    /// 服务器 → 机器人：hello / message / deleted / result / pong / error
    /// 机器人 → 服务器：send / typing / history / channels / ping（JSON，一条消息一个对象）
    /// 消息推送范围：研究频道里推送全部消息；其他频道只推送 @ 它或回复它的消息。
    /// 机器人不需要公网地址，电脑能上网即可；断线后自行重连。
    /// </summary>
    public sealed class BotGateway(ChatStore store, IServiceScopeFactory scopes, ChatAiQueue aiQueue, IHubContext<ChatHub> hub, ILogger<BotGateway> logger)
    {
        public const string Path = "/signalr/bot";
        public const int ProtocolVersion = 1;
        public const int MaxContent = 2000;
        public const int DailyLimit = 1000;
        private const int MaxFrame = 64 * 1024;

        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private sealed class Connection(ChatBot bot, WebSocket socket)
        {
            public ChatBot Bot { get; set; } = bot;
            public WebSocket Socket { get; } = socket;
            public SemaphoreSlim SendLock { get; } = new(1, 1);
            public Queue<DateTime> Sent { get; } = new();
        }

        private readonly ConcurrentDictionary<string, Connection> _connections = new();
        private static readonly ConcurrentDictionary<string, Queue<DateTime>> ChannelBuckets = new();
        private List<ChatChannel>? _channelCache;
        private DateTime _channelCacheAt;

        public bool IsOnline(string botId) => _connections.ContainsKey(botId);
        public IReadOnlyCollection<ChatBot> OnlineBots => _connections.Values.Select(c => c.Bot).ToList();

        // ---------------- 令牌 ----------------

        public static string NewBotId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant();

        public static string NewToken(string botId) =>
            $"tcb_{botId}_{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";

        public static string HashToken(string token) =>
            Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token))).ToLowerInvariant();

        private async Task<ChatBot?> AuthenticateAsync(string? token)
        {
            if (string.IsNullOrWhiteSpace(token) || !token.StartsWith("tcb_")) return null;
            var parts = token.Split('_', 3);
            if (parts.Length != 3) return null;
            var bot = await store.BotAsync(parts[1]);
            if (bot == null) return null;
            var expected = Encoding.ASCII.GetBytes(bot.TokenHash);
            var actual = Encoding.ASCII.GetBytes(HashToken(token));
            return CryptographicOperations.FixedTimeEquals(expected, actual) ? bot : null;
        }

        private async Task<List<ChatChannel>> ChannelsAsync()
        {
            if (_channelCache == null || DateTime.UtcNow - _channelCacheAt > TimeSpan.FromSeconds(30))
            {
                _channelCache = await store.ChannelsAsync();
                _channelCacheAt = DateTime.UtcNow;
            }
            return _channelCache;
        }

        public void InvalidateChannels() => _channelCache = null;

        /// <summary>机器人设置变化（改名、停用、删除、换令牌）后调用：停用或删除时断开连接，否则更新内存里的资料。</summary>
        public async Task RefreshBotAsync(string botId, bool disconnect)
        {
            if (!_connections.TryGetValue(botId, out var conn)) return;
            var bot = disconnect ? null : await store.BotAsync(botId);
            if (bot == null || !bot.Enabled)
            {
                await SendAsync(conn, new { type = "error", code = "disabled", message = "机器人已被停用、删除或更换了令牌，连接已断开" });
                try { await conn.Socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "disabled", CancellationToken.None); } catch { /* 忽略 */ }
                _connections.TryRemove(new KeyValuePair<string, Connection>(botId, conn));
                await NotifyBotsChangedAsync();
            }
            else conn.Bot = bot;
        }

        private Task NotifyBotsChangedAsync() => hub.Clients.All.SendAsync("BotsChanged");

        // ---------------- 连接 ----------------

        public async Task HandleAsync(HttpContext http)
        {
            if (!http.WebSockets.IsWebSocketRequest)
            {
                http.Response.StatusCode = 400;
                http.Response.ContentType = "text/plain; charset=utf-8";
                await http.Response.WriteAsync("这里是太初聊天室的机器人 WebSocket 接入口，请用 WebSocket 连接，并带上 ?token=你的机器人令牌。");
                return;
            }
            var token = http.Request.Headers["X-Bot-Token"].FirstOrDefault() ?? http.Request.Query["token"].FirstOrDefault();
            var bot = await AuthenticateAsync(token);
            if (bot == null) { http.Response.StatusCode = 401; return; }
            if (!bot.Enabled) { http.Response.StatusCode = 403; return; }

            using var socket = await http.WebSockets.AcceptWebSocketAsync();
            var conn = new Connection(bot, socket);
            if (_connections.TryRemove(bot.Id, out var old))
            {
                await SendAsync(old, new { type = "error", code = "replaced", message = "同一个机器人在别处建立了新连接，这个连接被替换" });
                try { await old.Socket.CloseAsync(WebSocketCloseStatus.PolicyViolation, "replaced", CancellationToken.None); } catch { /* 忽略 */ }
            }
            _connections[bot.Id] = conn;
            await store.TouchBotAsync(bot.Id);
            await NotifyBotsChangedAsync();
            logger.LogInformation("机器人 {Name}({Id}) 已连接", bot.Name, bot.Id);

            try
            {
                var channels = await ChannelsAsync();
                await SendAsync(conn, new
                {
                    type = "hello",
                    protocol = ProtocolVersion,
                    bot = new { id = bot.Id, name = bot.Name, ownerId = bot.OwnerId },
                    channels = channels.Where(c => c.AllowAi && bot.AllowsChannel(c.Id)).Select(ChannelInfo),
                    limits = new { maxContent = MaxContent, dailyLimit = DailyLimit, perMinute = 20, minIntervalMs = 1500 },
                    note = "研究频道里会收到全部消息；其他频道只收到 @ 你或回复你的消息。",
                });
                await ReceiveLoopAsync(conn, http.RequestAborted);
            }
            catch (Exception ex) when (ex is WebSocketException or OperationCanceledException)
            {
                // 对方断线，正常结束
            }
            finally
            {
                _connections.TryRemove(new KeyValuePair<string, Connection>(bot.Id, conn));
                try { await store.TouchBotAsync(bot.Id); } catch { /* 忽略 */ }
                await NotifyBotsChangedAsync();
                logger.LogInformation("机器人 {Name}({Id}) 已断开", bot.Name, bot.Id);
            }
        }

        private static object ChannelInfo(ChatChannel c) => new { id = c.Id, name = c.Name, description = c.Description, policy = c.PostPolicy, research = c.Research };

        private async Task ReceiveLoopAsync(Connection conn, CancellationToken ct)
        {
            var buffer = new byte[8192];
            using var message = new MemoryStream();
            while (conn.Socket.State == WebSocketState.Open && !ct.IsCancellationRequested)
            {
                var result = await conn.Socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await conn.Socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
                    return;
                }
                message.Write(buffer, 0, result.Count);
                if (message.Length > MaxFrame)
                {
                    await SendAsync(conn, new { type = "error", code = "too_large", message = "单条消息不能超过 64KB" });
                    message.SetLength(0);
                    continue;
                }
                if (!result.EndOfMessage) continue;
                var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                message.SetLength(0);
                await HandleCommandAsync(conn, text, ct);
            }
        }

        private async Task HandleCommandAsync(Connection conn, string text, CancellationToken ct)
        {
            JsonDocument doc;
            try { doc = JsonDocument.Parse(text); }
            catch { await SendAsync(conn, new { type = "error", code = "bad_json", message = "无法解析 JSON" }); return; }
            using var parsed = doc;
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) { await SendAsync(conn, new { type = "error", code = "bad_json", message = "每条消息必须是一个 JSON 对象" }); return; }

            string? Str(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            long? Num(string name) => root.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n) ? n : null;
            var echo = root.TryGetProperty("echo", out var e) ? e.Clone() : (JsonElement?)null;

            switch (Str("type"))
            {
                case "ping":
                    await SendAsync(conn, new { type = "pong", echo });
                    break;
                case "channels":
                    var all = await ChannelsAsync();
                    await Reply(conn, echo, true, all.Where(c => c.AllowAi && conn.Bot.AllowsChannel(c.Id)).Select(ChannelInfo));
                    break;
                case "typing":
                    var typingChannel = Str("channel");
                    if (typingChannel != null && conn.Bot.AllowsChannel(typingChannel))
                        await hub.Clients.Group(ChatHub.Group(typingChannel)).SendAsync("Typing", new { channelId = typingChannel, userId = conn.Bot.Id, name = conn.Bot.Name, isAi = true }, ct);
                    break;
                case "send":
                    await SendMessageAsync(conn, Str("channel"), Str("content"), Num("replyTo"), echo, ct);
                    break;
                case "history":
                    await HistoryAsync(conn, Str("channel"), Num("before"), (int)(Num("limit") ?? 30), echo, ct);
                    break;
                default:
                    await Reply(conn, echo, false, null, "未知的 type，可用：send / typing / history / channels / ping");
                    break;
            }
        }

        private Task Reply(Connection conn, JsonElement? echo, bool ok, object? data, string? error = null) =>
            SendAsync(conn, new { type = "result", echo, ok, data, error });

        private static bool Allow(Queue<DateTime> bucket, int perMinute, TimeSpan minGap)
        {
            var now = DateTime.UtcNow;
            lock (bucket)
            {
                while (bucket.Count > 0 && now - bucket.Peek() > TimeSpan.FromMinutes(1)) bucket.Dequeue();
                if (bucket.Count >= perMinute || (bucket.Count > 0 && now - bucket.Last() < minGap)) return false;
                bucket.Enqueue(now);
                return true;
            }
        }

        private async Task SendMessageAsync(Connection conn, string? channelId, string? content, long? replyTo, JsonElement? echo, CancellationToken ct)
        {
            var bot = conn.Bot;
            var channel = (await ChannelsAsync()).FirstOrDefault(c => c.Id == channelId);
            if (channel == null) { await Reply(conn, echo, false, null, "频道不存在"); return; }
            if (!channel.AllowAi) { await Reply(conn, echo, false, null, "这个频道不允许 AI 发言"); return; }
            if (!bot.AllowsChannel(channel.Id)) { await Reply(conn, echo, false, null, "你的机器人没有开放这个频道（在网站的机器人设置里修改）"); return; }
            var text = (content ?? string.Empty).Trim();
            if (text.Length == 0) { await Reply(conn, echo, false, null, "内容不能为空"); return; }
            if (text.Length > MaxContent) text = text[..MaxContent] + "…";
            if (!Allow(conn.Sent, 20, TimeSpan.FromMilliseconds(1500))) { await Reply(conn, echo, false, null, "发得太快了：每个机器人最多每 1.5 秒一条、每分钟 20 条"); return; }
            if (!Allow(ChannelBuckets.GetOrAdd(channel.Id, _ => new Queue<DateTime>()), 15, TimeSpan.Zero)) { await Reply(conn, echo, false, null, "这个频道的机器人消息太密集了（每分钟最多 15 条），稍后再说"); return; }
            if (await store.BotCountTodayAsync(bot.Id, ct) >= DailyLimit) { await Reply(conn, echo, false, null, $"今天的发言次数已达到上限（{DailyLimit} 条）"); return; }

            long? validReply = null;
            if (replyTo is long rid)
            {
                var target = await store.MessageAsync(rid, ct);
                if (target != null && target.ChannelId == channel.Id && !target.Deleted) validReply = rid;
            }

            var row = await store.InsertMessageAsync(new ChatMessageRow
            {
                ChannelId = channel.Id,
                UserId = bot.OwnerId,
                IsAi = true,
                AiName = bot.Name,
                AiAvatar = bot.Avatar,
                BotId = bot.Id,
                Content = text,
                ReplyToId = validReply,
            }, ct);

            using (var scope = scopes.CreateScope())
            {
                var service = scope.ServiceProvider.GetRequiredService<ChatService>();
                await service.BroadcastAsync(row, ct);
            }
            await hub.Clients.Group(ChatHub.Group(channel.Id)).SendAsync("TypingStop", new { channelId = channel.Id, userId = bot.Id, isAi = true }, ct);
            aiQueue.Enqueue(new ChatAiTrigger(channel.Id, row.Id));
            await Reply(conn, echo, true, new { id = row.Id, createdAt = row.CreatedAt });
        }

        private async Task HistoryAsync(Connection conn, string? channelId, long? before, int limit, JsonElement? echo, CancellationToken ct)
        {
            var channel = (await ChannelsAsync()).FirstOrDefault(c => c.Id == channelId);
            if (channel == null || !channel.AllowAi || !conn.Bot.AllowsChannel(channel.Id)) { await Reply(conn, echo, false, null, "频道不存在或没有开放给你的机器人"); return; }
            if (!channel.Research) { await Reply(conn, echo, false, null, "只有研究频道可以拉取历史；其他频道只会收到 @ 你或回复你的消息"); return; }
            var rows = await store.MessagesAsync(channel.Id, before, Math.Clamp(limit, 1, 100), ct);
            using var scope = scopes.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<ChatService>();
            var dtos = await service.ToDtosAsync(rows, ct);
            await Reply(conn, echo, true, new { channel = ChannelInfo(channel), messages = dtos.Select(d => ToBotMessage(d, conn.Bot)) });
        }

        // ---------------- 推送 ----------------

        private static bool MentionsBot(ChatMessageDto dto, ChatBot bot) =>
            dto.Content.Contains("@" + bot.Name, StringComparison.OrdinalIgnoreCase) || dto.ReplyTo?.BotId == bot.Id;

        /// <summary>转成发给机器人的消息格式</summary>
        private static object ToBotMessage(ChatMessageDto d, ChatBot bot) => new
        {
            id = d.Id,
            content = d.Content,
            createdAt = d.CreatedAt,
            sender = d.IsAi
                ? (object)new { kind = d.Ai?.BotId != null ? "bot" : "ai", id = d.Ai?.BotId ?? "persona:" + d.Author.Id, name = d.Ai?.Name, ownerId = d.Author.Id, ownerName = d.Author.Name, self = d.Ai?.BotId == bot.Id }
                : new { kind = "human", id = d.Author.Id.ToString(), name = d.Author.Name, ownerId = (Guid?)null, ownerName = (string?)null, self = false },
            replyTo = d.ReplyTo == null ? null : new { id = d.ReplyTo.Id, name = d.ReplyTo.Name, isAi = d.ReplyTo.IsAi, excerpt = d.ReplyTo.Excerpt, toYou = d.ReplyTo.BotId == bot.Id },
            mentionsYou = MentionsBot(d, bot),
        };

        /// <summary>新消息推给在线机器人（由 ChatService.BroadcastAsync 调用）。</summary>
        public async Task DispatchAsync(ChatMessageDto dto)
        {
            if (_connections.IsEmpty) return;
            var channel = (await ChannelsAsync()).FirstOrDefault(c => c.Id == dto.ChannelId);
            if (channel == null || !channel.AllowAi) return;
            foreach (var conn in _connections.Values)
            {
                var bot = conn.Bot;
                if (dto.Ai?.BotId == bot.Id || !bot.AllowsChannel(channel.Id)) continue;
                if (!channel.Research && !MentionsBot(dto, bot)) continue;
                await SendAsync(conn, new { type = "message", channel = ChannelInfo(channel), message = ToBotMessage(dto, bot) });
            }
        }

        public async Task DispatchDeletedAsync(string channelId, long messageId)
        {
            foreach (var conn in _connections.Values.Where(c => c.Bot.AllowsChannel(channelId)))
                await SendAsync(conn, new { type = "deleted", channel = channelId, id = messageId });
        }

        private async Task SendAsync(Connection conn, object payload)
        {
            if (conn.Socket.State != WebSocketState.Open) return;
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, Json);
            await conn.SendLock.WaitAsync();
            try
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await conn.Socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, timeout.Token);
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "推送给机器人 {Id} 失败", conn.Bot.Id);
            }
            finally { conn.SendLock.Release(); }
        }
    }
}
