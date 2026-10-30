using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Hubs;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services;
using TaiChuWeb_V2.Services.Chat;

namespace TaiChuWeb_V2.Controllers
{
    public sealed class SendChatMessageDto
    {
        public string Content { get; set; } = string.Empty;
        public long? ReplyToId { get; set; }
    }

    public sealed class SavePersonaDto
    {
        public bool Enabled { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public string Prompt { get; set; } = string.Empty;
        public string ReplyMode { get; set; } = "mention";
        public int AutoChance { get; set; } = 20;
        public int CooldownSeconds { get; set; } = 90;
        public bool ReplyToAi { get; set; }
        public int DailyLimit { get; set; } = 50;
        public List<string> Channels { get; set; } = new();
    }

    public sealed class SpeakDto
    {
        public string ChannelId { get; set; } = string.Empty;
    }

    public sealed class SaveBotDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public string? Description { get; set; }
        public bool Enabled { get; set; } = true;
        public List<string> Channels { get; set; } = new();
    }

    /// <summary>
    /// 社区聊天室：频道、消息、AI 人格。
    /// 看消息不需要登录；发消息、设置 AI 人格需要登录。AI 使用主人自己在个人中心配置的接口（平台不代付）。
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController(AppDbContext db, ChatStore store, ChatService service, ChatAiQueue aiQueue, IHubContext<ChatHub> hub, IConfiguration config, BotGateway gateway) : ControllerBase
    {
        private const int MaxLength = 1000;
        private const int MaxBotsPerUser = 3;
        private static readonly ConcurrentDictionary<Guid, Queue<DateTime>> SendLog = new();

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        /// <summary>防刷屏：每人每秒最多 1 条、每分钟最多 20 条。</summary>
        private static bool Throttled(Guid userId)
        {
            var now = DateTime.UtcNow;
            var log = SendLog.GetOrAdd(userId, _ => new Queue<DateTime>());
            lock (log)
            {
                while (log.Count > 0 && now - log.Peek() > TimeSpan.FromMinutes(1)) log.Dequeue();
                if (log.Count >= 20 || (log.Count > 0 && now - log.Last() < TimeSpan.FromSeconds(1))) return true;
                log.Enqueue(now);
                return false;
            }
        }

        private async Task<bool> IsModeratorAsync(Guid userId) =>
            await db.UserPermissions.AnyAsync(p => p.UserId == userId && (p.Permission == AdminPermission.SuperAdmin || p.Permission == AdminPermission.User_Audit));

        // ===== 频道 =====
        [HttpGet("channels")]
        public async Task<IActionResult> Channels()
        {
            var online = ChatHub.OnlineCounts();
            var list = await store.ChannelsAsync();
            var bots = gateway.OnlineBots;
            return Ok(list.Select(c => new
            {
                c.Id, c.Name, c.Description, c.Icon, c.AllowAi, c.PostPolicy, c.Research,
                online = online.GetValueOrDefault(c.Id),
                // 能读到这个频道全部消息的在线机器人数（研究频道），用于提示真人
                listeningBots = c.AllowAi && c.Research ? bots.Count(b => b.AllowsChannel(c.Id)) : 0,
            }));
        }

        // ===== 消息 =====
        [HttpGet("channels/{channelId}/messages")]
        public async Task<IActionResult> Messages(string channelId, [FromQuery] long? before, [FromQuery] int limit = 50)
        {
            if (await store.ChannelAsync(channelId) == null) return NotFound(new { message = "频道不存在" });
            var rows = await store.MessagesAsync(channelId, before, limit);
            return Ok(new { items = await service.ToDtosAsync(rows), hasMore = rows.Count >= Math.Clamp(limit, 1, 100) });
        }

        [Authorize]
        [HttpPost("channels/{channelId}/messages")]
        public async Task<IActionResult> Send(string channelId, [FromBody] SendChatMessageDto dto)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var channel = await store.ChannelAsync(channelId);
            if (channel == null) return NotFound(new { message = "频道不存在" });
            var content = (dto.Content ?? string.Empty).Trim();
            if (content.Length == 0) return BadRequest(new { message = "消息不能为空" });
            if (content.Length > MaxLength) return BadRequest(new { message = $"消息太长了（最多 {MaxLength} 字）" });
            if (channel.AiOnly) return StatusCode(403, new { message = "这个频道只有 AI 可以发言，你可以在这里观看" });
            if (Throttled(userId)) return StatusCode(429, new { message = "发得太快了，歇一下再说～" });

            long? replyTo = null;
            if (dto.ReplyToId is long rid)
            {
                var target = await store.MessageAsync(rid);
                if (target != null && target.ChannelId == channelId && !target.Deleted) replyTo = rid;
            }

            var row = await store.InsertMessageAsync(new ChatMessageRow { ChannelId = channelId, UserId = userId, Content = content, ReplyToId = replyTo });
            var result = await service.BroadcastAsync(row);
            if (channel.AllowAi) aiQueue.Enqueue(new ChatAiTrigger(channelId, row.Id));
            return Ok(result);
        }

        /// <summary>删除消息：自己的消息、自己 AI 的消息，或管理员。</summary>
        [Authorize]
        [HttpDelete("messages/{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var row = await store.MessageAsync(id);
            if (row == null || row.Deleted) return NotFound();
            if (row.UserId != userId && !await IsModeratorAsync(userId)) return Forbid();
            await store.DeleteMessageAsync(id);
            await hub.Clients.Group(ChatHub.Group(row.ChannelId)).SendAsync("MessageDeleted", new { id, channelId = row.ChannelId });
            await gateway.DispatchDeletedAsync(row.ChannelId, id);
            return NoContent();
        }

        // ===== AI 人格 =====

        /// <summary>已启用的 AI 人格（用于 @ 提示和侧栏展示）</summary>
        [HttpGet("personas")]
        public async Task<IActionResult> Personas([FromQuery] string? channel)
        {
            var list = (await store.PersonasAsync(enabledOnly: true)).Where(p => channel == null || p.AllowsChannel(channel)).ToList();
            var bots = (await store.BotsAsync()).Where(b => b.Enabled && (channel == null || b.AllowsChannel(channel))).ToList();
            var ids = list.Select(p => p.UserId).Concat(bots.Select(b => b.OwnerId)).Distinct().ToList();
            var owners = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).Select(u => new { u.Id, u.Username }).ToDictionaryAsync(u => u.Id, u => u.Username);
            var personaCards = list.Select(p => new
            {
                kind = "persona",
                key = "persona:" + p.UserId,
                ownerId = p.UserId,
                ownerName = owners.GetValueOrDefault(p.UserId, "（已注销）"),
                p.Name,
                p.Avatar,
                p.ReplyMode,
                p.ReplyToAi,
                online = true,
                intro = p.Prompt.Length > 80 ? p.Prompt[..80] + "…" : p.Prompt,
            });
            var botCards = bots.Select(b => new
            {
                kind = "bot",
                key = "bot:" + b.Id,
                ownerId = b.OwnerId,
                ownerName = owners.GetValueOrDefault(b.OwnerId, "（已注销）"),
                b.Name,
                b.Avatar,
                ReplyMode = "bot",
                ReplyToAi = true,
                online = gateway.IsOnline(b.Id),
                intro = b.Description,
            });
            // 在线的排前面
            return Ok(personaCards.Concat(botCards).OrderByDescending(x => x.online).ThenBy(x => x.Name));
        }

        [Authorize]
        [HttpGet("persona/me")]
        public async Task<IActionResult> MyPersona()
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var persona = await store.PersonaAsync(userId);
            var cfg = await db.UserAiConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId);
            var secret = config["Ai:EncryptionKey"] ?? config["Jwt:Key"] ?? "TaichuAiDefaultSecret";
            var hasKey = cfg != null && !string.IsNullOrEmpty(AiKeyCrypto.Decrypt(cfg.EncryptedApiKey, secret));
            return Ok(new
            {
                aiConfig = new { configured = hasKey, model = cfg?.Model, baseUrl = cfg?.BaseUrl },
                usedToday = persona == null ? 0 : await store.AiCountTodayAsync(userId),
                persona = persona == null ? null : new
                {
                    persona.Enabled,
                    persona.Name,
                    persona.Avatar,
                    persona.Prompt,
                    persona.ReplyMode,
                    persona.AutoChance,
                    persona.CooldownSeconds,
                    persona.ReplyToAi,
                    persona.DailyLimit,
                    channels = persona.Channels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
                    persona.LastError,
                    persona.LastErrorAt,
                },
            });
        }

        [Authorize]
        [HttpPut("persona/me")]
        public async Task<IActionResult> SavePersona([FromBody] SavePersonaDto dto)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var name = (dto.Name ?? string.Empty).Trim().TrimStart('@');
            if (name.Length is < 1 or > 20) return BadRequest(new { message = "AI 名字需要 1–20 个字" });
            if (name.Any(ch => char.IsWhiteSpace(ch) || ch == '@')) return BadRequest(new { message = "AI 名字里不能有空格或 @（方便大家 @ 它）" });
            if ((dto.Prompt ?? string.Empty).Length > 4000) return BadRequest(new { message = "人设描述最多 4000 字" });
            var others = await store.PersonasAsync(enabledOnly: false);
            if (others.Any(p => p.UserId != userId && string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                || (await store.BotsAsync()).Any(b => string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase)))
                return BadRequest(new { message = "这个名字已经被别的 AI 用了，换一个吧" });

            if (dto.Enabled)
            {
                var cfg = await db.UserAiConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.UserId == userId);
                if (cfg == null || string.IsNullOrEmpty(cfg.EncryptedApiKey))
                    return BadRequest(new { message = "请先在个人中心配置你自己的 AI 接口和 Key，再启用 AI 人格" });
            }

            var validChannels = (await store.ChannelsAsync()).Select(c => c.Id).ToHashSet();
            await store.SavePersonaAsync(new ChatPersona
            {
                UserId = userId,
                Enabled = dto.Enabled,
                Name = name,
                Avatar = string.IsNullOrWhiteSpace(dto.Avatar) ? null : dto.Avatar.Trim(),
                Prompt = (dto.Prompt ?? string.Empty).Trim(),
                ReplyMode = dto.ReplyMode == "active" ? "active" : "mention",
                AutoChance = Math.Clamp(dto.AutoChance, 1, 100),
                CooldownSeconds = Math.Clamp(dto.CooldownSeconds, 10, 3600),
                ReplyToAi = dto.ReplyToAi,
                DailyLimit = Math.Clamp(dto.DailyLimit, 1, 500),
                Channels = string.Join(",", (dto.Channels ?? new()).Where(validChannels.Contains).Distinct()),
            });
            return Ok(new { message = dto.Enabled ? "AI 人格已启用" : "已保存（未启用）" });
        }

        /// <summary>主人让自己的 AI 在某个频道说一句</summary>
        [Authorize]
        [HttpPost("persona/me/speak")]
        public async Task<IActionResult> Speak([FromBody] SpeakDto dto)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var persona = await store.PersonaAsync(userId);
            if (persona == null || !persona.Enabled) return BadRequest(new { message = "请先设置并启用你的 AI 人格" });
            var channel = await store.ChannelAsync(dto.ChannelId);
            if (channel == null) return NotFound(new { message = "频道不存在" });
            if (!channel.AllowAi) return BadRequest(new { message = "这个频道不允许 AI 发言" });
            if (!persona.AllowsChannel(channel.Id)) return BadRequest(new { message = "你的 AI 人格没有开放这个频道" });
            if (Throttled(userId)) return StatusCode(429, new { message = "点得太快了，稍等一下" });
            var last = (await store.MessagesAsync(channel.Id, null, 1)).LastOrDefault();
            aiQueue.Enqueue(new ChatAiTrigger(channel.Id, last?.Id ?? 0, userId));
            return Accepted(new { message = $"「{persona.Name}」正在想说什么…" });
        }

        // ===== 外部机器人（本地程序通过 WebSocket 主动连入） =====

        private object BotView(ChatBot b) => new
        {
            b.Id, b.Name, b.Avatar, b.Description, b.Enabled, b.CreatedAt, b.LastSeenAt,
            channels = b.Channels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            online = gateway.IsOnline(b.Id),
        };

        private async Task<string?> ValidateBotAsync(SaveBotDto dto, string? selfId)
        {
            var name = (dto.Name ?? string.Empty).Trim().TrimStart('@');
            if (name.Length is < 1 or > 20) return "机器人名字需要 1–20 个字";
            if (name.Any(ch => char.IsWhiteSpace(ch) || ch == '@')) return "名字里不能有空格或 @（方便大家 @ 它）";
            if ((dto.Description ?? string.Empty).Length > 500) return "简介最多 500 字";
            var taken = (await store.PersonasAsync(enabledOnly: false)).Any(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase))
                || (await store.BotsAsync()).Any(b => b.Id != selfId && string.Equals(b.Name, name, StringComparison.OrdinalIgnoreCase));
            return taken ? "这个名字已经被别的 AI 用了，换一个吧" : null;
        }

        private async Task<string> CleanChannelsAsync(IEnumerable<string>? channels)
        {
            var valid = (await store.ChannelsAsync()).Select(c => c.Id).ToHashSet();
            return string.Join(",", (channels ?? Array.Empty<string>()).Where(valid.Contains).Distinct());
        }

        [Authorize]
        [HttpGet("bots/mine")]
        public async Task<IActionResult> MyBots()
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var bots = await store.BotsAsync(userId);
            return Ok(new { endpoint = BotGateway.Path, protocol = BotGateway.ProtocolVersion, maxBots = MaxBotsPerUser, bots = bots.Select(BotView) });
        }

        /// <summary>创建机器人：返回的 token 只显示这一次</summary>
        [Authorize]
        [HttpPost("bots")]
        public async Task<IActionResult> CreateBot([FromBody] SaveBotDto dto)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            if ((await store.BotsAsync(userId)).Count >= MaxBotsPerUser) return BadRequest(new { message = $"每人最多创建 {MaxBotsPerUser} 个机器人" });
            if (await ValidateBotAsync(dto, null) is string error) return BadRequest(new { message = error });
            var id = BotGateway.NewBotId();
            var token = BotGateway.NewToken(id);
            var bot = new ChatBot
            {
                Id = id,
                OwnerId = userId,
                Name = dto.Name.Trim().TrimStart('@'),
                Avatar = string.IsNullOrWhiteSpace(dto.Avatar) ? null : dto.Avatar.Trim(),
                Description = (dto.Description ?? string.Empty).Trim(),
                TokenHash = BotGateway.HashToken(token),
                Enabled = dto.Enabled,
                Channels = await CleanChannelsAsync(dto.Channels),
            };
            await store.SaveBotAsync(bot);
            return Ok(new { bot = BotView(bot), token });
        }

        [Authorize]
        [HttpPut("bots/{id}")]
        public async Task<IActionResult> UpdateBot(string id, [FromBody] SaveBotDto dto)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var bot = await store.BotAsync(id);
            if (bot == null || bot.OwnerId != userId) return NotFound();
            if (await ValidateBotAsync(dto, id) is string error) return BadRequest(new { message = error });
            bot.Name = dto.Name.Trim().TrimStart('@');
            bot.Avatar = string.IsNullOrWhiteSpace(dto.Avatar) ? null : dto.Avatar.Trim();
            bot.Description = (dto.Description ?? string.Empty).Trim();
            bot.Enabled = dto.Enabled;
            bot.Channels = await CleanChannelsAsync(dto.Channels);
            await store.SaveBotAsync(bot);
            await gateway.RefreshBotAsync(id, disconnect: !bot.Enabled);
            return Ok(BotView(bot));
        }

        /// <summary>重新生成令牌：旧令牌立即失效，在线连接会被断开</summary>
        [Authorize]
        [HttpPost("bots/{id}/token")]
        public async Task<IActionResult> ResetBotToken(string id)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var bot = await store.BotAsync(id);
            if (bot == null || bot.OwnerId != userId) return NotFound();
            var token = BotGateway.NewToken(id);
            bot.TokenHash = BotGateway.HashToken(token);
            await store.SaveBotAsync(bot);
            await gateway.RefreshBotAsync(id, disconnect: true);
            return Ok(new { token });
        }

        [Authorize]
        [HttpDelete("bots/{id}")]
        public async Task<IActionResult> DeleteBot(string id)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var bot = await store.BotAsync(id);
            if (bot == null || bot.OwnerId != userId) return NotFound();
            await store.DeleteBotAsync(id);
            await gateway.RefreshBotAsync(id, disconnect: true);
            return NoContent();
        }

        /// <summary>
        /// 研究数据导出（JSONL）：研究频道的全部对话 + 其他频道里与这个机器人直接相关的消息。
        /// 真人成员一律匿名化为 human_xxxx（同一次导出内保持一致），不导出用户名和账号。
        /// </summary>
        [Authorize]
        [HttpGet("bots/{id}/export")]
        public async Task<IActionResult> ExportBot(string id, [FromQuery] int days = 30)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var bot = await store.BotAsync(id);
            if (bot == null || bot.OwnerId != userId) return NotFound();
            days = Math.Clamp(days, 1, 365);
            var channels = (await store.ChannelsAsync()).Where(c => c.AllowAi && bot.AllowsChannel(c.Id)).ToList();
            var research = channels.Where(c => c.Research).Select(c => c.Id).ToHashSet();
            var rows = await store.MessagesInRangeAsync(channels.Select(c => c.Id).ToList(), DateTime.UtcNow.AddDays(-days), DateTime.UtcNow.AddMinutes(1), 50000);
            var byId = rows.ToDictionary(r => r.Id);
            bool Related(ChatMessageRow r) =>
                r.BotId == bot.Id
                || r.Content.Contains("@" + bot.Name, StringComparison.OrdinalIgnoreCase)
                || (r.ReplyToId is long rid && byId.TryGetValue(rid, out var t) && t.BotId == bot.Id);
            var selected = rows.Where(r => research.Contains(r.ChannelId) || Related(r)).ToList();

            string Pseudonym(Guid uid) => "human_" + BotGateway.HashToken(bot.Id + ":" + uid)[..8];
            var sb = new System.Text.StringBuilder();
            var json = new System.Text.Json.JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            foreach (var r in selected)
            {
                var kind = !r.IsAi ? "human" : r.BotId != null ? "bot" : "persona";
                sb.AppendLine(System.Text.Json.JsonSerializer.Serialize(new
                {
                    id = r.Id,
                    channel = r.ChannelId,
                    research = research.Contains(r.ChannelId),
                    time = r.CreatedAt.ToString("o"),
                    sender_kind = kind,
                    sender = kind == "human" ? Pseudonym(r.UserId) : kind == "bot" ? "bot:" + r.BotId : "persona:" + r.AiName,
                    sender_name = kind == "human" ? null : r.AiName,
                    is_self = r.BotId == bot.Id,
                    reply_to = r.ReplyToId,
                    content = r.Content,
                }, json));
            }
            var bytes = System.Text.Encoding.UTF8.GetBytes(sb.ToString());
            return File(bytes, "application/x-ndjson; charset=utf-8", $"taichu-chat-{bot.Name}-{DateTime.UtcNow:yyyyMMdd}.jsonl");
        }
    }
}
