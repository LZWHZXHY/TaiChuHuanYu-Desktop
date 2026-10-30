using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Analytics;

namespace TaiChuWeb_V2.Services.Games
{
    /// <summary>
    /// 土豆：只在游戏区使用的娱乐积分。
    /// 不能充值、不能转让、不能变成经验、不能换任何有现实价值的东西（游戏 CDKey、网盘资源只收经验）——这是它不构成赌博的前提，改规则时请保持这一点。
    /// 在交易行只能买站内的装饰和功能（头衔、头像框、改名卡等）。
    /// 来源：新人礼包、每日领取（签到当天额外加成）、破产补助、经验兑换；用途：斗地主输赢、排行榜、交易行买装饰 / 改名卡；消耗：斗地主台费、输给电脑、交易行。
    /// 数据表 potato_wallets / potato_ledger 在第一次使用时自动创建（不需要 EF 迁移）。
    /// </summary>
    public sealed class PotatoService(IConfiguration configuration, IServiceScopeFactory scopes)
    {
        public const long WelcomeGift = 5000;
        public const long DailyAmount = 1000;
        public const long SignBonus = 500;
        public const long ReliefAmount = 1000;
        public const long ReliefThreshold = 200;
        public const int ReliefPerDay = 2;
        /// <summary>每天最多从电脑玩家身上赢多少（防止刷电脑）</summary>
        public const long BotWinDailyCap = 3000;

        private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);
        private volatile bool _ready;

        public sealed record Wallet(long Balance, bool DailyClaimed, bool SignBonusClaimed, bool SignedToday, int ReliefLeft, bool CanRelief);
        public sealed record Change(Guid UserId, long Delta, string Reason, string RefType, string? RefId);

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
CREATE TABLE IF NOT EXISTS potato_wallets (
  UserId CHAR(36) NOT NULL PRIMARY KEY,
  Balance BIGINT NOT NULL DEFAULT 0,
  LastDailyDate DATE NULL,
  LastSignBonusDate DATE NULL,
  ReliefDate DATE NULL,
  ReliefCount INT NOT NULL DEFAULT 0,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS potato_ledger (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  UserId CHAR(36) NOT NULL,
  Delta BIGINT NOT NULL,
  Balance BIGINT NOT NULL,
  Reason VARCHAR(100) NOT NULL,
  RefType VARCHAR(20) NOT NULL,
  RefId VARCHAR(64) NULL,
  CreatedAt DATETIME NOT NULL,
  KEY IX_potato_ledger_user (UserId, Id),
  KEY IX_potato_ledger_ref (UserId, RefType, CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                await cmd.ExecuteNonQueryAsync(ct);
                _ready = true;
            }
            finally { _schemaLock.Release(); }
        }

        private static DateTime Today => ChinaClock.Today;

        /// <summary>确保有钱包（新用户自动发放新人礼包）。在事务里调用。</summary>
        private static async Task<(long Balance, DateTime? Daily, DateTime? Sign, DateTime? ReliefDate, int ReliefCount)> LockWalletAsync(MySqlConnection c, MySqlTransaction tx, Guid userId, CancellationToken ct)
        {
            await using (var ins = c.CreateCommand())
            {
                ins.Transaction = tx;
                ins.CommandText = @"INSERT IGNORE INTO potato_wallets (UserId, Balance, CreatedAt, UpdatedAt) VALUES (@u, @b, UTC_TIMESTAMP(), UTC_TIMESTAMP());
SELECT ROW_COUNT();";
                ins.Parameters.AddWithValue("@u", userId.ToString());
                ins.Parameters.AddWithValue("@b", WelcomeGift);
                if (Convert.ToInt32(await ins.ExecuteScalarAsync(ct)) > 0)
                    await LedgerAsync(c, tx, userId, WelcomeGift, WelcomeGift, "新人礼包", "welcome", null, ct);
            }
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT Balance, LastDailyDate, LastSignBonusDate, ReliefDate, ReliefCount FROM potato_wallets WHERE UserId=@u FOR UPDATE";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            await using var r = await cmd.ExecuteReaderAsync(ct);
            await r.ReadAsync(ct);
            DateTime? D(int i) => r.IsDBNull(i) ? null : r.GetDateTime(i).Date;
            return (r.GetInt64(0), D(1), D(2), D(3), r.GetInt32(4));
        }

        private static async Task LedgerAsync(MySqlConnection c, MySqlTransaction tx, Guid userId, long delta, long balance, string reason, string refType, string? refId, CancellationToken ct)
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "INSERT INTO potato_ledger (UserId, Delta, Balance, Reason, RefType, RefId, CreatedAt) VALUES (@u,@d,@b,@r,@t,@i,UTC_TIMESTAMP())";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            cmd.Parameters.AddWithValue("@d", delta);
            cmd.Parameters.AddWithValue("@b", balance);
            cmd.Parameters.AddWithValue("@r", reason.Length > 100 ? reason[..100] : reason);
            cmd.Parameters.AddWithValue("@t", refType);
            cmd.Parameters.AddWithValue("@i", (object?)refId ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private static async Task SetAsync(MySqlConnection c, MySqlTransaction tx, Guid userId, string set, CancellationToken ct, params (string, object)[] ps)
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"UPDATE potato_wallets SET {set}, UpdatedAt=UTC_TIMESTAMP() WHERE UserId=@u";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            foreach (var (k, v) in ps) cmd.Parameters.AddWithValue(k, v);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task<bool> SignedTodayAsync(Guid userId, CancellationToken ct)
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var today = Today;
            return await db.UserSignLogs.AnyAsync(l => l.UserId == userId && l.SignDate == today, ct);
        }

        public async Task<Wallet> GetAsync(Guid userId, CancellationToken ct = default)
        {
            var signed = await SignedTodayAsync(userId, ct);
            await using var c = await OpenAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            var w = await LockWalletAsync(c, tx, userId, ct);
            await tx.CommitAsync(ct);
            var reliefUsed = w.ReliefDate == Today ? w.ReliefCount : 0;
            return new Wallet(w.Balance, w.Daily == Today, w.Sign == Today, signed, ReliefPerDay - reliefUsed,
                w.Balance < ReliefThreshold && reliefUsed < ReliefPerDay);
        }

        /// <summary>每日领取：1000；当天签到过再加 500（签到晚于领取也能回来补领签到加成）。</summary>
        public async Task<(long Gained, string Message)> ClaimDailyAsync(Guid userId, CancellationToken ct = default)
        {
            var signed = await SignedTodayAsync(userId, ct);
            await using var c = await OpenAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            var w = await LockWalletAsync(c, tx, userId, ct);
            long gained = 0;
            var balance = w.Balance;
            var parts = new List<string>();
            if (w.Daily != Today)
            {
                gained += DailyAmount; balance += DailyAmount;
                await LedgerAsync(c, tx, userId, DailyAmount, balance, "每日领取", "daily", null, ct);
                await SetAsync(c, tx, userId, "LastDailyDate=@d", ct, ("@d", Today));
                parts.Add($"每日土豆 +{DailyAmount}");
            }
            if (signed && w.Sign != Today)
            {
                gained += SignBonus; balance += SignBonus;
                await LedgerAsync(c, tx, userId, SignBonus, balance, "签到加成", "sign", null, ct);
                await SetAsync(c, tx, userId, "LastSignBonusDate=@d", ct, ("@d", Today));
                parts.Add($"签到加成 +{SignBonus}");
            }
            if (gained > 0) await SetAsync(c, tx, userId, "Balance=@b", ct, ("@b", balance));
            await tx.CommitAsync(ct);
            if (gained > 0) return (gained, string.Join("，", parts));
            return (0, signed ? "今天的土豆已经领过了，明天再来吧" : "今天的土豆已经领过了；去签到还能再领 500");
        }

        /// <summary>破产补助：余额低于 200 时，每天最多 2 次，每次 1000</summary>
        public async Task<(long Gained, string Message)> ClaimReliefAsync(Guid userId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            var w = await LockWalletAsync(c, tx, userId, ct);
            var used = w.ReliefDate == Today ? w.ReliefCount : 0;
            if (w.Balance >= ReliefThreshold) { await tx.RollbackAsync(ct); return (0, $"土豆少于 {ReliefThreshold} 时才能领补助"); }
            if (used >= ReliefPerDay) { await tx.RollbackAsync(ct); return (0, "今天的补助已经领完了，明天再来"); }
            var balance = w.Balance + ReliefAmount;
            await LedgerAsync(c, tx, userId, ReliefAmount, balance, "破产补助", "relief", null, ct);
            await SetAsync(c, tx, userId, "Balance=@b, ReliefDate=@d, ReliefCount=@n", ct, ("@b", balance), ("@d", Today), ("@n", used + 1));
            await tx.CommitAsync(ct);
            return (ReliefAmount, $"领到破产补助 {ReliefAmount} 土豆（今天还能领 {ReliefPerDay - used - 1} 次）");
        }

        public async Task<Dictionary<Guid, long>> BalancesAsync(IEnumerable<Guid> userIds, CancellationToken ct = default)
        {
            var result = new Dictionary<Guid, long>();
            foreach (var id in userIds.Distinct()) result[id] = (await GetAsync(id, ct)).Balance;
            return result;
        }

        /// <summary>今天已经从电脑玩家身上赢了多少（净值）</summary>
        public async Task<long> BotWinsTodayAsync(Guid userId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT COALESCE(SUM(Delta),0) FROM potato_ledger WHERE UserId=@u AND RefType='game-bot' AND CreatedAt >= @since";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            cmd.Parameters.AddWithValue("@since", ChinaClock.DayStartUtc(Today));
            return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct));
        }

        /// <summary>
        /// 一次性结算多人的输赢（同一个事务）。扣分不会扣成负数：余额不够时只扣到 0。
        /// 返回每个人实际变化的数额。
        /// </summary>
        public async Task<Dictionary<Guid, long>> ApplyAsync(IReadOnlyCollection<Change> changes, CancellationToken ct = default)
        {
            var actual = new Dictionary<Guid, long>();
            if (changes.Count == 0) return actual;
            await using var c = await OpenAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            // 按固定顺序加锁，避免死锁
            foreach (var ch in changes.OrderBy(x => x.UserId))
            {
                var w = await LockWalletAsync(c, tx, ch.UserId, ct);
                var delta = ch.Delta < 0 ? -Math.Min(-ch.Delta, w.Balance) : ch.Delta;
                // 同一个人可能有多笔（输赢 + 台费），实际变化累加
                actual[ch.UserId] = actual.GetValueOrDefault(ch.UserId) + delta;
                if (delta == 0) continue;
                var balance = w.Balance + delta;
                await SetAsync(c, tx, ch.UserId, "Balance=@b", ct, ("@b", balance));
                await LedgerAsync(c, tx, ch.UserId, delta, balance, ch.Reason, ch.RefType, ch.RefId, ct);
            }
            await tx.CommitAsync(ct);
            return actual;
        }

        /// <summary>
        /// 严格扣土豆（余额不够就失败，不会扣成 0），用于交易行买装饰、改名卡。
        /// 返回扣完后的余额；失败时返回错误提示。
        /// </summary>
        public async Task<(bool Ok, long Balance, string? Error)> SpendAsync(Guid userId, long amount, string reason, string refType, string? refId, CancellationToken ct = default)
        {
            if (amount <= 0) return (false, 0, "金额不对");
            await using var c = await OpenAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            var w = await LockWalletAsync(c, tx, userId, ct);
            if (w.Balance < amount)
            {
                await tx.RollbackAsync(ct);
                return (false, w.Balance, $"土豆不够：需要 {amount:N0}，现在有 {w.Balance:N0}");
            }
            var balance = w.Balance - amount;
            await SetAsync(c, tx, userId, "Balance=@b", ct, ("@b", balance));
            await LedgerAsync(c, tx, userId, -amount, balance, reason, refType, refId, ct);
            await tx.CommitAsync(ct);
            return (true, balance, null);
        }

        /// <summary>退还土豆（购买失败时把刚扣的还回去）</summary>
        public async Task RefundAsync(Guid userId, long amount, string reason, string refType, string? refId, CancellationToken ct = default)
        {
            if (amount <= 0) return;
            await ApplyAsync(new[] { new Change(userId, amount, reason, refType, refId) }, ct);
        }

        // ===================== 经验 → 土豆（单向） =====================

        /// <summary>1 经验换多少土豆（appsettings 里 Potato:ExpRate，默认 5）</summary>
        public int ExpRate => Math.Max(1, configuration.GetValue<int?>("Potato:ExpRate") ?? 5);
        /// <summary>每人每天最多用多少经验换土豆（Potato:ExpDailyCap，默认 200）</summary>
        public int ExpDailyCap => Math.Max(0, configuration.GetValue<int?>("Potato:ExpDailyCap") ?? 200);

        private (string Stats, string StatsUser, string StatsExp, string Log, string LogUser, string LogChange, string LogReason, string LogCreated)? _expTables;

        /// <summary>经验表的真实表名、列名从 EF 模型里取，避免和数据库命名不一致</summary>
        private (string Stats, string StatsUser, string StatsExp, string Log, string LogUser, string LogChange, string LogReason, string LogCreated) ExpTables()
        {
            if (_expTables is { } cached) return cached;
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var stats = db.Model.FindEntityType(typeof(TaiChuWeb_V2.Models.User.UserStats))!;
            var log = db.Model.FindEntityType(typeof(TaiChuWeb_V2.Models.User.UserExpLog))!;
            string Col(Microsoft.EntityFrameworkCore.Metadata.IEntityType t, string prop) => t.FindProperty(prop)!.GetColumnName();
            var result = (stats.GetTableName()!, Col(stats, "UserId"), Col(stats, "Experience"),
                log.GetTableName()!, Col(log, "UserId"), Col(log, "Change"), Col(log, "Reason"), Col(log, "CreatedAt"));
            _expTables = result;
            return result;
        }

        public sealed record ExpExchangeInfo(int Rate, int DailyCap, int UsedToday, int LeftToday, long Experience);

        public async Task<ExpExchangeInfo> ExpExchangeInfoAsync(Guid userId, CancellationToken ct = default)
        {
            var t = ExpTables();
            await using var c = await OpenAsync(ct);
            var used = await ExpUsedTodayAsync(c, null, userId, ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT `{t.StatsExp}` FROM `{t.Stats}` WHERE `{t.StatsUser}`=@u";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            var exp = await cmd.ExecuteScalarAsync(ct);
            return new ExpExchangeInfo(ExpRate, ExpDailyCap, used, Math.Max(0, ExpDailyCap - used), exp == null || exp == DBNull.Value ? 0 : Convert.ToInt64(exp));
        }

        private async Task<int> ExpUsedTodayAsync(MySqlConnection c, MySqlTransaction? tx, Guid userId, CancellationToken ct)
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT COALESCE(SUM(Delta),0) FROM potato_ledger WHERE UserId=@u AND RefType='exp' AND CreatedAt >= @since";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            cmd.Parameters.AddWithValue("@since", ChinaClock.DayStartUtc(Today));
            return (int)(Convert.ToInt64(await cmd.ExecuteScalarAsync(ct)) / ExpRate);
        }

        /// <summary>
        /// 用经验换土豆：扣经验（记经验流水）、加土豆（记土豆流水），在同一个事务里完成。土豆不能换回经验。
        /// </summary>
        public async Task<(bool Ok, string Message, long Gained)> ExchangeExpAsync(Guid userId, int exp, CancellationToken ct = default)
        {
            if (exp <= 0) return (false, "请输入要兑换的经验数", 0);
            var t = ExpTables();
            await using var c = await OpenAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            var w = await LockWalletAsync(c, tx, userId, ct);
            var used = await ExpUsedTodayAsync(c, tx, userId, ct);
            var left = ExpDailyCap - used;
            if (left <= 0) { await tx.RollbackAsync(ct); return (false, $"今天的兑换额度（{ExpDailyCap} 经验）已经用完了，明天再来", 0); }
            if (exp > left) { await tx.RollbackAsync(ct); return (false, $"今天最多还能兑换 {left} 经验", 0); }

            await using (var dec = c.CreateCommand())
            {
                dec.Transaction = tx;
                dec.CommandText = $"UPDATE `{t.Stats}` SET `{t.StatsExp}` = `{t.StatsExp}` - @e WHERE `{t.StatsUser}`=@u AND `{t.StatsExp}` >= @e";
                dec.Parameters.AddWithValue("@e", exp);
                dec.Parameters.AddWithValue("@u", userId.ToString());
                if (await dec.ExecuteNonQueryAsync(ct) == 0) { await tx.RollbackAsync(ct); return (false, "经验不够", 0); }
            }
            var gained = (long)exp * ExpRate;
            await using (var log = c.CreateCommand())
            {
                log.Transaction = tx;
                log.CommandText = $"INSERT INTO `{t.Log}` (`{t.LogUser}`, `{t.LogChange}`, `{t.LogReason}`, `{t.LogCreated}`) VALUES (@u, @c, @r, UTC_TIMESTAMP())";
                log.Parameters.AddWithValue("@u", userId.ToString());
                log.Parameters.AddWithValue("@c", -exp);
                log.Parameters.AddWithValue("@r", $"兑换土豆 +{gained}");
                await log.ExecuteNonQueryAsync(ct);
            }
            var balance = w.Balance + gained;
            await SetAsync(c, tx, userId, "Balance=@b", ct, ("@b", balance));
            await LedgerAsync(c, tx, userId, gained, balance, $"用 {exp} 经验兑换", "exp", null, ct);
            await tx.CommitAsync(ct);
            return (true, $"兑换成功：-{exp} 经验，+{gained:N0} 土豆", gained);
        }

        public sealed record LedgerRow(long Id, long Delta, long Balance, string Reason, string RefType, string? RefId, DateTime CreatedAt);

        public async Task<List<LedgerRow>> LedgerAsync(Guid userId, int limit, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Id, Delta, Balance, Reason, RefType, RefId, CreatedAt FROM potato_ledger WHERE UserId=@u ORDER BY Id DESC LIMIT @l";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            cmd.Parameters.AddWithValue("@l", Math.Clamp(limit, 1, 200));
            var list = new List<LedgerRow>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
                list.Add(new LedgerRow(r.GetInt64(0), r.GetInt64(1), r.GetInt64(2), r.GetString(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), DateTime.SpecifyKind(r.GetDateTime(6), DateTimeKind.Utc)));
            return list;
        }

        /// <summary>土豆排行榜（余额）</summary>
        public async Task<List<(Guid UserId, long Balance)>> TopAsync(int limit, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT UserId, Balance FROM potato_wallets ORDER BY Balance DESC LIMIT @l";
            cmd.Parameters.AddWithValue("@l", Math.Clamp(limit, 1, 100));
            var list = new List<(Guid, long)>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add((Chat.ChatStore.ReadGuid(r, 0), r.GetInt64(1)));
            return list;
        }

        /// <summary>某段时间内各人的输赢净值（周榜 / 月榜，只算对局，不算领取）</summary>
        public async Task<List<(Guid UserId, long Net)>> NetSinceAsync(DateTime sinceUtc, int limit, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT UserId, SUM(Delta) AS Net FROM potato_ledger WHERE RefType IN ('game','game-bot','fee') AND CreatedAt >= @s GROUP BY UserId ORDER BY Net DESC LIMIT @l";
            cmd.Parameters.AddWithValue("@s", sinceUtc);
            cmd.Parameters.AddWithValue("@l", Math.Clamp(limit, 1, 100));
            var list = new List<(Guid, long)>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add((Chat.ChatStore.ReadGuid(r, 0), Convert.ToInt64(r.GetValue(1))));
            return list;
        }
    }
}
