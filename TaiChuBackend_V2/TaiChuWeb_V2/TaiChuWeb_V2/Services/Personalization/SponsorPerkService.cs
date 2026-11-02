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
        /// 特权 → 默认最低等级（1 = 觉醒者 … 4 = 君者）。以后新增特权在这里加一行，再到 SponsorPerkCatalog.BuiltIns 里写上名字和说明，用 HasFeature 判断即可。
        /// 超级管理员可以在「管理面板 → 赞助福利」里改最低等级（存在 SystemConfigs 的 Sponsor:PerkCatalog），改了马上生效，见 Features。
        /// </summary>
        public static readonly IReadOnlyDictionary<string, int> DefaultFeatures = new Dictionary<string, int>
        {
            ["custom-background"] = 1, // 自定义网站背景（图片 / 视频）：赞助了就有
            ["custom-profile"] = 1,    // 自定义个人主页横幅 / 整页背景：赞助了就有
            ["dev-progress"] = 2,      // 开发进度详情页（研发报告、日志、资料）：超凡者及以上，觉醒者看不了；到期没续费也看不了（见 DevProgressAccess）
            ["life-planner"] = 2,      // 太初灵脉「生活管理」：超凡者及以上；到期后只能看不能改、提醒暂停
            ["planner-email"] = 4,     // 生活管理的邮件提醒：君者
        };

        private static volatile IReadOnlyDictionary<string, int> _features = DefaultFeatures;

        /// <summary>当前生效的「特权 → 最低等级」：默认值 + 后台赞助福利目录里改过的等级（启动时和保存时由 SponsorPerkCatalog.Apply 更新）</summary>
        public static IReadOnlyDictionary<string, int> Features => _features;

        /// <summary>用后台配置覆盖最低等级；只认 DefaultFeatures 里有的特权，等级限制在 1～4</summary>
        public static void ApplyFeatureTiers(IReadOnlyDictionary<string, int> overrides)
        {
            var next = new Dictionary<string, int>(DefaultFeatures);
            foreach (var (key, tier) in overrides)
                if (next.ContainsKey(key)) next[key] = Math.Clamp(tier, 1, Tiers.Length);
            _features = next;
        }

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
                // 赞助头衔（fix181）：以前的赞助者也补发一次（只做一次）
                try { await BackfillTierTitlesAsync(c, ct); }
                catch { /* 补发失败不影响赞助特权本身，下次启动再试 */ }
                _ready = true;
            }
            finally { _schemaLock.Release(); }
        }

        // ===================== 赞助头衔（fix181） =====================
        // 赞助到哪一档就自动得到同名头衔（觉醒者 / 超凡者 / 大超凡者 / 君者），放进「我的头衔」里，永久保留（到期也不收回）。
        // 自动戴上的规则：现在没戴头衔，或者戴的是更低一档的赞助头衔，就换成新的；戴着别的头衔（交易行买的、管理员给的）不动。
        // 头衔存在交易行那张 user_cosmetics 表（Kind = title），和交易行、管理员授予的头衔在同一个地方管理。

        /// <summary>和 ShopService 里的建表语句一样（谁先用谁建）</summary>
        private const string CosmeticsTableSql = @"CREATE TABLE IF NOT EXISTS user_cosmetics (
  UserId CHAR(36) NOT NULL,
  Kind VARCHAR(10) NOT NULL,
  ItemKey VARCHAR(100) NOT NULL,
  Source VARCHAR(60) NOT NULL,
  CreatedAt DATETIME NOT NULL,
  PRIMARY KEY (UserId, Kind, ItemKey)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

        /// <summary>给一个人发这一档的赞助头衔，必要时自动戴上。只认四个正式档位，别的方案名不发头衔</summary>
        public async Task GrantTierTitleAsync(Guid userId, string? tier, CancellationToken ct = default)
        {
            var rank = TierRank(tier);
            if (rank <= 0) return;
            var title = Tiers[rank - 1];
            await using var c = new MySqlConnection(_connectionString);
            await c.OpenAsync(ct);
            await using (var make = c.CreateCommand()) { make.CommandText = CosmeticsTableSql; await make.ExecuteNonQueryAsync(ct); }
            await using (var add = c.CreateCommand())
            {
                add.CommandText = "INSERT IGNORE INTO user_cosmetics (UserId, Kind, ItemKey, Source, CreatedAt) VALUES (@u, 'title', @k, @s, @t)";
                add.Parameters.AddWithValue("@u", userId.ToString());
                add.Parameters.AddWithValue("@k", title);
                add.Parameters.AddWithValue("@s", $"赞助 · {title}");
                add.Parameters.AddWithValue("@t", DateTime.UtcNow);
                await add.ExecuteNonQueryAsync(ct);
            }
            await EquipIfBetterAsync(c, userId, title, ct);
        }

        /// <summary>没戴头衔、或者戴的是更低档的赞助头衔时，换成这一档</summary>
        private static async Task EquipIfBetterAsync(MySqlConnection c, Guid userId, string title, CancellationToken ct)
        {
            var lower = Tiers.Take(Math.Max(0, TierRank(title) - 1)).ToList();
            await using var eq = c.CreateCommand();
            var inList = lower.Count == 0 ? "" : " OR Title IN (" + string.Join(", ", lower.Select((_, i) => $"@l{i}")) + ")";
            eq.CommandText = $"UPDATE UserStats SET Title = @k WHERE UserId = @u AND (Title IS NULL OR Title = ''{inList})";
            eq.Parameters.AddWithValue("@k", title);
            eq.Parameters.AddWithValue("@u", userId.ToString());
            for (var i = 0; i < lower.Count; i++) eq.Parameters.AddWithValue($"@l{i}", lower[i]);
            await eq.ExecuteNonQueryAsync(ct);
        }

        /// <summary>
        /// 上线这个功能前就赞助过的人：按赞助记录里出现过的每一档都补发头衔；现在还在有效期内、又没戴头衔的，戴上最高那一档。
        /// 只做一次（记在 sponsor_title_backfill 表里），之后不会把别人主动摘掉的头衔又戴回去
        /// </summary>
        private static async Task BackfillTierTitlesAsync(MySqlConnection c, CancellationToken ct)
        {
            await using (var make = c.CreateCommand())
            {
                make.CommandText = CosmeticsTableSql + ";\nCREATE TABLE IF NOT EXISTS sponsor_title_backfill (Id INT NOT NULL PRIMARY KEY, DoneAt DATETIME NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                await make.ExecuteNonQueryAsync(ct);
            }
            await using (var done = c.CreateCommand())
            {
                done.CommandText = "SELECT COUNT(*) FROM sponsor_title_backfill WHERE Id = 1";
                if (Convert.ToInt64(await done.ExecuteScalarAsync(ct)) > 0) return;
            }
            // 每个人出现过的每一档（赞助记录 + 现在的档位）
            var pairs = new HashSet<(string User, string Tier)>();
            await using (var read = c.CreateCommand())
            {
                read.CommandText = "SELECT UserId, Tier FROM sponsor_perk_log WHERE Tier IS NOT NULL AND Source <> 'revoke' UNION SELECT UserId, Tier FROM sponsor_perks WHERE Tier IS NOT NULL";
                await using var r = await read.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) pairs.Add((Convert.ToString(r.GetValue(0))!, r.GetString(1)));
            }
            foreach (var (user, tier) in pairs)
            {
                var rank = TierRank(tier);
                if (rank <= 0 || !Guid.TryParse(user, out var uid)) continue;
                await using var add = c.CreateCommand();
                add.CommandText = "INSERT IGNORE INTO user_cosmetics (UserId, Kind, ItemKey, Source, CreatedAt) VALUES (@u, 'title', @k, @s, @t)";
                add.Parameters.AddWithValue("@u", uid.ToString());
                add.Parameters.AddWithValue("@k", Tiers[rank - 1]);
                add.Parameters.AddWithValue("@s", $"赞助 · {Tiers[rank - 1]}");
                add.Parameters.AddWithValue("@t", DateTime.UtcNow);
                await add.ExecuteNonQueryAsync(ct);
            }
            // 还在有效期内的：没戴头衔就戴上现在这一档
            var active = new List<(Guid User, string Tier)>();
            await using (var read = c.CreateCommand())
            {
                read.CommandText = "SELECT UserId, Tier FROM sponsor_perks WHERE Tier IS NOT NULL AND Until > UTC_TIMESTAMP()";
                await using var r = await read.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct))
                    if (Guid.TryParse(Convert.ToString(r.GetValue(0)), out var uid) && TierRank(r.GetString(1)) > 0) active.Add((uid, Tiers[TierRank(r.GetString(1)) - 1]));
            }
            foreach (var (uid, tier) in active) await EquipIfBetterAsync(c, uid, tier, ct);
            await using (var mark = c.CreateCommand())
            {
                mark.CommandText = "INSERT IGNORE INTO sponsor_title_backfill (Id, DoneAt) VALUES (1, @t)";
                mark.Parameters.AddWithValue("@t", DateTime.UtcNow);
                await mark.ExecuteNonQueryAsync(ct);
            }
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
                    // 赞助头衔：发不出去也不影响特权（下次同步 / 管理员开通时还会再发）
                    try { await GrantTierTitleAsync(Guid.Parse(userText!), tier, ct); } catch { }
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
            try { await GrantTierTitleAsync(userId, tier, ct); } catch { }
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
