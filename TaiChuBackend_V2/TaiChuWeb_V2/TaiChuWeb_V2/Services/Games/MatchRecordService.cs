using MySqlConnector;

namespace TaiChuWeb_V2.Services.Games
{
    /// <summary>一局联机对局里的一位玩家</summary>
    public sealed class MatchPlayer
    {
        public int Seat { get; set; }
        /// <summary>真人为用户 ID；电脑玩家为空</summary>
        public Guid? UserId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsBot { get; set; }
        /// <summary>地主 / 农民 / 黑方 / 白方 …</summary>
        public string Role { get; set; } = string.Empty;
        /// <summary>win / lose / draw</summary>
        public string Result { get; set; } = "draw";
        public long PotatoChange { get; set; }
    }

    public sealed class MatchRecord
    {
        public long Id { get; set; }
        /// <summary>doudizhu / gobang …</summary>
        public string Game { get; set; } = string.Empty;
        /// <summary>场次或模式说明，例如「普通场 · 底分 50」</summary>
        public string Mode { get; set; } = string.Empty;
        /// <summary>一句话总结，例如「地主 ×8 · 春天」</summary>
        public string Summary { get; set; } = string.Empty;
        /// <summary>详细数据（JSON，例如出牌记录、棋谱）</summary>
        public string? DetailJson { get; set; }
        public bool HasBots { get; set; }
        public DateTime StartedAt { get; set; }
        public DateTime EndedAt { get; set; }
        public List<MatchPlayer> Players { get; set; } = new();
    }

    /// <summary>
    /// 联机对局战绩（斗地主、联机五子棋等由服务器判定结果的对局）。
    /// 本地双人的围棋 / 象棋仍存在原来的 BoardGameRecords 表里，战绩页把两边合在一起显示。
    /// 数据表 game_matches / game_match_players 在第一次使用时自动创建。
    /// </summary>
    public sealed class MatchRecordService(IConfiguration configuration)
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
CREATE TABLE IF NOT EXISTS game_matches (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  Game VARCHAR(20) NOT NULL,
  Mode VARCHAR(60) NOT NULL DEFAULT '',
  Summary VARCHAR(200) NOT NULL DEFAULT '',
  DetailJson MEDIUMTEXT NULL,
  HasBots TINYINT(1) NOT NULL DEFAULT 0,
  StartedAt DATETIME NOT NULL,
  EndedAt DATETIME NOT NULL,
  KEY IX_game_matches_game (Game, Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS game_match_players (
  MatchId BIGINT NOT NULL,
  Seat INT NOT NULL,
  UserId CHAR(36) NULL,
  Name VARCHAR(60) NOT NULL,
  IsBot TINYINT(1) NOT NULL DEFAULT 0,
  Role VARCHAR(20) NOT NULL DEFAULT '',
  Result VARCHAR(8) NOT NULL,
  PotatoChange BIGINT NOT NULL DEFAULT 0,
  PRIMARY KEY (MatchId, Seat),
  KEY IX_game_match_players_user (UserId, MatchId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                await cmd.ExecuteNonQueryAsync(ct);
                _ready = true;
            }
            finally { _schemaLock.Release(); }
        }

        public async Task<long> SaveAsync(MatchRecord m, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await using (var cmd = c.CreateCommand())
            {
                cmd.Transaction = tx;
                cmd.CommandText = @"INSERT INTO game_matches (Game, Mode, Summary, DetailJson, HasBots, StartedAt, EndedAt) VALUES (@g,@m,@s,@d,@b,@st,@en); SELECT LAST_INSERT_ID();";
                cmd.Parameters.AddWithValue("@g", m.Game);
                cmd.Parameters.AddWithValue("@m", m.Mode.Length > 60 ? m.Mode[..60] : m.Mode);
                cmd.Parameters.AddWithValue("@s", m.Summary.Length > 200 ? m.Summary[..200] : m.Summary);
                cmd.Parameters.AddWithValue("@d", (object?)m.DetailJson ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@b", m.HasBots);
                cmd.Parameters.AddWithValue("@st", m.StartedAt);
                cmd.Parameters.AddWithValue("@en", m.EndedAt);
                m.Id = Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
            }
            foreach (var p in m.Players)
            {
                await using var cmd = c.CreateCommand();
                cmd.Transaction = tx;
                cmd.CommandText = "INSERT INTO game_match_players (MatchId, Seat, UserId, Name, IsBot, Role, Result, PotatoChange) VALUES (@id,@seat,@u,@n,@bot,@role,@res,@pc)";
                cmd.Parameters.AddWithValue("@id", m.Id);
                cmd.Parameters.AddWithValue("@seat", p.Seat);
                cmd.Parameters.AddWithValue("@u", p.UserId.HasValue ? p.UserId.Value.ToString() : (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@n", p.Name.Length > 60 ? p.Name[..60] : p.Name);
                cmd.Parameters.AddWithValue("@bot", p.IsBot);
                cmd.Parameters.AddWithValue("@role", p.Role);
                cmd.Parameters.AddWithValue("@res", p.Result);
                cmd.Parameters.AddWithValue("@pc", p.PotatoChange);
                await cmd.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
            return m.Id;
        }

        public sealed record GameStat(string Game, int Total, int Wins, int Losses, int Draws, long PotatoNet);

        /// <summary>某人在各个游戏里的场次、胜负、土豆净值</summary>
        public async Task<List<GameStat>> StatsAsync(Guid userId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"SELECT m.Game, COUNT(*), SUM(p.Result='win'), SUM(p.Result='lose'), SUM(p.Result='draw'), COALESCE(SUM(p.PotatoChange),0)
FROM game_match_players p JOIN game_matches m ON m.Id = p.MatchId WHERE p.UserId=@u GROUP BY m.Game";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            var list = new List<GameStat>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add(new GameStat(r.GetString(0), Convert.ToInt32(r.GetValue(1)), Convert.ToInt32(r.GetValue(2)), Convert.ToInt32(r.GetValue(3)), Convert.ToInt32(r.GetValue(4)), Convert.ToInt64(r.GetValue(5))));
            return list;
        }

        /// <summary>某人最近的连胜 / 连败（按对局时间，从最新一局往前数）</summary>
        public async Task<(int Streak, string Kind)> StreakAsync(Guid userId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Result FROM game_match_players WHERE UserId=@u ORDER BY MatchId DESC LIMIT 100";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            var results = new List<string>();
            await using (var r = await cmd.ExecuteReaderAsync(ct)) while (await r.ReadAsync(ct)) results.Add(r.GetString(0));
            if (results.Count == 0) return (0, "none");
            var kind = results[0];
            return (results.TakeWhile(x => x == kind).Count(), kind);
        }

        public sealed record WinRow(Guid UserId, int Wins, int Total);

        /// <summary>常胜将军榜：所有游戏的胜利总场次（同场次按胜率）</summary>
        public async Task<List<WinRow>> WinsLeaderboardAsync(DateTime? sinceUtc, int limit, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $@"SELECT p.UserId, SUM(p.Result='win') AS Wins, COUNT(*) AS Total
FROM game_match_players p JOIN game_matches m ON m.Id = p.MatchId
WHERE p.UserId IS NOT NULL {(sinceUtc.HasValue ? "AND m.EndedAt >= @s" : "")}
GROUP BY p.UserId HAVING Wins > 0 ORDER BY Wins DESC, Wins / Total DESC LIMIT @l";
            if (sinceUtc.HasValue) cmd.Parameters.AddWithValue("@s", sinceUtc.Value);
            cmd.Parameters.AddWithValue("@l", Math.Clamp(limit, 1, 100));
            var list = new List<WinRow>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(new WinRow(Chat.ChatStore.ReadGuid(r, 0), Convert.ToInt32(r.GetValue(1)), Convert.ToInt32(r.GetValue(2))));
            return list;
        }

        public sealed record LossRow(Guid UserId, int Losses, int Total);

        /// <summary>常败将军榜：所有游戏的失败总场次（同场次按败率）</summary>
        public async Task<List<LossRow>> LossesLeaderboardAsync(DateTime? sinceUtc, int limit, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $@"SELECT p.UserId, SUM(p.Result='lose') AS Losses, COUNT(*) AS Total
FROM game_match_players p JOIN game_matches m ON m.Id = p.MatchId
WHERE p.UserId IS NOT NULL {(sinceUtc.HasValue ? "AND m.EndedAt >= @s" : "")}
GROUP BY p.UserId HAVING Losses > 0 ORDER BY Losses DESC, Losses / Total DESC LIMIT @l";
            if (sinceUtc.HasValue) cmd.Parameters.AddWithValue("@s", sinceUtc.Value);
            cmd.Parameters.AddWithValue("@l", Math.Clamp(limit, 1, 100));
            var list = new List<LossRow>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(new LossRow(Chat.ChatStore.ReadGuid(r, 0), Convert.ToInt32(r.GetValue(1)), Convert.ToInt32(r.GetValue(2))));
            return list;
        }

        /// <summary>某人参与过的对局（新的在前），包含同局所有玩家</summary>
        public async Task<List<MatchRecord>> ForUserAsync(Guid userId, string? game, int limit, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            var matches = new List<MatchRecord>();
            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = $@"SELECT m.Id, m.Game, m.Mode, m.Summary, m.HasBots, m.StartedAt, m.EndedAt FROM game_matches m
WHERE m.Id IN (SELECT MatchId FROM game_match_players WHERE UserId=@u) {(game != null ? "AND m.Game=@g" : "")}
ORDER BY m.Id DESC LIMIT @l";
                cmd.Parameters.AddWithValue("@u", userId.ToString());
                if (game != null) cmd.Parameters.AddWithValue("@g", game);
                cmd.Parameters.AddWithValue("@l", Math.Clamp(limit, 1, 500));
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                    matches.Add(new MatchRecord
                    {
                        Id = r.GetInt64(0), Game = r.GetString(1), Mode = r.GetString(2), Summary = r.GetString(3), HasBots = r.GetBoolean(4),
                        StartedAt = DateTime.SpecifyKind(r.GetDateTime(5), DateTimeKind.Utc), EndedAt = DateTime.SpecifyKind(r.GetDateTime(6), DateTimeKind.Utc),
                    });
            }
            if (matches.Count == 0) return matches;
            var byId = matches.ToDictionary(m => m.Id);
            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = $"SELECT MatchId, Seat, UserId, Name, IsBot, Role, Result, PotatoChange FROM game_match_players WHERE MatchId IN ({string.Join(",", byId.Keys)}) ORDER BY MatchId, Seat";
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var uid = r.IsDBNull(2) ? (Guid?)null : Chat.ChatStore.ReadGuid(r, 2);
                    byId[r.GetInt64(0)].Players.Add(new MatchPlayer
                    {
                        Seat = r.GetInt32(1), UserId = uid, Name = r.GetString(3), IsBot = r.GetBoolean(4),
                        Role = r.GetString(5), Result = r.GetString(6), PotatoChange = r.GetInt64(7),
                    });
                }
            }
            return matches;
        }
    }
}
