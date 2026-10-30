using System.Text.Json;
using System.Text.Json.Nodes;
using MySqlConnector;

namespace TaiChuWeb_V2.Services.Games
{
    /// <summary>太虚游大厅里展示的「太初寰宇出品」游戏（主要是 Steam 游戏，也可以是还没上架的开发中作品）。</summary>
    public sealed class ShowcaseGame
    {
        public int Id { get; set; }
        /// <summary>Steam AppID；还没上架的游戏为空</summary>
        public int? AppId { get; set; }
        public string Title { get; set; } = string.Empty;
        /// <summary>一句话推荐语（自己写的，显示在 Steam 简介前面）</summary>
        public string Tagline { get; set; } = string.Empty;
        /// <summary>released 已发售 / coming 即将推出 / dev 开发中</summary>
        public string Status { get; set; } = "released";
        /// <summary>是否放在大厅顶部主推位</summary>
        public bool Featured { get; set; }
        public int SortOrder { get; set; }
        /// <summary>自定义封面（填了就优先用，不填用 Steam 的）</summary>
        public string? CoverUrl { get; set; }
        /// <summary>关联的开发进度项目 ID（可选）</summary>
        public string? DevProjectId { get; set; }
        /// <summary>论坛讨论标签（可选）</summary>
        public string? DiscussionTag { get; set; }
        public bool Visible { get; set; } = true;
        /// <summary>从 Steam 抓到的数据（精简后的 JSON）</summary>
        public string? SteamJson { get; set; }
        public DateTime? SteamFetchedAt { get; set; }
    }

    /// <summary>精简后的 Steam 商店数据（只保留大厅用得到的字段）。</summary>
    public sealed class SteamInfo
    {
        public string Name { get; set; } = string.Empty;
        public string ShortDescription { get; set; } = string.Empty;
        public string? HeaderImage { get; set; }
        public string? CapsuleImage { get; set; }
        public string? Background { get; set; }
        public List<string> Screenshots { get; set; } = new();
        public List<string> ScreenshotThumbs { get; set; } = new();
        public string? MovieMp4 { get; set; }
        public string? MoviePoster { get; set; }
        public bool IsFree { get; set; }
        public string? Price { get; set; }
        public string? OriginalPrice { get; set; }
        public int DiscountPercent { get; set; }
        public bool ComingSoon { get; set; }
        public string? ReleaseDate { get; set; }
        public List<string> Genres { get; set; } = new();
        public List<string> Developers { get; set; } = new();
        public List<string> Platforms { get; set; } = new();
    }

    /// <summary>
    /// Steam 游戏展示：数据存在 game_showcase 表（首次使用时自动创建，不需要 EF 迁移）；
    /// Steam 商店信息通过官方 appdetails 接口抓取，缓存 6 小时，抓取失败时沿用旧数据。
    /// </summary>
    public sealed class GameShowcaseService(IConfiguration configuration, IHttpClientFactory httpFactory, ILogger<GameShowcaseService> logger)
    {
        private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        private readonly SemaphoreSlim _schemaLock = new(1, 1);
        private readonly SemaphoreSlim _refreshLock = new(1, 1);
        private volatile bool _ready;
        public static readonly TimeSpan CacheFor = TimeSpan.FromHours(6);
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

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
CREATE TABLE IF NOT EXISTS game_showcase (
  Id INT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  AppId INT NULL,
  Title VARCHAR(100) NOT NULL,
  Tagline VARCHAR(300) NOT NULL DEFAULT '',
  Status VARCHAR(16) NOT NULL DEFAULT 'released',
  Featured TINYINT(1) NOT NULL DEFAULT 0,
  SortOrder INT NOT NULL DEFAULT 0,
  CoverUrl VARCHAR(500) NULL,
  DevProjectId VARCHAR(64) NULL,
  DiscussionTag VARCHAR(40) NULL,
  Visible TINYINT(1) NOT NULL DEFAULT 1,
  SteamJson MEDIUMTEXT NULL,
  SteamFetchedAt DATETIME NULL,
  CreatedAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
INSERT INTO game_showcase (AppId, Title, Tagline, Status, Featured, SortOrder, Visible, CreatedAt, UpdatedAt)
SELECT 3654500, '不喝水就无法离开的房间', '被愤怒的古希腊水神困在房间里——不喝水，就别想离开。太初寰宇的第一款 Steam 游戏。', 'released', 1, 0, 1, UTC_TIMESTAMP(), UTC_TIMESTAMP()
FROM DUAL WHERE NOT EXISTS (SELECT 1 FROM game_showcase);";
                await cmd.ExecuteNonQueryAsync(ct);
                _ready = true;
            }
            finally { _schemaLock.Release(); }
        }

        private const string Columns = "Id, AppId, Title, Tagline, Status, Featured, SortOrder, CoverUrl, DevProjectId, DiscussionTag, Visible, SteamJson, SteamFetchedAt";

        private static ShowcaseGame Read(MySqlDataReader r) => new()
        {
            Id = r.GetInt32(0),
            AppId = r.IsDBNull(1) ? null : r.GetInt32(1),
            Title = r.GetString(2),
            Tagline = r.GetString(3),
            Status = r.GetString(4),
            Featured = r.GetBoolean(5),
            SortOrder = r.GetInt32(6),
            CoverUrl = r.IsDBNull(7) ? null : r.GetString(7),
            DevProjectId = r.IsDBNull(8) ? null : r.GetString(8),
            DiscussionTag = r.IsDBNull(9) ? null : r.GetString(9),
            Visible = r.GetBoolean(10),
            SteamJson = r.IsDBNull(11) ? null : r.GetString(11),
            SteamFetchedAt = r.IsDBNull(12) ? null : DateTime.SpecifyKind(r.GetDateTime(12), DateTimeKind.Utc),
        };

        public async Task<List<ShowcaseGame>> ListAsync(bool visibleOnly, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = $"SELECT {Columns} FROM game_showcase {(visibleOnly ? "WHERE Visible=1" : "")} ORDER BY Featured DESC, SortOrder, Id";
            var list = new List<ShowcaseGame>();
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) list.Add(Read(r));
            return list;
        }

        public async Task<ShowcaseGame?> GetAsync(int id, CancellationToken ct = default)
            => (await ListAsync(false, ct)).FirstOrDefault(g => g.Id == id);

        public async Task<int> SaveAsync(ShowcaseGame g, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            if (g.Id == 0)
            {
                cmd.CommandText = @"INSERT INTO game_showcase (AppId, Title, Tagline, Status, Featured, SortOrder, CoverUrl, DevProjectId, DiscussionTag, Visible, CreatedAt, UpdatedAt)
VALUES (@app,@t,@tl,@s,@f,@o,@cv,@dp,@dt,@v,UTC_TIMESTAMP(),UTC_TIMESTAMP()); SELECT LAST_INSERT_ID();";
            }
            else
            {
                // 换了 AppID 就清掉旧的 Steam 缓存
                cmd.CommandText = @"UPDATE game_showcase SET
  SteamJson = IF(AppId <=> @app, SteamJson, NULL), SteamFetchedAt = IF(AppId <=> @app, SteamFetchedAt, NULL),
  AppId=@app, Title=@t, Tagline=@tl, Status=@s, Featured=@f, SortOrder=@o, CoverUrl=@cv, DevProjectId=@dp, DiscussionTag=@dt, Visible=@v, UpdatedAt=UTC_TIMESTAMP()
WHERE Id=@id; SELECT @id;";
                cmd.Parameters.AddWithValue("@id", g.Id);
            }
            cmd.Parameters.AddWithValue("@app", (object?)g.AppId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@t", g.Title);
            cmd.Parameters.AddWithValue("@tl", g.Tagline);
            cmd.Parameters.AddWithValue("@s", g.Status);
            cmd.Parameters.AddWithValue("@f", g.Featured);
            cmd.Parameters.AddWithValue("@o", g.SortOrder);
            cmd.Parameters.AddWithValue("@cv", (object?)g.CoverUrl ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dp", (object?)g.DevProjectId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@dt", (object?)g.DiscussionTag ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@v", g.Visible);
            return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
        }

        public async Task DeleteAsync(int id, CancellationToken ct = default)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "DELETE FROM game_showcase WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        private async Task SaveSteamAsync(int id, SteamInfo info, CancellationToken ct)
        {
            await using var c = await OpenAsync(ct);
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE game_showcase SET SteamJson=@j, SteamFetchedAt=UTC_TIMESTAMP() WHERE Id=@id";
            cmd.Parameters.AddWithValue("@j", JsonSerializer.Serialize(info, Json));
            cmd.Parameters.AddWithValue("@id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        public static SteamInfo? ParseSteam(string? json) =>
            string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<SteamInfo>(json, Json);

        // ---------------- Steam ----------------

        /// <summary>从 Steam 商店官方接口取游戏信息（简体中文、人民币价格）。失败返回 null。</summary>
        public async Task<SteamInfo?> FetchSteamAsync(int appId, CancellationToken ct = default)
        {
            try
            {
                var client = httpFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(15);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("TaiChuHuanYu/1.0");
                var body = await client.GetStringAsync($"https://store.steampowered.com/api/appdetails?appids={appId}&cc=cn&l=schinese", ct);
                var root = JsonNode.Parse(body)?[appId.ToString()];
                if (root?["success"]?.GetValue<bool>() != true) return null;
                var d = root["data"];
                if (d == null) return null;

                static string? S(JsonNode? n) => n?.GetValueKind() == JsonValueKind.String ? n.GetValue<string>() : null;
                var info = new SteamInfo
                {
                    Name = S(d["name"]) ?? string.Empty,
                    ShortDescription = System.Net.WebUtility.HtmlDecode(S(d["short_description"]) ?? string.Empty),
                    HeaderImage = S(d["header_image"]),
                    CapsuleImage = S(d["capsule_imagev5"]) ?? S(d["capsule_image"]),
                    Background = S(d["background_raw"]) ?? S(d["background"]),
                    IsFree = d["is_free"]?.GetValueKind() == JsonValueKind.True,
                    ComingSoon = d["release_date"]?["coming_soon"]?.GetValueKind() == JsonValueKind.True,
                    ReleaseDate = S(d["release_date"]?["date"]),
                    Price = S(d["price_overview"]?["final_formatted"]),
                    OriginalPrice = S(d["price_overview"]?["initial_formatted"]),
                    DiscountPercent = d["price_overview"]?["discount_percent"]?.GetValue<int>() ?? 0,
                };
                if (d["screenshots"] is JsonArray shots)
                {
                    foreach (var s in shots.Take(8))
                    {
                        if (S(s?["path_full"]) is string full) info.Screenshots.Add(full);
                        if (S(s?["path_thumbnail"]) is string thumb) info.ScreenshotThumbs.Add(thumb);
                    }
                }
                if (d["movies"] is JsonArray movies && movies.Count > 0)
                {
                    var m = movies[0];
                    info.MovieMp4 = S(m?["mp4"]?["480"]) ?? S(m?["mp4"]?["max"]);
                    info.MoviePoster = S(m?["thumbnail"]);
                }
                if (d["genres"] is JsonArray genres)
                    info.Genres.AddRange(genres.Select(g => S(g?["description"])).OfType<string>());
                if (d["developers"] is JsonArray devs)
                    info.Developers.AddRange(devs.Select(S).OfType<string>());
                if (d["platforms"] is JsonObject platforms)
                    info.Platforms.AddRange(platforms.Where(p => p.Value?.GetValueKind() == JsonValueKind.True).Select(p => p.Key));
                return info;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
            {
                logger.LogWarning(ex, "获取 Steam 游戏信息失败 AppID={AppId}", appId);
                return null;
            }
        }

        /// <summary>刷新过期（或强制刷新）的 Steam 数据；同一时间只跑一次，失败就保留旧数据。</summary>
        public async Task RefreshAsync(IEnumerable<ShowcaseGame> games, bool force, CancellationToken ct = default)
        {
            if (!await _refreshLock.WaitAsync(0, ct)) return;
            try
            {
                foreach (var g in games)
                {
                    if (g.AppId is not int appId) continue;
                    if (!force && g.SteamFetchedAt.HasValue && DateTime.UtcNow - g.SteamFetchedAt.Value < CacheFor) continue;
                    var info = await FetchSteamAsync(appId, ct);
                    if (info != null)
                    {
                        await SaveSteamAsync(g.Id, info, ct);
                        g.SteamJson = JsonSerializer.Serialize(info, Json);
                        g.SteamFetchedAt = DateTime.UtcNow;
                    }
                }
            }
            finally { _refreshLock.Release(); }
        }

        /// <summary>没有抓到 Steam 数据时，用 Steam 公开图片地址兜底</summary>
        public static string? FallbackHeader(int? appId) =>
            appId is int id ? $"https://cdn.cloudflare.steamstatic.com/steam/apps/{id}/header.jpg" : null;

        public static string? StoreUrl(int? appId) =>
            appId is int id ? $"https://store.steampowered.com/app/{id}/" : null;

        /// <summary>转成前端用的格式</summary>
        public static object ToView(ShowcaseGame g)
        {
            var s = ParseSteam(g.SteamJson);
            return new
            {
                g.Id,
                g.AppId,
                title = g.Title,
                steamName = s?.Name,
                tagline = g.Tagline,
                description = s?.ShortDescription ?? string.Empty,
                status = g.Status,
                featured = g.Featured,
                sortOrder = g.SortOrder,
                visible = g.Visible,
                cover = g.CoverUrl ?? s?.HeaderImage ?? FallbackHeader(g.AppId),
                customCover = g.CoverUrl,
                capsule = s?.CapsuleImage,
                background = s?.Background,
                screenshots = s?.Screenshots ?? new List<string>(),
                screenshotThumbs = s?.ScreenshotThumbs ?? new List<string>(),
                movie = s?.MovieMp4,
                moviePoster = s?.MoviePoster,
                isFree = s?.IsFree ?? false,
                price = s?.Price,
                originalPrice = s?.OriginalPrice,
                discountPercent = s?.DiscountPercent ?? 0,
                releaseDate = s?.ReleaseDate,
                comingSoon = s?.ComingSoon ?? false,
                genres = s?.Genres ?? new List<string>(),
                developers = s?.Developers ?? new List<string>(),
                platforms = s?.Platforms ?? new List<string>(),
                storeUrl = StoreUrl(g.AppId),
                devProjectId = g.DevProjectId,
                discussionTag = g.DiscussionTag,
                steamFetchedAt = g.SteamFetchedAt,
            };
        }
    }
}
