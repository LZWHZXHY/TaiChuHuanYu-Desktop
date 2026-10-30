using MySqlConnector;

namespace TaiChuWeb_V2.Services.Analytics
{
    /// <summary>
    /// 统计数据的存取。用三张独立的小表保存历史（启动时自动建表，不需要 EF 迁移）：
    /// · analytics_user_days      每个登录用户每天一行（首次/最后出现时间、请求次数）→ 日活、周活、月活、留存
    /// · analytics_visitor_days   每个匿名访客每天一行 → 游客访问量
    /// · analytics_online_samples 每 5 分钟一次在线人数快照 → 在线曲线、每日峰值
    /// 这些表只按自身的列查询，不与其他表做字符串关联，避免排序规则冲突。
    /// </summary>
    public sealed class AnalyticsStore
    {
        private readonly string _connectionString;
        private readonly SemaphoreSlim _flushLock = new(1, 1);
        private static readonly int OffsetMinutes = (int)ChinaClock.Offset.TotalMinutes;

        public AnalyticsStore(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        }

        private async Task<MySqlConnection> OpenAsync(CancellationToken ct = default)
        {
            var connection = new MySqlConnection(_connectionString);
            await connection.OpenAsync(ct);
            return connection;
        }

        public async Task EnsureSchemaAsync(CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS analytics_user_days (
  Day DATE NOT NULL,
  UserId CHAR(36) NOT NULL,
  FirstSeen DATETIME NOT NULL,
  LastSeen DATETIME NOT NULL,
  Hits INT NOT NULL DEFAULT 0,
  PRIMARY KEY (Day, UserId),
  KEY IX_analytics_user_days_user (UserId, Day)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS analytics_visitor_days (
  Day DATE NOT NULL,
  VisitorId VARCHAR(64) NOT NULL,
  FirstSeen DATETIME NOT NULL,
  LastSeen DATETIME NOT NULL,
  Hits INT NOT NULL DEFAULT 0,
  PRIMARY KEY (Day, VisitorId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS analytics_online_samples (
  At DATETIME NOT NULL,
  Members INT NOT NULL,
  Guests INT NOT NULL,
  PRIMARY KEY (At)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <summary>把内存里攒下的活跃记录写入数据库（可被后台任务和管理接口同时调用）。</summary>
        public async Task FlushAsync(PresenceTracker tracker, CancellationToken ct = default)
        {
            await _flushLock.WaitAsync(ct);
            var (members, visitors) = tracker.Drain();
            try
            {
                if (members.Count == 0 && visitors.Count == 0) return;
                await using var c = await OpenAsync(ct);
                await UpsertAsync(c, "analytics_user_days", "UserId",
                    members.Select(kv => (kv.Key.Day, kv.Key.UserId.ToString(), kv.Value)).ToList(), ct);
                await UpsertAsync(c, "analytics_visitor_days", "VisitorId",
                    visitors.Select(kv => (kv.Key.Day, kv.Key.VisitorId, kv.Value)).ToList(), ct);
            }
            catch
            {
                tracker.Restore(members, visitors);
                throw;
            }
            finally { _flushLock.Release(); }
        }

        private static async Task UpsertAsync(MySqlConnection c, string table, string idColumn,
            List<(DateTime Day, string Id, PresenceTracker.Seen Seen)> rows, CancellationToken ct)
        {
            foreach (var chunk in rows.Chunk(200))
            {
                await using var cmd = c.CreateCommand();
                var values = new List<string>();
                for (var i = 0; i < chunk.Length; i++)
                {
                    var (day, id, seen) = chunk[i];
                    values.Add($"(@d{i},@i{i},@f{i},@l{i},@h{i})");
                    cmd.Parameters.AddWithValue($"@d{i}", day.Date);
                    cmd.Parameters.AddWithValue($"@i{i}", id);
                    cmd.Parameters.AddWithValue($"@f{i}", seen.FirstUtc);
                    cmd.Parameters.AddWithValue($"@l{i}", seen.LastUtc);
                    cmd.Parameters.AddWithValue($"@h{i}", seen.Hits);
                }
                cmd.CommandText = $@"INSERT INTO {table} (Day,{idColumn},FirstSeen,LastSeen,Hits) VALUES {string.Join(",", values)}
ON DUPLICATE KEY UPDATE FirstSeen = LEAST(FirstSeen, VALUES(FirstSeen)), LastSeen = GREATEST(LastSeen, VALUES(LastSeen)), Hits = Hits + VALUES(Hits)";
                await cmd.ExecuteNonQueryAsync(ct);
            }
        }

        public async Task InsertSampleAsync(DateTime atUtc, int members, int guests, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "INSERT INTO analytics_online_samples (At,Members,Guests) VALUES (@a,@m,@g) ON DUPLICATE KEY UPDATE Members = GREATEST(Members, VALUES(Members)), Guests = GREATEST(Guests, VALUES(Guests))";
            cmd.Parameters.AddWithValue("@a", atUtc);
            cmd.Parameters.AddWithValue("@m", members);
            cmd.Parameters.AddWithValue("@g", guests);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <summary>只保留最近 400 天的游客与在线快照（登录用户的每日记录保留，用于长期留存分析）。</summary>
        public async Task CleanupAsync(CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM analytics_online_samples WHERE At < @cut; DELETE FROM analytics_visitor_days WHERE Day < @day;";
            cmd.Parameters.AddWithValue("@cut", DateTime.UtcNow.AddDays(-400));
            cmd.Parameters.AddWithValue("@day", ChinaClock.Today.AddDays(-400));
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // ---------------- 查询 ----------------

        public sealed record DayCount(DateTime Day, int Count);
        public sealed record DayPeak(DateTime Day, int Total, int Members);
        public sealed record Sample(DateTime AtUtc, int Members, int Guests);

        /// <summary>开始记录活跃数据的第一天（之前的日期没有活跃数据）。</summary>
        public async Task<DateTime?> TrackingSinceAsync(CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT MIN(Day) FROM analytics_user_days";
            var value = await cmd.ExecuteScalarAsync(ct);
            return value is DateTime d ? d.Date : null;
        }

        public Task<List<DayCount>> ActiveByDayAsync(DateTime from, DateTime to, CancellationToken ct = default)
            => DayCountsAsync("analytics_user_days", from, to, ct);

        public Task<List<DayCount>> VisitorsByDayAsync(DateTime from, DateTime to, CancellationToken ct = default)
            => DayCountsAsync("analytics_visitor_days", from, to, ct);

        private async Task<List<DayCount>> DayCountsAsync(string table, DateTime from, DateTime to, CancellationToken ct)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT Day, COUNT(*) FROM {table} WHERE Day BETWEEN @a AND @b GROUP BY Day";
            cmd.Parameters.AddWithValue("@a", from.Date);
            cmd.Parameters.AddWithValue("@b", to.Date);
            var list = new List<DayCount>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(new DayCount(r.GetDateTime(0).Date, Convert.ToInt32(r.GetValue(1))));
            return list;
        }

        /// <summary>一段时间内活跃过的不重复用户数（周活、月活）。</summary>
        public async Task<int> UniqueActiveAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT COUNT(DISTINCT UserId) FROM analytics_user_days WHERE Day BETWEEN @a AND @b";
            cmd.Parameters.AddWithValue("@a", from.Date);
            cmd.Parameters.AddWithValue("@b", to.Date);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        /// <summary>某天在某个时刻之前就已出现过的用户数（用于“较昨日同一时段”的公平对比）。</summary>
        public async Task<int> ActiveBeforeAsync(DateTime day, DateTime beforeUtc, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM analytics_user_days WHERE Day = @d AND FirstSeen < @t";
            cmd.Parameters.AddWithValue("@d", day.Date);
            cmd.Parameters.AddWithValue("@t", beforeUtc);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        /// <summary>每天的在线峰值（按中国时间分天）。</summary>
        public async Task<List<DayPeak>> PeaksByDayAsync(DateTime from, DateTime to, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"SELECT DATE(DATE_ADD(At, INTERVAL @off MINUTE)) AS D, MAX(Members + Guests), MAX(Members)
FROM analytics_online_samples WHERE At >= @a AND At < @b GROUP BY D";
            cmd.Parameters.AddWithValue("@off", OffsetMinutes);
            cmd.Parameters.AddWithValue("@a", ChinaClock.DayStartUtc(from));
            cmd.Parameters.AddWithValue("@b", ChinaClock.DayStartUtc(to.AddDays(1)));
            var list = new List<DayPeak>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(new DayPeak(r.GetDateTime(0).Date, Convert.ToInt32(r.GetValue(1)), Convert.ToInt32(r.GetValue(2))));
            return list;
        }

        public async Task<List<Sample>> SamplesAsync(DateTime fromUtc, DateTime toUtc, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT At, Members, Guests FROM analytics_online_samples WHERE At >= @a AND At < @b ORDER BY At";
            cmd.Parameters.AddWithValue("@a", fromUtc);
            cmd.Parameters.AddWithValue("@b", toUtc);
            var list = new List<Sample>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(new Sample(DateTime.SpecifyKind(r.GetDateTime(0), DateTimeKind.Utc), r.GetInt32(1), r.GetInt32(2)));
            return list;
        }

        /// <summary>这些用户各自最后一次出现的时间（UTC；成员列表显示「几天前来过」用）。统计开始前没来过的人不在结果里。</summary>
        public async Task<Dictionary<Guid, DateTime>> LastSeenOfAsync(IReadOnlyCollection<Guid> userIds, CancellationToken ct = default)
        {
            var result = new Dictionary<Guid, DateTime>();
            if (userIds.Count == 0) return result;
            await using var c = await OpenAsync(ct);
            foreach (var chunk in userIds.Chunk(500))
            {
                await using var cmd = c.CreateCommand();
                var names = chunk.Select((id, i) => { cmd.Parameters.AddWithValue($"@u{i}", id.ToString()); return $"@u{i}"; });
                cmd.CommandText = $"SELECT UserId, MAX(LastSeen) FROM analytics_user_days WHERE UserId IN ({string.Join(",", names)}) GROUP BY UserId";
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var id = Chat.ChatStore.ReadGuid(r, 0);
                    if (id != Guid.Empty && !r.IsDBNull(1)) result[id] = DateTime.SpecifyKind(r.GetDateTime(1), DateTimeKind.Utc);
                }
            }
            return result;
        }

        /// <summary>给定用户在某段日期内各自活跃过的日期（用于新用户留存）。</summary>
        public async Task<Dictionary<Guid, HashSet<DateTime>>> ActiveDaysOfAsync(IReadOnlyCollection<Guid> userIds, DateTime from, DateTime to, CancellationToken ct = default)
        {
            var result = new Dictionary<Guid, HashSet<DateTime>>();
            if (userIds.Count == 0) return result;
            await using var c = await OpenAsync(ct);
            foreach (var chunk in userIds.Chunk(500))
            {
                await using var cmd = c.CreateCommand();
                var names = chunk.Select((id, i) => { cmd.Parameters.AddWithValue($"@u{i}", id.ToString()); return $"@u{i}"; });
                cmd.CommandText = $"SELECT UserId, Day FROM analytics_user_days WHERE Day BETWEEN @a AND @b AND UserId IN ({string.Join(",", names)})";
                cmd.Parameters.AddWithValue("@a", from.Date);
                cmd.Parameters.AddWithValue("@b", to.Date);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                {
                    var id = Chat.ChatStore.ReadGuid(r, 0); // CHAR(36) 会被读成 Guid
                    if (id == Guid.Empty) continue;
                    if (!result.TryGetValue(id, out var set)) result[id] = set = new HashSet<DateTime>();
                    set.Add(r.GetDateTime(1).Date);
                }
            }
            return result;
        }
    }
}
