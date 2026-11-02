using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.System;

namespace TaiChuWeb_V2.Services.Panels;

/// <summary>约战模板的一个维度：Key 是稳定的标识（存 OC 的值就按它存），Name 是显示的名字（随便改）</summary>
public sealed class BattleDim
{
    public string Key { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>一句话说明（填的时候悬停提示），可以不写</summary>
    public string? Hint { get; set; }
}

/// <summary>官方约战模板（整份存在 SystemConfigs 表一行 JSON 里）</summary>
public sealed class BattleTemplate
{
    /// <summary>每改一次 +1（前端可以据此提示「约战模板更新了」）</summary>
    public int Version { get; set; } = 1;
    public string Title { get; set; } = BattlePanelTemplate.DefaultTitle;
    /// <summary>能选的等级（默认 A～E 和 ?）</summary>
    public List<string> Grades { get; set; } = new();
    /// <summary>当前的维度，顺序就是雷达图上的顺序</summary>
    public List<BattleDim> Dims { get; set; } = new();
    /// <summary>
    /// 删掉的维度（只记 Key 和名字）：OC 填过的值还在数据库里，只是不显示；
    /// 管理面板里可以「加回来」（用回原来的 Key），OC 以前填的值就又出现了。
    /// </summary>
    public List<BattleDim> Retired { get; set; } = new();
}

/// <summary>
/// 官方约战模板：参加柴圈约战时用的「约战面板」，所有 OC 用同一套维度和刻度，方便对比、论战。
/// （OC 的「展示面板」和世界观条目的面板可以随便设计，那个见 StatPanelRules。）
///
/// 模板 = 下面的默认值（JOJO 替身面板六项）+ 数据库覆盖：超级管理员在「管理面板 → 约战面板模板」里改，
/// 存在 SystemConfigs 表 Key = "Chai:BattlePanelTemplate" 的一行里（不需要新表）。
///
/// 数据安全：OC 的约战面板按维度 Key 存（{"values":{"power":"A",...}}），改模板绝不改写、删除任何 OC 的数据——
/// · 删掉一个维度：Key 记进 Retired，OC 的旧值还在，只是不显示；加回来（同一个 Key）就又出现了；
/// · 新加一个维度：自动生成新 Key（不会和以前用过的重复），老 OC 没有这一项，显示成 ?（未知）；
/// · 改维度名字：只改 Name，Key 不动，旧值照常显示；
/// · 去掉某个等级（比如不让选 E）：已经填了 E 的值还存着，只是显示成 ?；加回来就恢复。
/// </summary>
public static class BattlePanelTemplate
{
    public const string ConfigKey = "Chai:BattlePanelTemplate";
    public const string DefaultTitle = "约战面板";
    public const int MinDims = 3;
    /// <summary>防滥用的上限（正常用不到）</summary>
    public const int MaxDims = 100;
    public const int MaxName = 12;
    public const int MaxHint = 40;
    public const int MaxTitle = 20;

    /// <summary>所有可能的等级（按这个顺序排，S 比 A 高一档）；S、∞ 默认不开（∞ 是「无限」，约战里没法比），管理员在模板里勾上就能用</summary>
    public static readonly List<string> AllGrades = new() { "S", "A", "B", "C", "D", "E", "∞", "?", "—" };
    private static readonly List<string> DefaultGrades = new() { "A", "B", "C", "D", "E", "?" };
    private static readonly Regex KeyPattern = new("^[a-z][a-z0-9_]{0,39}$");

    /// <summary>存取模板用的 JSON 设置（属性名小驼峰、读的时候不分大小写、中文不转义）</summary>
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>默认模板：JOJO 替身面板六项（数据库里没配置、或者配置坏了时用）</summary>
    public static BattleTemplate Default() => new()
    {
        Version = 1,
        Title = DefaultTitle,
        Grades = new List<string>(DefaultGrades),
        Dims = new List<BattleDim>
        {
            new() { Key = "power", Name = "破坏力", Hint = "一击能造成多大的破坏" },
            new() { Key = "speed", Name = "速度", Hint = "动作、移动、反应有多快" },
            new() { Key = "range", Name = "射程距离", Hint = "能力能作用到多远" },
            new() { Key = "stamina", Name = "持久力", Hint = "能力能维持多久、多耐打" },
            new() { Key = "precision", Name = "精密动作性", Hint = "动作有多精确、多灵巧" },
            new() { Key = "growth", Name = "成长性", Hint = "还有多大的成长空间" },
        },
    };

    /// <summary>读当前模板（走 SystemConfigService 的缓存）；没配置或配置坏了用默认的</summary>
    public static async Task<BattleTemplate> LoadAsync(SystemConfigService config)
    {
        var json = await config.GetValueAsync(ConfigKey);
        if (string.IsNullOrWhiteSpace(json)) return Default();
        try
        {
            var t = JsonSerializer.Deserialize<BattleTemplate>(json, Json);
            // 存的东西不合规（比如有人手改了数据库）：当作没配置，不让约战页报错
            if (t == null || Check(t) != null) return Default();
            return t;
        }
        catch (JsonException) { return Default(); }
    }

    /// <summary>已经存好的模板是否合规（读的时候用）</summary>
    private static string? Check(BattleTemplate t)
    {
        if (t.Dims == null || t.Dims.Count < MinDims || t.Dims.Count > MaxDims) return "维度个数不对";
        if (t.Grades == null || t.Grades.Count < 2 || t.Grades.Any(g => !AllGrades.Contains(g))) return "等级不对";
        if (t.Dims.Any(d => d == null || d.Key == null || !KeyPattern.IsMatch(d.Key) || string.IsNullOrWhiteSpace(d.Name))) return "维度不对";
        if (t.Dims.Select(d => d.Key).Distinct().Count() != t.Dims.Count) return "Key 重复";
        t.Retired ??= new List<BattleDim>();
        t.Title = string.IsNullOrWhiteSpace(t.Title) ? DefaultTitle : t.Title;
        return null;
    }

    /// <summary>
    /// 超级管理员保存模板：检查、整理，新维度自动生成 Key，删掉的维度记进 Retired。
    /// 返回整理好的模板；有问题时 error 是中文原因、返回 null。
    /// </summary>
    public static BattleTemplate? Clean(BattleTemplate? incoming, BattleTemplate current, out string error)
    {
        error = "";
        if (incoming == null) { error = "模板格式不正确"; return null; }

        var title = (incoming.Title ?? "").Trim();
        if (title.Length > MaxTitle) { error = $"模板标题最多 {MaxTitle} 个字"; return null; }

        // 等级：只留认识的，按固定顺序排，去重
        var picked = (incoming.Grades ?? new List<string>()).ToHashSet();
        var grades = AllGrades.Where(picked.Contains).ToList();
        if (grades.Count < 2) { error = "至少要选两个可选等级"; return null; }

        var dims = incoming.Dims ?? new List<BattleDim>();
        if (dims.Count < MinDims) { error = $"约战模板最少要 {MinDims} 个维度"; return null; }
        if (dims.Count > MaxDims) { error = $"维度太多了（最多 {MaxDims} 个）"; return null; }

        // 以前用过的 Key（当前的 + 删掉的）：新维度生成 Key 时避开，不会把新维度错连到旧数据上
        var everUsed = current.Dims.Concat(current.Retired).Select(d => d.Key).ToHashSet();
        var used = new HashSet<string>();
        var cleanDims = new List<BattleDim>();
        foreach (var d in dims)
        {
            if (d == null) { error = "维度格式不正确"; return null; }
            var name = (d.Name ?? "").Trim();
            if (name.Length == 0) { error = "每个维度都要有名字"; return null; }
            if (name.Length > MaxName) { error = $"维度名字最多 {MaxName} 个字（「{name}」太长了）"; return null; }
            var hint = (d.Hint ?? "").Trim();
            if (hint.Length > MaxHint) { error = $"维度说明最多 {MaxHint} 个字（「{name}」的说明太长了）"; return null; }

            var key = (d.Key ?? "").Trim();
            if (key.Length == 0)
            {
                // 新加的维度：自动生成一个从没用过的 Key
                do { key = "d" + Guid.NewGuid().ToString("N")[..8]; } while (everUsed.Contains(key) || used.Contains(key));
            }
            else if (!KeyPattern.IsMatch(key)) { error = $"「{name}」的标识格式不正确"; return null; }
            if (!used.Add(key)) { error = $"有两个维度用了同一个标识（{key}），请刷新页面后重试"; return null; }
            cleanDims.Add(new BattleDim { Key = key, Name = name, Hint = hint.Length > 0 ? hint : null });
        }

        // 删掉的维度：记下 Key 和最后的名字（以前删掉的继续留着），方便以后加回来
        var retired = new List<BattleDim>();
        foreach (var old in current.Dims.Concat(current.Retired))
        {
            if (used.Contains(old.Key) || retired.Any(r => r.Key == old.Key)) continue;
            retired.Add(new BattleDim { Key = old.Key, Name = old.Name, Hint = old.Hint });
        }

        var result = new BattleTemplate
        {
            Version = current.Version,
            Title = title.Length > 0 ? title : DefaultTitle,
            Grades = grades,
            Dims = cleanDims,
            Retired = retired.Take(500).ToList(),
        };
        // 有变化才升版本
        if (JsonSerializer.Serialize(result, Json) != JsonSerializer.Serialize(current, Json)) result.Version = current.Version + 1;
        return result;
    }

    /// <summary>存进 SystemConfigs，并清掉 SystemConfigService 的缓存（不然 10 分钟内还读到旧模板）</summary>
    public static async Task SaveAsync(AppDbContext db, SystemConfigService config, BattleTemplate t, Guid by)
    {
        var row = await db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == ConfigKey);
        if (row == null)
        {
            row = new SystemConfig { Key = ConfigKey, Description = "柴圈官方约战面板模板（JSON，在管理面板里改）" };
            db.SystemConfigs.Add(row);
        }
        row.Value = JsonSerializer.Serialize(t, Json);
        row.UpdatedAt = DateTime.UtcNow;
        row.UpdatedBy = by.ToString();
        await db.SaveChangesAsync();
        config.ClearCache();
    }

    // ===================== OC 的约战面板数据 =====================

    /// <summary>读出存着的所有值（包括模板里已经去掉的 Key），坏数据当作没有</summary>
    public static Dictionary<string, string> ReadValues(string? json)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            if (JsonNode.Parse(json) is JsonObject root && root["values"] is JsonObject values)
            {
                foreach (var (key, node) in values)
                {
                    if (node is JsonValue v && v.TryGetValue<string>(out var grade) && key.Length <= 40) result[key] = grade;
                }
            }
        }
        catch (JsonException) { }
        return result;
    }

    /// <summary>
    /// OC 保存约战面板：从旧数据出发，只用提交上来的值覆盖「当前模板里有的 Key」。
    /// 模板里没有的旧 Key 原样保留（以后模板加回来还能用），提交里不认识的 Key 不存。
    /// body 格式：{ "values": { "power": "A", "speed": "?" ... } }。格式不对返回 null，error 是中文原因。
    /// </summary>
    public static string? Merge(BattleTemplate template, string? oldJson, JsonNode? body, out string error)
    {
        error = "";
        if (body is not JsonObject obj || obj["values"] is not JsonObject posted)
        {
            error = "约战面板格式不正确";
            return null;
        }
        var values = ReadValues(oldJson);
        foreach (var dim in template.Dims)
        {
            var node = posted[dim.Key];
            if (node == null) continue;
            if (node is not JsonValue v || !v.TryGetValue<string>(out var grade) || !template.Grades.Contains(grade))
            {
                error = $"「{dim.Name}」只能选 {string.Join("、", template.Grades)}";
                return null;
            }
            values[dim.Key] = grade;
        }
        var target = new JsonObject();
        foreach (var (key, grade) in values) target[key] = grade;
        var stored = new JsonObject { ["v"] = 1, ["template"] = template.Version, ["values"] = target };
        return stored.ToJsonString(StatPanelRules.Raw);
    }

    /// <summary>当前模板里至少有一项填了允许的等级、而且不是 ?，才算「填了约战面板」（和前端 battleFilled 一致）</summary>
    public static bool IsFilled(BattleTemplate template, Dictionary<string, string> values) =>
        template.Dims.Any(d => values.TryGetValue(d.Key, out var g) && g != "?" && template.Grades.Contains(g));
}
