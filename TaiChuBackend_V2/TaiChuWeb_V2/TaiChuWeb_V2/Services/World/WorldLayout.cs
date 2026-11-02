using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using TaiChuWeb_V2.Services.Panels;

namespace TaiChuWeb_V2.Services.World;

/// <summary>
/// 条目的区块排列（WorldCards.Layout，前端写的 JSON）：
/// { "v": 1, "rows": [ { "id": "...", "blocks": [ { "id", "kind", "title", "hideTitle", "hidden", "text", "images" } ] } ] }
/// 每行 1～2 块（左右并排）。kind：
///   description = 正文（内容在 Description）、text = 自定义文本块（内容在 text）、media = 图片块（images）、
///   gallery = 图库（GalleryImages）、timeline = 时间线（TimelineEvents）、embeds = 引用设定（ContentBlocks）、relations = 设定关系。
/// map = 地图块（底图 + 图钉，图钉可以连到条目，还可以 subMap 进入另一个条目的地图，数据都在块里）。
/// sources = 参考来源；canvas = 画布（items 便签 / 图片 / 条目卡片 + links 连线）；tree = 关系树（nodes，parentId 指向父节点）。
/// statpanel = 能力面板（多维雷达图，数据在块的 panel 字段里，格式和检查见 Services/Panels/StatPanelRules.cs）。
/// hidden = 对访客隐藏：只有 HideableKinds 里的块能隐藏，访客读取时服务器直接去掉，不会发给浏览器。
/// 注意：不认识的 kind 整个保存会被拒绝（报「有不认识的区块类型」），所以前端加新区块时这里的 Kinds 一定要跟着加。
/// </summary>
public static class WorldLayout
{
    public const int MaxLength = 512 * 1024;
    public static readonly string[] Kinds = { "description", "text", "media", "gallery", "timeline", "embeds", "relations", "map", "sources", "canvas", "tree", "statpanel" };
    /// <summary>能「对访客隐藏」的区块（其余的区块内容在条目字段里，不能单独隐藏）</summary>
    private static readonly string[] HideableKinds = { "text", "media", "map", "sources", "canvas", "tree", "statpanel" };
    // 画布、关系树的数量上限，和前端 worldLayout.ts 里的 CANVAS_MAX_ITEMS 等保持一致
    private const int MaxCanvasItems = 300;
    private const int MaxCanvasLinks = 600;
    private const int MaxTreeNodes = 300;
    private const int MaxMapPins = 500;
    private static readonly JsonSerializerOptions Raw = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>检查前端传来的排列；空的返回 null（= 按老样子显示）。格式不对直接报错。</summary>
    public static string? Validate(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        if (json.Length > MaxLength) throw new WorldOperationException(WorldErrorKind.Invalid, "条目内容太长了，请拆成几个条目");
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException) { throw new WorldOperationException(WorldErrorKind.Invalid, "区块排列格式不正确"); }
        if (root is not JsonObject obj || obj["rows"] is not JsonArray rows)
            throw new WorldOperationException(WorldErrorKind.Invalid, "区块排列格式不正确");
        if (rows.Count > 200) throw new WorldOperationException(WorldErrorKind.Invalid, "区块太多了（最多 200 行）");
        foreach (var row in rows)
        {
            if (row is not JsonObject r || r["blocks"] is not JsonArray blocks || blocks.Count is < 1 or > 2)
                throw new WorldOperationException(WorldErrorKind.Invalid, "每一行要有 1～2 个区块");
            foreach (var block in blocks)
            {
                var kind = KindOf(block);
                if (kind == null || !Kinds.Contains(kind))
                    throw new WorldOperationException(WorldErrorKind.Invalid, "有不认识的区块类型");
                ValidateBlock((JsonObject)block!, kind);
            }
        }
        // 原样保存（不重新序列化，中文不会被转义，知识库搜索才能直接找到）
        return json;
    }

    /// <summary>新区块的数据结构检查：数组要是数组、数量不能超上限（前端已经整理过，这里防的是直接调接口乱传）</summary>
    private static void ValidateBlock(JsonObject block, string kind)
    {
        switch (kind)
        {
            case "map":
                if (CountOf(block, "pins") > MaxMapPins) throw new WorldOperationException(WorldErrorKind.Invalid, $"地图上的图钉太多了（最多 {MaxMapPins} 个）");
                break;
            case "canvas":
                if (CountOf(block, "items") > MaxCanvasItems) throw new WorldOperationException(WorldErrorKind.Invalid, $"画布上的东西太多了（最多 {MaxCanvasItems} 个），可以拆成几块画布");
                if (CountOf(block, "links") > MaxCanvasLinks) throw new WorldOperationException(WorldErrorKind.Invalid, $"画布上的连线太多了（最多 {MaxCanvasLinks} 条）");
                break;
            case "tree":
                if (CountOf(block, "nodes") > MaxTreeNodes) throw new WorldOperationException(WorldErrorKind.Invalid, $"关系树的节点太多了（最多 {MaxTreeNodes} 个），可以拆成几棵树");
                break;
            case "statpanel":
                // 能力面板：和柴圈 OC 的展示面板用同一套检查（维度最少 3 个、整个面板不超过 64KB 等），只检查不改写，原样保存
                if (block["panel"] != null && StatPanelRules.Check(block["panel"]) is { } panelError)
                    throw new WorldOperationException(WorldErrorKind.Invalid, panelError);
                break;
        }
    }

    /// <summary>块里某个数组字段的长度；没有这个字段算 0，不是数组直接报格式错</summary>
    private static int CountOf(JsonObject block, string field) => block[field] switch
    {
        null => 0,
        JsonArray array => array.Count,
        _ => throw new WorldOperationException(WorldErrorKind.Invalid, "区块排列格式不正确"),
    };

    private static JsonArray? Rows(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return (JsonNode.Parse(json) as JsonObject)?["rows"] as JsonArray; }
        catch (JsonException) { return null; }
    }

    private static bool IsHidden(JsonNode? block) =>
        block is JsonObject b && b["hidden"] is JsonValue v && v.TryGetValue<bool>(out var hidden) && hidden
        && KindOf(b) is { } kind && HideableKinds.Contains(kind);

    private static string? KindOf(JsonNode? block) =>
        (block as JsonObject)?["kind"] is JsonValue v && v.TryGetValue<string>(out var kind) ? kind : null;

    /// <summary>给访客看的版本：去掉「对访客隐藏」的区块，空行也去掉</summary>
    public static string? StripHidden(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return json;
        JsonObject? root;
        try { root = JsonNode.Parse(json) as JsonObject; }
        catch (JsonException) { return null; }
        if (root?["rows"] is not JsonArray rows) return json;
        var changed = false;
        foreach (var row in rows.OfType<JsonObject>().ToList())
        {
            if (row["blocks"] is not JsonArray blocks) continue;
            foreach (var block in blocks.Where(IsHidden).ToList()) { blocks.Remove(block); changed = true; }
            if (blocks.Count == 0) rows.Remove(row);
        }
        return changed ? root!.ToJsonString(Raw) : json;
    }

    public sealed record Block(string Id, string Kind, string Title, string Text, bool Hidden, string Raw);

    /// <summary>所有区块（按显示顺序）</summary>
    public static IEnumerable<Block> Blocks(string? json)
    {
        var rows = Rows(json);
        if (rows == null) yield break;
        foreach (var row in rows.OfType<JsonObject>())
        {
            if (row["blocks"] is not JsonArray blocks) continue;
            foreach (var node in blocks.OfType<JsonObject>())
            {
                var id = node["id"]?.ToString() ?? "";
                var kind = node["kind"]?.ToString() ?? "";
                if (id.Length == 0) continue;
                yield return new Block(id, kind, node["title"]?.ToString() ?? "", node["text"]?.ToString() ?? "", IsHidden(node), node.ToJsonString(Raw));
            }
        }
    }

    /// <summary>区块的排列（哪几块、怎么排），用来判断「调整了顺序」</summary>
    public static string Arrangement(string? json)
    {
        var rows = Rows(json);
        if (rows == null) return "";
        return string.Join("|", rows.OfType<JsonObject>().Select(r =>
            string.Join(",", (r["blocks"] as JsonArray ?? new JsonArray()).OfType<JsonObject>().Select(b => b["id"]?.ToString()))));
    }

    /// <summary>访客能看到的区块文字（知识库搜索摘要用）：文本块、地图图钉、画布便签和连线说明、关系树节点、能力面板的维度名和备注。隐藏的块不算。</summary>
    public static string VisibleText(string? json)
    {
        var rows = Rows(json);
        if (rows == null) return "";
        var parts = new List<string>();
        foreach (var node in rows.OfType<JsonObject>().SelectMany(r => (r["blocks"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()))
        {
            if (IsHidden(node)) continue;
            var kind = KindOf(node);
            var title = Str(node, "title");
            switch (kind)
            {
                case "text":
                    parts.Add($"{title} {Str(node, "text")}");
                    break;
                case "map":
                    parts.Add(title);
                    parts.AddRange(Items(node, "pins").Select(p => $"{Str(p, "label")} {Str(p, "relation")} {Str(p, "note")}"));
                    break;
                case "canvas":
                    parts.Add(title);
                    parts.AddRange(Items(node, "items").Select(i => Str(i, "text")));
                    parts.AddRange(Items(node, "links").Select(l => Str(l, "label")));
                    break;
                case "tree":
                    parts.Add(title);
                    parts.AddRange(Items(node, "nodes").Select(n => $"{Str(n, "label")} {Str(n, "relation")} {Str(n, "note")}"));
                    break;
                case "statpanel":
                    // 能力面板：区块标题、各维度名字、备注能被搜到（等级、数值没必要）
                    parts.Add(title);
                    if (node["panel"] is JsonObject panel)
                    {
                        parts.AddRange(Items(panel, "dims").Select(d => Str(d, "name")));
                        parts.Add(Str(panel, "note"));
                    }
                    break;
            }
        }
        // 去掉空白片段，免得摘要里一串空格
        return string.Join(" ", parts.Select(p => p.Trim()).Where(p => p.Length > 0));
    }

    /// <summary>
    /// 区块里连到的条目 ID：地图图钉（以及「下一层地图」）、画布上的条目卡片、关系树节点。
    /// includeHidden = false 时不算「对访客隐藏」的块（给访客算引用关系用）。
    /// 正文 / 文本块里的 {CARD:id} 不在这里，那个按文字匹配。
    /// </summary>
    public static HashSet<Guid> CardIds(string? json, bool includeHidden)
    {
        var ids = new HashSet<Guid>();
        var rows = Rows(json);
        if (rows == null) return ids;
        void Add(string value) { if (Guid.TryParse(value, out var id)) ids.Add(id); }
        foreach (var node in rows.OfType<JsonObject>().SelectMany(r => (r["blocks"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()))
        {
            if (!includeHidden && IsHidden(node)) continue;
            switch (KindOf(node))
            {
                case "map":
                    foreach (var pin in Items(node, "pins"))
                    {
                        Add(Str(pin, "cardId"));
                        if (pin["subMap"] is JsonObject sub) Add(Str(sub, "cardId"));
                    }
                    break;
                case "canvas":
                    foreach (var item in Items(node, "items")) Add(Str(item, "cardId"));
                    break;
                case "tree":
                    foreach (var n in Items(node, "nodes")) Add(Str(n, "cardId"));
                    break;
            }
        }
        return ids;
    }

    /// <summary>块里某个数组字段里的对象（不是数组就当没有）</summary>
    private static IEnumerable<JsonObject> Items(JsonObject block, string field) =>
        (block[field] as JsonArray)?.OfType<JsonObject>() ?? Enumerable.Empty<JsonObject>();

    /// <summary>取字符串字段；不是字符串（数字、对象、没有）都当空字符串</summary>
    private static string Str(JsonObject obj, string field) =>
        obj[field] is JsonValue v && v.TryGetValue<string>(out var text) ? text : "";
}
