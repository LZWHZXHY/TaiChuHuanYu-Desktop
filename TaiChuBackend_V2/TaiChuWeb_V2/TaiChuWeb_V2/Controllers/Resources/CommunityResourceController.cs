using System.Reactive.Linq; // MinIO 列文件返回的是 IObservable 流，和旧的 ResourceController 一样用 Rx 遍历
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Minio;
using Minio.ApiEndpoints;
using Minio.DataModel.Args;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services;
using TaiChuWeb_V2.Services.Notify;
using TaiChuWeb_V2.Services.Resources;

namespace TaiChuWeb_V2.Controllers.Resources;

/// <summary>
/// 知识库「下载资源」：
/// · 列表 / 详情（公开）：审核通过的用户资源 + 站长直接放进 NAS 的「官方资源」；
/// · 下载（要登录）：价格单位是经验。付费资源第一次下载扣下载者的经验、同样多的经验加给上传者，买一次永久可以重下（资源区不碰土豆）；
/// · 上传（要登录）：浏览器把文件分片直接传到 NAS 的 MinIO（不走网站服务器带宽），传完等管理员审核；
/// · 我的资源：状态、打回理由、下载数、经验收入，修改 / 删除；
/// · 管理：审核通过 / 打回 / 下架 / 恢复（超级管理员或「用户审核」权限，和约战作品审核一样）；
/// · 荣誉墙「资源贡献」榜。
/// 旧的 /api/resource/list、/api/resource/download 保留给旧页面和知识图谱用（只列官方资源）。
/// </summary>
[ApiController]
[Route("api/resources")]
public class CommunityResourceController(
    AppDbContext db,
    CommunityResourceStore store,
    ResourceStorage storage,
    SystemConfigService sysConfig,
    IMinioClient minio,
    IMemoryCache cache,
    ILogger<CommunityResourceController> logger) : ControllerBase
{
    private const string OfficialCacheKey = "community-resources:official";
    private const string MyResourcesLink = "/knowledge?type=resource&mine=1";

    private Guid? Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    /// <summary>审核员：超级管理员或「用户审核」权限（和约战作品审核同一套判断）</summary>
    private async Task<bool> IsReviewerAsync(Guid me) =>
        await db.UserPermissions.AsNoTracking().AnyAsync(p => p.UserId == me && (p.Permission == AdminPermission.SuperAdmin || p.Permission == AdminPermission.User_Audit));

    private async Task<int> MaxPriceAsync() => Math.Max(0, await sysConfig.GetIntAsync(ResourceRules.MaxPriceConfigKey, ResourceRules.DefaultMaxPrice));

    private async Task<long> MaxFileBytesAsync()
    {
        var mb = await sysConfig.GetIntAsync(ResourceRules.MaxFileMbConfigKey, 0);
        return mb > 0 ? mb * 1024L * 1024L : ResourceRules.DefaultMaxFileBytes;
    }

    private static string Clip(string? text, int max) => string.IsNullOrEmpty(text) ? "" : text.Length > max ? text[..max] + "…" : text;

    private static object Fail(string message, string? code = null) => new { message, code };

    private async Task<Dictionary<Guid, (string Name, string? Avatar)>> UsersAsync(IEnumerable<Guid> ids)
    {
        var list = ids.Distinct().ToList();
        if (list.Count == 0) return new Dictionary<Guid, (string Name, string? Avatar)>();
        var rows = await db.Users.AsNoTracking().Where(u => list.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, Avatar = u.Profile != null ? u.Profile.Avatar : null }).ToListAsync();
        return rows.ToDictionary(r => r.Id, r => (r.Username, r.Avatar));
    }

    private static object? UploaderView(Guid id, Dictionary<Guid, (string Name, string? Avatar)> users) =>
        users.TryGetValue(id, out var u) ? new { id, name = u.Name, avatar = u.Avatar } : new { id, name = "社区用户", avatar = (string?)null };

    /// <summary>公开展示用的字段（列表、详情、我的资源都用这一套，前端一个类型就够）</summary>
    private static Dictionary<string, object?> PublicView(ResourceRow r, Dictionary<Guid, (string Name, string? Avatar)> users, bool purchased, bool mine) => new()
    {
        ["id"] = r.Id.ToString(),
        ["kind"] = "community",
        ["key"] = null,
        ["title"] = r.Title,
        ["description"] = r.Description ?? "",
        ["category"] = r.Category,
        // 位置：上传者没填就用分类名（只是展示 / 整理用的，NAS 上的真实位置不变）
        ["folder"] = ResourceRules.EffectiveFolder(r.Folder, r.Category),
        ["tags"] = r.TagList,
        ["fileName"] = r.FileName,
        ["ext"] = ResourceRules.ExtensionOf(r.FileName),
        ["size"] = r.Size,
        ["price"] = r.Price,
        ["downloadCount"] = r.DownloadCount,
        ["uniqueDownloadCount"] = r.UniqueDownloadCount,
        ["uploader"] = UploaderView(r.UploaderId, users),
        ["publishedAt"] = r.PublishedAt ?? r.UpdatedAt,
        ["purchased"] = purchased,
        ["mine"] = mine,
    };

    /// <summary>上传者自己 / 管理员额外能看的字段</summary>
    private static Dictionary<string, object?> OwnerView(ResourceRow r, Dictionary<Guid, (string Name, string? Avatar)> users, bool mine)
    {
        var v = PublicView(r, users, false, mine);
        v["status"] = r.Status;
        v["reviewNote"] = r.ReviewNote;
        v["reviewedAt"] = r.ReviewedAt;
        v["folderRaw"] = r.Folder; // 上传者自己填的位置（编辑框里用；null = 用分类名）
        v["storage"] = r.Storage == ResourceStorage.Cos ? ResourceStorage.Cos : ResourceStorage.Nas; // 文件存在哪（NAS / 腾讯云）
        v["expEarned"] = r.ExpEarned;
        v["createdAt"] = r.CreatedAt;
        v["updatedAt"] = r.UpdatedAt;
        v["partSize"] = r.PartSize;
        v["partCount"] = r.PartCount;
        return v;
    }

    // =====================================================================
    // 官方资源：站长直接放进桶里、不在 uploads/ 下面的文件（照旧免费、可以匿名下载）
    // =====================================================================

    private sealed record OfficialFile(string Key, long Size, DateTime? LastModified);

    /// <summary>列一遍桶很慢（NAS 上十几 TB），缓存 5 分钟</summary>
    private async Task<List<OfficialFile>> OfficialAsync()
    {
        if (cache.TryGetValue(OfficialCacheKey, out List<OfficialFile>? cached) && cached != null) return cached;
        var files = new List<OfficialFile>();
        var listArgs = new ListObjectsArgs().WithBucket(storage.Bucket).WithPrefix("").WithRecursive(true);
        var observable = minio.ListObjectsAsync(listArgs);
        await observable.ForEachAsync(item =>
        {
            // 用户上传区不算官方资源（没审核的、付费的都在里面）
            if (item.IsDir || ResourceRules.IsUserUploadKey(item.Key)) return;
            files.Add(new OfficialFile(item.Key, (long)item.Size, item.LastModifiedDateTime));
        });
        cache.Set(OfficialCacheKey, files, TimeSpan.FromMinutes(5));
        return files;
    }

    private static Dictionary<string, object?> OfficialView(OfficialFile f)
    {
        var slash = f.Key.LastIndexOf('/');
        var name = slash >= 0 ? f.Key[(slash + 1)..] : f.Key;
        return new Dictionary<string, object?>
        {
            ["id"] = "official:" + f.Key,
            ["kind"] = "official",
            ["key"] = f.Key,
            ["title"] = name,
            ["description"] = "",
            ["category"] = "官方",
            // 官方资源的位置 = 它在 NAS 里的文件夹；根目录是空字符串（前端显示「根目录」）
            ["folder"] = ResourceRules.OfficialFolder(f.Key),
            ["tags"] = new List<string>(),
            ["fileName"] = name,
            ["ext"] = ResourceRules.ExtensionOf(name),
            ["size"] = f.Size,
            ["price"] = 0,
            ["downloadCount"] = 0,
            ["uniqueDownloadCount"] = 0,
            ["uploader"] = null,
            ["publishedAt"] = f.LastModified,
            ["purchased"] = false,
            ["mine"] = false,
        };
    }

    // =====================================================================
    // 公开：列表 / 详情 / 上传设置 / 资源贡献榜
    // =====================================================================

    private sealed record Entry(Dictionary<string, object?> View, DateTime Time, int Downloads, int Price, string Category, bool Official, string Search, string Folder);

    /// <summary>
    /// 资源列表：q 搜索（社区资源：标题、介绍、标签、文件名、上传者、分类、位置；官方资源：文件路径），每个词都要出现，不分大小写
    /// （关系图谱里的资源节点用同样的字段和规则匹配）；category 分类（official = 只看官方资源）；
    /// folder 位置（含下面的子文件夹；「/」= 官方资源的根目录）；kind=community / official 只要一种（关系图谱取社区资源用）；
    /// sort：new 最新 / downloads 下载最多 / free 只看免费 / paid 只看付费。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Browse([FromQuery] string? q, [FromQuery] string? category, [FromQuery] string? sort, [FromQuery] string? folder = null, [FromQuery] string? kind = null,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 24, CancellationToken ct = default)
    {
        page = Math.Clamp(page, 1, 100000);
        // 列表页一页最多 60；关系图谱要一次取全部社区资源，放宽到 1000
        pageSize = Math.Clamp(pageSize, 1, 1000);
        var me = Me;

        var rows = new List<ResourceRow>();
        var communityError = false;
        try { rows = await store.ApprovedAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { communityError = true; logger.LogError(ex, "读取社区资源失败"); }
        var users = await UsersAsync(rows.Select(r => r.UploaderId));
        var purchased = me is Guid uid && rows.Count > 0 ? await store.PurchasedIdsAsync(uid, ct) : new HashSet<Guid>();

        var official = new List<OfficialFile>();
        var officialError = false;
        try { official = await OfficialAsync(); }
        catch (Exception ex) { officialError = true; logger.LogWarning(ex, "读取 NAS 上的官方资源目录失败"); }

        var entries = new List<Entry>(rows.Count + official.Count);
        foreach (var r in rows)
        {
            var uploaderName = users.TryGetValue(r.UploaderId, out var u) ? u.Name : "";
            var location = ResourceRules.EffectiveFolder(r.Folder, r.Category);
            var search = string.Join('\n', r.Title, r.Description ?? "", r.Tags, r.FileName, uploaderName, r.Category, location).ToLowerInvariant();
            entries.Add(new Entry(PublicView(r, users, purchased.Contains(r.Id), me == r.UploaderId), r.PublishedAt ?? r.UpdatedAt, r.DownloadCount, r.Price, r.Category, false, search, location));
        }
        foreach (var f in official)
            entries.Add(new Entry(OfficialView(f), f.LastModified ?? DateTime.MinValue, 0, 0, "官方", true, (f.Key + "\n官方").ToLowerInvariant(), ResourceRules.OfficialFolder(f.Key)));

        IEnumerable<Entry> query = entries;
        var terms = (q ?? "").Trim().ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(10).ToList();
        if (terms.Count > 0) query = query.Where(e => terms.All(t => e.Search.Contains(t)));
        if (category == "official") query = query.Where(e => e.Official);
        else if (!string.IsNullOrEmpty(category) && ResourceRules.Categories.Contains(category)) query = query.Where(e => !e.Official && e.Category == category);
        if (kind == "community") query = query.Where(e => !e.Official);
        else if (kind == "official") query = query.Where(e => e.Official);
        if (folder == "/") query = query.Where(e => e.Folder.Length == 0);
        else if (ResourceRules.CleanFolder(folder) is string place)
            query = query.Where(e => e.Folder == place || e.Folder.StartsWith(place + "/", StringComparison.Ordinal));
        query = sort switch
        {
            "downloads" => query.OrderByDescending(e => e.Downloads).ThenByDescending(e => e.Time),
            "free" => query.Where(e => e.Price == 0).OrderByDescending(e => e.Time),
            "paid" => query.Where(e => e.Price > 0).OrderByDescending(e => e.Time),
            _ => query.OrderByDescending(e => e.Time),
        };
        var all = query.ToList();
        return Ok(new
        {
            items = all.Skip((page - 1) * pageSize).Take(pageSize).Select(e => e.View),
            total = all.Count,
            page,
            pageSize,
            officialError,
            communityError,
        });
    }

    /// <summary>上传需要的设置：分类、价格上限、文件上限、分片大小、禁止的扩展名</summary>
    [HttpGet("config")]
    public async Task<IActionResult> Config() => Ok(new
    {
        categories = ResourceRules.Categories,
        maxPrice = await MaxPriceAsync(),
        maxFileBytes = await MaxFileBytesAsync(),
        partSize = ResourceRules.PartSize,
        concurrency = ResourceRules.UploadConcurrency,
        blockedExtensions = ResourceRules.BlockedExtensions.OrderBy(x => x),
        titleMax = ResourceRules.TitleMax,
        descriptionMax = ResourceRules.DescriptionMax,
        tagCountMax = ResourceRules.TagCountMax,
        approveExp = ResourceRules.ApproveExp,
        freeDownloadExp = ResourceRules.FreeDownloadExp,
        freeDownloadDailyCap = ResourceRules.FreeDownloadDailyCap,
        // 上传时可以选存到哪（腾讯云没配置就只有 NAS）
        storages = new[]
        {
            new { key = ResourceStorage.Nas, label = "NAS", hint = "站长的 NAS（在海外），空间大；在海外上传、下载快，在国内可能比较慢", available = true },
            new { key = ResourceStorage.Cos, label = "腾讯云", hint = storage.CosAccelerated ? "腾讯云对象存储（北京，已开全球加速），国内、海外都比较快" : "腾讯云对象存储（北京），在国内上传、下载都快", available = storage.CosAvailable },
        },
        defaultStorage = ResourceStorage.Nas,
    });

    /// <summary>
    /// 可选的位置：官方资源的所有文件夹路径（含上级文件夹）+ 社区资源已经用过的位置，去重排序。
    /// 上传、编辑、审核时「选位置」和列表的「按位置筛选」都用它（也可以手填新位置）。
    /// </summary>
    [HttpGet("folders")]
    public async Task<IActionResult> Folders(CancellationToken ct)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        try
        {
            foreach (var f in await OfficialAsync())
            {
                var dir = ResourceRules.OfficialFolder(f.Key);
                // 「PS笔刷/分类A」也要有「PS笔刷」这一层
                for (var i = dir.IndexOf('/'); i > 0; i = dir.IndexOf('/', i + 1)) set.Add(dir[..i]);
                if (dir.Length > 0) set.Add(dir);
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "读取官方资源目录失败（位置列表只给社区资源用过的）"); }
        try { foreach (var f in await store.UsedFoldersAsync(ct)) set.Add(f); }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "读取社区资源的位置失败"); }
        return Ok(set.OrderBy(x => x, StringComparer.Ordinal).Take(3000));
    }

    /// <summary>资源详情：已公开的谁都能看；没公开的只有上传者和管理员能看</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Detail(Guid id, CancellationToken ct)
    {
        var row = await store.GetAsync(id, ct);
        if (row == null || row.DeletedAt != null) return NotFound(Fail("资源不存在或已经删除"));
        var me = Me;
        var mine = me == row.UploaderId;
        var reviewer = me is Guid uid && !mine && row.Status != ResourceStatus.Approved && await IsReviewerAsync(uid);
        if (row.Status != ResourceStatus.Approved && !mine && !reviewer) return NotFound(Fail("资源还没公开，或者已经下架了"));
        var users = await UsersAsync(new[] { row.UploaderId });
        var purchased = me is Guid buyer && row.Price > 0 && !mine && await store.HasPurchasedAsync(row.Id, buyer, ct);
        return Ok(mine || reviewer ? OwnerView(row, users, mine) : PublicView(row, users, purchased, mine));
    }

    /// <summary>荣誉墙「资源贡献」榜：by=count 按公开的资源数，by=downloads 按被多少人下载过（同一个人重复下载只算一次，防刷）</summary>
    [HttpGet("leaderboard")]
    public async Task<IActionResult> Leaderboard([FromQuery] string by = "count", [FromQuery] int limit = 50, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        List<CommunityResourceStore.ContributorRow> rows;
        try { rows = await store.ContributorsAsync(ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "读取资源贡献榜失败"); rows = new(); }
        var ordered = (by == "downloads"
            ? rows.Where(r => r.UniqueDownloads > 0).OrderByDescending(r => r.UniqueDownloads).ThenByDescending(r => r.Approved)
            : rows.OrderByDescending(r => r.Approved).ThenByDescending(r => r.UniqueDownloads)).Take(limit).ToList();
        var users = await UsersAsync(ordered.Select(r => r.UploaderId));
        return Ok(ordered.Where(r => users.ContainsKey(r.UploaderId)).Select((r, i) => new
        {
            rank = i + 1,
            userId = r.UploaderId,
            name = users[r.UploaderId].Name,
            avatar = users[r.UploaderId].Avatar,
            approved = r.Approved,
            downloads = r.UniqueDownloads,
            totalDownloads = r.Downloads,
        }));
    }

    // =====================================================================
    // 下载（要登录）
    // =====================================================================

    public sealed class DownloadDto
    {
        /// <summary>付费资源第一次下载时，前端把用户确认过的价格带上；和现在的价格不一样（上传者刚改过价）就不扣，让用户重新确认</summary>
        public int? ConfirmPrice { get; set; }
    }

    /// <summary>
    /// 拿下载链接（15 分钟过期）。
    /// · 自己的资源不扣经验、不计下载数；
    /// · 付费资源第一次下载：扣下载者经验、上传者加同样多 + 记购买 + 双方经验流水，同一个事务；以后再下载不扣；
    /// · 免费资源：每多一个不同的人下载，上传者 +2 经验（每天最多 50）。
    /// </summary>
    [Authorize]
    [HttpPost("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, [FromBody] DownloadDto? dto, CancellationToken ct)
    {
        if (Me is not Guid me) return Unauthorized();
        var row = await store.GetAsync(id, ct);
        if (row == null || row.DeletedAt != null) return NotFound(Fail("资源不存在或已经删除"));
        if (row.Status == ResourceStatus.Uploading) return BadRequest(Fail("这个资源还没传完"));
        var mine = row.UploaderId == me;
        if (row.Status != ResourceStatus.Approved)
        {
            // 没公开 / 被打回 / 被下架：只有上传者和管理员能拿链接（买过的人也不行）
            if (!mine && !await IsReviewerAsync(me)) return NotFound(Fail("资源还没公开，或者已经下架了"));
            return Ok(new { url = storage.For(row.Storage).DownloadUrl(row.ObjectKey, row.FileName), fileName = row.FileName, charged = 0, balance = (long?)null });
        }
        if (mine) return Ok(new { url = storage.For(row.Storage).DownloadUrl(row.ObjectKey, row.FileName), fileName = row.FileName, charged = 0, balance = (long?)null });

        var charged = 0;
        long? balance = null;
        if (row.Price > 0 && !await store.HasPurchasedAsync(row.Id, me, ct))
        {
            if (dto?.ConfirmPrice != row.Price)
                return Conflict(new { code = "confirm", price = row.Price, message = $"这个资源要花 {row.Price} 点经验，确认后再下载" });
            var (outcome, bal) = await store.PurchaseAsync(row, me, ExpTableNames.From(db), ct);
            if (outcome == PurchaseOutcome.NotEnough)
                return BadRequest(new { code = "insufficient", price = row.Price, balance = bal, shortfall = row.Price - bal, message = $"经验不够，还差 {row.Price - bal} 经验" });
            if (outcome == PurchaseOutcome.MissingStats)
                return BadRequest(Fail("账号的经验数据不完整，暂时不能购买，请联系管理员"));
            if (outcome == PurchaseOutcome.Bought)
            {
                charged = row.Price;
                balance = bal;
                // 每次购买都通知上传者（付费资源买的人不会太多；以后嫌多可以改成每天合并一条）
                new NotificationService(db).Add(row.UploaderId, me, "resource", "resource", row.Id.ToString(),
                    $"有人花 {row.Price} 点经验买了你的资源《{Clip(row.Title, 30)}》，经验已经加给你了", MyResourcesLink);
                await db.SaveChangesAsync(ct);
            }
        }

        try
        {
            var first = await store.RecordDownloadAsync(row, me, ct);
            if (first && row.Price == 0) await RewardFreeDownloadAsync(row, me, ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "记录资源下载失败 {Id}", row.Id); }

        return Ok(new { url = storage.For(row.Storage).DownloadUrl(row.ObjectKey, row.FileName), fileName = row.FileName, charged, balance });
    }

    /// <summary>免费资源有新的人下载：上传者 +2 经验（每天最多 50；同一个下载者对同一个资源只算一次，由「首次下载」表保证）</summary>
    private async Task RewardFreeDownloadAsync(ResourceRow row, Guid downloader, CancellationToken ct)
    {
        try
        {
            var today = await store.FreeRewardTodayAsync(row.UploaderId, ct);
            var amount = Math.Min(ResourceRules.FreeDownloadExp, ResourceRules.FreeDownloadDailyCap - today);
            if (amount <= 0) return;
            await store.GrantFreeDownloadExpAsync(row, downloader, amount, ExpTableNames.From(db), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogError(ex, "免费资源下载奖励发放失败 {Id}", row.Id); }
    }

    /// <summary>
    /// 我现在的经验和等级；cost 给了的话，再算一下花掉这么多经验后是几级（付费确认框提示「等级可能下降」用）。
    /// 等级用的是 UserStats.CalculateLevel，和全站一致。
    /// </summary>
    [Authorize]
    [HttpGet("my-exp")]
    public async Task<IActionResult> MyExp([FromQuery] int cost = 0, CancellationToken ct = default)
    {
        if (Me is not Guid me) return Unauthorized();
        var exp = await db.UserStats.AsNoTracking().Where(s => s.UserId == me).Select(s => (long?)s.Experience).FirstOrDefaultAsync(ct) ?? 0;
        var after = Math.Max(0, exp - Math.Max(0, cost));
        return Ok(new { experience = exp, level = UserStats.CalculateLevel(exp), experienceAfter = after, levelAfter = UserStats.CalculateLevel(after) });
    }

    // =====================================================================
    // 上传（要登录）：init → 浏览器分片直传 → complete
    // =====================================================================

    public sealed class MetaDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public List<string>? Tags { get; set; }
        public int Price { get; set; }
        /// <summary>位置；不传（null）= 不改，空字符串 = 用分类名</summary>
        public string? Folder { get; set; }
    }

    public sealed class InitUploadDto
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Category { get; set; }
        public List<string>? Tags { get; set; }
        public int Price { get; set; }
        public string? FileName { get; set; }
        public long Size { get; set; }
        public string? ContentType { get; set; }
        /// <summary>位置（可以不填，不填就用分类名）</summary>
        public string? Folder { get; set; }
        /// <summary>存到哪：nas（默认）/ cos（腾讯云）</summary>
        public string? Storage { get; set; }
    }

    private sealed record CleanMeta(string Title, string Description, string Category, string Tags, int Price);

    /// <summary>检查并清洗标题、介绍、分类、标签、价格。返回 (错误提示, 清洗后的值)</summary>
    private static (string? Error, CleanMeta? Meta) ValidateMeta(string? title, string? description, string? category, List<string>? tags, int price, int maxPrice)
    {
        var t = (title ?? "").Trim();
        if (t.Length == 0) return ("请填写标题", null);
        if (t.Length > ResourceRules.TitleMax) return ($"标题最多 {ResourceRules.TitleMax} 个字", null);
        var d = (description ?? "").Trim();
        if (d.Length > ResourceRules.DescriptionMax) return ($"介绍最多 {ResourceRules.DescriptionMax} 个字", null);
        var c = (category ?? "").Trim();
        if (!ResourceRules.Categories.Contains(c)) return ("请选择分类", null);
        if (price < 0) return ("价格不能是负数", null);
        if (price > maxPrice) return ($"价格最多 {maxPrice} 经验", null);
        return (null, new CleanMeta(t, d, c, string.Join(',', ResourceRules.CleanTags(tags)), price));
    }

    private object PartUrls(ResourceRow row, IEnumerable<int> partNumbers, DateTime expiresAt) =>
        partNumbers.Select(n => new { partNumber = n, url = storage.For(row.Storage).PartUrl(row.ObjectKey, row.UploadId!, n, expiresAt) }).ToList();

    /// <summary>第 n 片应该多大（最后一片是剩下的）</summary>
    private static long ExpectedPartSize(ResourceRow row, int n)
    {
        var partSize = row.PartSize > 0 ? row.PartSize : ResourceRules.PartSize;
        var count = row.PartCount;
        return n < count ? partSize : row.Size - partSize * (count - 1);
    }

    /// <summary>开始上传：检查信息 → 在 MinIO 开一次分片上传 → 记一条「上传中」→ 返回每一片的上传链接</summary>
    [Authorize]
    [HttpPost("upload/init")]
    public async Task<IActionResult> InitUpload([FromBody] InitUploadDto dto, CancellationToken ct)
    {
        if (Me is not Guid me) return Unauthorized();
        var (error, meta) = ValidateMeta(dto.Title, dto.Description, dto.Category, dto.Tags, dto.Price, await MaxPriceAsync());
        if (error != null || meta == null) return BadRequest(Fail(error ?? "信息不完整"));

        var fileName = ResourceRules.CleanFileName(dto.FileName);
        var ext = ResourceRules.ExtensionOf(fileName);
        if (ResourceRules.BlockedExtensions.Contains(ext))
            return BadRequest(Fail($"不能上传 {ext} 文件：能直接运行的程序、脚本和安装包不收。确实要分享工具的话，请打包成压缩包，并在介绍里写清楚是什么、怎么用（管理员审核时会检查）", "blocked_ext"));
        if (dto.Size <= 0) return BadRequest(Fail("文件是空的"));
        var maxBytes = await MaxFileBytesAsync();
        if (dto.Size > maxBytes) return BadRequest(Fail($"文件太大了，单个文件最大 {maxBytes / 1024d / 1024 / 1024:0.##} GB", "too_large"));

        var mineCounts = await store.CountMineAsync(me, ct);
        if (mineCounts.GetValueOrDefault(ResourceStatus.Uploading) >= ResourceRules.MaxUploadingPerUser)
            return BadRequest(Fail($"你同时最多只能有 {ResourceRules.MaxUploadingPerUser} 个没传完的上传，请先在「我的资源」里继续传完或者取消", "too_many_uploading"));
        if (mineCounts.GetValueOrDefault(ResourceStatus.Pending) >= ResourceRules.MaxPendingPerUser)
            return BadRequest(Fail($"你已经有 {ResourceRules.MaxPendingPerUser} 个资源在等审核了，等审核完再传吧", "too_many_pending"));

        var target = string.IsNullOrWhiteSpace(dto.Storage) ? ResourceStorage.Nas : dto.Storage.Trim().ToLowerInvariant();
        if (target != ResourceStorage.Nas && target != ResourceStorage.Cos) return BadRequest(Fail("存放位置不对，请重新选择"));
        if (target == ResourceStorage.Cos && !storage.CosAvailable) return BadRequest(Fail("网站还没有配置腾讯云存储，请选 NAS", "storage_unavailable"));

        var id = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var row = new ResourceRow
        {
            Id = id,
            UploaderId = me,
            Title = meta.Title,
            Description = meta.Description,
            Category = meta.Category,
            Folder = ResourceRules.CleanFolder(dto.Folder),
            Tags = meta.Tags,
            // 腾讯云上放在 community-resources/ 下面（前端的临时密钥写不了这个目录）
            ObjectKey = (target == ResourceStorage.Cos ? ResourceRules.CosKeyPrefix : "") + ResourceRules.ObjectKeyFor(me, id, fileName),
            Storage = target == ResourceStorage.Cos ? ResourceStorage.Cos : null,
            FileName = fileName,
            Size = dto.Size,
            ContentType = ResourceRules.CleanContentType(dto.ContentType),
            Price = meta.Price,
            Status = ResourceStatus.Uploading,
            PartSize = ResourceRules.PartSize,
            CreatedAt = now,
            UpdatedAt = now,
        };
        try { row.UploadId = await storage.For(row.Storage).StartUploadAsync(row.ObjectKey, row.ContentType, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "开始资源分片上传失败");
            return StatusCode(502, Fail($"连不上文件存储（{ResourceStorage.Label(row.Storage)}），请稍后再试"));
        }
        try { await store.InsertAsync(row, ct); }
        catch
        {
            // 记录没写进去：把刚开的分片上传也取消掉，不留垃圾
            try { await storage.For(row.Storage).AbortAsync(row.ObjectKey, row.UploadId!, CancellationToken.None); } catch { /* 交给 MinIO 自己的过期清理 */ }
            throw;
        }

        var expiresAt = DateTime.UtcNow.Add(ResourceRules.PartUrlLife);
        return Ok(new
        {
            id = id.ToString(),
            fileName,
            partSize = row.PartSize,
            partCount = row.PartCount,
            concurrency = ResourceRules.UploadConcurrency,
            expiresAt,
            parts = PartUrls(row, Enumerable.Range(1, row.PartCount), expiresAt),
        });
    }

    /// <summary>找出自己的、还在上传中的资源；不是的话返回错误结果</summary>
    private async Task<(ResourceRow? Row, IActionResult? Error)> UploadingRowAsync(Guid id, Guid me, CancellationToken ct)
    {
        var row = await store.GetAsync(id, ct);
        if (row == null || row.UploaderId != me || row.DeletedAt != null) return (null, NotFound(Fail("找不到这次上传，可能已经取消或超过 24 小时被清理了", "gone")));
        if (row.Status != ResourceStatus.Uploading || string.IsNullOrEmpty(row.UploadId)) return (row, BadRequest(Fail("这个资源已经传完了", "done")));
        return (row, null);
    }

    public sealed class PartsDto
    {
        public List<int>? Parts { get; set; }
    }

    /// <summary>重新要上传链接（链接过期了、或者页面刷新后接着传）</summary>
    [Authorize]
    [HttpPost("upload/{id:guid}/urls")]
    public async Task<IActionResult> RefreshUrls(Guid id, [FromBody] PartsDto dto, CancellationToken ct)
    {
        if (Me is not Guid me) return Unauthorized();
        var (row, err) = await UploadingRowAsync(id, me, ct);
        if (err != null || row == null) return err ?? NotFound();
        var count = row.PartCount;
        var wanted = (dto.Parts ?? new List<int>()).Where(n => n >= 1 && n <= count).Distinct().OrderBy(n => n).Take(1000).ToList();
        if (wanted.Count == 0) wanted = Enumerable.Range(1, count).ToList();
        var expiresAt = DateTime.UtcNow.Add(ResourceRules.PartUrlLife);
        return Ok(new { expiresAt, parts = PartUrls(row, wanted, expiresAt) });
    }

    public sealed class PartDoneDto
    {
        public int PartNumber { get; set; }
        public long Size { get; set; }
    }

    /// <summary>浏览器每传完一片报告一下，服务器记住进度（页面刷新后知道哪些片不用再传）</summary>
    [Authorize]
    [HttpPost("upload/{id:guid}/part")]
    public async Task<IActionResult> PartDone(Guid id, [FromBody] PartDoneDto dto, CancellationToken ct)
    {
        if (Me is not Guid me) return Unauthorized();
        var (row, err) = await UploadingRowAsync(id, me, ct);
        if (err != null || row == null) return err ?? NotFound();
        if (dto.PartNumber < 1 || dto.PartNumber > row.PartCount) return BadRequest(Fail("片号不对"));
        await store.AddPartAsync(row.Id, dto.PartNumber, Math.Max(0, dto.Size), ct);
        return Ok(new { ok = true });
    }

    /// <summary>
    /// 上传进度（继续上传用）：以 MinIO 上实际收到的片为准（大小不对的片算没传，会重传覆盖），
    /// 同时参考服务器记下的进度。上传编号已经失效（超过 24 小时被清理）时 expired=true。
    /// </summary>
    [Authorize]
    [HttpGet("upload/{id:guid}")]
    public async Task<IActionResult> UploadStatus(Guid id, CancellationToken ct)
    {
        if (Me is not Guid me) return Unauthorized();
        var (row, err) = await UploadingRowAsync(id, me, ct);
        if (err != null || row == null) return err ?? NotFound();
        List<int> done;
        var expired = false;
        try
        {
            var listed = await storage.For(row.Storage).ListPartsAsync(row.ObjectKey, row.UploadId!, ct);
            if (listed == null)
            {
                // 上传编号不在了，但如果文件已经合并好躺在 NAS 上（上次「完成」只是核对那步出错），
                // 就告诉前端「所有片都传完了」，前端再点「完成」时会直接算成功
                long? existing = null;
                try { existing = await storage.For(row.Storage).SizeAsync(row.ObjectKey, ct); } catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "检查已合并的文件失败 {Id}", row.Id); }
                if (existing == row.Size) done = Enumerable.Range(1, row.PartCount).ToList();
                else { expired = true; done = new List<int>(); }
            }
            else done = listed.Where(p => p.PartNumber is int n && n >= 1 && n <= row.PartCount && p.Size == ExpectedPartSize(row, n))
                .Select(p => p.PartNumber!.Value).Distinct().OrderBy(n => n).ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // NAS 暂时问不到：先用服务器记下的进度
            logger.LogWarning(ex, "查询分片进度失败 {Id}", row.Id);
            var recorded = await store.PartsAsync(row.Id, ct);
            done = recorded.Where(p => p.Value == ExpectedPartSize(row, p.Key)).Select(p => p.Key).OrderBy(n => n).ToList();
        }
        return Ok(new
        {
            id = row.Id.ToString(),
            title = row.Title,
            fileName = row.FileName,
            size = row.Size,
            partSize = row.PartSize,
            partCount = row.PartCount,
            concurrency = ResourceRules.UploadConcurrency,
            doneParts = done,
            expired,
        });
    }

    /// <summary>
    /// 传完了：服务器自己向 MinIO 要各片的 ETag（ListParts），每一片都在、大小都对才合并；
    /// 合并后再核对整个文件的大小，不对就删掉这次的文件、作废。成功后进入「待审核」。
    /// </summary>
    [Authorize]
    [HttpPost("upload/{id:guid}/complete")]
    public async Task<IActionResult> Complete(Guid id, CancellationToken ct)
    {
        if (Me is not Guid me) return Unauthorized();
        var current = await store.GetAsync(id, ct);
        if (current != null && current.UploaderId == me && current.DeletedAt == null && current.Status == ResourceStatus.Pending)
            return Ok(new { status = current.Status, message = "已提交，等管理员审核" }); // 重复点「完成」
        var (row, err) = await UploadingRowAsync(id, me, ct);
        if (err != null || row == null) return err ?? NotFound();

        List<Amazon.S3.Model.PartDetail>? listed;
        try { listed = await storage.For(row.Storage).ListPartsAsync(row.ObjectKey, row.UploadId!, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "合并前查询分片失败 {Id}", row.Id);
            return StatusCode(502, Fail($"连不上文件存储（{ResourceStorage.Label(row.Storage)}），请稍后再点一次「完成」"));
        }
        if (listed == null)
        {
            // 上传编号已经不在了：可能是上一次「完成」其实已经合并成功（只是后面核对大小那步出错），
            // 这时 NAS 上已经有完整的文件，大小对得上就直接算传完，不让用户白传一遍
            long? existing = null;
            try { existing = await storage.For(row.Storage).SizeAsync(row.ObjectKey, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "检查已合并的文件失败 {Id}", row.Id); }
            if (existing == row.Size)
            {
                await store.MarkUploadedAsync(row.Id, CancellationToken.None);
                await store.ClearPartsAsync(row.Id, CancellationToken.None);
                return Ok(new { status = ResourceStatus.Pending, message = "已提交，等管理员审核" });
            }
            return BadRequest(Fail("这次上传已经失效了（超过 24 小时或被取消），请重新上传", "expired"));
        }

        var byNumber = listed.Where(p => p.PartNumber is int n && n >= 1 && n <= row.PartCount)
            .GroupBy(p => p.PartNumber!.Value).ToDictionary(g => g.Key, g => g.First());
        var missing = Enumerable.Range(1, row.PartCount)
            .Where(n => !byNumber.TryGetValue(n, out var p) || p.Size != ExpectedPartSize(row, n)).ToList();
        if (missing.Count > 0)
            return BadRequest(new { code = "missing_parts", missing, message = $"还有 {missing.Count} 片没传上去或者不完整，请继续上传" });

        try { await storage.For(row.Storage).CompleteAsync(row.ObjectKey, row.UploadId!, byNumber.Values, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "合并分片失败 {Id}", row.Id);
            return StatusCode(502, Fail("文件存储合并文件失败，请稍后再点一次「完成」"));
        }

        long? actual;
        try { actual = await storage.For(row.Storage).SizeAsync(row.ObjectKey, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "核对资源大小失败 {Id}", row.Id);
            return StatusCode(502, Fail($"文件已经合并，但核对大小时连不上{ResourceStorage.Label(row.Storage)}，请稍后再点一次「完成」"));
        }
        if (actual == null)
        {
            // 合并请求可能已经成功、只是这一次没读到；也可能 MinIO 还在处理：让用户再点一次
            return StatusCode(502, Fail("文件存储还没准备好，请稍后再点一次「完成」"));
        }
        if (actual.Value != row.Size)
        {
            // 作废之前再查一次（刚合并完 MinIO 可能还没整理好），确认真的不对才删
            await Task.Delay(1500, CancellationToken.None);
            try { actual = await storage.For(row.Storage).SizeAsync(row.ObjectKey, ct) ?? actual; } catch (Exception ex) when (ex is not OperationCanceledException) { logger.LogWarning(ex, "复查资源大小失败 {Id}", row.Id); }
        }
        if (actual.Value != row.Size)
        {
            logger.LogWarning("资源 {Id} 合并后大小 {Actual} 和声明的 {Size} 不一致，作废", row.Id, actual, row.Size);
            try { await storage.For(row.Storage).DeleteAsync(row.ObjectKey, CancellationToken.None); } catch (Exception ex) { logger.LogError(ex, "删除作废的资源文件失败 {Id}", row.Id); }
            await store.SoftDeleteAsync(row.Id, false, CancellationToken.None);
            await store.ClearPartsAsync(row.Id, CancellationToken.None);
            return BadRequest(Fail("合并后的文件大小和你选的文件对不上，这次上传作废了，请重新上传", "size_mismatch"));
        }

        await store.MarkUploadedAsync(row.Id, CancellationToken.None);
        await store.ClearPartsAsync(row.Id, CancellationToken.None);
        return Ok(new { status = ResourceStatus.Pending, message = "已提交，等管理员审核" });
    }

    /// <summary>取消上传：让 MinIO 删掉已传的碎片，记录标记删除。MinIO 暂时连不上时留给后台任务再清理。</summary>
    [Authorize]
    [HttpPost("upload/{id:guid}/abort")]
    public async Task<IActionResult> Abort(Guid id, CancellationToken ct)
    {
        if (Me is not Guid me) return Unauthorized();
        var (row, err) = await UploadingRowAsync(id, me, ct);
        if (err != null || row == null) return err ?? NotFound();
        var cleaned = true;
        try { await storage.For(row.Storage).AbortAsync(row.ObjectKey, row.UploadId!, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException) { cleaned = false; logger.LogWarning(ex, "取消分片上传失败，留给后台清理 {Id}", row.Id); }
        await store.SoftDeleteAsync(row.Id, keepUploadId: !cleaned, ct);
        await store.ClearPartsAsync(row.Id, ct);
        return Ok(new { ok = true });
    }

    // =====================================================================
    // 我的资源（要登录）
    // =====================================================================

    [Authorize]
    [HttpGet("mine")]
    public async Task<IActionResult> Mine(CancellationToken ct)
    {
        if (Me is not Guid me) return Unauthorized();
        var rows = await store.ByUploaderAsync(me, ct);
        var users = await UsersAsync(new[] { me });
        return Ok(new
        {
            items = rows.Select(r => OwnerView(r, users, true)),
            totalEarned = rows.Sum(r => r.ExpEarned),
            totalDownloads = rows.Sum(r => (long)r.UniqueDownloadCount),
        });
    }

    /// <summary>
    /// 修改资料。规则：只改价格、分类、标签不用重新审核；已公开的资源改了标题或介绍要重新审核（审核期间暂时不显示）；
    /// 被打回的资源改完自动重新提交审核；被下架的不能改；改价格不影响已经买过的人。文件本身不能换，要换请重新上传。
    /// </summary>
    [Authorize]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] MetaDto dto, CancellationToken ct)
    {
        if (Me is not Guid me) return Unauthorized();
        var row = await store.GetAsync(id, ct);
        if (row == null || row.UploaderId != me || row.DeletedAt != null) return NotFound(Fail("资源不存在或已经删除"));
        if (row.Status == ResourceStatus.Removed) return BadRequest(Fail("资源已经被管理员下架，不能修改；有疑问请联系管理员"));
        var (error, meta) = ValidateMeta(dto.Title, dto.Description, dto.Category, dto.Tags, dto.Price, await MaxPriceAsync());
        if (error != null || meta == null) return BadRequest(Fail(error ?? "信息不完整"));

        var textChanged = meta.Title != row.Title || meta.Description != (row.Description ?? "");
        string? toStatus = null;
        var message = "已保存";
        if (row.Status == ResourceStatus.Approved && textChanged) { toStatus = ResourceStatus.Pending; message = "已保存。改了标题或介绍，需要管理员重新审核，审核期间资源暂时不显示"; }
        else if (row.Status == ResourceStatus.Rejected) { toStatus = ResourceStatus.Pending; message = "已保存，并重新提交审核"; }
        // 位置只是展示 / 整理用的，改了不需要重新审核
        var folder = dto.Folder == null ? row.Folder : ResourceRules.CleanFolder(dto.Folder);
        await store.UpdateMetaAsync(row.Id, meta.Title, meta.Description, meta.Category, meta.Tags, meta.Price, folder, toStatus, ct);
        return Ok(new { message, status = toStatus ?? row.Status });
    }

    /// <summary>删除（软删除）：谁都看不到、下载不了了（买过的人也不行）；NAS 上的文件保留，数据也保留</summary>
    [Authorize]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (Me is not Guid me) return Unauthorized();
        var row = await store.GetAsync(id, ct);
        if (row == null || row.UploaderId != me) return NotFound(Fail("资源不存在"));
        if (row.DeletedAt != null) return Ok(new { ok = true });
        var cleaned = true;
        if (row.Status == ResourceStatus.Uploading && !string.IsNullOrEmpty(row.UploadId))
        {
            try { await storage.For(row.Storage).AbortAsync(row.ObjectKey, row.UploadId, ct); }
            catch (Exception ex) when (ex is not OperationCanceledException) { cleaned = false; logger.LogWarning(ex, "删除时取消分片上传失败 {Id}", row.Id); }
        }
        await store.SoftDeleteAsync(row.Id, keepUploadId: !cleaned, ct);
        return Ok(new { ok = true });
    }

    // =====================================================================
    // 管理：资源审核（超级管理员或「用户审核」权限）
    // =====================================================================

    private static readonly List<string> AdminStatuses = new() { ResourceStatus.Pending, ResourceStatus.Approved, ResourceStatus.Rejected, ResourceStatus.Removed };

    /// <summary>审核时给管理员的提醒：压缩包要解开看看；文件名里藏着可执行扩展名（比如 setup.exe.zip）</summary>
    private static List<string> Warnings(ResourceRow r)
    {
        var list = new List<string>();
        var ext = ResourceRules.ExtensionOf(r.FileName);
        if (ResourceRules.ArchiveExtensions.Contains(ext)) list.Add("压缩包：请下载解开，看看里面有没有程序、脚本或者和介绍不符的东西");
        var inner = r.FileName.Split('.').Skip(1).SkipLast(1).Select(p => "." + p.ToLowerInvariant());
        if (inner.Any(ResourceRules.BlockedExtensions.Contains)) list.Add("文件名中间带有可执行程序的扩展名，可能是伪装的程序");
        if (r.Size >= 1024L * 1024 * 1024) list.Add("文件超过 1GB，下载核查会比较慢");
        return list;
    }

    [Authorize]
    [HttpGet("admin")]
    public async Task<IActionResult> AdminList([FromQuery] string status = "pending", [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default)
    {
        if (Me is not Guid me || !await IsReviewerAsync(me)) return Forbid();
        if (!AdminStatuses.Contains(status)) status = ResourceStatus.Pending;
        page = Math.Clamp(page, 1, 100000);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var (rows, total) = await store.ByStatusAsync(status, page, pageSize, ct);
        var users = await UsersAsync(rows.Select(r => r.UploaderId).Concat(rows.Where(r => r.ReviewerId.HasValue).Select(r => r.ReviewerId!.Value)));
        return Ok(new
        {
            items = rows.Select(r =>
            {
                var v = OwnerView(r, users, false);
                v["contentType"] = r.ContentType;
                v["warnings"] = Warnings(r);
                v["previewable"] = ResourceRules.PreviewTypes.ContainsKey(ResourceRules.ExtensionOf(r.FileName));
                v["reviewerName"] = r.ReviewerId is Guid rid && users.TryGetValue(rid, out var reviewer) ? reviewer.Name : null;
                return v;
            }),
            total,
            page,
            pageSize,
        });
    }

    [Authorize]
    [HttpGet("admin/counts")]
    public async Task<IActionResult> AdminCounts(CancellationToken ct)
    {
        if (Me is not Guid me || !await IsReviewerAsync(me)) return Forbid();
        var map = await store.CountsAsync(ct);
        return Ok(AdminStatuses.ToDictionary(s => s, s => map.GetValueOrDefault(s)));
    }

    public sealed class ReasonDto
    {
        public string? Reason { get; set; }
    }

    /// <summary>审核通过：公开到知识库、进社区动态；第一次通过给上传者 +20 经验（每人每天最多算 5 个资源），并通知上传者</summary>
    [Authorize]
    [HttpPost("admin/{id:guid}/approve")]
    public async Task<IActionResult> Approve(Guid id, CancellationToken ct)
    {
        if (Me is not Guid me || !await IsReviewerAsync(me)) return Forbid();
        var row = await store.GetAsync(id, ct);
        if (row == null || row.DeletedAt != null) return NotFound(Fail("资源不存在或已经被上传者删除"));
        if (!await store.ReviewAsync(id, ResourceStatus.Approved, new[] { ResourceStatus.Pending, ResourceStatus.Rejected }, me, null, true, ct))
            return Conflict(Fail("状态已经变了（可能别的管理员刚处理过），请刷新"));

        var grant = await store.ClaimApproveExpAsync(row, ResourceRules.ApproveExp, ResourceRules.ApproveExpDailyCount, ct);
        var extra = "";
        if (grant > 0)
        {
            var stats = await db.UserStats.FirstOrDefaultAsync(s => s.UserId == row.UploaderId, ct);
            if (stats != null)
            {
                stats.Experience += grant;
                db.UserExpLogs.Add(new UserExpLog { UserId = row.UploaderId, Change = grant, Reason = $"资源《{Clip(row.Title, 30)}》审核通过" });
                extra = $"，获得 {grant} 经验";
            }
        }
        else if (grant == 0) extra = $"（今天已经有 {ResourceRules.ApproveExpDailyCount} 个资源拿过经验，这个就不再加了）";
        new NotificationService(db).Add(row.UploaderId, me, "resource", "resource", row.Id.ToString(),
            $"你上传的资源《{Clip(row.Title, 30)}》审核通过，已经在知识库公开了{extra}", $"/knowledge?type=resource&res={row.Id}");
        await db.SaveChangesAsync(ct);
        return Ok(new { ok = true, exp = Math.Max(0, grant) });
    }

    /// <summary>打回：要写理由，通知上传者；上传者改好资料后会自动重新提交</summary>
    [Authorize]
    [HttpPost("admin/{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, [FromBody] ReasonDto dto, CancellationToken ct)
    {
        if (Me is not Guid me || !await IsReviewerAsync(me)) return Forbid();
        var reason = (dto.Reason ?? "").Trim();
        if (reason.Length == 0) return BadRequest(Fail("请写打回理由，上传者才知道怎么改"));
        if (reason.Length > ResourceRules.ReasonMax) reason = reason[..ResourceRules.ReasonMax];
        var row = await store.GetAsync(id, ct);
        if (row == null || row.DeletedAt != null) return NotFound(Fail("资源不存在或已经被上传者删除"));
        if (!await store.ReviewAsync(id, ResourceStatus.Rejected, new[] { ResourceStatus.Pending }, me, reason, false, ct))
            return Conflict(Fail("状态已经变了（可能别的管理员刚处理过），请刷新"));
        new NotificationService(db).Add(row.UploaderId, me, "resource", "resource", row.Id.ToString(),
            $"你上传的资源《{Clip(row.Title, 30)}》没有通过审核：{Clip(reason, 120)}。改好后在「我的资源」里保存即可重新提交", MyResourcesLink);
        await db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    /// <summary>下架：从知识库消失，谁都不能再下载（买过的人也不行），数据和文件不删；通知上传者</summary>
    [Authorize]
    [HttpPost("admin/{id:guid}/remove")]
    public async Task<IActionResult> Remove(Guid id, [FromBody] ReasonDto dto, CancellationToken ct)
    {
        if (Me is not Guid me || !await IsReviewerAsync(me)) return Forbid();
        var reason = (dto.Reason ?? "").Trim();
        if (reason.Length == 0) return BadRequest(Fail("请写下架理由"));
        if (reason.Length > ResourceRules.ReasonMax) reason = reason[..ResourceRules.ReasonMax];
        var row = await store.GetAsync(id, ct);
        if (row == null || row.DeletedAt != null) return NotFound(Fail("资源不存在或已经被上传者删除"));
        if (!await store.ReviewAsync(id, ResourceStatus.Removed, new[] { ResourceStatus.Approved, ResourceStatus.Pending }, me, reason, false, ct))
            return Conflict(Fail("状态已经变了（可能别的管理员刚处理过），请刷新"));
        new NotificationService(db).Add(row.UploaderId, me, "resource", "resource", row.Id.ToString(),
            $"你的资源《{Clip(row.Title, 30)}》被管理员下架了：{Clip(reason, 120)}", MyResourcesLink);
        await db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    /// <summary>恢复：把下架的资源重新公开（不会再给一次经验）</summary>
    [Authorize]
    [HttpPost("admin/{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken ct)
    {
        if (Me is not Guid me || !await IsReviewerAsync(me)) return Forbid();
        var row = await store.GetAsync(id, ct);
        if (row == null || row.DeletedAt != null) return NotFound(Fail("资源不存在或已经被上传者删除"));
        if (!await store.ReviewAsync(id, ResourceStatus.Approved, new[] { ResourceStatus.Removed }, me, null, true, ct))
            return Conflict(Fail("状态已经变了（可能别的管理员刚处理过），请刷新"));
        new NotificationService(db).Add(row.UploaderId, me, "resource", "resource", row.Id.ToString(),
            $"你的资源《{Clip(row.Title, 30)}》已经恢复公开", $"/knowledge?type=resource&res={row.Id}");
        await db.SaveChangesAsync(ct);
        return Ok(new { ok = true });
    }

    public sealed class FolderDto
    {
        public string? Folder { get; set; }
    }

    /// <summary>管理员改位置（整理用，不改状态、不需要重审；空 = 用分类名）</summary>
    [Authorize]
    [HttpPut("admin/{id:guid}/folder")]
    public async Task<IActionResult> AdminSetFolder(Guid id, [FromBody] FolderDto dto, CancellationToken ct)
    {
        if (Me is not Guid me || !await IsReviewerAsync(me)) return Forbid();
        var folder = ResourceRules.CleanFolder(dto.Folder);
        if (!await store.SetFolderAsync(id, folder, ct)) return NotFound(Fail("资源不存在或已经被删除"));
        var row = await store.GetAsync(id, ct);
        return Ok(new { folder = ResourceRules.EffectiveFolder(folder, row?.Category ?? ""), folderRaw = folder });
    }

    /// <summary>审核时下载 / 预览文件（不扣经验、不计下载数）。inline=true 只对图片、音频、视频、PDF、纯文本有效。</summary>
    [Authorize]
    [HttpGet("admin/{id:guid}/file")]
    public async Task<IActionResult> AdminFile(Guid id, [FromQuery] bool inline = false, CancellationToken ct = default)
    {
        if (Me is not Guid me || !await IsReviewerAsync(me)) return Forbid();
        var row = await store.GetAsync(id, ct);
        if (row == null) return NotFound(Fail("资源不存在"));
        if (row.Status == ResourceStatus.Uploading) return BadRequest(Fail("还没传完"));
        var previewType = ResourceRules.PreviewTypes.TryGetValue(ResourceRules.ExtensionOf(row.FileName), out var t) ? t : null;
        var asInline = inline && previewType != null;
        return Ok(new { url = storage.For(row.Storage).DownloadUrl(row.ObjectKey, row.FileName, asInline, asInline ? previewType : null), fileName = row.FileName, inline = asInline });
    }
}
