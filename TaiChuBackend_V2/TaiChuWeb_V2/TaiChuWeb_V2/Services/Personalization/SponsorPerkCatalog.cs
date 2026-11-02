using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.System;

namespace TaiChuWeb_V2.Services.Personalization;

/// <summary>一个赞助等级的介绍（等级名字固定，见 SponsorPerkService.Tiers；这里只放价格说明和一句话）</summary>
public sealed class PerkTierInfo
{
    /// <summary>1 = 觉醒者 … 4 = 君者</summary>
    public int Rank { get; set; }
    /// <summary>价格说明，比如「每月 ¥10」，随便写</summary>
    public string Price { get; set; } = "";
    /// <summary>一句话介绍</summary>
    public string Blurb { get; set; } = "";
}

/// <summary>一条赞助福利</summary>
public sealed class PerkEntry
{
    /// <summary>稳定的编号（前端排序、编辑用）</summary>
    public string Id { get; set; } = "";
    /// <summary>
    /// 有 Key 的是「真的控制权限」的福利（比如 dev-progress、planner-email），改它的最低等级会马上改变谁能用；
    /// 没有 Key 的只是展示用的文字（比如「专属头衔」），网站不会自动判断。
    /// </summary>
    public string? Key { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    /// <summary>最低等级（1 = 觉醒者 … 4 = 君者），高等级自动包含低等级的福利</summary>
    public int MinTier { get; set; } = 1;
    /// <summary>是否在赞助墙上公开显示（不公开的照样生效）</summary>
    public bool Visible { get; set; } = true;
}

/// <summary>赞助福利说明（整份存在 SystemConfigs 一行 JSON 里，不需要新表）</summary>
public sealed class PerkCatalog
{
    public int Version { get; set; } = 1;
    /// <summary>赞助墙顶部的说明文字</summary>
    public string Intro { get; set; } = "";
    public List<PerkTierInfo> Tiers { get; set; } = new();
    public List<PerkEntry> Perks { get; set; } = new();
}

/// <summary>
/// 赞助福利目录：赞助墙上「赞助福利」那一块展示的内容，超级管理员在「管理面板 → 赞助福利」里随时改。
/// 存在 SystemConfigs 表 Key = "Sponsor:PerkCatalog" 的一行里。
///
/// 带 Key 的福利和 SponsorPerkService.Features 是同一套东西：在这里改最低等级，网站真正的权限判断也跟着变
/// （启动时由 SponsorPerkCatalogLoader 读一次，保存时马上生效）。带 Key 的不能删，只能隐藏（隐藏了照样生效）。
/// 数据库里没配置时用默认值（和以前写死在代码里的一样），所以不配置也不会改变任何人的权限。
/// </summary>
public static class SponsorPerkCatalog
{
    public const string ConfigKey = "Sponsor:PerkCatalog";
    public const int MaxIntro = 500;
    public const int MaxPrice = 30;
    public const int MaxBlurb = 200;
    public const int MaxPerks = 100;
    public const int MaxTitle = 30;
    public const int MaxDescription = 300;

    private static readonly Regex IdPattern = new("^[a-z0-9_-]{1,40}$");

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>代码里真正用来判断权限的福利（Key 要和 SponsorPerkService.DefaultFeatures 一致），以及默认的名字和说明</summary>
    public sealed record BuiltIn(string Key, string Title, string Description);
    public static readonly List<BuiltIn> BuiltIns = new()
    {
        new("custom-background", "自定义网站背景", "上传自己的图片或视频当网站背景"),
        new("custom-profile", "个人主页装扮", "自定义个人主页的横幅和整页背景"),
        new("dev-progress", "开发进度详情", "查看开发进度里的研发报告、开发日志和资料"),
        new("life-planner", "生活管理", "太初灵脉里的个人日历：安排每天要做的事、写备忘、到点提醒；赞助到期后安排都还在，可以查看和导出"),
        new("planner-email", "生活管理邮件提醒", "生活管理里的安排到点时，额外发邮件提醒你"),
    };

    public const string DefaultIntro = "通过爱赞助支持太初寰宇。赞助后在爱赞助私信里告诉我们你的网站用户名，我们会给你的账号开通对应等级的赞助福利（每次赞助开通 30 天，连续赞助自动顺延）。";

    private static int DefaultTier(string key) =>
        SponsorPerkService.DefaultFeatures.TryGetValue(key, out var t) ? t : 1;

    public static PerkCatalog Default() => new()
    {
        Version = 1,
        Intro = DefaultIntro,
        Tiers = Enumerable.Range(1, SponsorPerkService.Tiers.Length).Select(r => new PerkTierInfo { Rank = r }).ToList(),
        Perks = BuiltIns.Select(b => new PerkEntry { Id = b.Key, Key = b.Key, Title = b.Title, Description = b.Description, MinTier = DefaultTier(b.Key), Visible = true }).ToList(),
    };

    /// <summary>读当前目录（走 SystemConfigService 的缓存）；没配置或配置坏了用默认的。读出来的总是补齐过的（新加的内置福利会自动出现）</summary>
    public static async Task<PerkCatalog> LoadAsync(SystemConfigService config)
    {
        var json = await config.GetValueAsync(ConfigKey);
        if (string.IsNullOrWhiteSpace(json)) return Default();
        try
        {
            var c = JsonSerializer.Deserialize<PerkCatalog>(json, Json);
            return c == null ? Default() : Normalize(c);
        }
        catch (JsonException) { return Default(); }
    }

    /// <summary>补齐、修正读出来的目录（不报错，尽量保留内容）</summary>
    private static PerkCatalog Normalize(PerkCatalog c)
    {
        c.Intro ??= "";
        c.Tiers = FixTiers(c.Tiers);
        var perks = new List<PerkEntry>();
        var keys = new HashSet<string>();
        var ids = new HashSet<string>();
        foreach (var p in c.Perks ?? new List<PerkEntry>())
        {
            if (p == null) continue;
            // 不认识的 Key（比如代码里删掉了某个特权）：当成普通展示文字，内容保留
            if (p.Key != null && (!BuiltIns.Any(b => b.Key == p.Key) || !keys.Add(p.Key))) p.Key = null;
            if (string.IsNullOrWhiteSpace(p.Id) || !IdPattern.IsMatch(p.Id) || !ids.Add(p.Id)) { p.Id = NewId(ids); }
            p.Title ??= ""; p.Description ??= "";
            p.MinTier = Math.Clamp(p.MinTier, 1, SponsorPerkService.Tiers.Length);
            perks.Add(p);
        }
        AppendMissingBuiltIns(perks, keys, ids);
        c.Perks = perks;
        return c;
    }

    private static List<PerkTierInfo> FixTiers(List<PerkTierInfo>? tiers) =>
        Enumerable.Range(1, SponsorPerkService.Tiers.Length).Select(r =>
        {
            var t = tiers?.FirstOrDefault(x => x != null && x.Rank == r);
            return new PerkTierInfo { Rank = r, Price = t?.Price ?? "", Blurb = t?.Blurb ?? "" };
        }).ToList();

    private static void AppendMissingBuiltIns(List<PerkEntry> perks, HashSet<string> keys, HashSet<string> ids)
    {
        foreach (var b in BuiltIns)
        {
            if (keys.Contains(b.Key)) continue;
            keys.Add(b.Key);
            var id = ids.Contains(b.Key) ? NewId(ids) : b.Key;
            ids.Add(id);
            perks.Add(new PerkEntry { Id = id, Key = b.Key, Title = b.Title, Description = b.Description, MinTier = DefaultTier(b.Key), Visible = true });
        }
    }

    private static string NewId(HashSet<string> used)
    {
        string id;
        do { id = "p" + Guid.NewGuid().ToString("N")[..10]; } while (!used.Add(id));
        return id;
    }

    /// <summary>检查管理员提交的目录；不合规返回 null 和原因。内置福利被删掉的自动加回来（只能隐藏，不能删）</summary>
    public static PerkCatalog? Clean(PerkCatalog? incoming, PerkCatalog current, out string error)
    {
        error = "";
        if (incoming == null) { error = "内容格式不正确"; return null; }
        var intro = (incoming.Intro ?? "").Trim();
        if (intro.Length > MaxIntro) { error = $"顶部说明最多 {MaxIntro} 个字"; return null; }
        var tiers = FixTiers(incoming.Tiers);
        foreach (var t in tiers)
        {
            t.Price = t.Price.Trim(); t.Blurb = t.Blurb.Trim();
            var name = SponsorPerkService.Tiers[t.Rank - 1];
            if (t.Price.Length > MaxPrice) { error = $"「{name}」的价格说明最多 {MaxPrice} 个字"; return null; }
            if (t.Blurb.Length > MaxBlurb) { error = $"「{name}」的介绍最多 {MaxBlurb} 个字"; return null; }
        }
        var list = (incoming.Perks ?? new List<PerkEntry>()).Where(p => p != null).ToList();
        if (list.Count > MaxPerks) { error = $"福利最多 {MaxPerks} 条"; return null; }
        var perks = new List<PerkEntry>();
        var keys = new HashSet<string>();
        var ids = new HashSet<string>();
        foreach (var p in list)
        {
            var title = (p.Title ?? "").Trim();
            var desc = (p.Description ?? "").Trim();
            if (title.Length == 0) { error = "有一条福利没写名字"; return null; }
            if (title.Length > MaxTitle) { error = $"福利名字最多 {MaxTitle} 个字（「{title[..Math.Min(10, title.Length)]}…」）"; return null; }
            if (desc.Length > MaxDescription) { error = $"「{title}」的说明最多 {MaxDescription} 个字"; return null; }
            if (p.MinTier < 1 || p.MinTier > SponsorPerkService.Tiers.Length) { error = $"「{title}」的等级不对"; return null; }
            string? key = null;
            if (!string.IsNullOrWhiteSpace(p.Key))
            {
                if (!BuiltIns.Any(b => b.Key == p.Key)) { error = $"「{title}」关联的权限不存在，请刷新页面后重试"; return null; }
                if (!keys.Add(p.Key!)) { error = $"「{title}」重复了，请刷新页面后重试"; return null; }
                key = p.Key;
            }
            var id = !string.IsNullOrWhiteSpace(p.Id) && IdPattern.IsMatch(p.Id) && ids.Add(p.Id) ? p.Id : NewId(ids);
            perks.Add(new PerkEntry { Id = id, Key = key, Title = title, Description = desc, MinTier = p.MinTier, Visible = p.Visible });
        }
        AppendMissingBuiltIns(perks, keys, ids);
        var result = new PerkCatalog { Version = current.Version, Intro = intro, Tiers = tiers, Perks = perks };
        if (JsonSerializer.Serialize(result, Json) != JsonSerializer.Serialize(current, Json)) result.Version = current.Version + 1;
        return result;
    }

    /// <summary>存进 SystemConfigs，清掉配置缓存，并马上让新的最低等级生效</summary>
    public static async Task SaveAsync(AppDbContext db, SystemConfigService config, PerkCatalog c, Guid by)
    {
        var row = await db.SystemConfigs.FirstOrDefaultAsync(x => x.Key == ConfigKey);
        if (row == null)
        {
            row = new SystemConfig { Key = ConfigKey, Description = "赞助福利说明和各项特权的最低等级（JSON，在管理面板「赞助福利」里改）" };
            db.SystemConfigs.Add(row);
        }
        row.Value = JsonSerializer.Serialize(c, Json);
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = by.ToString();
        await db.SaveChangesAsync();
        config.ClearCache();
        Apply(c);
    }

    /// <summary>把目录里带 Key 的福利的最低等级交给 SponsorPerkService（权限判断都看那里）</summary>
    public static void Apply(PerkCatalog c) =>
        SponsorPerkService.ApplyFeatureTiers(c.Perks.Where(p => p.Key != null).GroupBy(p => p.Key!).ToDictionary(g => g.Key, g => g.First().MinTier));
}

/// <summary>启动时读一次赞助福利目录，让后台改过的最低等级生效（读失败就用代码里的默认等级，不影响启动）</summary>
public sealed class SponsorPerkCatalogLoader(IServiceScopeFactory scopes, ILogger<SponsorPerkCatalogLoader> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var config = scope.ServiceProvider.GetRequiredService<SystemConfigService>();
            SponsorPerkCatalog.Apply(await SponsorPerkCatalog.LoadAsync(config));
        }
        catch (Exception ex) { logger.LogError(ex, "读取赞助福利目录失败，先用默认等级"); }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
