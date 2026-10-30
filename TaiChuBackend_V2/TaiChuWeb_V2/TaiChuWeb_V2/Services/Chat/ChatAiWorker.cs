using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Hubs;

namespace TaiChuWeb_V2.Services.Chat
{
    /// <summary>
    /// AI 人格发言调度（后台单线程按顺序处理，不会并发刷屏）。
    /// 每条新消息到来时，按每个 AI 人格主人的设定决定是否发言：
    /// · 被 @ 名字、或有人回复了它的消息 → 回应（所有模式都会）；
    /// · 「主动接话」模式 → 冷却时间过后，按设定的概率接话（每条消息最多一个 AI 主动接话）；
    /// · 消息来自另一个 AI → 只有开启了「回应其他 AI」才会接；连续 AI 消息达到上限后停下，等真人说话；
    /// · 每天发言次数到上限后不再发言。
    /// API 调用一律使用 AI 主人自己在个人中心配置的接口和 Key，平台不代付。
    /// </summary>
    public sealed class ChatAiWorker(
        ChatAiQueue queue,
        ChatStore store,
        IServiceScopeFactory scopes,
        IHubContext<ChatHub> hub,
        IHttpClientFactory httpFactory,
        IConfiguration config,
        ILogger<ChatAiWorker> logger) : BackgroundService
    {
        /// <summary>频道里连续多少条 AI 消息后，AI 不再互相接话</summary>
        public const int MaxAiChain = 4;
        private const int ContextMessages = 20;
        private const int MaxMentionResponders = 3;

        private string EncryptionSecret => config["Ai:EncryptionKey"] ?? config["Jwt:Key"] ?? "TaichuAiDefaultSecret";

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var trigger in queue.Reader.ReadAllAsync(stoppingToken))
            {
                try { await HandleAsync(trigger, stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
                catch (Exception ex) { logger.LogWarning(ex, "聊天室 AI 发言处理失败"); }
            }
        }

        private static bool Mentions(string content, string name) =>
            content.Contains("@" + name, StringComparison.OrdinalIgnoreCase);

        private async Task HandleAsync(ChatAiTrigger trigger, CancellationToken ct)
        {
            var channel = await store.ChannelAsync(trigger.ChannelId, ct);
            if (channel == null || !channel.AllowAi) return;
            var history = await store.MessagesAsync(trigger.ChannelId, null, ContextMessages, ct);
            var message = history.FirstOrDefault(m => m.Id == trigger.MessageId) ?? (trigger.MessageId > 0 ? await store.MessageAsync(trigger.MessageId, ct) : null);
            if (trigger.ManualOwnerId == null && (message == null || message.Deleted)) return;
            var personas = (await store.PersonasAsync(enabledOnly: true, ct)).Where(p => p.AllowsChannel(channel.Id)).ToList();
            if (personas.Count == 0) return;

            var responders = new List<(ChatPersona Persona, bool Direct)>();
            if (trigger.ManualOwnerId is Guid manual)
            {
                var p = personas.FirstOrDefault(x => x.UserId == manual);
                if (p != null) responders.Add((p, false)); // 主人让它说一句：接着当前话题说，不针对某条消息
            }
            else if (message != null)
            {
                // 结尾连续的 AI 消息条数
                var chain = 0;
                for (var i = history.Count - 1; i >= 0 && history[i].IsAi; i--) chain++;
                // 普通频道：连续 AI 消息达到上限就停，等真人说话；
                // 「仅 AI 发言」频道本来就没有真人插话，改为限速：最近 1 分钟 AI 消息超过 6 条就先不接。
                var chainBlocked = channel.AiOnly
                    ? history.Count(h => h.IsAi && DateTime.UtcNow - h.CreatedAt < TimeSpan.FromMinutes(1)) >= 6
                    : chain >= MaxAiChain;
                ChatMessageRow? repliedTo = message.ReplyToId is long rid ? history.FirstOrDefault(h => h.Id == rid) ?? await store.MessageAsync(rid, ct) : null;

                foreach (var p in personas)
                {
                    if (message.IsAi && message.UserId == p.UserId) continue; // 不接自己的话
                    if (message.IsAi && (!p.ReplyToAi || chainBlocked)) continue;
                    var direct = Mentions(message.Content, p.Name) || (repliedTo is { IsAi: true, BotId: null } && repliedTo.UserId == p.UserId);
                    if (direct) responders.Add((p, true));
                }
                responders = responders.Take(MaxMentionResponders).ToList();

                if (responders.Count == 0)
                {
                    // 没人点名时，从「主动接话」的 AI 里按概率挑一个
                    var candidates = new List<ChatPersona>();
                    foreach (var p in personas.Where(x => x.ReplyMode == "active").OrderBy(_ => Random.Shared.Next()))
                    {
                        if (message.IsAi && (message.UserId == p.UserId || !p.ReplyToAi || chainBlocked)) continue;
                        if (Random.Shared.Next(100) >= Math.Clamp(p.AutoChance, 0, 100)) continue;
                        var last = await store.LastAiMessageAtAsync(p.UserId, channel.Id, ct);
                        if (last.HasValue && DateTime.UtcNow - last.Value < TimeSpan.FromSeconds(Math.Max(10, p.CooldownSeconds))) continue;
                        candidates.Add(p);
                        break;
                    }
                    responders.AddRange(candidates.Select(c => (c, false)));
                }
            }

            foreach (var (persona, direct) in responders)
                await SpeakAsync(persona, channel, history, message, direct, trigger.ManualOwnerId.HasValue, ct);
        }

        private async Task SpeakAsync(ChatPersona persona, ChatChannel channel, List<ChatMessageRow> history, ChatMessageRow? trigger, bool direct, bool manual, CancellationToken ct)
        {
            if (await store.AiCountTodayAsync(persona.UserId, ct) >= Math.Max(1, persona.DailyLimit))
            {
                if (manual) await NotifyOwnerAsync(persona.UserId, $"「{persona.Name}」今天的发言次数已达到上限（{persona.DailyLimit} 次）。", ct);
                return;
            }

            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var cfg = await db.UserAiConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == persona.UserId, ct);
            var apiKey = cfg == null ? string.Empty : AiKeyCrypto.Decrypt(cfg.EncryptedApiKey, EncryptionSecret);
            if (cfg == null || string.IsNullOrEmpty(apiKey))
            {
                await FailAsync(persona, "还没有在个人中心配置 AI 接口和 Key，AI 人格无法发言。", ct);
                return;
            }

            await hub.Clients.Group(ChatHub.Group(channel.Id)).SendAsync("Typing", new { channelId = channel.Id, userId = persona.UserId, name = persona.Name, isAi = true }, ct);

            // 准备上下文：人名 → 显示名
            var ids = history.Select(h => h.UserId).Append(persona.UserId).Distinct().ToList();
            var names = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.Username }).ToDictionaryAsync(u => u.Id, u => u.Username, ct);
            string Speaker(ChatMessageRow m) => m.IsAi ? $"{m.AiName ?? "AI"}（AI）" : names.GetValueOrDefault(m.UserId, "某位成员");
            var ownerName = names.GetValueOrDefault(persona.UserId, "一位成员");

            var system = new StringBuilder();
            system.AppendLine(string.IsNullOrWhiteSpace(persona.Prompt) ? $"你是「{persona.Name}」。" : persona.Prompt.Trim());
            system.AppendLine();
            system.AppendLine($"【场景】你正在「太初寰宇」社区聊天室的「{channel.Name}」频道里聊天。你的名字是「{persona.Name}」，由成员 {ownerName} 接入。聊天室会把你的消息标注为 AI，大家都知道你是 AI。");
            system.AppendLine("【要求】像群聊一样自然地说话，回复简短（一般不超过 120 字），不要用 Markdown 标题和长列表；不要在开头写自己的名字；不要冒充其他成员；不讨论违法、色情、人身攻击等内容。");
            if (direct && trigger != null) system.AppendLine($"【当前】{Speaker(trigger)} 刚刚在和你说话，请回应 TA。");

            var messages = new List<object> { new { role = "system", content = system.ToString() } };
            foreach (var m in history.Where(h => !h.Deleted))
            {
                var mine = m.IsAi && m.UserId == persona.UserId;
                messages.Add(mine
                    ? new { role = "assistant", content = m.Content }
                    : new { role = "user", content = $"{Speaker(m)}：{m.Content}" });
            }
            if (history.Count == 0 || (history[^1].IsAi && history[^1].UserId == persona.UserId))
                messages.Add(new { role = "user", content = "（大家在等你接着说点什么）" });

            string reply;
            try
            {
                reply = await CallAsync(cfg.BaseUrl, apiKey, cfg.Model, messages, ct);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                await hub.Clients.Group(ChatHub.Group(channel.Id)).SendAsync("TypingStop", new { channelId = channel.Id, userId = persona.UserId, isAi = true }, ct);
                await FailAsync(persona, "调用 AI 接口失败：" + ex.Message, ct);
                return;
            }

            reply = Clean(reply, persona.Name);
            await hub.Clients.Group(ChatHub.Group(channel.Id)).SendAsync("TypingStop", new { channelId = channel.Id, userId = persona.UserId, isAi = true }, ct);
            if (string.IsNullOrWhiteSpace(reply)) return;

            var row = await store.InsertMessageAsync(new ChatMessageRow
            {
                ChannelId = channel.Id,
                UserId = persona.UserId,
                IsAi = true,
                AiName = persona.Name,
                AiAvatar = persona.Avatar,
                Content = reply,
                ReplyToId = direct ? trigger?.Id : null,
            }, ct);
            if (persona.LastError != null) await store.SetPersonaErrorAsync(persona.UserId, null, ct);

            var service = scope.ServiceProvider.GetRequiredService<ChatService>();
            await service.BroadcastAsync(row, ct);
            // 让其他 AI 有机会接这条话（受「回应其他 AI」和连续条数上限约束）
            queue.Enqueue(new ChatAiTrigger(channel.Id, row.Id));
        }

        private static string Clean(string text, string name)
        {
            var s = (text ?? string.Empty).Trim();
            foreach (var prefix in new[] { name + "（AI）：", name + "(AI):", name + "：", name + ":" })
                if (s.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) { s = s[prefix.Length..].TrimStart(); break; }
            return s.Length > 1000 ? s[..1000] + "…" : s;
        }

        private async Task FailAsync(ChatPersona persona, string error, CancellationToken ct)
        {
            await store.SetPersonaErrorAsync(persona.UserId, error, ct);
            await NotifyOwnerAsync(persona.UserId, error, ct);
        }

        private Task NotifyOwnerAsync(Guid ownerId, string message, CancellationToken ct) =>
            hub.Clients.User(ownerId.ToString()).SendAsync("AiError", new { message }, ct);

        private async Task<string> CallAsync(string baseUrl, string apiKey, string model, List<object> messages, CancellationToken ct)
        {
            var url = baseUrl.TrimEnd('/') + "/chat/completions";
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            req.Content = new StringContent(JsonSerializer.Serialize(new { model, messages, temperature = 0.9, max_tokens = 400 }), Encoding.UTF8, "application/json");
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(60));
            var client = httpFactory.CreateClient();
            using var resp = await client.SendAsync(req, timeout.Token);
            var body = await resp.Content.ReadAsStringAsync(timeout.Token);
            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"HTTP {(int)resp.StatusCode} {(body.Length > 160 ? body[..160] : body)}");
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.GetProperty("choices")[0].GetProperty("message").GetProperty("content").GetString() ?? string.Empty;
        }
    }
}
