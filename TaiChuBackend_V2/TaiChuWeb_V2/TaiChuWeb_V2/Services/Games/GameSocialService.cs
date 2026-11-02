using System.Collections.Concurrent;
using MySqlConnector;

namespace TaiChuWeb_V2.Services.Games
{
    /// <summary>
    /// 太虚游的「点赞 / 砸番茄」和游戏留言板（按游戏 key 区分，现在只接了斗地主 ddz，别的游戏把 key 加进 Games 就能用）。
    /// · 牌桌上给某位玩家点赞 / 砸番茄：记进 game_player_reactions（谁→谁、哪种、哪个房间第几局），战绩页的「被赞 / 被砸」和两个小排行都从这里算；
    /// · 留言板：game_board_messages（留言 + 回复，删除是软删除）；
    /// · 给「这个游戏」点赞 / 砸番茄：game_board_reactions（可以多次，显示总数和最近谁赞了 / 砸了）。
    /// 点赞、砸番茄都要花土豆（花掉，不转给对方），扣土豆走 PotatoService.SpendAsync。
    /// 表由 GameSocialSchema 在启动时创建；万一启动时没建成，第一次用的时候这里还会再试一次。
    /// </summary>
    public sealed class GameSocialService(IConfiguration configuration)
    {
        // ===== 规则常量（想改价格、次数就改这里） =====

        /// <summary>点一次赞花多少土豆</summary>
        public const long LikeCost = 1;
        /// <summary>砸一个番茄花多少土豆</summary>
        public const long TomatoCost = 2;
        /// <summary>牌桌上每人每局最多点赞 + 砸番茄多少次（防刷）</summary>
        public const int MaxReactsPerRound = 20;
        /// <summary>牌桌上两次点赞 / 砸番茄之间至少隔几秒</summary>
        public const int ReactCooldownSeconds = 2;
        /// <summary>牌桌评论区：每人几秒才能发一条</summary>
        public const int ChatCooldownSeconds = 3;
        /// <summary>牌桌评论区：一条最多几个字</summary>
        public const int ChatMaxLength = 100;
        /// <summary>牌桌评论区：房间里只留最近多少条（只在内存里，不存数据库）</summary>
        public const int ChatKeep = 50;
        /// <summary>留言板：一条留言最多几个字</summary>
        public const int BoardMaxLength = 500;
        /// <summary>留言板：每人几秒才能发一条（防止刷屏）</summary>
        public const int BoardCooldownSeconds = 5;
        /// <summary>给游戏点赞 / 砸番茄：两次之间至少隔几秒（防止连点把土豆点光）</summary>
        public const int BoardReactCooldownSeconds = 1;

        public sealed record GameInfo(string Name, string Link);

        /// <summary>接入了留言板 / 点赞的游戏：key → 名字、通知里的链接。以后别的游戏在这里加一行就行</summary>
        public static readonly IReadOnlyDictionary<string, GameInfo> Games = new Dictionary<string, GameInfo>
        {
            ["ddz"] = new("斗地主", "/games?game=Doudizhu"),
        };

        public static bool ValidKind(string? kind) => kind is "like" or "tomato";
        public static long CostOf(string kind) => kind == "tomato" ? TomatoCost : LikeCost;
        public static string KindLabel(string kind) => kind == "tomato" ? "砸番茄" : "点赞";

        private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);
        private volatile bool _ready;

        // ===== 建表 =====

        private const string SchemaSql = @"
CREATE TABLE IF NOT EXISTS game_player_reactions (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  Game VARCHAR(20) NOT NULL,
  Kind VARCHAR(10) NOT NULL,
  FromUserId CHAR(36) NOT NULL,
  ToUserId CHAR(36) NULL,
  ToName VARCHAR(60) NOT NULL DEFAULT '',
  RoomId VARCHAR(32) NOT NULL DEFAULT '',
  Round INT NOT NULL DEFAULT 0,
  Cost INT NOT NULL DEFAULT 0,
  CreatedAt DATETIME NOT NULL,
  KEY IX_gpr_to (ToUserId, Kind, CreatedAt),
  KEY IX_gpr_from (FromUserId, CreatedAt),
  KEY IX_gpr_kind (Kind, CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS game_board_messages (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  GameKey VARCHAR(20) NOT NULL,
  UserId CHAR(36) NOT NULL,
  Content VARCHAR(500) NOT NULL DEFAULT '',
  ParentId BIGINT NULL,
  ReplyToUserId CHAR(36) NULL,
  Deleted TINYINT(1) NOT NULL DEFAULT 0,
  DeletedBy CHAR(36) NULL,
  DeletedAt DATETIME NULL,
  CreatedAt DATETIME NOT NULL,
  KEY IX_gbm_game (GameKey, ParentId, Id),
  KEY IX_gbm_parent (ParentId, Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS game_board_reactions (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  GameKey VARCHAR(20) NOT NULL,
  UserId CHAR(36) NOT NULL,
  Kind VARCHAR(10) NOT NULL,
  Cost INT NOT NULL DEFAULT 0,
  CreatedAt DATETIME NOT NULL,
  KEY IX_gbr_game (GameKey, Kind),
  KEY IX_gbr_recent (GameKey, Id),
  KEY IX_gbr_user (UserId, GameKey)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

        /// <summary>建表（启动时由 GameSocialSchema 调用；第一次用之前也会确认一次）</summary>
        public async Task EnsureSchemaAsync(CancellationToken ct = default)
        {
            if (_ready) return;
            await _schemaLock.WaitAsync(ct);
            try
            {
                if (_ready) return;
                await using var c = new MySqlConnection(_connectionString);
                await c.OpenAsync(ct);
                await using var cmd = c.CreateCommand();
                cmd.CommandText = SchemaSql;
                await cmd.ExecuteNonQueryAsync(ct);
                _ready = true;
            }
            finally { _schemaLock.Release(); }
        }

        private async Task<MySqlConnection> OpenAsync(CancellationToken ct = default)
        {
            if (!_ready) await EnsureSchemaAsync(ct);
            var c = new MySqlConnection(_connectionString);
            await c.OpenAsync(ct);
            return c;
        }

        private static DateTime Utc(MySqlDataReader r, int i) => DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc);

        // ===== 简单的「冷却」：同一个人同一件事隔几秒才能再做（只在内存里，重启就清空，够用了） =====

        private readonly ConcurrentDictionary<string, DateTime> _gates = new();

        /// <summary>能做就记下这次时间并返回 true；离上次太近返回 false</summary>
        public bool TryPass(string key, int seconds)
        {
            var now = DateTime.UtcNow;
            // 偶尔清理一下很久以前的记录，免得越攒越多
            if (_gates.Count > 20000)
                foreach (var kv in _gates)
                    if ((now - kv.Value).TotalMinutes > 10) _gates.TryRemove(kv.Key, out _);
            while (true)
            {
                if (_gates.TryGetValue(key, out var last))
                {
                    if ((now - last).TotalSeconds < seconds) return false;
                    if (_gates.TryUpdate(key, now, last)) return true;
                }
                else if (_gates.TryAdd(key, now)) return true;
            }
        }

        // ===================== 牌桌上：给玩家点赞 / 砸番茄 =====================

        /// <summary>记一次「谁给谁点赞 / 砸番茄」。toUserId 为空表示对方是电脑玩家（不进排行）</summary>
        public async Task RecordPlayerReactionAsync(string game, string kind, Guid fromUserId, Guid? toUserId, string toName, string roomId, int round, long cost, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"INSERT INTO game_player_reactions (Game, Kind, FromUserId, ToUserId, ToName, RoomId, Round, Cost, CreatedAt)
VALUES (@g, @k, @f, @t, @n, @r, @rd, @c, @at)";
            cmd.Parameters.AddWithValue("@g", game);
            cmd.Parameters.AddWithValue("@k", kind);
            cmd.Parameters.AddWithValue("@f", fromUserId.ToString());
            cmd.Parameters.AddWithValue("@t", toUserId.HasValue ? toUserId.Value.ToString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@n", toName.Length > 60 ? toName[..60] : toName);
            cmd.Parameters.AddWithValue("@r", roomId.Length > 32 ? roomId[..32] : roomId);
            cmd.Parameters.AddWithValue("@rd", round);
            cmd.Parameters.AddWithValue("@c", cost);
            cmd.Parameters.AddWithValue("@at", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public sealed record ReactionStats(int Likes, int Tomatoes, int Likes30, int Tomatoes30, int GivenLikes, int GivenTomatoes);

        /// <summary>某人累计被赞 / 被砸（全部 + 最近 30 天），以及送出去的赞和番茄</summary>
        public async Task<ReactionStats> StatsAsync(Guid userId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            var u = userId.ToString();
            // 分成「收到的」「送出的」两句查，各自走自己的索引
            cmd.CommandText = @"SELECT COALESCE(SUM(Kind='like'),0), COALESCE(SUM(Kind='tomato'),0),
  COALESCE(SUM(Kind='like' AND CreatedAt >= @s),0), COALESCE(SUM(Kind='tomato' AND CreatedAt >= @s),0)
FROM game_player_reactions WHERE ToUserId=@u;
SELECT COALESCE(SUM(Kind='like'),0), COALESCE(SUM(Kind='tomato'),0) FROM game_player_reactions WHERE FromUserId=@u;";
            cmd.Parameters.AddWithValue("@u", u);
            cmd.Parameters.AddWithValue("@s", DateTime.UtcNow.AddDays(-30));
            await using var r = await cmd.ExecuteReaderAsync(ct);
            int likes = 0, tomatoes = 0, likes30 = 0, tomatoes30 = 0, givenLikes = 0, givenTomatoes = 0;
            if (await r.ReadAsync(ct))
            {
                likes = Convert.ToInt32(r.GetValue(0)); tomatoes = Convert.ToInt32(r.GetValue(1));
                likes30 = Convert.ToInt32(r.GetValue(2)); tomatoes30 = Convert.ToInt32(r.GetValue(3));
            }
            if (await r.NextResultAsync(ct) && await r.ReadAsync(ct))
            {
                givenLikes = Convert.ToInt32(r.GetValue(0)); givenTomatoes = Convert.ToInt32(r.GetValue(1));
            }
            return new ReactionStats(likes, tomatoes, likes30, tomatoes30, givenLikes, givenTomatoes);
        }

        /// <summary>「最受欢迎」（被赞最多）/「番茄收割机」（被砸最多）。sinceUtc 为空表示全部时间</summary>
        public async Task<List<(Guid UserId, int Count)>> LeaderboardAsync(string kind, DateTime? sinceUtc, int limit, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $@"SELECT ToUserId, COUNT(*) AS Cnt FROM game_player_reactions
WHERE Kind=@k AND ToUserId IS NOT NULL {(sinceUtc.HasValue ? "AND CreatedAt >= @s" : "")}
GROUP BY ToUserId ORDER BY Cnt DESC, MAX(Id) DESC LIMIT @l";
            cmd.Parameters.AddWithValue("@k", kind);
            if (sinceUtc.HasValue) cmd.Parameters.AddWithValue("@s", sinceUtc.Value);
            cmd.Parameters.AddWithValue("@l", Math.Clamp(limit, 1, 50));
            var list = new List<(Guid, int)>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add((Chat.ChatStore.ReadGuid(r, 0), Convert.ToInt32(r.GetValue(1))));
            return list;
        }

        // ===================== 留言板 =====================

        public sealed record BoardMessage(long Id, string GameKey, Guid UserId, string Content, long? ParentId, Guid? ReplyToUserId, bool Deleted, DateTime CreatedAt);

        private const string MessageColumns = "Id, GameKey, UserId, Content, ParentId, ReplyToUserId, Deleted, CreatedAt";

        private static BoardMessage ReadMessage(MySqlDataReader r) => new(
            r.GetInt64(0), r.GetString(1), Chat.ChatStore.ReadGuid(r, 2), r.GetString(3),
            r.IsDBNull(4) ? null : r.GetInt64(4),
            r.IsDBNull(5) ? null : Chat.ChatStore.ReadGuid(r, 5),
            r.GetBoolean(6), Utc(r, 7));

        /// <summary>
        /// 一页留言（新的在前，每页 limit 条主留言），连同这些留言下面的全部回复（旧的在前）。
        /// beforeId：上一页最后一条主留言的 Id，用来「加载更早的」。
        /// 已删除、又没有回复的主留言直接不显示；有回复的显示成「这条留言已删除」，免得回复没头没尾。
        /// </summary>
        public async Task<(List<BoardMessage> Roots, List<BoardMessage> Replies, bool HasMore)> ListAsync(string game, long? beforeId, int limit, CancellationToken ct = default)
        {
            limit = Math.Clamp(limit, 1, 50);
            await using var c = await OpenAsync(ct);
            var roots = new List<BoardMessage>();
            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = $@"SELECT {MessageColumns} FROM game_board_messages m
WHERE m.GameKey=@g AND m.ParentId IS NULL {(beforeId.HasValue ? "AND m.Id < @b" : "")}
  AND (m.Deleted=0 OR EXISTS (SELECT 1 FROM game_board_messages x WHERE x.ParentId = m.Id AND x.Deleted=0))
ORDER BY m.Id DESC LIMIT @l";
                cmd.Parameters.AddWithValue("@g", game);
                if (beforeId.HasValue) cmd.Parameters.AddWithValue("@b", beforeId.Value);
                cmd.Parameters.AddWithValue("@l", limit + 1); // 多取一条，看看还有没有更早的
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) roots.Add(ReadMessage(r));
            }
            var hasMore = roots.Count > limit;
            if (hasMore) roots.RemoveAt(roots.Count - 1);
            var replies = new List<BoardMessage>();
            if (roots.Count > 0)
            {
                await using var cmd = c.CreateCommand();
                // Id 都是数字，直接拼进 SQL 没有注入风险
                cmd.CommandText = $"SELECT {MessageColumns} FROM game_board_messages WHERE ParentId IN ({string.Join(",", roots.Select(m => m.Id))}) AND Deleted=0 ORDER BY Id";
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) replies.Add(ReadMessage(r));
            }
            return (roots, replies, hasMore);
        }

        public async Task<BoardMessage?> GetMessageAsync(long id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {MessageColumns} FROM game_board_messages WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            return await r.ReadAsync(ct) ? ReadMessage(r) : null;
        }

        /// <summary>发一条留言或回复（回复一律挂在主留言下面，只有两层，ReplyToUserId 记「回复的是谁」）</summary>
        public async Task<BoardMessage> AddMessageAsync(string game, Guid userId, string content, long? parentId, Guid? replyToUserId, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"INSERT INTO game_board_messages (GameKey, UserId, Content, ParentId, ReplyToUserId, Deleted, CreatedAt)
VALUES (@g, @u, @c, @p, @ru, 0, @at); SELECT LAST_INSERT_ID();";
            cmd.Parameters.AddWithValue("@g", game);
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            cmd.Parameters.AddWithValue("@c", content);
            cmd.Parameters.AddWithValue("@p", parentId.HasValue ? parentId.Value : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@ru", replyToUserId.HasValue ? replyToUserId.Value.ToString() : (object)DBNull.Value);
            cmd.Parameters.AddWithValue("@at", now);
            var id = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
            return new BoardMessage(id, game, userId, content, parentId, replyToUserId, false, now);
        }

        /// <summary>删除（软删除：标记一下，内容留在库里方便管理员追查）</summary>
        public async Task DeleteMessageAsync(long id, Guid by, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE game_board_messages SET Deleted=1, DeletedBy=@by, DeletedAt=@at WHERE Id=@id";
            cmd.Parameters.AddWithValue("@by", by.ToString());
            cmd.Parameters.AddWithValue("@at", DateTime.UtcNow);
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // ===================== 给「这个游戏」点赞 / 砸番茄 =====================

        public async Task AddGameReactionAsync(string game, Guid userId, string kind, long cost, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "INSERT INTO game_board_reactions (GameKey, UserId, Kind, Cost, CreatedAt) VALUES (@g, @u, @k, @c, @at)";
            cmd.Parameters.AddWithValue("@g", game);
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            cmd.Parameters.AddWithValue("@k", kind);
            cmd.Parameters.AddWithValue("@c", cost);
            cmd.Parameters.AddWithValue("@at", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public sealed record GameReactionSummary(int Likes, int Tomatoes, int MyLikes, int MyTomatoes, List<(Guid UserId, string Kind, DateTime At)> Recent);

        /// <summary>这个游戏一共被赞 / 被砸多少次、我赞 / 砸了几次、最近是谁赞 / 砸的</summary>
        public async Task<GameReactionSummary> GameReactionsAsync(string game, Guid? me, int recent, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"SELECT COALESCE(SUM(Kind='like'),0), COALESCE(SUM(Kind='tomato'),0),
  COALESCE(SUM(Kind='like' AND UserId=@u),0), COALESCE(SUM(Kind='tomato' AND UserId=@u),0)
FROM game_board_reactions WHERE GameKey=@g;
SELECT UserId, Kind, CreatedAt FROM game_board_reactions WHERE GameKey=@g ORDER BY Id DESC LIMIT @l;";
            cmd.Parameters.AddWithValue("@g", game);
            cmd.Parameters.AddWithValue("@u", me.HasValue ? me.Value.ToString() : "");
            cmd.Parameters.AddWithValue("@l", Math.Clamp(recent, 1, 50));
            await using var r = await cmd.ExecuteReaderAsync(ct);
            int likes = 0, tomatoes = 0, myLikes = 0, myTomatoes = 0;
            if (await r.ReadAsync(ct))
            {
                likes = Convert.ToInt32(r.GetValue(0)); tomatoes = Convert.ToInt32(r.GetValue(1));
                myLikes = Convert.ToInt32(r.GetValue(2)); myTomatoes = Convert.ToInt32(r.GetValue(3));
            }
            var list = new List<(Guid, string, DateTime)>();
            if (await r.NextResultAsync(ct))
                while (await r.ReadAsync(ct)) list.Add((Chat.ChatStore.ReadGuid(r, 0), r.GetString(1), Utc(r, 2)));
            return new GameReactionSummary(likes, tomatoes, myLikes, myTomatoes, list);
        }
    }
}
