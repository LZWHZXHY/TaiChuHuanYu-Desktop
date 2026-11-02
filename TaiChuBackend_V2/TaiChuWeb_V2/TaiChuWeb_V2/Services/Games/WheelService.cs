using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Analytics;

namespace TaiChuWeb_V2.Services.Games
{
    /// <summary>
    /// 太虚游 · 转盘。
    /// · 用户转盘（Kind = user）：大家自己做的转盘，纯娱乐（帮忙做决定、抽人、抽题），转了不给任何奖励，结果由浏览器自己随机；
    ///   创建要花经验或土豆（价格站长在后台调），编辑不再收费，删除不退费；每人最多 MaxPerUser 个。
    ///   公开的进大厅列表；「仅自己」的不进列表，只有自己和拿到带分享码链接的人能打开。
    /// · 官方转盘（Kind = official）：管理员做的抽奖转盘，扇区可以设奖品（土豆 / 经验）。
    ///   登录用户每个官方转盘每天（北京时间）免费转 1 次，结果由服务器用加密随机数按权重决定，服务器发奖并记录；
    ///   防重复领取靠 wheel_official_spins 上的唯一索引（同一转盘 + 同一人 + 同一天只能插进去一条）。
    /// 数据表 wheel_wheels / wheel_official_spins 由 WheelSchema 启动时创建（不需要 EF 迁移），万一没建成第一次用时还会再试。
    /// </summary>
    public sealed class WheelService(IConfiguration configuration, IServiceScopeFactory scopes, PotatoService potatoes, ILogger<WheelService> logger)
    {
        // ===== 规则常量（想改上限就改这里；价格在后台改，见 WheelController） =====

        /// <summary>每人最多做几个转盘</summary>
        public const int MaxPerUser = 30;
        public const int MinSegments = 2;
        public const int MaxSegments = 24;
        public const int TitleMax = 40;
        public const int IntroMax = 300;
        public const int SegmentTextMax = 20;
        /// <summary>权重最大值（权重是整数，1 起）</summary>
        public const int WeightMax = 1000;
        /// <summary>官方转盘一个扇区最多奖多少土豆 / 经验（防止手滑多打几个 0）</summary>
        public const int PotatoPrizeMax = 100000;
        public const int ExpPrizeMax = 10000;

        /// <summary>SystemConfigs 里的键：创建转盘花多少经验 / 土豆。填负数表示不允许用这种方式付</summary>
        public const string CostExpKey = "Wheel:CreateCostExp";
        public const string CostPotatoKey = "Wheel:CreateCostPotato";
        public const int DefaultCostExp = 30;
        public const int DefaultCostPotato = 300;

        /// <summary>扇区没选颜色时按顺序用这一组</summary>
        public static readonly string[] Palette =
        {
            "#E4572E", "#F3A712", "#29335C", "#669BBC", "#A8C686", "#7D5BA6", "#D1495B", "#00798C", "#EDAE49", "#30638E", "#B56576", "#3D9970",
        };

        private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);
        private volatile bool _ready;

        // ===== 数据结构 =====

        /// <summary>一个扇区。PrizeType：none 无 / potato 土豆 / exp 经验（只有官方转盘能设奖品）</summary>
        public sealed class Segment
        {
            public string Text { get; set; } = "";
            public string Color { get; set; } = "";
            public int Weight { get; set; } = 1;
            public string PrizeType { get; set; } = "none";
            public int PrizeAmount { get; set; }
        }

        public sealed class WheelRow
        {
            public long Id { get; set; }
            public string Kind { get; set; } = "user";
            public Guid OwnerId { get; set; }
            public string Title { get; set; } = "";
            public string Intro { get; set; } = "";
            public string Visibility { get; set; } = "public";
            public string ShareCode { get; set; } = "";
            public List<Segment> Segments { get; set; } = new();
            public bool RemoveOnHit { get; set; }
            public string Status { get; set; } = "active";
            public string? HiddenReason { get; set; }
            public long SpinCount { get; set; }
            public int SortOrder { get; set; }
            public DateTime CreatedAt { get; set; }
            public DateTime UpdatedAt { get; set; }
        }

        /// <summary>创建 / 编辑时用户填的内容</summary>
        public class WheelInput
        {
            public string? Title { get; set; }
            public string? Intro { get; set; }
            /// <summary>public 公开 / private 仅自己</summary>
            public string? Visibility { get; set; }
            public bool RemoveOnHit { get; set; }
            public List<Segment>? Segments { get; set; }
            /// <summary>官方转盘的排序（小的在前），用户转盘忽略</summary>
            public int SortOrder { get; set; }
        }

        public sealed record SpinLog(long Id, long WheelId, Guid UserId, string WheelTitle, int SegmentIndex, string SegmentText, string PrizeType, int PrizeAmount, DateTime SpinDate, DateTime CreatedAt);

        public sealed record SpinResult(bool Ok, string? Error, int SegmentIndex, Segment? Segment, List<Segment>? Segments, string? Message);

        private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

        // ===== 建表 =====

        private const string SchemaSql = @"
CREATE TABLE IF NOT EXISTS wheel_wheels (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  Kind VARCHAR(10) NOT NULL DEFAULT 'user',
  OwnerId CHAR(36) NOT NULL,
  Title VARCHAR(40) NOT NULL DEFAULT '',
  Intro VARCHAR(300) NOT NULL DEFAULT '',
  Visibility VARCHAR(10) NOT NULL DEFAULT 'public',
  ShareCode VARCHAR(16) NOT NULL DEFAULT '',
  SegmentsJson TEXT NOT NULL,
  RemoveOnHit TINYINT(1) NOT NULL DEFAULT 0,
  Status VARCHAR(10) NOT NULL DEFAULT 'active',
  HiddenReason VARCHAR(200) NULL,
  SpinCount BIGINT NOT NULL DEFAULT 0,
  SortOrder INT NOT NULL DEFAULT 0,
  PaidWith VARCHAR(10) NOT NULL DEFAULT '',
  PaidAmount INT NOT NULL DEFAULT 0,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL,
  KEY IX_ww_hot (Kind, Visibility, Status, SpinCount),
  KEY IX_ww_new (Kind, Visibility, Status, Id),
  KEY IX_ww_owner (OwnerId, Kind, Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS wheel_official_spins (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  WheelId BIGINT NOT NULL,
  UserId CHAR(36) NOT NULL,
  SpinDate DATE NOT NULL,
  WheelTitle VARCHAR(40) NOT NULL DEFAULT '',
  SegmentIndex INT NOT NULL DEFAULT 0,
  SegmentText VARCHAR(40) NOT NULL DEFAULT '',
  PrizeType VARCHAR(10) NOT NULL DEFAULT 'none',
  PrizeAmount INT NOT NULL DEFAULT 0,
  Granted TINYINT NOT NULL DEFAULT 0,
  CreatedAt DATETIME NOT NULL,
  UNIQUE KEY UX_wos_daily (WheelId, UserId, SpinDate),
  KEY IX_wos_user (UserId, Id),
  KEY IX_wos_pending (Granted, PrizeType, Id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

        /// <summary>建表（启动时由 WheelSchema 调用；第一次用之前也会确认一次）</summary>
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

        private static Guid ReadGuid(MySqlDataReader r, int i)
        {
            // CHAR(36) 的列 MySqlConnector 可能直接读成 Guid，也可能是字符串，两种都接住
            var v = r.GetValue(i);
            return v is Guid g ? g : Guid.TryParse(Convert.ToString(v), out var parsed) ? parsed : Guid.Empty;
        }

        private static DateTime Utc(MySqlDataReader r, int i) => DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc);

        // ===== 输入清理 =====

        /// <summary>控制字符换成空格（keepNewLines 时保留换行），去掉首尾空白，再截到最大长度</summary>
        public static string CleanText(string? s, int max, bool keepNewLines = false)
        {
            if (string.IsNullOrEmpty(s)) return "";
            var sb = new StringBuilder(s.Length);
            foreach (var ch in s)
            {
                if (ch == '\n' && keepNewLines) { sb.Append(ch); continue; }
                if (char.IsControl(ch)) { sb.Append(' '); continue; }
                sb.Append(ch);
            }
            var t = sb.ToString().Trim();
            return t.Length > max ? t[..max] : t;
        }

        private static bool IsHexColor(string? c) =>
            c is { Length: 7 } && c[0] == '#' && c.Skip(1).All(Uri.IsHexDigit);

        /// <summary>
        /// 检查并整理扇区：2–24 个、文字不能空、权重 1–1000、颜色必须是 #RRGGBB（不对就按调色板补）。
        /// 只有官方转盘（allowPrizes）才保留奖品，用户转盘一律改成「无」。
        /// </summary>
        public static (List<Segment>? Segments, string? Error) CleanSegments(List<Segment>? input, bool allowPrizes)
        {
            if (input == null || input.Count < MinSegments) return (null, $"至少要有 {MinSegments} 个扇区");
            if (input.Count > MaxSegments) return (null, $"最多 {MaxSegments} 个扇区");
            var list = new List<Segment>(input.Count);
            for (var i = 0; i < input.Count; i++)
            {
                var s = input[i];
                var text = CleanText(s?.Text, SegmentTextMax);
                if (text.Length == 0) return (null, $"第 {i + 1} 个扇区还没写文字");
                if (s!.Weight < 1 || s.Weight > WeightMax) return (null, $"第 {i + 1} 个扇区的权重要在 1 到 {WeightMax} 之间");
                var color = IsHexColor(s.Color) ? s.Color.ToUpperInvariant() : Palette[i % Palette.Length];
                var prizeType = "none";
                var amount = 0;
                if (allowPrizes && s.PrizeType is "potato" or "exp")
                {
                    var max = s.PrizeType == "potato" ? PotatoPrizeMax : ExpPrizeMax;
                    if (s.PrizeAmount < 1 || s.PrizeAmount > max)
                        return (null, $"第 {i + 1} 个扇区的奖品数量要在 1 到 {max} 之间");
                    prizeType = s.PrizeType;
                    amount = s.PrizeAmount;
                }
                list.Add(new Segment { Text = text, Color = color, Weight = s.Weight, PrizeType = prizeType, PrizeAmount = amount });
            }
            return (list, null);
        }

        /// <summary>整理标题、简介、可见范围；有问题返回错误提示</summary>
        private static (string Title, string Intro, string Visibility, List<Segment>? Segments, string? Error) CleanInput(WheelInput? input, bool official)
        {
            if (input == null) return ("", "", "", null, "内容不能为空");
            var title = CleanText(input.Title, TitleMax);
            if (title.Length == 0) return ("", "", "", null, "给转盘起个名字吧");
            var intro = CleanText(input.Intro, IntroMax, keepNewLines: true);
            // 官方转盘一律公开（上下架靠 Status）
            var visibility = official ? "public" : input.Visibility == "private" ? "private" : "public";
            var (segments, error) = CleanSegments(input.Segments, official);
            return (title, intro, visibility, segments, error);
        }

        /// <summary>随机生成分享码（给「仅自己」的转盘做链接用，别人猜不到）</summary>
        private static string NewShareCode()
        {
            const string chars = "abcdefghjkmnpqrstuvwxyz23456789";
            var sb = new StringBuilder(10);
            for (var i = 0; i < 10; i++) sb.Append(chars[RandomNumberGenerator.GetInt32(chars.Length)]);
            return sb.ToString();
        }

        /// <summary>按权重抽一个扇区（用加密随机数，结果没法预测）</summary>
        public static int PickWeighted(IReadOnlyList<Segment> segments)
        {
            var total = segments.Sum(s => Math.Max(1, s.Weight));
            var roll = RandomNumberGenerator.GetInt32(total);
            for (var i = 0; i < segments.Count; i++)
            {
                roll -= Math.Max(1, segments[i].Weight);
                if (roll < 0) return i;
            }
            return segments.Count - 1;
        }

        public static string PrizeLabel(string prizeType, int amount) => prizeType switch
        {
            "potato" => $"{amount:N0} 土豆",
            "exp" => $"{amount:N0} 经验",
            _ => "",
        };

        // ===== 读 =====

        private const string WheelColumns = "Id, Kind, OwnerId, Title, Intro, Visibility, ShareCode, SegmentsJson, RemoveOnHit, Status, HiddenReason, SpinCount, SortOrder, CreatedAt, UpdatedAt";

        private static WheelRow ReadWheel(MySqlDataReader r)
        {
            List<Segment>? segments = null;
            try { segments = JsonSerializer.Deserialize<List<Segment>>(r.GetString(7), Json); }
            catch (JsonException) { /* 数据坏了就当空转盘，不让整个列表报错 */ }
            return new WheelRow
            {
                Id = r.GetInt64(0),
                Kind = r.GetString(1),
                OwnerId = ReadGuid(r, 2),
                Title = r.GetString(3),
                Intro = r.GetString(4),
                Visibility = r.GetString(5),
                ShareCode = r.GetString(6),
                Segments = segments ?? new List<Segment>(),
                RemoveOnHit = r.GetBoolean(8),
                Status = r.GetString(9),
                HiddenReason = r.IsDBNull(10) ? null : r.GetString(10),
                SpinCount = r.GetInt64(11),
                SortOrder = r.GetInt32(12),
                CreatedAt = Utc(r, 13),
                UpdatedAt = Utc(r, 14),
            };
        }

        private static async Task<List<WheelRow>> ReadWheelsAsync(MySqlCommand cmd, CancellationToken ct)
        {
            var list = new List<WheelRow>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(ReadWheel(r));
            return list;
        }

        public async Task<WheelRow?> GetAsync(long id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {WheelColumns} FROM wheel_wheels WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            return (await ReadWheelsAsync(cmd, ct)).FirstOrDefault();
        }

        /// <summary>大厅里的公开转盘：sort = hot 按转动次数 / new 按最新；q 按标题搜</summary>
        public async Task<(List<WheelRow> Items, bool HasMore)> ListPublicAsync(string sort, string? q, int page, int pageSize, CancellationToken ct = default)
        {
            page = Math.Max(1, page);
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            var where = "Kind='user' AND Visibility='public' AND Status='active'";
            var keyword = CleanText(q, 40);
            if (keyword.Length > 0)
            {
                // LIKE 里的 % _ \ 要转义，不然用户搜「%」会匹配全部
                var escaped = keyword.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
                where += " AND Title LIKE @q";
                cmd.Parameters.AddWithValue("@q", "%" + escaped + "%");
            }
            var order = sort == "new" ? "Id DESC" : "SpinCount DESC, Id DESC";
            cmd.CommandText = $"SELECT {WheelColumns} FROM wheel_wheels WHERE {where} ORDER BY {order} LIMIT @take OFFSET @skip";
            cmd.Parameters.AddWithValue("@take", pageSize + 1);
            cmd.Parameters.AddWithValue("@skip", (page - 1) * pageSize);
            var rows = await ReadWheelsAsync(cmd, ct);
            var hasMore = rows.Count > pageSize;
            return (rows.Take(pageSize).ToList(), hasMore);
        }

        /// <summary>官方转盘（includeHidden：管理员看全部，包括下架的）</summary>
        public async Task<List<WheelRow>> ListOfficialAsync(bool includeHidden, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {WheelColumns} FROM wheel_wheels WHERE Kind='official'{(includeHidden ? "" : " AND Status='active'")} ORDER BY SortOrder, Id LIMIT 100";
            return await ReadWheelsAsync(cmd, ct);
        }

        public async Task<List<WheelRow>> ListMineAsync(Guid userId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {WheelColumns} FROM wheel_wheels WHERE Kind='user' AND OwnerId=@u ORDER BY Id DESC LIMIT 100";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            return await ReadWheelsAsync(cmd, ct);
        }

        public async Task<int> CountMineAsync(Guid userId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            return await CountMineAsync(c, null, userId, ct);
        }

        private static async Task<int> CountMineAsync(MySqlConnection c, MySqlTransaction? tx, Guid userId, CancellationToken ct)
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = "SELECT COUNT(*) FROM wheel_wheels WHERE Kind='user' AND OwnerId=@u";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        // ===== 经验表（表名、列名从 EF 模型里取，和 PotatoService 一样，避免和数据库命名不一致） =====

        private (string Stats, string StatsUser, string StatsExp, string Log, string LogUser, string LogChange, string LogReason, string LogCreated)? _expTables;

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

        /// <summary>在事务里改经验（delta 负数 = 扣，扣的时候经验不够就不改），同时写一条经验流水。返回是否成功</summary>
        private async Task<bool> ChangeExpAsync(MySqlConnection c, MySqlTransaction tx, Guid userId, int delta, string reason, CancellationToken ct)
        {
            var t = ExpTables();
            await using (var upd = c.CreateCommand())
            {
                upd.Transaction = tx;
                upd.CommandText = delta < 0
                    ? $"UPDATE `{t.Stats}` SET `{t.StatsExp}` = `{t.StatsExp}` + @d WHERE `{t.StatsUser}`=@u AND `{t.StatsExp}` >= @need"
                    : $"UPDATE `{t.Stats}` SET `{t.StatsExp}` = `{t.StatsExp}` + @d WHERE `{t.StatsUser}`=@u";
                upd.Parameters.AddWithValue("@d", delta);
                upd.Parameters.AddWithValue("@u", userId.ToString());
                if (delta < 0) upd.Parameters.AddWithValue("@need", -delta);
                if (await upd.ExecuteNonQueryAsync(ct) == 0) return false;
            }
            await using var log = c.CreateCommand();
            log.Transaction = tx;
            log.CommandText = $"INSERT INTO `{t.Log}` (`{t.LogUser}`, `{t.LogChange}`, `{t.LogReason}`, `{t.LogCreated}`) VALUES (@u, @c, @r, UTC_TIMESTAMP())";
            log.Parameters.AddWithValue("@u", userId.ToString());
            log.Parameters.AddWithValue("@c", delta);
            log.Parameters.AddWithValue("@r", reason.Length > 200 ? reason[..200] : reason);
            await log.ExecuteNonQueryAsync(ct);
            return true;
        }

        private async Task<long> ReadExpAsync(MySqlConnection c, MySqlTransaction? tx, Guid userId, CancellationToken ct)
        {
            var t = ExpTables();
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = $"SELECT `{t.StatsExp}` FROM `{t.Stats}` WHERE `{t.StatsUser}`=@u";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            var v = await cmd.ExecuteScalarAsync(ct);
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt64(v);
        }

        // ===== 写：用户转盘 =====

        /// <summary>同一个人同时点两次「创建」时排队，避免一起越过 30 个的上限</summary>
        private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _createLocks = new();

        private static async Task<long> InsertWheelAsync(MySqlConnection c, MySqlTransaction? tx, string kind, Guid ownerId, string title, string intro, string visibility,
            List<Segment> segments, bool removeOnHit, int sortOrder, string paidWith, int paidAmount, CancellationToken ct)
        {
            await using var cmd = c.CreateCommand();
            cmd.Transaction = tx;
            cmd.CommandText = @"INSERT INTO wheel_wheels (Kind, OwnerId, Title, Intro, Visibility, ShareCode, SegmentsJson, RemoveOnHit, Status, SpinCount, SortOrder, PaidWith, PaidAmount, CreatedAt, UpdatedAt)
VALUES (@k, @o, @t, @i, @v, @s, @j, @r, 'active', 0, @so, @pw, @pa, UTC_TIMESTAMP(), UTC_TIMESTAMP())";
            cmd.Parameters.AddWithValue("@k", kind);
            cmd.Parameters.AddWithValue("@o", ownerId.ToString());
            cmd.Parameters.AddWithValue("@t", title);
            cmd.Parameters.AddWithValue("@i", intro);
            cmd.Parameters.AddWithValue("@v", visibility);
            cmd.Parameters.AddWithValue("@s", NewShareCode());
            cmd.Parameters.AddWithValue("@j", JsonSerializer.Serialize(segments));
            cmd.Parameters.AddWithValue("@r", removeOnHit);
            cmd.Parameters.AddWithValue("@so", sortOrder);
            cmd.Parameters.AddWithValue("@pw", paidWith);
            cmd.Parameters.AddWithValue("@pa", paidAmount);
            await cmd.ExecuteNonQueryAsync(ct);
            return cmd.LastInsertedId;
        }

        /// <summary>
        /// 用户创建转盘：先检查数量上限，再按选的方式付费（经验 / 土豆），付费和建转盘要么都成功要么都不算。
        /// cost 小于 0 表示站长关掉了这种付费方式；等于 0 表示免费。
        /// </summary>
        public async Task<(long Id, string? Error)> CreateUserWheelAsync(Guid userId, WheelInput input, string payWith, int costExp, int costPotato, CancellationToken ct = default)
        {
            var (title, intro, visibility, segments, error) = CleanInput(input, official: false);
            if (error != null || segments == null) return (0, error ?? "内容不对");
            if (payWith is not ("exp" or "potato")) return (0, "请选择用经验还是土豆付");
            var cost = payWith == "exp" ? costExp : costPotato;
            if (cost < 0) return (0, payWith == "exp" ? "现在不能用经验创建转盘，换土豆试试" : "现在不能用土豆创建转盘，换经验试试");

            var gate = _createLocks.GetOrAdd(userId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(ct);
            try
            {
                await using var c = await OpenAsync(ct);
                if (await CountMineAsync(c, null, userId, ct) >= MaxPerUser)
                    return (0, $"每人最多做 {MaxPerUser} 个转盘，删掉几个不用的再来吧");

                if (payWith == "exp")
                {
                    // 扣经验、写经验流水、建转盘放在同一个事务里：任何一步失败都不扣经验
                    await using var tx = await c.BeginTransactionAsync(ct);
                    if (cost > 0 && !await ChangeExpAsync(c, tx, userId, -cost, $"创建转盘「{title}」", ct))
                    {
                        var have = await ReadExpAsync(c, tx, userId, ct);
                        await tx.RollbackAsync(ct);
                        return (0, $"经验不够：创建转盘需要 {cost} 经验，你现在有 {have}");
                    }
                    var id = await InsertWheelAsync(c, tx, "user", userId, title, intro, visibility, segments, input.RemoveOnHit, 0, "exp", Math.Max(0, cost), ct);
                    await tx.CommitAsync(ct);
                    return (id, null);
                }

                // 土豆：先建转盘拿到编号，再用 PotatoService 扣（流水里能看到是哪个转盘）；扣不成就把刚建的删掉
                var newId = await InsertWheelAsync(c, null, "user", userId, title, intro, visibility, segments, input.RemoveOnHit, 0, "potato", Math.Max(0, cost), ct);
                if (cost > 0)
                {
                    string? spendError;
                    try
                    {
                        var (ok, _, err) = await potatoes.SpendAsync(userId, cost, $"创建转盘「{title}」", "wheel", newId.ToString(), ct);
                        spendError = ok ? null : err ?? "土豆不够";
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "创建转盘扣土豆失败 user={User}", userId);
                        spendError = "扣土豆的时候出错了，稍后再试";
                    }
                    if (spendError != null)
                    {
                        await DeleteRawAsync(c, newId, CancellationToken.None);
                        return (0, spendError);
                    }
                }
                return (newId, null);
            }
            finally { gate.Release(); }
        }

        private static async Task DeleteRawAsync(MySqlConnection c, long id, CancellationToken ct)
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM wheel_wheels WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <summary>编辑自己的转盘（不收费）。被管理员下架的转盘编辑后仍然是下架状态</summary>
        public async Task<string?> UpdateUserWheelAsync(Guid userId, long id, WheelInput input, CancellationToken ct = default)
        {
            var (title, intro, visibility, segments, error) = CleanInput(input, official: false);
            if (error != null || segments == null) return error ?? "内容不对";
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"UPDATE wheel_wheels SET Title=@t, Intro=@i, Visibility=@v, SegmentsJson=@j, RemoveOnHit=@r, UpdatedAt=UTC_TIMESTAMP()
WHERE Id=@id AND OwnerId=@u AND Kind='user'";
            cmd.Parameters.AddWithValue("@t", title);
            cmd.Parameters.AddWithValue("@i", intro);
            cmd.Parameters.AddWithValue("@v", visibility);
            cmd.Parameters.AddWithValue("@j", JsonSerializer.Serialize(segments));
            cmd.Parameters.AddWithValue("@r", input.RemoveOnHit);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            return await cmd.ExecuteNonQueryAsync(ct) == 0 ? "这个转盘不存在，或者不是你的" : null;
        }

        /// <summary>删除自己的转盘（不退费）</summary>
        public async Task<bool> DeleteUserWheelAsync(Guid userId, long id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM wheel_wheels WHERE Id=@id AND OwnerId=@u AND Kind='user'";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            return await cmd.ExecuteNonQueryAsync(ct) > 0;
        }

        // ===== 转动次数（用户转盘，只计数不记录结果） =====

        private readonly ConcurrentDictionary<string, DateTime> _spinGates = new();

        /// <summary>
        /// 用户转盘转了一次：次数 +1。同一个人（或同一个 IP）同一个转盘 2 秒内只算一次，防止刷热度。
        /// </summary>
        public async Task<bool> CountSpinAsync(long id, string who, CancellationToken ct = default)
        {
            var now = DateTime.UtcNow;
            if (_spinGates.Count > 20000)
                foreach (var kv in _spinGates)
                    if ((now - kv.Value).TotalMinutes > 5) _spinGates.TryRemove(kv.Key, out _);
            var key = id + ":" + who;
            if (_spinGates.TryGetValue(key, out var last) && (now - last).TotalSeconds < 2) return false;
            _spinGates[key] = now;

            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE wheel_wheels SET SpinCount=SpinCount+1 WHERE Id=@id AND Kind='user' AND Status='active'";
            cmd.Parameters.AddWithValue("@id", id);
            return await cmd.ExecuteNonQueryAsync(ct) > 0;
        }

        // ===== 官方转盘：管理员 =====

        public async Task<(long Id, string? Error)> CreateOfficialAsync(Guid adminId, WheelInput input, CancellationToken ct = default)
        {
            var (title, intro, _, segments, error) = CleanInput(input, official: true);
            if (error != null || segments == null) return (0, error ?? "内容不对");
            await using var c = await OpenAsync(ct);
            var id = await InsertWheelAsync(c, null, "official", adminId, title, intro, "public", segments, false, input.SortOrder, "", 0, ct);
            return (id, null);
        }

        public async Task<string?> UpdateOfficialAsync(long id, WheelInput input, CancellationToken ct = default)
        {
            var (title, intro, _, segments, error) = CleanInput(input, official: true);
            if (error != null || segments == null) return error ?? "内容不对";
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"UPDATE wheel_wheels SET Title=@t, Intro=@i, SegmentsJson=@j, SortOrder=@so, UpdatedAt=UTC_TIMESTAMP()
WHERE Id=@id AND Kind='official'";
            cmd.Parameters.AddWithValue("@t", title);
            cmd.Parameters.AddWithValue("@i", intro);
            cmd.Parameters.AddWithValue("@j", JsonSerializer.Serialize(segments));
            cmd.Parameters.AddWithValue("@so", input.SortOrder);
            cmd.Parameters.AddWithValue("@id", id);
            return await cmd.ExecuteNonQueryAsync(ct) == 0 ? "这个官方转盘不存在" : null;
        }

        public async Task<bool> DeleteOfficialAsync(long id, CancellationToken ct = default)
        {
            // 抽奖记录（wheel_official_spins）留着：里面存了转盘名字和奖品，删了转盘也查得到当时发了什么
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM wheel_wheels WHERE Id=@id AND Kind='official'";
            cmd.Parameters.AddWithValue("@id", id);
            return await cmd.ExecuteNonQueryAsync(ct) > 0;
        }

        /// <summary>上架 / 下架（官方转盘上下架；用户转盘被举报后管理员下架）。status：active / hidden</summary>
        public async Task<bool> SetStatusAsync(long id, string status, string? reason, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE wheel_wheels SET Status=@s, HiddenReason=@r, UpdatedAt=UTC_TIMESTAMP() WHERE Id=@id";
            cmd.Parameters.AddWithValue("@s", status == "hidden" ? "hidden" : "active");
            var why = CleanText(reason, 200);
            cmd.Parameters.AddWithValue("@r", status == "hidden" && why.Length > 0 ? (object)why : DBNull.Value);
            cmd.Parameters.AddWithValue("@id", id);
            return await cmd.ExecuteNonQueryAsync(ct) > 0;
        }

        // ===== 官方转盘：转 =====

        /// <summary>
        /// 转一次官方转盘：服务器按权重抽扇区 → 写抽奖记录（唯一索引保证同一人同一转盘每天只成功一次）→ 发奖。
        /// 经验奖品和抽奖记录在同一个事务里；土豆走 PotatoService（它自己有事务），记录上用 Granted 标记有没有发成功，没发成功的下次打开页面时补发。
        /// </summary>
        public async Task<SpinResult> SpinOfficialAsync(Guid userId, long wheelId)
        {
            // 抽完以后不管浏览器有没有断开都要把奖发完，所以这里不用请求的取消令牌
            var ct = CancellationToken.None;
            var wheel = await GetAsync(wheelId, ct);
            if (wheel == null || wheel.Kind != "official") return new SpinResult(false, "这个转盘不存在", -1, null, null, null);
            if (wheel.Status != "active") return new SpinResult(false, "这个转盘已经下架了", -1, null, null, null);
            if (wheel.Segments.Count < MinSegments) return new SpinResult(false, "这个转盘还没设置好", -1, null, null, null);

            var index = PickWeighted(wheel.Segments);
            var seg = wheel.Segments[index];
            var today = ChinaClock.Today;

            await using var c = await OpenAsync(ct);
            await using var tx = await c.BeginTransactionAsync(ct);
            long spinId;
            try
            {
                await using var ins = c.CreateCommand();
                ins.Transaction = tx;
                ins.CommandText = @"INSERT INTO wheel_official_spins (WheelId, UserId, SpinDate, WheelTitle, SegmentIndex, SegmentText, PrizeType, PrizeAmount, Granted, CreatedAt)
VALUES (@w, @u, @d, @wt, @i, @st, @pt, @pa, @g, UTC_TIMESTAMP())";
                ins.Parameters.AddWithValue("@w", wheelId);
                ins.Parameters.AddWithValue("@u", userId.ToString());
                ins.Parameters.AddWithValue("@d", today);
                ins.Parameters.AddWithValue("@wt", wheel.Title);
                ins.Parameters.AddWithValue("@i", index);
                ins.Parameters.AddWithValue("@st", seg.Text);
                ins.Parameters.AddWithValue("@pt", seg.PrizeType);
                ins.Parameters.AddWithValue("@pa", seg.PrizeAmount);
                // 土豆要等 PotatoService 发完才算发了（0）；经验在本事务里发、没奖品的直接算发完（1）
                ins.Parameters.AddWithValue("@g", seg.PrizeType == "potato" ? 0 : 1);
                await ins.ExecuteNonQueryAsync(ct);
                spinId = ins.LastInsertedId;
            }
            catch (MySqlException ex) when (ex.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
            {
                await tx.RollbackAsync(ct);
                return new SpinResult(false, "今天已经转过这个转盘了，明天再来吧", -1, null, null, null);
            }

            if (seg.PrizeType == "exp" && seg.PrizeAmount > 0
                && !await ChangeExpAsync(c, tx, userId, seg.PrizeAmount, $"幸运转盘「{wheel.Title}」抽中 {seg.PrizeAmount} 经验", ct))
            {
                // 正常不会走到这里（每个用户都有经验记录）；万一没有就整次作废，今天还能再转
                await tx.RollbackAsync(ct);
                logger.LogWarning("官方转盘发经验失败：找不到用户经验记录 user={User}", userId);
                return new SpinResult(false, "你的账号数据不完整，没法领经验，请联系管理员", -1, null, null, null);
            }

            await using (var cnt = c.CreateCommand())
            {
                cnt.Transaction = tx;
                cnt.CommandText = "UPDATE wheel_wheels SET SpinCount=SpinCount+1 WHERE Id=@id";
                cnt.Parameters.AddWithValue("@id", wheelId);
                await cnt.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);

            var message = seg.PrizeType switch
            {
                "potato" => $"恭喜！抽中 {seg.PrizeAmount:N0} 土豆，已经放进你的土豆钱包",
                "exp" => $"恭喜！抽中 {seg.PrizeAmount:N0} 经验，已经加到你的账号",
                _ => $"转到了「{seg.Text}」，明天再来试试手气",
            };
            if (seg.PrizeType == "potato" && seg.PrizeAmount > 0 && !await GrantPotatoAsync(spinId, userId, seg.PrizeAmount, wheel.Title))
                message = $"抽中 {seg.PrizeAmount:N0} 土豆！发放时出了点问题，系统会自动补发，过一会儿刷新看看";
            return new SpinResult(true, null, index, seg, wheel.Segments, message);
        }

        /// <summary>
        /// 发土豆奖品。先把记录从 0（没发）改成 2（正在发），改成功的那个请求才去发，避免两个请求同时补发发两次；
        /// 发成功改成 1，失败改回 0 等下次补发。
        /// </summary>
        private async Task<bool> GrantPotatoAsync(long spinId, Guid userId, int amount, string wheelTitle)
        {
            var ct = CancellationToken.None;
            try
            {
                await using var c = await OpenAsync(ct);
                await using (var claim = c.CreateCommand())
                {
                    claim.CommandText = "UPDATE wheel_official_spins SET Granted=2 WHERE Id=@id AND Granted=0";
                    claim.Parameters.AddWithValue("@id", spinId);
                    if (await claim.ExecuteNonQueryAsync(ct) == 0) return true; // 别的请求已经在发 / 发过了
                }
                try
                {
                    await potatoes.ApplyAsync(new[] { new PotatoService.Change(userId, amount, $"幸运转盘「{wheelTitle}」奖品", "wheel", spinId.ToString()) }, ct);
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "官方转盘发土豆失败 spin={Spin} user={User}", spinId, userId);
                    await using var back = c.CreateCommand();
                    back.CommandText = "UPDATE wheel_official_spins SET Granted=0 WHERE Id=@id AND Granted=2";
                    back.Parameters.AddWithValue("@id", spinId);
                    await back.ExecuteNonQueryAsync(ct);
                    return false;
                }
                await using var done = c.CreateCommand();
                done.CommandText = "UPDATE wheel_official_spins SET Granted=1 WHERE Id=@id";
                done.Parameters.AddWithValue("@id", spinId);
                await done.ExecuteNonQueryAsync(ct);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "官方转盘发土豆出错 spin={Spin}", spinId);
                return false;
            }
        }

        /// <summary>补发这个人之前没发成功的土豆奖品（打开转盘页时顺便调用）</summary>
        public async Task RetryPendingAsync(Guid userId, CancellationToken ct = default)
        {
            var pending = new List<(long Id, int Amount, string Title)>();
            await using (var c = await OpenAsync(ct))
            await using (var cmd = c.CreateCommand())
            {
                cmd.CommandText = @"SELECT Id, PrizeAmount, WheelTitle FROM wheel_official_spins
WHERE UserId=@u AND Granted=0 AND PrizeType='potato' AND CreatedAt < UTC_TIMESTAMP() - INTERVAL 1 MINUTE ORDER BY Id LIMIT 10";
                cmd.Parameters.AddWithValue("@u", userId.ToString());
                await using var r = await cmd.ExecuteReaderAsync(ct);
                while (await r.ReadAsync(ct)) pending.Add((r.GetInt64(0), r.GetInt32(1), r.GetString(2)));
            }
            foreach (var p in pending) await GrantPotatoAsync(p.Id, userId, p.Amount, p.Title);
        }

        private static SpinLog ReadSpin(MySqlDataReader r) => new(
            r.GetInt64(0), r.GetInt64(1), ReadGuid(r, 2), r.GetString(3), r.GetInt32(4), r.GetString(5), r.GetString(6), r.GetInt32(7),
            r.GetDateTime(8).Date, Utc(r, 9));

        private const string SpinColumns = "Id, WheelId, UserId, WheelTitle, SegmentIndex, SegmentText, PrizeType, PrizeAmount, SpinDate, CreatedAt";

        /// <summary>这个人今天（北京时间）在各个官方转盘上转的结果</summary>
        public async Task<List<SpinLog>> TodaySpinsAsync(Guid userId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {SpinColumns} FROM wheel_official_spins WHERE UserId=@u AND SpinDate=@d";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            cmd.Parameters.AddWithValue("@d", ChinaClock.Today);
            var list = new List<SpinLog>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(ReadSpin(r));
            return list;
        }

        /// <summary>我最近的抽奖记录</summary>
        public async Task<List<SpinLog>> MySpinsAsync(Guid userId, int limit, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {SpinColumns} FROM wheel_official_spins WHERE UserId=@u ORDER BY Id DESC LIMIT @l";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            cmd.Parameters.AddWithValue("@l", Math.Clamp(limit, 1, 50));
            var list = new List<SpinLog>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(ReadSpin(r));
            return list;
        }

        /// <summary>全站最近中奖（只要有奖品的）</summary>
        public async Task<List<SpinLog>> RecentWinsAsync(int limit, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {SpinColumns} FROM wheel_official_spins WHERE PrizeType<>'none' ORDER BY Id DESC LIMIT @l";
            cmd.Parameters.AddWithValue("@l", Math.Clamp(limit, 1, 50));
            var list = new List<SpinLog>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(ReadSpin(r));
            return list;
        }
    }
}
