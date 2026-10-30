using System.Text.RegularExpressions;
using MySqlConnector;
using TaiChuWeb_V2.Services.Chat;

namespace TaiChuWeb_V2.Services.Shop
{
    /// <summary>商品的扩展设置：土豆价、效果（头衔 / 头像框 / 改名卡）</summary>
    public sealed record StoreItemExt(int StoreItemId, long? PotatoCost, string? Effect, string? EffectValue);

    /// <summary>
    /// 交易行扩展（不改原来的 EF 表，扩展数据放在自己建的表里，第一次用时自动创建）：
    /// · store_item_ext：商品的土豆价、效果；
    /// · user_cosmetics：用户拥有的头衔、头像框、官方背景（Kind = title / frame / bg）；
    /// · user_cosmetic_equip：正在佩戴的头像框（头衔沿用 UserStats.Title）；
    /// · user_item_counts：可叠加的道具（目前只有改名卡）；
    /// · avatar_frame_defs：后台上传的图片头像框（key 是 img-{Id}；代码里写好的 8 种 CSS 框不在这张表里）；
    /// · avatar_defs：官方头像（静态 / 动图）。用户自己只能上传静态头像，GIF 等动图头像只能用官方的；没上架成商品的官方头像所有人免费用。
    /// 素材可以填「原作者」：作者账号自动免费拥有（user_cosmetics 里 Source = 原作者）。
    /// 规则：发卡密 / 链接的商品（游戏 CDKey、网盘资源）只能用经验买；土豆只能买站内的装饰和功能。
    /// </summary>
    public sealed class ShopService(IConfiguration configuration)
    {
        public const string EffectTitle = "title";
        public const string EffectFrame = "frame";
        public const string EffectRename = "rename";
        /// <summary>官方背景：EffectValue 是 site_backgrounds 的 Id。设成商品后，这个背景要先兑换才能用</summary>
        public const string EffectBackground = "bg";
        /// <summary>个人主页背景（横幅 / 整页）：EffectValue 是 profile_backgrounds 的 Id</summary>
        public const string EffectProfileBg = "pbg";
        public const string AuthorSource = "原作者";
        /// <summary>官方头像：EffectValue 是 avatar_defs 的 Id</summary>
        public const string EffectAvatar = "avatar";
        /// <summary>买了会进「我的装扮」、只能拥有一份的效果</summary>
        public static bool IsCosmetic(string? effect) => effect is EffectTitle or EffectFrame or EffectBackground or EffectProfileBg or EffectAvatar;
        /// <summary>图片头像框的 key</summary>
        public static string ImageFrameKey(int id) => $"img-{id}";
        public static int? ImageFrameId(string? key) => key != null && key.StartsWith("img-") && int.TryParse(key[4..], out var id) && id > 0 ? id : null;
        public const string RenameCardKey = "rename-card";
        public const int MaxTitleLength = 12;
        private static readonly Regex FrameKeyRe = new("^[a-z0-9-]{2,30}$", RegexOptions.Compiled);

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
CREATE TABLE IF NOT EXISTS store_item_ext (
  StoreItemId INT NOT NULL PRIMARY KEY,
  PotatoCost BIGINT NULL,
  Effect VARCHAR(20) NULL,
  EffectValue VARCHAR(100) NULL,
  UpdatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS user_cosmetics (
  UserId CHAR(36) NOT NULL,
  Kind VARCHAR(10) NOT NULL,
  ItemKey VARCHAR(100) NOT NULL,
  Source VARCHAR(60) NOT NULL,
  CreatedAt DATETIME NOT NULL,
  PRIMARY KEY (UserId, Kind, ItemKey)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS user_cosmetic_equip (
  UserId CHAR(36) NOT NULL PRIMARY KEY,
  Frame VARCHAR(40) NULL,
  UpdatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS avatar_frame_defs (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  Name VARCHAR(40) NOT NULL,
  Url VARCHAR(1000) NOT NULL,
  Scale DOUBLE NOT NULL DEFAULT 1.4,
  AuthorId CHAR(36) NULL,
  SortOrder INT NOT NULL DEFAULT 0,
  Enabled TINYINT(1) NOT NULL DEFAULT 1,
  CreatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS avatar_defs (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  Name VARCHAR(40) NOT NULL,
  Url VARCHAR(1000) NOT NULL,
  Animated TINYINT(1) NOT NULL DEFAULT 0,
  AuthorId CHAR(36) NULL,
  SortOrder INT NOT NULL DEFAULT 0,
  Enabled TINYINT(1) NOT NULL DEFAULT 1,
  CreatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS user_item_counts (
  UserId CHAR(36) NOT NULL,
  ItemKey VARCHAR(40) NOT NULL,
  Count INT NOT NULL DEFAULT 0,
  UpdatedAt DATETIME NOT NULL,
  PRIMARY KEY (UserId, ItemKey)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                await cmd.ExecuteNonQueryAsync(ct);
                _ready = true;
            }
            finally { _schemaLock.Release(); }
        }

        // ================= 商品扩展 =================

        public static string? ValidateExt(string? effect, string? value, out string? cleanEffect, out string? cleanValue)
        {
            cleanEffect = string.IsNullOrWhiteSpace(effect) ? null : effect.Trim();
            cleanValue = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
            switch (cleanEffect)
            {
                case null: cleanValue = null; return null;
                case EffectTitle:
                    if (cleanValue == null) return "头衔商品要填头衔文字";
                    if (cleanValue.Length > MaxTitleLength) return $"头衔最多 {MaxTitleLength} 个字";
                    return null;
                case EffectFrame:
                    if (cleanValue == null || !FrameKeyRe.IsMatch(cleanValue)) return "请选择头像框样式";
                    return null;
                case EffectRename:
                    cleanValue = null;
                    return null;
                case EffectBackground:
                case EffectProfileBg:
                case EffectAvatar:
                    if (!int.TryParse(cleanValue, out var bgId) || bgId <= 0) return cleanEffect == EffectAvatar ? "请选择要出售的头像" : "请选择要出售的背景";
                    cleanValue = bgId.ToString();
                    return null;
                default: return "不支持的商品效果";
            }
        }

        public async Task<Dictionary<int, StoreItemExt>> ExtAsync(IEnumerable<int> ids, CancellationToken ct = default)
        {
            var list = ids.Distinct().ToList();
            var result = new Dictionary<int, StoreItemExt>();
            if (list.Count == 0) return result;
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            var names = list.Select((_, i) => $"@i{i}").ToList();
            cmd.CommandText = $"SELECT StoreItemId, PotatoCost, Effect, EffectValue FROM store_item_ext WHERE StoreItemId IN ({string.Join(",", names)})";
            for (var i = 0; i < list.Count; i++) cmd.Parameters.AddWithValue(names[i], list[i]);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var id = r.GetInt32(0);
                result[id] = new StoreItemExt(id, r.IsDBNull(1) ? null : r.GetInt64(1), r.IsDBNull(2) ? null : r.GetString(2), r.IsDBNull(3) ? null : r.GetString(3));
            }
            return result;
        }

        public async Task SaveExtAsync(int id, long? potatoCost, string? effect, string? value, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"INSERT INTO store_item_ext (StoreItemId, PotatoCost, Effect, EffectValue, UpdatedAt) VALUES (@id,@p,@e,@v,@t)
ON DUPLICATE KEY UPDATE PotatoCost=@p, Effect=@e, EffectValue=@v, UpdatedAt=@t";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@p", potatoCost is > 0 ? potatoCost : null);
            cmd.Parameters.AddWithValue("@e", effect);
            cmd.Parameters.AddWithValue("@v", value);
            cmd.Parameters.AddWithValue("@t", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <summary>设成了商品的官方背景：背景 Id → 商品 Id（这些背景要先兑换才能用）</summary>
        public Task<Dictionary<int, int>> BackgroundItemsAsync(CancellationToken ct = default) => ItemsForEffectAsync(EffectBackground, ct);

        /// <summary>某种效果（bg / pbg）的商品：素材 Id → 商品 Id</summary>
        public async Task<Dictionary<int, int>> ItemsForEffectAsync(string effect, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT StoreItemId, EffectValue FROM store_item_ext WHERE Effect = @e";
            cmd.Parameters.AddWithValue("@e", effect);
            var result = new Dictionary<int, int>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                if (!r.IsDBNull(1) && int.TryParse(r.GetString(1), out var bg)) result.TryAdd(bg, r.GetInt32(0));
            }
            return result;
        }

        // ================= 装饰（头衔 / 头像框 / 官方背景） =================

        public sealed record Cosmetic(string Kind, string Key, string Source, DateTime CreatedAt);

        public async Task<List<Cosmetic>> CosmeticsAsync(Guid u, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Kind, ItemKey, Source, CreatedAt FROM user_cosmetics WHERE UserId=@u ORDER BY CreatedAt";
            cmd.Parameters.AddWithValue("@u", u.ToString());
            var list = new List<Cosmetic>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(new Cosmetic(r.GetString(0), r.GetString(1), r.GetString(2), r.GetDateTime(3)));
            return list;
        }

        public async Task<bool> OwnsAsync(Guid u, string kind, string key, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM user_cosmetics WHERE UserId=@u AND Kind=@k AND ItemKey=@i";
            cmd.Parameters.AddWithValue("@u", u.ToString());
            cmd.Parameters.AddWithValue("@k", kind);
            cmd.Parameters.AddWithValue("@i", key);
            return Convert.ToInt64(await cmd.ExecuteScalarAsync(ct)) > 0;
        }

        /// <summary>发放装饰，已经有了返回 false</summary>
        public async Task<bool> GrantCosmeticAsync(Guid u, string kind, string key, string source, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "INSERT IGNORE INTO user_cosmetics (UserId, Kind, ItemKey, Source, CreatedAt) VALUES (@u,@k,@i,@s,@t)";
            cmd.Parameters.AddWithValue("@u", u.ToString());
            cmd.Parameters.AddWithValue("@k", kind);
            cmd.Parameters.AddWithValue("@i", key);
            cmd.Parameters.AddWithValue("@s", source.Length > 60 ? source[..60] : source);
            cmd.Parameters.AddWithValue("@t", DateTime.UtcNow);
            return await cmd.ExecuteNonQueryAsync(ct) > 0;
        }

        /// <summary>收回装饰（管理员收回头衔用），没有返回 false</summary>
        public async Task<bool> RevokeCosmeticAsync(Guid u, string kind, string key, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM user_cosmetics WHERE UserId=@u AND Kind=@k AND ItemKey=@i";
            cmd.Parameters.AddWithValue("@u", u.ToString());
            cmd.Parameters.AddWithValue("@k", kind);
            cmd.Parameters.AddWithValue("@i", key);
            return await cmd.ExecuteNonQueryAsync(ct) > 0;
        }

        public async Task<string?> EquippedFrameAsync(Guid u, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Frame FROM user_cosmetic_equip WHERE UserId=@u";
            cmd.Parameters.AddWithValue("@u", u.ToString());
            return await cmd.ExecuteScalarAsync(ct) as string;
        }

        public async Task<Dictionary<Guid, string>> EquippedFramesAsync(IEnumerable<Guid> users, CancellationToken ct = default)
        {
            var list = users.Distinct().Take(200).ToList();
            var result = new Dictionary<Guid, string>();
            if (list.Count == 0) return result;
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            var names = list.Select((_, i) => $"@u{i}").ToList();
            cmd.CommandText = $"SELECT UserId, Frame FROM user_cosmetic_equip WHERE Frame IS NOT NULL AND UserId IN ({string.Join(",", names)})";
            for (var i = 0; i < list.Count; i++) cmd.Parameters.AddWithValue(names[i], list[i].ToString());
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) result[ChatStore.ReadGuid(r, 0)] = r.GetString(1);
            return result;
        }

        public async Task SetFrameAsync(Guid u, string? frame, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "INSERT INTO user_cosmetic_equip (UserId, Frame, UpdatedAt) VALUES (@u,@f,@t) ON DUPLICATE KEY UPDATE Frame=@f, UpdatedAt=@t";
            cmd.Parameters.AddWithValue("@u", u.ToString());
            cmd.Parameters.AddWithValue("@f", frame);
            cmd.Parameters.AddWithValue("@t", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // ================= 可叠加道具（改名卡） =================

        public async Task<int> CountAsync(Guid u, string key, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Count FROM user_item_counts WHERE UserId=@u AND ItemKey=@k";
            cmd.Parameters.AddWithValue("@u", u.ToString());
            cmd.Parameters.AddWithValue("@k", key);
            var v = await cmd.ExecuteScalarAsync(ct);
            return v == null || v == DBNull.Value ? 0 : Convert.ToInt32(v);
        }

        public async Task AddCountAsync(Guid u, string key, int delta, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "INSERT INTO user_item_counts (UserId, ItemKey, Count, UpdatedAt) VALUES (@u,@k,GREATEST(0,@d),@t) ON DUPLICATE KEY UPDATE Count=GREATEST(0, Count+@d), UpdatedAt=@t";
            cmd.Parameters.AddWithValue("@u", u.ToString());
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@d", delta);
            cmd.Parameters.AddWithValue("@t", DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <summary>原子地用掉一个道具，没有了返回 false</summary>
        public async Task<bool> ConsumeAsync(Guid u, string key, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE user_item_counts SET Count=Count-1, UpdatedAt=@t WHERE UserId=@u AND ItemKey=@k AND Count>0";
            cmd.Parameters.AddWithValue("@u", u.ToString());
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@t", DateTime.UtcNow);
            return await cmd.ExecuteNonQueryAsync(ct) > 0;
        }
    
        // ================= 图片头像框（后台上传） =================

        /// <param name="Scale">框图相对头像的大小（1.4 = 比头像大 40%，框图居中套在头像外面）</param>
        public sealed record FrameDef(int Id, string Key, string Name, string Url, double Scale, Guid? AuthorId, int SortOrder, bool Enabled, DateTime CreatedAt);

        private static FrameDef ReadFrameDef(MySqlDataReader r)
        {
            var id = Convert.ToInt32(r.GetValue(0));
            Guid? author = r.IsDBNull(4) ? null : ChatStore.ReadGuid(r, 4);
            return new FrameDef(id, ImageFrameKey(id), r.GetString(1), r.GetString(2), Convert.ToDouble(r.GetValue(3)), author,
                Convert.ToInt32(r.GetValue(5)), Convert.ToBoolean(r.GetValue(6)), DateTime.SpecifyKind(r.GetDateTime(7), DateTimeKind.Utc));
        }

        public async Task<List<FrameDef>> ListFrameDefsAsync(bool enabledOnly, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Url, Scale, AuthorId, SortOrder, Enabled, CreatedAt FROM avatar_frame_defs"
                + (enabledOnly ? " WHERE Enabled = 1" : "") + " ORDER BY SortOrder, Id";
            var list = new List<FrameDef>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(ReadFrameDef(r));
            return list;
        }

        public async Task<FrameDef?> GetFrameDefAsync(int id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Id, Name, Url, Scale, AuthorId, SortOrder, Enabled, CreatedAt FROM avatar_frame_defs WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            return await r.ReadAsync(ct) ? ReadFrameDef(r) : null;
        }

        /// <summary>新增（id 为空）或修改图片头像框，返回 Id</summary>
        public async Task<int> SaveFrameDefAsync(int? id, string name, string url, double scale, Guid? authorId, int sortOrder, bool enabled, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            if (id is int existing)
            {
                cmd.CommandText = "UPDATE avatar_frame_defs SET Name=@n, Url=@url, Scale=@s, AuthorId=@a, SortOrder=@o, Enabled=@e WHERE Id=@id";
                cmd.Parameters.AddWithValue("@id", existing);
            }
            else
            {
                cmd.CommandText = "INSERT INTO avatar_frame_defs (Name, Url, Scale, AuthorId, SortOrder, Enabled, CreatedAt) VALUES (@n,@url,@s,@a,@o,@e,@t); SELECT LAST_INSERT_ID();";
                cmd.Parameters.AddWithValue("@t", DateTime.UtcNow);
            }
            cmd.Parameters.AddWithValue("@n", name);
            cmd.Parameters.AddWithValue("@url", url);
            cmd.Parameters.AddWithValue("@s", Math.Clamp(scale, 1.0, 2.0));
            cmd.Parameters.AddWithValue("@a", authorId == null ? DBNull.Value : (object)authorId.Value.ToString());
            cmd.Parameters.AddWithValue("@o", sortOrder);
            cmd.Parameters.AddWithValue("@e", enabled);
            if (id is int updated) { await cmd.ExecuteNonQueryAsync(ct); return updated; }
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        /// <summary>删除图片头像框；戴着它的人自动取下（已拥有的记录保留，不再显示）</summary>
        public async Task DeleteFrameDefAsync(int id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM avatar_frame_defs WHERE Id=@id; UPDATE user_cosmetic_equip SET Frame=NULL WHERE Frame=@k";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@k", ImageFrameKey(id));
            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <summary>原作者免费拥有：发放给作者（重复调用不会重复发）</summary>
        public Task GrantToAuthorAsync(Guid? authorId, string kind, string key, CancellationToken ct = default)
            => authorId is Guid a && a != Guid.Empty ? GrantCosmeticAsync(a, kind, key, AuthorSource, ct) : Task.CompletedTask;
    
        // ================= 官方头像（静态 / 动图） =================

        public sealed record AvatarDef(int Id, string Name, string Url, bool Animated, Guid? AuthorId, int SortOrder, bool Enabled, DateTime CreatedAt);

        private const string AvatarColumns = "Id, Name, Url, Animated, AuthorId, SortOrder, Enabled, CreatedAt";

        private static AvatarDef ReadAvatarDef(MySqlDataReader r) => new(
            Convert.ToInt32(r.GetValue(0)), r.GetString(1), r.GetString(2), Convert.ToBoolean(r.GetValue(3)),
            r.IsDBNull(4) ? null : ChatStore.ReadGuid(r, 4),
            Convert.ToInt32(r.GetValue(5)), Convert.ToBoolean(r.GetValue(6)), DateTime.SpecifyKind(r.GetDateTime(7), DateTimeKind.Utc));

        public async Task<List<AvatarDef>> ListAvatarDefsAsync(bool enabledOnly, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {AvatarColumns} FROM avatar_defs" + (enabledOnly ? " WHERE Enabled = 1" : "") + " ORDER BY SortOrder, Id";
            var list = new List<AvatarDef>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(ReadAvatarDef(r));
            return list;
        }

        public async Task<AvatarDef?> GetAvatarDefAsync(int id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {AvatarColumns} FROM avatar_defs WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            return await r.ReadAsync(ct) ? ReadAvatarDef(r) : null;
        }

        /// <summary>按图片地址找官方头像（检查用户设置的头像是不是官方的）</summary>
        public async Task<AvatarDef?> FindAvatarDefByUrlAsync(string url, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {AvatarColumns} FROM avatar_defs WHERE Url = @u ORDER BY Id LIMIT 1";
            cmd.Parameters.AddWithValue("@u", url);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            return await r.ReadAsync(ct) ? ReadAvatarDef(r) : null;
        }

        public async Task<int> SaveAvatarDefAsync(int? id, string name, string url, bool animated, Guid? authorId, int sortOrder, bool enabled, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            if (id is int existing)
            {
                cmd.CommandText = "UPDATE avatar_defs SET Name=@n, Url=@url, Animated=@an, AuthorId=@a, SortOrder=@o, Enabled=@e WHERE Id=@id";
                cmd.Parameters.AddWithValue("@id", existing);
            }
            else
            {
                cmd.CommandText = "INSERT INTO avatar_defs (Name, Url, Animated, AuthorId, SortOrder, Enabled, CreatedAt) VALUES (@n,@url,@an,@a,@o,@e,@t); SELECT LAST_INSERT_ID();";
                cmd.Parameters.AddWithValue("@t", DateTime.UtcNow);
            }
            cmd.Parameters.AddWithValue("@n", name);
            cmd.Parameters.AddWithValue("@url", url);
            cmd.Parameters.AddWithValue("@an", animated);
            cmd.Parameters.AddWithValue("@a", authorId == null ? DBNull.Value : (object)authorId.Value.ToString());
            cmd.Parameters.AddWithValue("@o", sortOrder);
            cmd.Parameters.AddWithValue("@e", enabled);
            if (id is int updated) { await cmd.ExecuteNonQueryAsync(ct); return updated; }
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        public async Task DeleteAvatarDefAsync(int id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM avatar_defs WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        /// <summary>这个官方头像能不能用：没上架成商品的所有人免费用；上架了的要兑换过（或者是原作者）</summary>
        public async Task<bool> CanUseAvatarAsync(Guid userId, AvatarDef def, CancellationToken ct = default)
        {
            if (!def.Enabled) return false;
            if (!(await ItemsForEffectAsync(EffectAvatar, ct)).ContainsKey(def.Id)) return true;
            return await OwnsAsync(userId, EffectAvatar, def.Id.ToString(), ct);
        }
    }
}
