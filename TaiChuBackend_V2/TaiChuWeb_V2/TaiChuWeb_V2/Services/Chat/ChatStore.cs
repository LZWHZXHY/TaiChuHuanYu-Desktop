using MySqlConnector;

namespace TaiChuWeb_V2.Services.Chat
{
    public sealed class ChatChannel
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Icon { get; set; } = "#";
        public int SortOrder { get; set; }
        public bool AllowAi { get; set; } = true;
        /// <summary>all：真人和 AI 都能发言；ai-only：只有 AI 能发言，真人只能观看</summary>
        public string PostPolicy { get; set; } = "all";
        /// <summary>研究频道：进入时提示「对话可能用于 AI 研究」，接入的机器人能收到全部消息</summary>
        public bool Research { get; set; }
        public bool AiOnly => PostPolicy == "ai-only";
    }

    public sealed class ChatMessageRow
    {
        public long Id { get; set; }
        public string ChannelId { get; set; } = string.Empty;
        /// <summary>发送者；AI 消息为这个 AI 人格的主人</summary>
        public Guid UserId { get; set; }
        public bool IsAi { get; set; }
        public string? AiName { get; set; }
        public string? AiAvatar { get; set; }
        public string Content { get; set; } = string.Empty;
        public long? ReplyToId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool Deleted { get; set; }
        /// <summary>由外部机器人（WebSocket 接入）发出时为机器人编号；API 人格发的为空</summary>
        public string? BotId { get; set; }
    }

    /// <summary>用户接入的外部机器人（本地程序通过 WebSocket 主动连入）。</summary>
    public sealed class ChatBot
    {
        public string Id { get; set; } = string.Empty;
        public Guid OwnerId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        public string Description { get; set; } = string.Empty;
        public string TokenHash { get; set; } = string.Empty;
        public bool Enabled { get; set; } = true;
        /// <summary>允许发言的频道，逗号分隔；空 = 所有允许 AI 的频道</summary>
        public string Channels { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? LastSeenAt { get; set; }

        public bool AllowsChannel(string channelId) =>
            string.IsNullOrWhiteSpace(Channels) || Channels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(channelId);
    }

    /// <summary>用户的 AI 人格设定（每人一个，使用用户自己在个人中心配置的 AI 接口）。</summary>
    public sealed class ChatPersona
    {
        public Guid UserId { get; set; }
        public bool Enabled { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Avatar { get; set; }
        /// <summary>人设提示词：性格、说话方式、背景</summary>
        public string Prompt { get; set; } = string.Empty;
        /// <summary>mention：只在被 @ 或被回复时发言；active：还会按概率主动接话</summary>
        public string ReplyMode { get; set; } = "mention";
        /// <summary>主动接话的概率（%）</summary>
        public int AutoChance { get; set; } = 20;
        /// <summary>同一频道两次主动发言的最短间隔（秒）</summary>
        public int CooldownSeconds { get; set; } = 90;
        /// <summary>是否回应其他 AI 的消息（AI 互聊）</summary>
        public bool ReplyToAi { get; set; }
        /// <summary>每天最多发言次数，保护主人的 API 额度</summary>
        public int DailyLimit { get; set; } = 50;
        /// <summary>允许发言的频道，逗号分隔；空 = 所有允许 AI 的频道</summary>
        public string Channels { get; set; } = string.Empty;
        public string? LastError { get; set; }
        public DateTime? LastErrorAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public bool AllowsChannel(string channelId) =>
            string.IsNullOrWhiteSpace(Channels) || Channels.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Contains(channelId);
    }

    /// <summary>
    /// 聊天室数据存取。三张表在第一次使用时自动创建（不需要 EF 迁移），只按自身列查询，不与其他表做字符串关联。
    /// </summary>
    public sealed class ChatStore(IConfiguration configuration)
    {
        private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);
        private volatile bool _ready;

        private async Task<MySqlConnection> OpenAsync(CancellationToken ct = default)
        {
            if (!_ready) await EnsureSchemaAsync(ct);
            var c = new MySqlConnection(_connectionString);
            await c.OpenAsync(ct);
            return c;
        }

        private async Task EnsureSchemaAsync(CancellationToken ct)
        {
            await _schemaLock.WaitAsync(ct);
            try
            {
                if (_ready) return;
                await using var c = new MySqlConnection(_connectionString);
                await c.OpenAsync(ct);
                await using var cmd = c.CreateCommand();
                cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS chat_channels (
  Id VARCHAR(32) NOT NULL PRIMARY KEY,
  Name VARCHAR(40) NOT NULL,
  Description VARCHAR(200) NOT NULL DEFAULT '',
  Icon VARCHAR(16) NOT NULL DEFAULT '#',
  SortOrder INT NOT NULL DEFAULT 0,
  AllowAi TINYINT(1) NOT NULL DEFAULT 1,
  CreatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS chat_messages (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  ChannelId VARCHAR(32) NOT NULL,
  UserId CHAR(36) NOT NULL,
  IsAi TINYINT(1) NOT NULL DEFAULT 0,
  AiName VARCHAR(30) NULL,
  AiAvatar VARCHAR(500) NULL,
  Content TEXT NOT NULL,
  ReplyToId BIGINT NULL,
  CreatedAt DATETIME(3) NOT NULL,
  Deleted TINYINT(1) NOT NULL DEFAULT 0,
  KEY IX_chat_messages_channel (ChannelId, Id),
  KEY IX_chat_messages_user (UserId, CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS chat_personas (
  UserId CHAR(36) NOT NULL PRIMARY KEY,
  Enabled TINYINT(1) NOT NULL DEFAULT 0,
  Name VARCHAR(30) NOT NULL,
  Avatar VARCHAR(500) NULL,
  Prompt TEXT NOT NULL,
  ReplyMode VARCHAR(16) NOT NULL DEFAULT 'mention',
  AutoChance INT NOT NULL DEFAULT 20,
  CooldownSeconds INT NOT NULL DEFAULT 90,
  ReplyToAi TINYINT(1) NOT NULL DEFAULT 0,
  DailyLimit INT NOT NULL DEFAULT 50,
  Channels VARCHAR(500) NOT NULL DEFAULT '',
  LastError VARCHAR(300) NULL,
  LastErrorAt DATETIME NULL,
  UpdatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
INSERT IGNORE INTO chat_channels (Id, Name, Description, Icon, SortOrder, AllowAi, CreatedAt) VALUES
  ('lobby', '大厅', '社区公共大厅，什么都可以聊', '💬', 0, 1, UTC_TIMESTAMP()),
  ('casual', '闲聊', '摸鱼、日常、吐槽', '☕', 10, 1, UTC_TIMESTAMP()),
  ('ai-theater', 'AI 剧场', '让大家的 AI 人格在这里互相对话', '🤖', 20, 1, UTC_TIMESTAMP());";
                await cmd.ExecuteNonQueryAsync(ct);

                // 后加的列：老库里表已存在，CREATE TABLE IF NOT EXISTS 不会补列，这里逐个检查后补上
                await AddColumnIfMissingAsync(c, "chat_channels", "PostPolicy", "VARCHAR(16) NOT NULL DEFAULT 'all'", ct);
                await AddColumnIfMissingAsync(c, "chat_channels", "Research", "TINYINT(1) NOT NULL DEFAULT 0", ct);
                await AddColumnIfMissingAsync(c, "chat_messages", "BotId", "VARCHAR(32) NULL", ct);

                await using var more = c.CreateCommand();
                more.CommandText = @"
CREATE TABLE IF NOT EXISTS chat_bots (
  Id VARCHAR(32) NOT NULL PRIMARY KEY,
  OwnerId CHAR(36) NOT NULL,
  Name VARCHAR(30) NOT NULL,
  Avatar VARCHAR(500) NULL,
  Description VARCHAR(500) NOT NULL DEFAULT '',
  TokenHash CHAR(64) NOT NULL,
  Enabled TINYINT(1) NOT NULL DEFAULT 1,
  Channels VARCHAR(500) NOT NULL DEFAULT '',
  CreatedAt DATETIME NOT NULL,
  LastSeenAt DATETIME NULL,
  KEY IX_chat_bots_owner (OwnerId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
INSERT IGNORE INTO chat_channels (Id, Name, Description, Icon, SortOrder, AllowAi, PostPolicy, Research, CreatedAt) VALUES
  ('ai-lab', 'AI 观察室', '只有 AI 可以发言，大家可以围观。这里的对话可能用于 AI 研究。', '🔬', 30, 1, 'ai-only', 1, UTC_TIMESTAMP()),
  ('ai-salon', '人机共谈', 'AI 和成员一起聊。这里的对话可能用于 AI 研究。', '🧪', 40, 1, 'all', 1, UTC_TIMESTAMP());";
                await more.ExecuteNonQueryAsync(ct);
                _ready = true;
            }
            finally { _schemaLock.Release(); }
        }

        private static async Task AddColumnIfMissingAsync(MySqlConnection c, string table, string column, string definition, CancellationToken ct)
        {
            await using var check = c.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t AND COLUMN_NAME = @c";
            check.Parameters.AddWithValue("@t", table);
            check.Parameters.AddWithValue("@c", column);
            if (Convert.ToInt32(await check.ExecuteScalarAsync(ct)) > 0) return;
            await using var alter = c.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
            await alter.ExecuteNonQueryAsync(ct);
        }

        // ---------------- 频道 ----------------

        public async Task<List<ChatChannel>> ChannelsAsync(CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Description, Icon, SortOrder, AllowAi, PostPolicy, Research FROM chat_channels ORDER BY SortOrder, CreatedAt";
            var list = new List<ChatChannel>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add(new ChatChannel { Id = r.GetString(0), Name = r.GetString(1), Description = r.GetString(2), Icon = r.GetString(3), SortOrder = r.GetInt32(4), AllowAi = r.GetBoolean(5), PostPolicy = r.GetString(6), Research = r.GetBoolean(7) });
            return list;
        }

        public async Task<ChatChannel?> ChannelAsync(string id, CancellationToken ct = default)
            => (await ChannelsAsync(ct)).FirstOrDefault(x => x.Id == id);

        public async Task SaveChannelAsync(ChatChannel ch, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"INSERT INTO chat_channels (Id, Name, Description, Icon, SortOrder, AllowAi, PostPolicy, Research, CreatedAt) VALUES (@id,@n,@d,@i,@s,@a,@pp,@rs,UTC_TIMESTAMP())
ON DUPLICATE KEY UPDATE Name=VALUES(Name), Description=VALUES(Description), Icon=VALUES(Icon), SortOrder=VALUES(SortOrder), AllowAi=VALUES(AllowAi), PostPolicy=VALUES(PostPolicy), Research=VALUES(Research)";
            cmd.Parameters.AddWithValue("@pp", ch.PostPolicy == "ai-only" ? "ai-only" : "all");
            cmd.Parameters.AddWithValue("@rs", ch.Research);
            cmd.Parameters.AddWithValue("@id", ch.Id);
            cmd.Parameters.AddWithValue("@n", ch.Name);
            cmd.Parameters.AddWithValue("@d", ch.Description);
            cmd.Parameters.AddWithValue("@i", ch.Icon);
            cmd.Parameters.AddWithValue("@s", ch.SortOrder);
            cmd.Parameters.AddWithValue("@a", ch.AllowAi);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task DeleteChannelAsync(string id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            // 消息保留在库里（标记删除），频道删除后不再显示
            cmd.CommandText = "DELETE FROM chat_channels WHERE Id=@id; UPDATE chat_messages SET Deleted=1 WHERE ChannelId=@id;";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // ---------------- 消息 ----------------

        /// <summary>
        /// MySqlConnector 默认把 CHAR(36) 列直接读成 Guid（不是字符串），用 GetString 会抛异常；
        /// 这里两种情况都兼容。
        /// </summary>
        internal static Guid ReadGuid(MySqlDataReader r, int i)
        {
            var v = r.GetValue(i);
            return v is Guid g ? g : Guid.TryParse(Convert.ToString(v), out var parsed) ? parsed : Guid.Empty;
        }

        private const string MessageColumns = "Id, ChannelId, UserId, IsAi, AiName, AiAvatar, Content, ReplyToId, CreatedAt, Deleted, BotId";

        private static ChatMessageRow ReadMessage(MySqlDataReader r) => new()
        {
            Id = r.GetInt64(0),
            ChannelId = r.GetString(1),
            UserId = ReadGuid(r, 2),
            IsAi = r.GetBoolean(3),
            AiName = r.IsDBNull(4) ? null : r.GetString(4),
            AiAvatar = r.IsDBNull(5) ? null : r.GetString(5),
            Content = r.GetString(6),
            ReplyToId = r.IsDBNull(7) ? null : r.GetInt64(7),
            CreatedAt = DateTime.SpecifyKind(r.GetDateTime(8), DateTimeKind.Utc),
            Deleted = r.GetBoolean(9),
            BotId = r.IsDBNull(10) ? null : r.GetString(10),
        };

        /// <summary>某频道最近的消息（按时间正序返回）；before 用于向上翻历史。</summary>
        public async Task<List<ChatMessageRow>> MessagesAsync(string channelId, long? before, int limit, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {MessageColumns} FROM chat_messages WHERE ChannelId=@c AND Deleted=0 {(before.HasValue ? "AND Id < @b" : "")} ORDER BY Id DESC LIMIT @l";
            cmd.Parameters.AddWithValue("@c", channelId);
            if (before.HasValue) cmd.Parameters.AddWithValue("@b", before.Value);
            cmd.Parameters.AddWithValue("@l", Math.Clamp(limit, 1, 100));
            var list = new List<ChatMessageRow>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(ReadMessage(r));
            list.Reverse();
            return list;
        }

        public async Task<List<ChatMessageRow>> MessagesByIdsAsync(IEnumerable<long> ids, CancellationToken ct = default)
        {
            var list = ids.Distinct().ToList();
            if (list.Count == 0) return new();
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {MessageColumns} FROM chat_messages WHERE Id IN ({string.Join(",", list)})";
            var result = new List<ChatMessageRow>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) result.Add(ReadMessage(r));
            return result;
        }

        public async Task<ChatMessageRow?> MessageAsync(long id, CancellationToken ct = default)
            => (await MessagesByIdsAsync(new[] { id }, ct)).FirstOrDefault();

        public async Task<ChatMessageRow> InsertMessageAsync(ChatMessageRow m, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"INSERT INTO chat_messages (ChannelId, UserId, IsAi, AiName, AiAvatar, Content, ReplyToId, CreatedAt, BotId) VALUES (@c,@u,@ai,@an,@av,@t,@r,@at,@bot);
SELECT LAST_INSERT_ID();";
            m.CreatedAt = DateTime.UtcNow;
            cmd.Parameters.AddWithValue("@c", m.ChannelId);
            cmd.Parameters.AddWithValue("@u", m.UserId.ToString());
            cmd.Parameters.AddWithValue("@ai", m.IsAi);
            cmd.Parameters.AddWithValue("@an", (object?)m.AiName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@av", (object?)m.AiAvatar ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@t", m.Content);
            cmd.Parameters.AddWithValue("@r", (object?)m.ReplyToId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@at", m.CreatedAt);
            cmd.Parameters.AddWithValue("@bot", (object?)m.BotId ?? DBNull.Value);
            m.Id = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
            return m;
        }

        public async Task DeleteMessageAsync(long id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE chat_messages SET Deleted=1 WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <summary>某人的 AI 今天（北京时间）已发言次数。</summary>
        public async Task<int> AiCountTodayAsync(Guid ownerId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            // 只算 API 人格（BotId 为空）；外部机器人有自己的限额
            cmd.CommandText = "SELECT COUNT(*) FROM chat_messages WHERE UserId=@u AND IsAi=1 AND BotId IS NULL AND CreatedAt >= @since";
            cmd.Parameters.AddWithValue("@u", ownerId.ToString());
            cmd.Parameters.AddWithValue("@since", Analytics.ChinaClock.DayStartUtc(Analytics.ChinaClock.Today));
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        /// <summary>某人的 AI 在某频道最后一次发言时间（用于冷却）。</summary>
        public async Task<DateTime?> LastAiMessageAtAsync(Guid ownerId, string channelId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT MAX(CreatedAt) FROM chat_messages WHERE UserId=@u AND IsAi=1 AND BotId IS NULL AND ChannelId=@c";
            cmd.Parameters.AddWithValue("@u", ownerId.ToString());
            cmd.Parameters.AddWithValue("@c", channelId);
            var v = await cmd.ExecuteScalarAsync(ct);
            return v is DateTime d ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : null;
        }

        /// <summary>某段时间内若干频道的消息（研究导出用），按时间正序。</summary>
        public async Task<List<ChatMessageRow>> MessagesInRangeAsync(IReadOnlyCollection<string> channelIds, DateTime fromUtc, DateTime toUtc, int max, CancellationToken ct = default)
        {
            if (channelIds.Count == 0) return new();
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            var names = channelIds.Select((id, i) => { cmd.Parameters.AddWithValue($"@ch{i}", id); return $"@ch{i}"; }).ToList();
            cmd.CommandText = $"SELECT {MessageColumns} FROM chat_messages WHERE Deleted=0 AND ChannelId IN ({string.Join(",", names)}) AND CreatedAt >= @a AND CreatedAt < @b ORDER BY Id LIMIT @l";
            cmd.Parameters.AddWithValue("@a", fromUtc);
            cmd.Parameters.AddWithValue("@b", toUtc);
            cmd.Parameters.AddWithValue("@l", max);
            var list = new List<ChatMessageRow>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(ReadMessage(r));
            return list;
        }

        /// <summary>某个机器人今天（北京时间）已发言次数。</summary>
        public async Task<int> BotCountTodayAsync(string botId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM chat_messages WHERE BotId=@b AND CreatedAt >= @since";
            cmd.Parameters.AddWithValue("@b", botId);
            cmd.Parameters.AddWithValue("@since", Analytics.ChinaClock.DayStartUtc(Analytics.ChinaClock.Today));
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        // ---------------- 外部机器人 ----------------

        private const string BotColumns = "Id, OwnerId, Name, Avatar, Description, TokenHash, Enabled, Channels, CreatedAt, LastSeenAt";

        private static ChatBot ReadBot(MySqlDataReader r) => new()
        {
            Id = r.GetString(0),
            OwnerId = ReadGuid(r, 1),
            Name = r.GetString(2),
            Avatar = r.IsDBNull(3) ? null : r.GetString(3),
            Description = r.GetString(4),
            TokenHash = r.GetString(5),
            Enabled = r.GetBoolean(6),
            Channels = r.GetString(7),
            CreatedAt = DateTime.SpecifyKind(r.GetDateTime(8), DateTimeKind.Utc),
            LastSeenAt = r.IsDBNull(9) ? null : DateTime.SpecifyKind(r.GetDateTime(9), DateTimeKind.Utc),
        };

        public async Task<List<ChatBot>> BotsAsync(Guid? ownerId = null, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {BotColumns} FROM chat_bots {(ownerId.HasValue ? "WHERE OwnerId=@o" : "")} ORDER BY CreatedAt";
            if (ownerId.HasValue) cmd.Parameters.AddWithValue("@o", ownerId.Value.ToString());
            var list = new List<ChatBot>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(ReadBot(r));
            return list;
        }

        public async Task<ChatBot?> BotAsync(string id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {BotColumns} FROM chat_bots WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            return await r.ReadAsync(ct) ? ReadBot(r) : null;
        }

        public async Task SaveBotAsync(ChatBot b, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $@"INSERT INTO chat_bots ({BotColumns}) VALUES (@id,@o,@n,@a,@d,@t,@e,@ch,UTC_TIMESTAMP(),NULL)
ON DUPLICATE KEY UPDATE Name=VALUES(Name), Avatar=VALUES(Avatar), Description=VALUES(Description), TokenHash=VALUES(TokenHash), Enabled=VALUES(Enabled), Channels=VALUES(Channels)";
            cmd.Parameters.AddWithValue("@id", b.Id);
            cmd.Parameters.AddWithValue("@o", b.OwnerId.ToString());
            cmd.Parameters.AddWithValue("@n", b.Name);
            cmd.Parameters.AddWithValue("@a", (object?)b.Avatar ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@d", b.Description);
            cmd.Parameters.AddWithValue("@t", b.TokenHash);
            cmd.Parameters.AddWithValue("@e", b.Enabled);
            cmd.Parameters.AddWithValue("@ch", b.Channels);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task DeleteBotAsync(string id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM chat_bots WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task TouchBotAsync(string id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE chat_bots SET LastSeenAt=UTC_TIMESTAMP() WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // ---------------- AI 人格 ----------------

        private const string PersonaColumns = "UserId, Enabled, Name, Avatar, Prompt, ReplyMode, AutoChance, CooldownSeconds, ReplyToAi, DailyLimit, Channels, LastError, LastErrorAt, UpdatedAt";

        private static ChatPersona ReadPersona(MySqlDataReader r) => new()
        {
            UserId = ReadGuid(r, 0),
            Enabled = r.GetBoolean(1),
            Name = r.GetString(2),
            Avatar = r.IsDBNull(3) ? null : r.GetString(3),
            Prompt = r.GetString(4),
            ReplyMode = r.GetString(5),
            AutoChance = r.GetInt32(6),
            CooldownSeconds = r.GetInt32(7),
            ReplyToAi = r.GetBoolean(8),
            DailyLimit = r.GetInt32(9),
            Channels = r.GetString(10),
            LastError = r.IsDBNull(11) ? null : r.GetString(11),
            LastErrorAt = r.IsDBNull(12) ? null : DateTime.SpecifyKind(r.GetDateTime(12), DateTimeKind.Utc),
            UpdatedAt = DateTime.SpecifyKind(r.GetDateTime(13), DateTimeKind.Utc),
        };

        public async Task<List<ChatPersona>> PersonasAsync(bool enabledOnly, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {PersonaColumns} FROM chat_personas {(enabledOnly ? "WHERE Enabled=1" : "")} ORDER BY Name";
            var list = new List<ChatPersona>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(ReadPersona(r));
            return list;
        }

        public async Task<ChatPersona?> PersonaAsync(Guid userId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {PersonaColumns} FROM chat_personas WHERE UserId=@u";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            await using var r = await cmd.ExecuteReaderAsync(ct);
            return await r.ReadAsync(ct) ? ReadPersona(r) : null;
        }

        public async Task SavePersonaAsync(ChatPersona p, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $@"INSERT INTO chat_personas ({PersonaColumns}) VALUES (@u,@e,@n,@a,@p,@m,@ch,@cd,@ra,@dl,@cs,NULL,NULL,UTC_TIMESTAMP())
ON DUPLICATE KEY UPDATE Enabled=VALUES(Enabled), Name=VALUES(Name), Avatar=VALUES(Avatar), Prompt=VALUES(Prompt), ReplyMode=VALUES(ReplyMode),
  AutoChance=VALUES(AutoChance), CooldownSeconds=VALUES(CooldownSeconds), ReplyToAi=VALUES(ReplyToAi), DailyLimit=VALUES(DailyLimit), Channels=VALUES(Channels), UpdatedAt=UTC_TIMESTAMP()";
            cmd.Parameters.AddWithValue("@u", p.UserId.ToString());
            cmd.Parameters.AddWithValue("@e", p.Enabled);
            cmd.Parameters.AddWithValue("@n", p.Name);
            cmd.Parameters.AddWithValue("@a", (object?)p.Avatar ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@p", p.Prompt);
            cmd.Parameters.AddWithValue("@m", p.ReplyMode);
            cmd.Parameters.AddWithValue("@ch", p.AutoChance);
            cmd.Parameters.AddWithValue("@cd", p.CooldownSeconds);
            cmd.Parameters.AddWithValue("@ra", p.ReplyToAi);
            cmd.Parameters.AddWithValue("@dl", p.DailyLimit);
            cmd.Parameters.AddWithValue("@cs", p.Channels);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public async Task SetPersonaErrorAsync(Guid userId, string? error, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE chat_personas SET LastError=@e, LastErrorAt=IF(@e IS NULL, NULL, UTC_TIMESTAMP()) WHERE UserId=@u";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            cmd.Parameters.AddWithValue("@e", error is null ? (object)DBNull.Value : (error.Length > 300 ? error[..300] : error));
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
