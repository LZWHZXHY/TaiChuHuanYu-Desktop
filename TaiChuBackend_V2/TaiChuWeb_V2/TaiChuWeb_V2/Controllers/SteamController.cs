using System.Collections.Concurrent;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MySqlConnector;

namespace TaiChuWeb_V2.Controllers;

/// <summary>
/// 绑定 Steam（fix173）：
/// · 用户点「用 Steam 登录绑定」→ 跳到 Steam 官方登录页（OpenID，我们拿不到 Steam 密码）→ 跳回 /api/steam/callback，
///   向 Steam 核对这次登录是真的，记下他的 64 位 Steam ID；
/// · 用 Steam Web API（密钥放在配置 Steam:ApiKey，不写在代码里）读头像、昵称、在线状态、游戏和时长，存一份快照：
///   在线状态 10 分钟、游戏 6 小时才重新读一次（官方每天限 10 万次调用，也避免每次打开主页都去连 Steam）；
/// · 用户可以选显示哪些（在线状态 / 游戏 / 时长），解绑时连快照一起删掉（Steam API 条款要求）。
/// 新表 user_steam，启动后第一次用时自动建，不动任何旧表。
/// </summary>
[ApiController]
[Route("api/steam")]
public class SteamController(IConfiguration config, IHttpClientFactory httpFactory, ILogger<SteamController> logger) : ControllerBase
{
    private const string OpenIdEndpoint = "https://steamcommunity.com/openid/login";
    private static readonly Regex ClaimedIdPattern = new(@"^https?://steamcommunity\.com/openid/id/(\d{17})$", RegexOptions.Compiled);
    private static readonly TimeSpan SummaryTtl = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan GamesTtl = TimeSpan.FromHours(6);
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // 正在进行的绑定（state → 谁、绑完回哪、Steam 回来的地址），10 分钟有效，只能用一次
    private sealed record Pending(Guid UserId, string Back, string ReturnTo, DateTime ExpiresAt);
    private static readonly ConcurrentDictionary<string, Pending> PendingBinds = new();
    // 同一个人的资料同时只读一次
    private static readonly ConcurrentDictionary<Guid, Task<SteamCard?>> Refreshing = new();

    private string ConnectionString => config.GetConnectionString("DefaultConnection") ?? string.Empty;
    private string? ApiKey => string.IsNullOrWhiteSpace(config["Steam:ApiKey"]) ? null : config["Steam:ApiKey"]!.Trim();
    private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    // ===================== 数据结构 =====================

    public sealed class SteamGame
    {
        public int AppId { get; set; }
        public string Name { get; set; } = "";
        /// <summary>总时长（分钟）</summary>
        public int Minutes { get; set; }
        /// <summary>最近两周（分钟）</summary>
        public int? Minutes2Weeks { get; set; }
        public string? Icon { get; set; }
    }

    /// <summary>存在 user_steam.CardJson 里的快照，也是接口返回的样子</summary>
    public sealed class SteamCard
    {
        public string SteamId { get; set; } = "";
        public string? Name { get; set; }
        public string? Avatar { get; set; }
        public string? ProfileUrl { get; set; }
        /// <summary>0 离线 1 在线 2 忙碌 3 离开 4 打盹 5 想交易 6 想玩游戏；null = 不显示</summary>
        public int? State { get; set; }
        /// <summary>正在玩的游戏名（没在玩为 null）</summary>
        public string? Playing { get; set; }
        public int? PlayingAppId { get; set; }
        /// <summary>Steam 个人资料是不是公开的（不公开时拿不到游戏）</summary>
        public bool ProfilePublic { get; set; }
        public int? Level { get; set; }
        public long? CreatedAt { get; set; }
        public int? GameCount { get; set; }
        public int? TotalMinutes { get; set; }
        public List<SteamGame>? Top { get; set; }
        public List<SteamGame>? Recent { get; set; }
        /// <summary>三角洲行动（有的话单独拿出来显示）</summary>
        public SteamGame? Featured { get; set; }
        public DateTime? SummaryAt { get; set; }
        public DateTime? GamesAt { get; set; }
    }

    private sealed record SteamRow(Guid UserId, string SteamId, bool ShowStatus, bool ShowGames, bool ShowPlaytime, string? CardJson, DateTime BoundAt);

    public sealed class StartRequest { public string? Back { get; set; } }
    public sealed class SettingsRequest { public bool? ShowStatus { get; set; } public bool? ShowGames { get; set; } public bool? ShowPlaytime { get; set; } }

    // ===================== 表 =====================

    private static bool _ready;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);

    private async Task<MySqlConnection> OpenAsync()
    {
        if (!_ready)
        {
            await SchemaLock.WaitAsync();
            try
            {
                if (!_ready)
                {
                    await using var c0 = new MySqlConnection(ConnectionString);
                    await c0.OpenAsync();
                    await using var cmd = c0.CreateCommand();
                    cmd.CommandText = @"CREATE TABLE IF NOT EXISTS user_steam (
  UserId CHAR(36) NOT NULL PRIMARY KEY,
  SteamId VARCHAR(20) NOT NULL,
  ShowStatus TINYINT(1) NOT NULL DEFAULT 1,
  ShowGames TINYINT(1) NOT NULL DEFAULT 1,
  ShowPlaytime TINYINT(1) NOT NULL DEFAULT 1,
  CardJson MEDIUMTEXT NULL,
  BoundAt DATETIME NOT NULL,
  UpdatedAt DATETIME NOT NULL,
  UNIQUE KEY UX_user_steam_sid (SteamId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";
                    await cmd.ExecuteNonQueryAsync();
                    _ready = true;
                }
            }
            finally { SchemaLock.Release(); }
        }
        var c = new MySqlConnection(ConnectionString);
        await c.OpenAsync();
        return c;
    }

    private async Task<SteamRow?> GetRowAsync(Guid userId)
    {
        await using var c = await OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "SELECT UserId, SteamId, ShowStatus, ShowGames, ShowPlaytime, CardJson, BoundAt FROM user_steam WHERE UserId = @u";
        cmd.Parameters.AddWithValue("@u", userId.ToString());
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return null;
        return new SteamRow(Guid.Parse(Convert.ToString(r.GetValue(0))!), Convert.ToString(r.GetValue(1))!, r.GetBoolean(2), r.GetBoolean(3), r.GetBoolean(4),
            r.IsDBNull(5) ? null : Convert.ToString(r.GetValue(5)), r.GetDateTime(6));
    }

    private async Task SaveCardAsync(Guid userId, SteamCard card)
    {
        await using var c = await OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE user_steam SET CardJson = @j, UpdatedAt = @t WHERE UserId = @u AND SteamId = @s";
        cmd.Parameters.AddWithValue("@j", JsonSerializer.Serialize(card, Json));
        cmd.Parameters.AddWithValue("@t", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("@u", userId.ToString());
        cmd.Parameters.AddWithValue("@s", card.SteamId);
        await cmd.ExecuteNonQueryAsync();
    }

    // ===================== 绑定：跳去 Steam 登录 =====================

    /// <summary>开始绑定：返回 Steam 登录页地址，前端整页跳过去</summary>
    [HttpPost("bind/start")]
    [Authorize]
    public IActionResult Start([FromBody] StartRequest request)
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized(new { message = "请先登录" });
        var back = request.Back?.Trim() ?? "";
        if (!IsAllowedBack(back)) return BadRequest(new { message = "返回地址无效" });

        foreach (var (k, v) in PendingBinds) if (v.ExpiresAt < DateTime.UtcNow) PendingBinds.TryRemove(k, out _);

        var state = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant();
        var apiBase = ApiBase();
        var returnTo = $"{apiBase}/api/steam/callback?state={state}";
        var realm = new Uri(apiBase).GetLeftPart(UriPartial.Authority);
        PendingBinds[state] = new Pending(me, back, returnTo, DateTime.UtcNow.AddMinutes(10));

        var q = new Dictionary<string, string>
        {
            ["openid.ns"] = "http://specs.openid.net/auth/2.0",
            ["openid.mode"] = "checkid_setup",
            ["openid.return_to"] = returnTo,
            ["openid.realm"] = realm,
            ["openid.identity"] = "http://specs.openid.net/auth/2.0/identifier_select",
            ["openid.claimed_id"] = "http://specs.openid.net/auth/2.0/identifier_select",
        };
        var url = OpenIdEndpoint + "?" + string.Join("&", q.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
        return Ok(new { url });
    }

    /// <summary>Steam 登录完跳回这里：向 Steam 核对、记下 Steam ID，然后跳回网站</summary>
    [HttpGet("callback")]
    [AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string? state)
    {
        if (string.IsNullOrEmpty(state) || !PendingBinds.TryRemove(state, out var pending) || pending.ExpiresAt < DateTime.UtcNow)
            return Content("绑定链接已经过期或已经用过了，请回到网站重新点「用 Steam 登录绑定」。", "text/plain; charset=utf-8");

        string Fail(string reason) => AppendQuery(pending.Back, "steam", "error") + "&reason=" + Uri.EscapeDataString(reason);

        var query = Request.Query;
        if (query["openid.mode"] == "cancel") return Redirect(Fail("你取消了 Steam 登录"));
        if (query["openid.mode"] != "id_res") return Redirect(Fail("Steam 没有返回登录结果"));
        if (query["openid.op_endpoint"] != OpenIdEndpoint) return Redirect(Fail("登录来源不对"));
        if (query["openid.return_to"] != pending.ReturnTo) return Redirect(Fail("登录回跳地址对不上"));
        var claimed = ClaimedIdPattern.Match(query["openid.claimed_id"].ToString());
        if (!claimed.Success || query["openid.identity"] != query["openid.claimed_id"]) return Redirect(Fail("Steam 返回的账号信息不对"));
        var steamId = claimed.Groups[1].Value;

        // 向 Steam 核对这次登录（防止有人伪造回跳地址）
        try
        {
            var form = query.Where(p => p.Key.StartsWith("openid.", StringComparison.Ordinal))
                .ToDictionary(p => p.Key, p => p.Key == "openid.mode" ? "check_authentication" : p.Value.ToString());
            var http = SteamHttp(20);
            using var res = await http.PostAsync(OpenIdEndpoint, new FormUrlEncodedContent(form));
            var text = await res.Content.ReadAsStringAsync();
            if (!text.Contains("is_valid:true", StringComparison.Ordinal)) return Redirect(Fail("Steam 没有确认这次登录"));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Steam 登录核对失败（服务器连 steamcommunity.com）");
            return Redirect(Fail(NetworkReason(ex, "steamcommunity.com")));
        }

        // 一个 Steam 号只能绑一个网站账号
        await using (var c = await OpenAsync())
        {
            await using var check = c.CreateCommand();
            check.CommandText = "SELECT UserId FROM user_steam WHERE SteamId = @s";
            check.Parameters.AddWithValue("@s", steamId);
            var ownerObj = await check.ExecuteScalarAsync();
            var owner = ownerObj == null || ownerObj is DBNull ? null : Convert.ToString(ownerObj);
            if (owner != null && !string.Equals(owner, pending.UserId.ToString(), StringComparison.OrdinalIgnoreCase))
                return Redirect(Fail("这个 Steam 号已经绑在别的账号上了，先在那个账号里解绑"));

            await using var up = c.CreateCommand();
            // 换绑别的 Steam 号时，旧快照一起清掉
            up.CommandText = @"INSERT INTO user_steam (UserId, SteamId, BoundAt, UpdatedAt) VALUES (@u, @s, @t, @t)
ON DUPLICATE KEY UPDATE CardJson = IF(SteamId = @s, CardJson, NULL), BoundAt = IF(SteamId = @s, BoundAt, @t), SteamId = @s, UpdatedAt = @t";
            // 注意顺序：MySQL 按顺序赋值，SteamId 要放在用到旧 SteamId 的两项后面
            up.Parameters.AddWithValue("@u", pending.UserId.ToString());
            up.Parameters.AddWithValue("@s", steamId);
            up.Parameters.AddWithValue("@t", DateTime.UtcNow);
            await up.ExecuteNonQueryAsync();
        }

        // 顺手读一次资料（读不到也没关系，打开主页时会再读）
        try { await RefreshAsync(pending.UserId, steamId, null, force: true); } catch { /* 忽略 */ }
        return Redirect(AppendQuery(pending.Back, "steam", "bound"));
    }

    // ===================== 自己的绑定 =====================

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Mine()
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized();
        var row = await GetRowAsync(me);
        if (row == null) return Ok(new { bound = false, keyReady = ApiKey != null });
        var card = await CardForAsync(row, force: false);
        return Ok(new
        {
            bound = true,
            keyReady = ApiKey != null,
            steamId = row.SteamId,
            boundAt = row.BoundAt,
            settings = new { showStatus = row.ShowStatus, showGames = row.ShowGames, showPlaytime = row.ShowPlaytime },
            card,
        });
    }

    [HttpPut("me/settings")]
    [Authorize]
    public async Task<IActionResult> SaveSettings([FromBody] SettingsRequest request)
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized();
        await using var c = await OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = @"UPDATE user_steam SET ShowStatus = COALESCE(@a, ShowStatus), ShowGames = COALESCE(@b, ShowGames), ShowPlaytime = COALESCE(@p, ShowPlaytime), UpdatedAt = @t WHERE UserId = @u";
        cmd.Parameters.AddWithValue("@a", request.ShowStatus.HasValue ? request.ShowStatus.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@b", request.ShowGames.HasValue ? request.ShowGames.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@p", request.ShowPlaytime.HasValue ? request.ShowPlaytime.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("@t", DateTime.UtcNow);
        cmd.Parameters.AddWithValue("@u", me.ToString());
        if (await cmd.ExecuteNonQueryAsync() == 0) return NotFound(new { message = "还没有绑定 Steam" });
        return Ok(new { success = true });
    }

    /// <summary>马上重新读一次（一分钟内只能点一次）</summary>
    [HttpPost("me/refresh")]
    [Authorize]
    public async Task<IActionResult> Refresh()
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized();
        var row = await GetRowAsync(me);
        if (row == null) return NotFound(new { message = "还没有绑定 Steam" });
        if (ApiKey == null) return BadRequest(new { message = "服务器还没配置 Steam 密钥" });
        var old = Parse(row.CardJson);
        if (old?.SummaryAt is { } at && DateTime.UtcNow - at < TimeSpan.FromMinutes(1))
            return BadRequest(new { message = "刚刚才更新过，过一分钟再试" });
        var card = await RefreshAsync(me, row.SteamId, old, force: true);
        if (card == null) return StatusCode(502, new { message = "连不上 Steam，请稍后再试" });
        return Ok(card);
    }

    /// <summary>解绑：连同保存的 Steam 资料一起删掉</summary>
    [HttpDelete("me")]
    [Authorize]
    public async Task<IActionResult> Unbind()
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized();
        await using var c = await OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM user_steam WHERE UserId = @u";
        cmd.Parameters.AddWithValue("@u", me.ToString());
        await cmd.ExecuteNonQueryAsync();
        return NoContent();
    }

    // ===================== 别人看到的卡片 =====================

    /// <summary>公开主页上的 Steam 卡片（按主人的设置隐藏）。没绑定返回 204</summary>
    [HttpGet("user/{userId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> OfUser(Guid userId)
    {
        var row = await GetRowAsync(userId);
        if (row == null) return NoContent();
        var card = await CardForAsync(row, force: false) ?? new SteamCard { SteamId = row.SteamId, ProfileUrl = $"https://steamcommunity.com/profiles/{row.SteamId}" };
        // 按设置藏起来（复制一份，不改缓存）
        var view = JsonSerializer.Deserialize<SteamCard>(JsonSerializer.Serialize(card, Json), Json)!;
        if (!row.ShowStatus) { view.State = null; view.Playing = null; view.PlayingAppId = null; }
        if (!row.ShowGames) { view.Top = null; view.Recent = null; view.GameCount = null; view.Featured = null; }
        if (!row.ShowPlaytime)
        {
            view.TotalMinutes = null;
            foreach (var g in (view.Top ?? new()).Concat(view.Recent ?? new()).Append(view.Featured).Where(g => g != null)) { g!.Minutes = 0; g.Minutes2Weeks = null; }
        }
        return Ok(new { steamId = row.SteamId, showPlaytime = row.ShowPlaytime, card = view });
    }

    // ===================== 读 Steam =====================

    private async Task<SteamCard?> CardForAsync(SteamRow row, bool force)
    {
        var card = Parse(row.CardJson);
        if (ApiKey == null) return card;
        var stale = card == null || card.SteamId != row.SteamId
            || card.SummaryAt == null || DateTime.UtcNow - card.SummaryAt > SummaryTtl
            || card.GamesAt == null || DateTime.UtcNow - card.GamesAt > GamesTtl;
        if (!stale && !force) return card;
        try { return await RefreshAsync(row.UserId, row.SteamId, card, force) ?? card; }
        catch { return card; }
    }

    private static SteamCard? Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<SteamCard>(json, Json); } catch (JsonException) { return null; }
    }

    private Task<SteamCard?> RefreshAsync(Guid userId, string steamId, SteamCard? old, bool force)
    {
        if (ApiKey == null) return Task.FromResult(old);
        // 同一个人同时只读一次，其他请求等同一个结果
        var task = Refreshing.GetOrAdd(userId, _ => DoRefreshAsync(userId, steamId, old, force));
        _ = task.ContinueWith(_ => Refreshing.TryRemove(userId, out var _), TaskScheduler.Default);
        return task;
    }

    private async Task<SteamCard?> DoRefreshAsync(Guid userId, string steamId, SteamCard? old, bool force)
    {
        var key = ApiKey!;
        var http = SteamHttp(15);
        var card = old != null && old.SteamId == steamId ? old : new SteamCard { SteamId = steamId };
        var now = DateTime.UtcNow;
        var changed = false;

        try
        {
            // 资料、在线状态（10 分钟一次）
            if (force || card.SummaryAt == null || now - card.SummaryAt > SummaryTtl)
            {
                var j = await GetJsonAsync(http, $"https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key={key}&steamids={steamId}");
                var p = j?["response"]?["players"]?.AsArray().FirstOrDefault();
                if (p != null)
                {
                    card.Name = (string?)p["personaname"];
                    card.Avatar = (string?)p["avatarfull"];
                    card.ProfileUrl = (string?)p["profileurl"] ?? $"https://steamcommunity.com/profiles/{steamId}";
                    card.State = (int?)p["personastate"] ?? 0;
                    card.Playing = (string?)p["gameextrainfo"];
                    card.PlayingAppId = int.TryParse((string?)p["gameid"], out var gid) ? gid : null;
                    card.ProfilePublic = ((int?)p["communityvisibilitystate"] ?? 1) == 3;
                    card.CreatedAt = (long?)p["timecreated"];
                    card.SummaryAt = now;
                    changed = true;
                }
            }

            // 游戏、时长、等级（6 小时一次）
            if (force || card.GamesAt == null || now - card.GamesAt > GamesTtl)
            {
                var owned = await GetJsonAsync(http, $"https://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={key}&steamid={steamId}&include_appinfo=1&include_played_free_games=1");
                var recent = await GetJsonAsync(http, $"https://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v1/?key={key}&steamid={steamId}&count=6");
                var level = await GetJsonAsync(http, $"https://api.steampowered.com/IPlayerService/GetSteamLevel/v1/?key={key}&steamid={steamId}");

                var games = (owned?["response"]?["games"] as JsonArray ?? new JsonArray()).Select(ToGame).Where(g => g != null).Select(g => g!).ToList();
                card.GameCount = (int?)owned?["response"]?["game_count"] ?? (games.Count > 0 ? games.Count : null);
                card.TotalMinutes = games.Count > 0 ? games.Sum(g => g.Minutes) : null;
                card.Top = games.Where(g => g.Minutes > 0).OrderByDescending(g => g.Minutes).Take(6).ToList();
                card.Recent = (recent?["response"]?["games"] as JsonArray ?? new JsonArray()).Select(ToGame).Where(g => g != null).Select(g => g!).Take(6).ToList();
                card.Featured = games.FirstOrDefault(g => g.Name.Contains("Delta Force", StringComparison.OrdinalIgnoreCase) || g.Name.Contains("三角洲"));
                card.Level = (int?)level?["response"]?["player_level"] ?? card.Level;
                card.GamesAt = now;
                changed = true;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "读取 Steam 资料失败 {SteamId}", steamId);
            if (!changed) return null;
        }

        if (changed) await SaveCardAsync(userId, card);
        return card;
    }

    private static SteamGame? ToGame(JsonNode? g)
    {
        if (g == null) return null;
        var appId = (int?)g["appid"] ?? 0;
        if (appId == 0) return null;
        var icon = (string?)g["img_icon_url"];
        return new SteamGame
        {
            AppId = appId,
            Name = (string?)g["name"] ?? $"App {appId}",
            Minutes = (int?)g["playtime_forever"] ?? 0,
            Minutes2Weeks = (int?)g["playtime_2weeks"],
            Icon = string.IsNullOrEmpty(icon) ? null : $"https://media.steampowered.com/steamcommunity/public/images/apps/{appId}/{icon}.jpg",
        };
    }

    private static async Task<JsonNode?> GetJsonAsync(HttpClient http, string url)
    {
        using var res = await http.GetAsync(url);
        if (!res.IsSuccessStatusCode) return null;
        return JsonNode.Parse(await res.Content.ReadAsStringAsync());
    }

    // ===================== 连 Steam：可以走代理 =====================

    // 服务器在国内时 steamcommunity.com（Steam 登录核对）经常连不上。
    // 配置 Steam:Proxy（比如 "http://你的NAS地址:7890" 或 "socks5://127.0.0.1:1080"）后，连 Steam 的请求都走这个代理；不配就直连。
    private static readonly ConcurrentDictionary<string, HttpMessageHandler> ProxyHandlers = new();
    private HttpClient SteamHttp(int seconds)
    {
        var proxy = config["Steam:Proxy"]?.Trim();
        if (string.IsNullOrEmpty(proxy))
        {
            var direct = httpFactory.CreateClient();
            direct.Timeout = TimeSpan.FromSeconds(seconds);
            return direct;
        }
        var handler = ProxyHandlers.GetOrAdd(proxy, p => new SocketsHttpHandler
        {
            Proxy = new System.Net.WebProxy(p),
            UseProxy = true,
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        });
        return new HttpClient(handler, disposeHandler: false) { Timeout = TimeSpan.FromSeconds(seconds) };
    }

    /// <summary>连不上时告诉用户 / 管理员具体是哪种情况</summary>
    private static string NetworkReason(Exception ex, string host)
    {
        var inner = ex;
        while (inner.InnerException != null) inner = inner.InnerException;
        return ex switch
        {
            TaskCanceledException => $"服务器连 {host} 超时（国内服务器经常连不上 Steam，需要管理员配置代理）",
            HttpRequestException when inner is System.Net.Sockets.SocketException se =>
                $"服务器连不上 {host}（{se.SocketErrorCode}，可能被墙或 DNS 解析失败，需要管理员配置代理）",
            HttpRequestException when inner is System.Security.Authentication.AuthenticationException =>
                $"服务器和 {host} 的加密连接失败（可能被干扰，需要管理员配置代理）",
            _ => $"服务器连 {host} 出错：{inner.GetType().Name}",
        };
    }

    private static (DateTime At, object Result)? _diagnoseCache;

    /// <summary>
    /// 诊断：服务器现在能不能连上 Steam 的两个地址（登录核对用的 steamcommunity.com、读资料用的 api.steampowered.com）。
    /// 浏览器直接打开 /api/steam/diagnose 就能看；不返回密钥，结果记 1 分钟
    /// </summary>
    [HttpGet("diagnose")]
    [AllowAnonymous]
    public async Task<IActionResult> Diagnose()
    {
        if (_diagnoseCache is { } c && DateTime.UtcNow - c.At < TimeSpan.FromMinutes(1)) return Ok(c.Result);
        async Task<object> Probe(string name, string url)
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                using var res = await SteamHttp(10).GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                return new { name, ok = true, status = (int)res.StatusCode, ms = sw.ElapsedMilliseconds };
            }
            catch (Exception ex) { return new { name, ok = false, error = NetworkReason(ex, new Uri(url).Host), ms = sw.ElapsedMilliseconds }; }
        }
        var results = await Task.WhenAll(
            Probe("Steam 登录核对（steamcommunity.com）", OpenIdEndpoint),
            Probe("Steam 资料接口（api.steampowered.com）", "https://api.steampowered.com/ISteamWebAPIUtil/GetServerInfo/v1/"));
        var result = new
        {
            proxy = string.IsNullOrWhiteSpace(config["Steam:Proxy"]) ? "没配置（直连）" : "已配置",
            apiKey = ApiKey == null ? "没配置" : "已配置",
            checks = results,
            at = DateTime.UtcNow,
        };
        _diagnoseCache = (DateTime.UtcNow, result);
        return Ok(result);
    }

    // ===================== 小工具 =====================

    /// <summary>后端对外的地址（Steam 登录完跳回这里）。配置 Steam:ApiBaseUrl 优先；没配就按请求的域名，正式环境一律 https</summary>
    private string ApiBase()
    {
        var configured = config["Steam:ApiBaseUrl"]?.Trim().TrimEnd('/');
        if (!string.IsNullOrEmpty(configured)) return configured;
        var host = Request.Host.Host;
        var local = host is "localhost" or "127.0.0.1";
        var scheme = local ? Request.Scheme : "https";
        return $"{scheme}://{Request.Host}{Request.PathBase}".TrimEnd('/');
    }

    /// <summary>绑完跳回的页面只能是我们自己的网站（防止被拿去跳到别的网站）</summary>
    private bool IsAllowedBack(string back)
    {
        if (!Uri.TryCreate(back, UriKind.Absolute, out var u)) return false;
        if (u.Scheme == Uri.UriSchemeHttp && (u.Host == "localhost" || u.Host == "127.0.0.1")) return true;
        var origin = u.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        var allowed = config.GetSection("Steam:ReturnOrigins").Get<string[]>() ?? new[] { "https://www.bianyuzhou.com", "https://bianyuzhou.com" };
        return allowed.Any(a => string.Equals(a.TrimEnd('/'), origin, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>在返回地址后面加参数（地址是 #/user/xxx?tab=steam 这种，参数加在 # 后面那段上）</summary>
    private static string AppendQuery(string url, string key, string value)
    {
        var hash = url.IndexOf('#');
        var tail = hash >= 0 ? url[hash..] : url;
        var sep = tail.Contains('?') ? "&" : "?";
        return $"{url}{sep}{Uri.EscapeDataString(key)}={Uri.EscapeDataString(value)}";
    }
}
