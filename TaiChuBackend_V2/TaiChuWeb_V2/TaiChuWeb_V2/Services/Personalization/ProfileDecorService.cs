using MySqlConnector;
using TaiChuWeb_V2.Services.Chat;

namespace TaiChuWeb_V2.Services.Personalization
{
    /// <summary>
    /// 个人主页装扮（别人访问你的主页时看到的）：顶部横幅 + 整页背景，两者分开设置。
    /// 每一项三种来源：default 不设置 / preset 官方素材（独立素材库 profile_backgrounds，和「网站背景」分开）/ custom 自己上传（赞助特权 custom-profile）。
    /// 官方素材可以在交易行出售（商品效果 pbg），设成商品后要先兑换才能用；素材可以填原作者，作者自动免费拥有。
    /// 赞助到期后自定义的横幅 / 背景暂时不显示，但设置保留，续费后自动恢复。
    /// 数据表在第一次使用时自动创建（不需要 EF 迁移）。
    /// </summary>
    public sealed class ProfileDecorService(IConfiguration configuration)
    {
        public const string SlotBanner = "banner";
        public const string SlotPage = "page";
        public static readonly string[] Slots = [SlotBanner, SlotPage];

        private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);
        private volatile bool _ready;

        /// <summary>横幅或整页背景的一项设置</summary>
        /// <param name="Url">自己上传的素材（即使当前没在用也保留，方便切回来）</param>
        public sealed record Part(string Source, int? PresetId, string? Url, string Kind, string? Poster, string Focus, int Veil, int Blur);

        public sealed record Decor(Part Banner, Part Page, DateTime? UpdatedAt);

        /// <summary>官方素材。Slot = banner 横幅 / page 整页背景</summary>
        public sealed record Preset(int Id, string Slot, string Title, string Kind, string Url, string? Poster, Guid? AuthorId, int SortOrder, bool Enabled, DateTime CreatedAt);

        public static Part EmptyPart => new("default", null, null, "image", null, "center", 45, 0);
        public static Decor Default => new(EmptyPart, EmptyPart, null);

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
CREATE TABLE IF NOT EXISTS profile_backgrounds (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  Slot VARCHAR(8) NOT NULL DEFAULT 'banner',
  Title VARCHAR(60) NOT NULL,
  Kind VARCHAR(8) NOT NULL DEFAULT 'image',
  Url VARCHAR(1000) NOT NULL,
  Poster VARCHAR(1000) NULL,
  AuthorId CHAR(36) NULL,
  SortOrder INT NOT NULL DEFAULT 0,
  Enabled TINYINT(1) NOT NULL DEFAULT 1,
  CreatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS user_profile_decor (
  UserId CHAR(36) NOT NULL PRIMARY KEY,
  BannerSource VARCHAR(8) NOT NULL DEFAULT 'default',
  BannerPresetId INT NULL,
  BannerUrl VARCHAR(1000) NULL,
  BannerKind VARCHAR(8) NOT NULL DEFAULT 'image',
  BannerPoster VARCHAR(1000) NULL,
  BannerFocus VARCHAR(16) NOT NULL DEFAULT 'center',
  PageSource VARCHAR(8) NOT NULL DEFAULT 'default',
  PagePresetId INT NULL,
  PageUrl VARCHAR(1000) NULL,
  PageKind VARCHAR(8) NOT NULL DEFAULT 'image',
  PagePoster VARCHAR(1000) NULL,
  PageFocus VARCHAR(16) NOT NULL DEFAULT 'center',
  PageVeil INT NOT NULL DEFAULT 45,
  PageBlur INT NOT NULL DEFAULT 0,
  UpdatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                await cmd.ExecuteNonQueryAsync(ct);
                _ready = true;
            }
            finally { _schemaLock.Release(); }
        }

        /// <summary>整理一项设置：非法值换成默认值</summary>
        public static Part Clean(Part p)
        {
            var url = UserAppearanceService.IsSafeUrl(p.Url) ? p.Url : null;
            var kind = UserAppearanceService.Kinds.Contains(p.Kind) ? p.Kind : "image";
            var poster = kind == "video" && UserAppearanceService.IsSafeUrl(p.Poster) ? p.Poster : null;
            var source = UserAppearanceService.Sources.Contains(p.Source) ? p.Source : "default";
            if (source == "custom" && url == null) source = "default";
            if (source == "preset" && p.PresetId == null) source = "default";
            var focus = UserAppearanceService.Focuses.Contains(p.Focus) ? p.Focus : "center";
            return new Part(source, source == "preset" ? p.PresetId : null, url, kind, poster, focus, Math.Clamp(p.Veil, 0, 90), Math.Clamp(p.Blur, 0, 20));
        }

        // ===================== 用户设置 =====================

        public async Task<Decor> GetAsync(Guid userId, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"SELECT BannerSource, BannerPresetId, BannerUrl, BannerKind, BannerPoster, BannerFocus,
  PageSource, PagePresetId, PageUrl, PageKind, PagePoster, PageFocus, PageVeil, PageBlur, UpdatedAt
FROM user_profile_decor WHERE UserId = @u";
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct)) return Default;
            string? S(int i) => r.IsDBNull(i) ? null : r.GetString(i);
            int? I(int i) => r.IsDBNull(i) ? null : Convert.ToInt32(r.GetValue(i));
            var banner = new Part(S(0) ?? "default", I(1), S(2), S(3) ?? "image", S(4), S(5) ?? "center", 0, 0);
            var page = new Part(S(6) ?? "default", I(7), S(8), S(9) ?? "image", S(10), S(11) ?? "center", I(12) ?? 45, I(13) ?? 0);
            return new Decor(banner, page, DateTime.SpecifyKind(r.GetDateTime(14), DateTimeKind.Utc));
        }

        /// <summary>保存（调用方负责检查自定义特权、官方素材是否可用）</summary>
        public async Task<Decor> SaveAsync(Guid userId, Part banner, Part page, CancellationToken ct = default)
        {
            var b = Clean(banner);
            var p = Clean(page);
            var now = DateTime.UtcNow;
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"
INSERT INTO user_profile_decor (UserId, BannerSource, BannerPresetId, BannerUrl, BannerKind, BannerPoster, BannerFocus,
  PageSource, PagePresetId, PageUrl, PageKind, PagePoster, PageFocus, PageVeil, PageBlur, UpdatedAt)
VALUES (@u, @bs, @bp, @bu, @bk, @bpo, @bf, @ps, @pp, @pu, @pk, @ppo, @pf, @pv, @pb, @t)
ON DUPLICATE KEY UPDATE BannerSource=@bs, BannerPresetId=@bp, BannerUrl=@bu, BannerKind=@bk, BannerPoster=@bpo, BannerFocus=@bf,
  PageSource=@ps, PagePresetId=@pp, PageUrl=@pu, PageKind=@pk, PagePoster=@ppo, PageFocus=@pf, PageVeil=@pv, PageBlur=@pb, UpdatedAt=@t";
            object N(object? v) => v ?? DBNull.Value;
            cmd.Parameters.AddWithValue("@u", userId.ToString());
            cmd.Parameters.AddWithValue("@bs", b.Source);
            cmd.Parameters.AddWithValue("@bp", N(b.PresetId));
            cmd.Parameters.AddWithValue("@bu", N(b.Url));
            cmd.Parameters.AddWithValue("@bk", b.Kind);
            cmd.Parameters.AddWithValue("@bpo", N(b.Poster));
            cmd.Parameters.AddWithValue("@bf", b.Focus);
            cmd.Parameters.AddWithValue("@ps", p.Source);
            cmd.Parameters.AddWithValue("@pp", N(p.PresetId));
            cmd.Parameters.AddWithValue("@pu", N(p.Url));
            cmd.Parameters.AddWithValue("@pk", p.Kind);
            cmd.Parameters.AddWithValue("@ppo", N(p.Poster));
            cmd.Parameters.AddWithValue("@pf", p.Focus);
            cmd.Parameters.AddWithValue("@pv", p.Veil);
            cmd.Parameters.AddWithValue("@pb", p.Blur);
            cmd.Parameters.AddWithValue("@t", now);
            await cmd.ExecuteNonQueryAsync(ct);
            return new Decor(b, p, now);
        }

        // ===================== 官方素材 =====================

        private static Preset ReadPreset(MySqlDataReader r) => new(
            Convert.ToInt32(r.GetValue(0)),
            r.GetString(1),
            r.GetString(2),
            r.GetString(3),
            r.GetString(4),
            r.IsDBNull(5) ? null : r.GetString(5),
            r.IsDBNull(6) ? null : ChatStore.ReadGuid(r, 6),
            Convert.ToInt32(r.GetValue(7)),
            Convert.ToBoolean(r.GetValue(8)),
            DateTime.SpecifyKind(r.GetDateTime(9), DateTimeKind.Utc));

        private const string PresetColumns = "Id, Slot, Title, Kind, Url, Poster, AuthorId, SortOrder, Enabled, CreatedAt";

        public async Task<List<Preset>> ListPresetsAsync(bool enabledOnly, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {PresetColumns} FROM profile_backgrounds" + (enabledOnly ? " WHERE Enabled = 1" : "") + " ORDER BY Slot, SortOrder, Id";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            var list = new List<Preset>();
            while (await r.ReadAsync(ct)) list.Add(ReadPreset(r));
            return list;
        }

        public async Task<Preset?> GetPresetAsync(int id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {PresetColumns} FROM profile_backgrounds WHERE Id = @id";
            cmd.Parameters.AddWithValue("@id", id);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            return await r.ReadAsync(ct) ? ReadPreset(r) : null;
        }

        /// <summary>新增（id 为空）或修改官方素材，返回 Id</summary>
        public async Task<int> SavePresetAsync(int? id, string slot, string title, string kind, string url, string? poster, Guid? authorId, int sortOrder, bool enabled, CancellationToken ct = default)
        {
            slot = Slots.Contains(slot) ? slot : SlotBanner;
            kind = UserAppearanceService.Kinds.Contains(kind) ? kind : "image";
            poster = kind == "video" && UserAppearanceService.IsSafeUrl(poster) ? poster : null;
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            if (id is int existing)
            {
                cmd.CommandText = "UPDATE profile_backgrounds SET Slot=@slot, Title=@t, Kind=@k, Url=@url, Poster=@p, AuthorId=@a, SortOrder=@s, Enabled=@e WHERE Id=@id";
                cmd.Parameters.AddWithValue("@id", existing);
            }
            else
            {
                cmd.CommandText = "INSERT INTO profile_backgrounds (Slot, Title, Kind, Url, Poster, AuthorId, SortOrder, Enabled, CreatedAt) VALUES (@slot,@t,@k,@url,@p,@a,@s,@e,@now); SELECT LAST_INSERT_ID();";
                cmd.Parameters.AddWithValue("@now", DateTime.UtcNow);
            }
            cmd.Parameters.AddWithValue("@slot", slot);
            cmd.Parameters.AddWithValue("@t", title);
            cmd.Parameters.AddWithValue("@k", kind);
            cmd.Parameters.AddWithValue("@url", url);
            cmd.Parameters.AddWithValue("@p", poster == null ? DBNull.Value : (object)poster);
            cmd.Parameters.AddWithValue("@a", authorId == null ? DBNull.Value : (object)authorId.Value.ToString());
            cmd.Parameters.AddWithValue("@s", sortOrder);
            cmd.Parameters.AddWithValue("@e", enabled);
            if (id is int updated) { await cmd.ExecuteNonQueryAsync(ct); return updated; }
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        /// <summary>删除官方素材；正在用它的人自动回到「不设置」</summary>
        public async Task DeletePresetAsync(int id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = @"DELETE FROM profile_backgrounds WHERE Id = @id;
UPDATE user_profile_decor SET BannerSource = 'default', BannerPresetId = NULL WHERE BannerSource = 'preset' AND BannerPresetId = @id;
UPDATE user_profile_decor SET PageSource = 'default', PagePresetId = NULL WHERE PageSource = 'preset' AND PagePresetId = @id;";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }
    }
}
