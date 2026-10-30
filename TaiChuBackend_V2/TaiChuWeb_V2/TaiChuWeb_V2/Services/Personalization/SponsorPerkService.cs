using MySqlConnector;
using TaiChuWeb_V2.Services.Afdian;

namespace TaiChuWeb_V2.Services.Personalization
{
    /// <summary>
    /// 赞助特权：每笔赞助开通 30 天，连续赞助顺延；同时记录赞助等级，不同特权可以要求不同的最低等级（见 Features）。
    /// 两种开通方式：
    /// 1. 自动：用户在个人中心填一次爱发电订单号，绑定爱发电账号；之后每次同步到这个账号的新订单都会自动顺延。
    /// 2. 手动：管理员在后台给某个账号开通 / 延长 / 收回。
    /// 数据表 sponsor_perks / sponsor_perk_log 在第一次使用时自动创建（不需要 EF 迁移）。
    /// </summary>
    public sealed class SponsorPerkService(IConfiguration configuration)
    {
        public const int DaysPerMonth = 30;

        /// <summary>赞助等级，从低到高。爱发电方案名或后台手动选择的等级都用这几个名字。</summary>
        public static readonly string[] Tiers = ["觉醒者", "超凡者", "大超凡者", "君者"];

        /// <summary>
        /// 特权 → 最低等级（1 = 觉醒者 … 4 = 君者）。以后新增特权在这里加一行，再用 HasFeature 判断即可。
        /// </summary>
        public static readonly IReadOnlyDictionary<string, int> Features = new Dictionary<string, int>
        {
            ["custom-background"] = 1, // 自定义网站背景（图片 / 视频）：赞助了就有
            ["custom-profile"] = 1,    // 自定义个人主页横幅 / 整页背景：赞助了就有
        };

        /// <summary>等级名 → 级别（不认识的方案名按最低级算；没有等级为 0）</summary>
        public static int TierRank(string? tier)
        {
            if (string.IsNullOrWhiteSpace(tier)) return 0;
            var i = Array.IndexOf(Tiers, tier.Trim());
            return i >= 0 ? i + 1 : 1;
        }

        public static string NormalizeTier(string? tier)
        {
            if (string.IsNullOrWhiteSpace(tier)) return Tiers[0];
            var t = tier.Trim();
            return t.Length > 30 ? t[..30] : t;
        }

        public static bool HasFeature(Perk perk, string feature) =>
            perk.Active && Features.TryGetValue(feature, out var need) && TierRank(perk.Tier) >= need;

        private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        /// <summary>单笔订单至少多少元才开通特权（appsettings 里 Sponsor:PerkMinAmount，默认 0 = 任意金额）</summary>
        private readonly double _minAmount = configuration.GetValue<double?>("Sponsor:PerkMinAmount") ?? 0;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);
        private volatile bool _ready;

        public sealed record Perk(DateTime? Until, string? AfdianUserId, string? Tier)
        {
            public bool Active => Until is DateTime u && u > DateTime.UtcNow;
        }
        public sealed record PerkRow(Guid UserId, DateTime? Until, string? AfdianUserId, string? Tier, DateTime UpdatedAt);
        public sealed record LogRow(long Id, Guid UserId, string Source, string? RefId, int Days, string? Tier, DateTime? UntilAfter, string? Note, DateTime CreatedAt);

        public double MinAmount => _minAmount;

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
CREATE TABLE IF NOT EXISTS sponsor_perks (
  UserId CHAR(36) NOT NULL PRIMARY KEY,
  AfdianUserId VARCHAR(100) NULL,
  Until DATETIME NULL,
  Tier VARCHAR(30) NULL,
  UpdatedAt DATETIME NOT NULL,
  UNIQUE KEY UX_sponsor_perks_afdian (AfdianUserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS sponsor_perk_log (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  UserId CHAR(36) NOT NULL,
  Source VARCHAR(10) NOT NULL,
  RefId VARCHAR(100) NULL,
  Days INT NOT NULL,
  Tier VARCHAR(30) NULL,
  UntilAfter DATETIME NULL,
  Note VARCHAR(200) NULL,
  OperatorId CHAR(36) NULL,
  CreatedAt DATETIME NOT NULL,
  UNIQUE KEY UX_sponsor_perk_log_ref (Source, RefId),
  KEY IX_sponsor_perk_log_user (UserId, Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                await cmd.ExecuteNonQueryAsync(ct);
                _ready = true;
            }
            finally { _schemaLock.Release(); }
        }

        private static DateTime? ReadDate(MySqlDataReader r, int i) => r.IsDBNull(i) ? null : DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc);

        public async Task<Perk> GetAsync(Guid userId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Until, AfdianUserId, Tier FROM sponsor_perks WHERE UserId = @u";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return new Perk(null, null, null);
            return new Perk(ReadDate(r, 0), r.IsDBNull(1) ? null : r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2));
        }

        public async Task<bool> IsActiveAsync(Guid userId, CancellationToken ct = default) => (await GetAsync(userId, ct)).Active;

        /// <summary>绑定爱发电账号。一个爱发电账号只能绑定一个网站账号。</summary>
        public async Task<(bool Ok, string Message)> BindAfdianAsync(Guid userId, string afdianUserId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using (var check = c.CreateCommand())
            {
                check.CommandText = "SELECT UserId FROM sponsor_perks WHERE AfdianUserId = @a";
                check.Parameters.AddWithValue("@a", afdianUserId);
                var owner = await check.ExecuteScalarAsync(ct);
                if (owner != null && owner != DBNull.Value)
                {
                    var ownerId = owner is Guid g ? g : Guid.Parse(Convert.ToString(owner)!);
                    if (ownerId != userId) return (false, "这个爱发电账号已经绑定了另一个网站账号。如果是你本人的账号，请联系管理员处理。");
                    return (true, "已经绑定过了");
                }
            }
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"INSERT INTO sponsor_perks (UserId, AfdianUserId, Until, UpdatedAt) VALUES (@u, @a, NULL, @t)
ON DUPLICATE KEY UPDATE AfdianUserId = @a, UpdatedAt = @t";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            cmd.Parameters.AddWithValue("@a", afdianUserId);
            cmd.Parameters.AddWithValue("@t", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync(ct);
            return (true, "绑定成功");
        }

        public async Task UnbindAfdianAsync(Guid userId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE sponsor_perks SET AfdianUserId = NULL, UpdatedAt = @t WHERE UserId = @u";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            cmd.Parameters.AddWithValue("@t", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <summary>
        /// 把爱发电订单折算成特权天数（只处理已绑定账号的已支付订单；同一订单只算一次）。
        /// 每笔订单 30 天 × 月数，从「订单时间」和「当前到期时间」中较晚的那个开始顺延。
        /// 等级取爱发电里这个赞助者当前的方案名；特权还没到期时保留两者中较高的等级。
        /// </summary>
        /// <param name="tierByAfdianUser">爱发电 user_id → 方案名（来自 query-sponsor 的 current_plan）</param>
        /// <returns>新处理的订单数</returns>
        public async Task<int> ApplyOrdersAsync(IEnumerable<AfdianOrderDto> orders, IReadOnlyDictionary<string, string>? tierByAfdianUser = null, CancellationToken ct = default)
        {
            var paid = orders
                .Where(o => o.Status == 2 && !string.IsNullOrWhiteSpace(o.OutTradeNo) && !string.IsNullOrWhiteSpace(o.UserId))
                .Where(o => (double.TryParse(o.TotalAmount, out var amount) ? amount : 0) >= _minAmount)
                .GroupBy(o => o.OutTradeNo).Select(g => g.First())
                .OrderBy(o => o.CreateTime)
                .ToList();
            if (paid.Count == 0) return 0;

            await using var c = await OpenAsync(ct);
            var applied = 0;
            foreach (var o in paid)
            {
                await using var tx = await c.BeginTransactionAsync(ct);
                try
                {
                    string? userText = null, currentTier = null;
                    DateTime? until = null;
                    var found = false;
                    await using (var find = c.CreateCommand())
                    {
                        find.Transaction = tx;
                        find.CommandText = "SELECT UserId, Until, Tier FROM sponsor_perks WHERE AfdianUserId = @a FOR UPDATE";
                        find.Parameters.AddWithValue("@a", o.UserId);
                        await using var r = await find.ExecuteReaderAsync(ct);
                        if (await r.ReadAsync(ct))
                        {
                            found = true;
                            userText = Convert.ToString(r.GetValue(0));
                            until = ReadDate(r, 1);
                            currentTier = r.IsDBNull(2) ? null : r.GetString(2);
                        }
                    }
                    if (!found) { await tx.RollbackAsync(ct); continue; }
                    await using (var seen = c.CreateCommand())
                    {
                        seen.Transaction = tx;
                        seen.CommandText = "SELECT COUNT(*) FROM sponsor_perk_log WHERE Source = 'afdian' AND RefId = @ref";
                        seen.Parameters.AddWithValue("@ref", o.OutTradeNo);
                        if (Convert.ToInt64(await seen.ExecuteScalarAsync(ct)) > 0) { await tx.RollbackAsync(ct); continue; }
                    }

                    var months = int.TryParse(o.Month, out var m) && m > 0 ? Math.Min(m, 36) : 1;
                    var days = months * DaysPerMonth;
                    var orderTime = o.CreateTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(o.CreateTime).UtcDateTime : DateTime.UtcNow;
                    var start = until is DateTime u && u > orderTime ? u : orderTime;
                    var newUntil = start.AddDays(days);
                    var orderTier = NormalizeTier(tierByAfdianUser != null && tierByAfdianUser.TryGetValue(o.UserId, out var t) ? t : null);
                    var stillActive = until is DateTime cu && cu > orderTime;
                    var tier = stillActive && TierRank(currentTier) > TierRank(orderTier) ? currentTier! : orderTier;

                    await WriteAsync(c, tx, Guid.Parse(userText!), newUntil, tier, "afdian", o.OutTradeNo, days, $"爱发电赞助 {o.TotalAmount} 元", null, ct);
                    await tx.CommitAsync(ct);
                    applied++;
                }
                catch
                {
                    await tx.RollbackAsync(CancellationToken.None);
                    throw;
                }
            }
            return applied;
        }

        private static async Task WriteAsync(MySqlConnection c, MySqlTransaction tx, Guid userId, DateTime? until, string? tier, string source, string? refId, int days, string? note, Guid? operatorId, CancellationToken ct)
        {
            var now = DateTime.UtcNow;
            await using (var up = c.CreateCommand())
            {
                up.Transaction = tx;
                up.CommandText = @"INSERT INTO sponsor_perks (UserId, AfdianUserId, Until, Tier, UpdatedAt) VALUES (@u, NULL, @until, @tier, @t)
ON DUPLICATE KEY UPDATE Until = @until, Tier = COALESCE(@tier, Tier), UpdatedAt = @t";
                up.Parameters.AddWithValue("@tier", tier == null ? DBNull.Value : (object)tier);
                up.Parameters.AddWithValue("@u", userId.ToString());
                up.Parameters.AddWithValue("@until", until == null ? DBNull.Value : (object)until.Value);
                up.Parameters.AddWithValue("@t", now);
                await up.ExecuteNonQueryAsync(ct);
            }
            await using var log = c.CreateCommand();
            log.Transaction = tx;
            log.CommandText = @"INSERT INTO sponsor_perk_log (UserId, Source, RefId, Days, Tier, UntilAfter, Note, OperatorId, CreatedAt)
VALUES (@u, @s, @ref, @d, @tier, @until, @note, @op, @t)";
            log.Parameters.AddWithValue("@tier", tier == null ? DBNull.Value : (object)tier);
            log.Parameters.AddWithValue("@u", userId.ToString());
            log.Parameters.AddWithValue("@s", source);
            log.Parameters.AddWithValue("@ref", refId == null ? DBNull.Value : (object)refId);
            log.Parameters.AddWithValue("@d", days);
            log.Parameters.AddWithValue("@until", until == null ? DBNull.Value : (object)until.Value);
            log.Parameters.AddWithValue("@note", string.IsNullOrWhiteSpace(note) ? DBNull.Value : (object)note.Trim()[..Math.Min(note.Trim().Length, 200)]);
            log.Parameters.AddWithValue("@op", operatorId == null ? DBNull.Value : (object)operatorId.Value.ToString());
            log.Parameters.AddWithValue("@t", now);
            await log.ExecuteNonQueryAsync(ct);
        }

        /// <summary>管理员手动开通 / 延长：从「现在」和「当前到期时间」中较晚的那个开始加天数，等级设为所选等级。</summary>
        public async Task<DateTime> GrantAsync(Guid userId, int days, string tier, string? note, Guid operatorId, CancellationToken ct = default)
        {
            days = Math.Clamp(days, 1, 3650);
            await using var c = await OpenAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            DateTime? until = null;
            await using (var find = c.CreateCommand())
            {
                find.Transaction = tx;
                find.CommandText = "SELECT Until FROM sponsor_perks WHERE UserId = @u FOR UPDATE";
                find.Parameters.AddWithValue("@u", userId.ToString());
                await using var r = await find.ExecuteReaderAsync(ct);
                if (await r.ReadAsync(ct)) until = ReadDate(r, 0);
            }
            var now = DateTime.UtcNow;
            var newUntil = (until is DateTime u && u > now ? u : now).AddDays(days);
            await WriteAsync(c, tx, userId, newUntil, NormalizeTier(tier), "manual", null, days, note ?? "管理员开通", operatorId, ct);
            await tx.CommitAsync(ct);
            return newUntil;
        }

        /// <summary>管理员收回：立即到期</summary>
        public async Task RevokeAsync(Guid userId, string? note, Guid operatorId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            await WriteAsync(c, tx, userId, DateTime.UtcNow, null, "revoke", null, 0, note ?? "管理员收回", operatorId, ct);
            await tx.CommitAsync(ct);
        }

        public async Task<List<PerkRow>> ListAsync(int limit = 200, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT UserId, Until, AfdianUserId, Tier, UpdatedAt FROM sponsor_perks ORDER BY (Until IS NULL), Until DESC LIMIT @n";
            cmd.Parameters.AddWithValue("@n", Math.Clamp(limit, 1, 1000));
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var list = new List<PerkRow>();
            while (await r.ReadAsync(ct))
                list.Add(new PerkRow(Guid.Parse(Convert.ToString(r.GetValue(0))!), ReadDate(r, 1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3), DateTime.SpecifyKind(r.GetDateTime(4), DateTimeKind.Utc)));
            return list;
        }

        public async Task<List<LogRow>> LogAsync(Guid? userId, int limit = 50, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Id, UserId, Source, RefId, Days, Tier, UntilAfter, Note, CreatedAt FROM sponsor_perk_log"
                + (userId != null ? " WHERE UserId = @u" : "") + " ORDER BY Id DESC LIMIT @n";
            if (userId != null) cmd.Parameters.AddWithValue("@u", userId.Value.ToString());
            cmd.Parameters.AddWithValue("@n", Math.Clamp(limit, 1, 500));
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var list = new List<LogRow>();
            while (await r.ReadAsync(ct))
                list.Add(new LogRow(r.GetInt64(0), Guid.Parse(Convert.ToString(r.GetValue(1))!), r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3),
                    Convert.ToInt32(r.GetValue(4)), r.IsDBNull(5) ? null : r.GetString(5), ReadDate(r, 6), r.IsDBNull(7) ? null : r.GetString(7), DateTime.SpecifyKind(r.GetDateTime(8), DateTimeKind.Utc)));
            return list;
        }
    }
}
