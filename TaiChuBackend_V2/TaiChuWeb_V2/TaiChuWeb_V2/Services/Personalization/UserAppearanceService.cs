using MySqlConnector;
using TaiChuWeb_V2.Services.Chat;

namespace TaiChuWeb_V2.Services.Personalization
{
    /// <summary>
    /// 网站背景（只影响这个用户自己看到的页面）。三种来源：
    /// default 站点默认的纯色坐标纸 / preset 官方提供的背景（所有人可选；在交易行出售的要先兑换）/ custom 自己上传的图片或视频（赞助特权）。
    /// 官方背景可以填原作者（社区成员投稿被采用的图），作者自动免费拥有。
    /// 选了背景后它替换掉纯色底色，网格、粒子、氛围线条等特效照常叠在上面。
    /// 赞助到期后自定义背景暂时显示为默认，但设置会保留，续费后自动恢复。
    /// 数据表 user_appearance / site_backgrounds 在第一次使用时自动创建（不需要 EF 迁移）。
    /// </summary>
    public sealed class UserAppearanceService(IConfiguration configuration)
    {
        public const int MaxUrlLength = 1000;
        public static readonly string[] Focuses = ["center", "top", "bottom", "left", "right"];
        public static readonly string[] Kinds = ["image", "video"];
        public static readonly string[] Sources = ["default", "preset", "custom"];

        private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);
        private volatile bool _ready;

        /// <param name="Source">default / preset / custom</param>
        /// <param name="PresetId">选择的官方背景</param>
        /// <param name="Url">自己上传的图片或视频（即使当前没在用也保留，方便切回来）</param>
        /// <param name="Kind">image 图片（含 GIF）/ video 视频（MP4、WebM）</param>
        /// <param name="Poster">视频的封面帧（视频加载完成前先显示它）</param>
        /// <param name="Nav">左侧导航栏的不透明度（30–100，%），越小越透、越能看到背景</param>
        public sealed record Appearance(string Source, int? PresetId, string? Url, string Kind, string? Poster, int Veil, int Blur, bool Grid, string Focus, DateTime? UpdatedAt, int Nav = DefaultNav);

        public const int DefaultNav = 80;

        /// <summary>官方背景（AuthorId：原作者，社区投稿被采用时填）</summary>
        public sealed record Preset(int Id, string Title, string Kind, string Url, string? Poster, int SortOrder, bool Enabled, DateTime CreatedAt, Guid? AuthorId = null);

        public static Appearance Default => new("default", null, null, "image", null, 55, 0, true, "center", null, DefaultNav);

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
                await using (var cmd = c.CreateCommand())
                {
                    cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS user_appearance (
  UserId CHAR(36) NOT NULL PRIMARY KEY,
  BgEnabled TINYINT(1) NOT NULL DEFAULT 0,
  BgSource VARCHAR(8) NOT NULL DEFAULT 'default',
  BgPresetId INT NULL,
  BgUrl VARCHAR(1000) NULL,
  BgKind VARCHAR(8) NOT NULL DEFAULT 'image',
  BgPoster VARCHAR(1000) NULL,
  BgVeil INT NOT NULL DEFAULT 55,
  BgBlur INT NOT NULL DEFAULT 0,
  BgGrid TINYINT(1) NOT NULL DEFAULT 1,
  BgFocus VARCHAR(16) NOT NULL DEFAULT 'center',
  UpdatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS site_backgrounds (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  Title VARCHAR(60) NOT NULL,
  Kind VARCHAR(8) NOT NULL DEFAULT 'image',
  Url VARCHAR(1000) NOT NULL,
  Poster VARCHAR(1000) NULL,
  SortOrder INT NOT NULL DEFAULT 0,
  Enabled TINYINT(1) NOT NULL DEFAULT 1,
  CreatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                    await cmd.ExecuteNonQueryAsync(ct);
                }
                // 早期版本建的表缺少的列，这里补上
                await AddColumnIfMissingAsync(c, "BgKind", "VARCHAR(8) NOT NULL DEFAULT 'image' AFTER BgUrl", ct);
                await AddColumnIfMissingAsync(c, "BgPoster", "VARCHAR(1000) NULL AFTER BgKind", ct);
                await AddColumnIfMissingAsync(c, "BgPresetId", "INT NULL AFTER BgEnabled", ct);
                await AddColumnIfMissingAsync(c, "AuthorId", "CHAR(36) NULL AFTER Poster", ct, "site_backgrounds");
                await AddColumnIfMissingAsync(c, "BgNav", "INT NOT NULL DEFAULT 80 AFTER BgFocus", ct);
                if (await AddColumnIfMissingAsync(c, "BgSource", "VARCHAR(8) NOT NULL DEFAULT 'default' AFTER BgEnabled", ct))
                {
                    // 第一版只有「自定义图片」，已经开启的记录算作 custom
                    await using var fix = c.CreateCommand();
                    fix.CommandText = "UPDATE user_appearance SET BgSource = 'custom' WHERE BgEnabled = 1 AND BgUrl IS NOT NULL";
                    await fix.ExecuteNonQueryAsync(ct);
                }
                _ready = true;
            }
            finally { _schemaLock.Release(); }
        }

        /// <returns>这次是否新加了列</returns>
        private static async Task<bool> AddColumnIfMissingAsync(MySqlConnection c, string column, string definition, CancellationToken ct, string table = "user_appearance")
        {
            await using var check = c.CreateCommand();
            check.CommandText = "SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @tb AND COLUMN_NAME = @c";
            check.Parameters.AddWithValue("@tb", table);
            check.Parameters.AddWithValue("@c", column);
            if (Convert.ToInt64(await check.ExecuteScalarAsync(ct)) > 0) return false;
            await using var alter = c.CreateCommand();
            alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
            await alter.ExecuteNonQueryAsync(ct);
            return true;
        }

        /// <summary>只接受 https 地址，且不含会破坏 CSS url() 的字符。</summary>
        public static bool IsSafeUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url) || url.Length > MaxUrlLength) return false;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps) return false;
            foreach (var ch in url)
            {
                if (char.IsWhiteSpace(ch) || char.IsControl(ch) || ch is '"' or '\'' or '(' or ')' or '\\' or '<' or '>') return false;
            }
            return true;
        }

        // ===================== 用户设置 =====================

        public async Task<Appearance> GetAsync(Guid userId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT BgSource, BgPresetId, BgUrl, BgKind, BgPoster, BgVeil, BgBlur, BgGrid, BgFocus, UpdatedAt, BgNav FROM user_appearance WHERE UserId = @u";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return Default;
            return new Appearance(
                r.IsDBNull(0) ? "default" : r.GetString(0),
                r.IsDBNull(1) ? null : Convert.ToInt32(r.GetValue(1)),
                r.IsDBNull(2) ? null : r.GetString(2),
                r.IsDBNull(3) ? "image" : r.GetString(3),
                r.IsDBNull(4) ? null : r.GetString(4),
                Convert.ToInt32(r.GetValue(5)),
                Convert.ToInt32(r.GetValue(6)),
                Convert.ToBoolean(r.GetValue(7)),
                r.GetString(8),
                DateTime.SpecifyKind(r.GetDateTime(9), DateTimeKind.Utc),
                r.IsDBNull(10) ? DefaultNav : Convert.ToInt32(r.GetValue(10)));
        }

        /// <summary>保存（调用方负责检查 custom 是否有赞助特权、preset 是否存在）。</summary>
        public async Task<Appearance> SaveAsync(Guid userId, Appearance a, CancellationToken ct = default)
        {
            var url = IsSafeUrl(a.Url) ? a.Url : null;
            var kind = Kinds.Contains(a.Kind) ? a.Kind : "image";
            var poster = kind == "video" && IsSafeUrl(a.Poster) ? a.Poster : null;
            var source = Sources.Contains(a.Source) ? a.Source : "default";
            if (source == "custom" && url == null) source = "default";
            if (source == "preset" && a.PresetId == null) source = "default";
            var veil = Math.Clamp(a.Veil, 0, 90);
            var blur = Math.Clamp(a.Blur, 0, 20);
            var focus = Focuses.Contains(a.Focus) ? a.Focus : "center";
            var nav = Math.Clamp(a.Nav, 30, 100);
            var now = DateTime.UtcNow;

            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"
INSERT INTO user_appearance (UserId, BgEnabled, BgSource, BgPresetId, BgUrl, BgKind, BgPoster, BgVeil, BgBlur, BgGrid, BgFocus, BgNav, UpdatedAt)
VALUES (@u, @e, @s, @pid, @url, @k, @p, @v, @b, @g, @f, @n, @t)
ON DUPLICATE KEY UPDATE BgEnabled = @e, BgSource = @s, BgPresetId = @pid, BgUrl = @url, BgKind = @k, BgPoster = @p, BgVeil = @v, BgBlur = @b, BgGrid = @g, BgFocus = @f, BgNav = @n, UpdatedAt = @t";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            cmd.Parameters.AddWithValue("@e", source != "default");
            cmd.Parameters.AddWithValue("@s", source);
            cmd.Parameters.AddWithValue("@pid", a.PresetId == null ? DBNull.Value : (object)a.PresetId.Value);
            cmd.Parameters.AddWithValue("@url", url == null ? DBNull.Value : (object)url);
            cmd.Parameters.AddWithValue("@k", kind);
            cmd.Parameters.AddWithValue("@p", poster == null ? DBNull.Value : (object)poster);
            cmd.Parameters.AddWithValue("@v", veil);
            cmd.Parameters.AddWithValue("@b", blur);
            cmd.Parameters.AddWithValue("@g", a.Grid);
            cmd.Parameters.AddWithValue("@f", focus);
            cmd.Parameters.AddWithValue("@n", nav);
            cmd.Parameters.AddWithValue("@t", now);
            await cmd.ExecuteNonQueryAsync(ct);
            return new Appearance(source, a.PresetId, url, kind, poster, veil, blur, a.Grid, focus, now, nav);
        }

        public async Task ResetAsync(Guid userId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM user_appearance WHERE UserId = @u";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        }

        // ===================== 官方背景 =====================

        private static Preset ReadPreset(MySqlDataReader r) => new(
            Convert.ToInt32(r.GetValue(0)),
            r.GetString(1),
            r.GetString(2),
            r.GetString(3),
            r.IsDBNull(4) ? null : r.GetString(4),
            Convert.ToInt32(r.GetValue(5)),
            Convert.ToBoolean(r.GetValue(6)),
            DateTime.SpecifyKind(r.GetDateTime(7), DateTimeKind.Utc),
            r.IsDBNull(8) ? null : ChatStore.ReadGuid(r, 8));

        public async Task<List<Preset>> ListPresetsAsync(bool enabledOnly, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Id, Title, Kind, Url, Poster, SortOrder, Enabled, CreatedAt, AuthorId FROM site_backgrounds"
                + (enabledOnly ? " WHERE Enabled = 1" : "") + " ORDER BY SortOrder, Id";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var list = new List<Preset>();
            while (await r.ReadAsync(ct)) list.Add(ReadPreset(r));
            return list;
        }

        public async Task<Preset?> GetPresetAsync(int id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "SELECT Id, Title, Kind, Url, Poster, SortOrder, Enabled, CreatedAt, AuthorId FROM site_backgrounds WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            return await r.ReadAsync(ct) ? ReadPreset(r) : null;
        }

        /// <summary>新增（id 为空）或修改官方背景，返回 Id</summary>
        public async Task<int> SavePresetAsync(int? id, string title, string kind, string url, string? poster, int sortOrder, bool enabled, Guid? authorId = null, CancellationToken ct = default)
        {
            kind = Kinds.Contains(kind) ? kind : "image";
            poster = kind == "video" && IsSafeUrl(poster) ? poster : null;
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            if (id is int existing)
            {
                cmd.CommandText = "UPDATE site_backgrounds SET Title = @t, Kind = @k, Url = @url, Poster = @p, SortOrder = @s, Enabled = @e, AuthorId = @a WHERE Id = @id";
                cmd.Parameters.AddWithValue("@id", existing);
            }
            else
            {
                cmd.CommandText = "INSERT INTO site_backgrounds (Title, Kind, Url, Poster, SortOrder, Enabled, CreatedAt, AuthorId) VALUES (@t, @k, @url, @p, @s, @e, @now, @a); SELECT LAST_INSERT_ID();";
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
            }
            cmd.Parameters.AddWithValue("@t", title);
            cmd.Parameters.AddWithValue("@k", kind);
            cmd.Parameters.AddWithValue("@url", url);
            cmd.Parameters.AddWithValue("@p", poster == null ? DBNull.Value : (object)poster);
            cmd.Parameters.AddWithValue("@s", sortOrder);
            cmd.Parameters.AddWithValue("@e", enabled);
            cmd.Parameters.AddWithValue("@a", authorId == null ? DBNull.Value : (object)authorId.Value.ToString());
            if (id is int updated) { await cmd.ExecuteNonQueryAsync(ct); return updated; }
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        /// <summary>删除官方背景；正在用它的人会自动回到默认背景</summary>
        public async Task DeletePresetAsync(int id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM site_backgrounds WHERE Id = @id; UPDATE user_appearance SET BgSource = 'default', BgEnabled = 0, BgPresetId = NULL WHERE BgSource = 'preset' AND BgPresetId = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
