using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TaiChuWeb_V2.Services.World;

/// <summary>
/// 条目的区块排列（WorldCards.Layout，前端写的 JSON）：
/// { "v": 1, "rows": [ { "id": "...", "blocks": [ { "id", "kind", "title", "hideTitle", "hidden", "text", "images" } ] } ] }
/// 每行 1～2 块（左右并排）。kind：
///   description = 正文（内容在 Description）、text = 自定义文本块（内容在 text）、media = 图片块（images）、
///   gallery = 图库（GalleryImages）、timeline = 时间线（TimelineEvents）、embeds = 引用设定（ContentBlocks）、relations = 设定关系。
/// map = 地图块（底图 + 图钉，图钉可以连到条目，数据都在块里）。
/// hidden = 对访客隐藏：只有 text / media / map 块能隐藏，访客读取时服务器直接去掉，不会发给浏览器。
/// </summary>
public static class WorldLayout
{
    public const int MaxLength = 512 * 1024;
    public static readonly string[] Kinds = { "description", "text", "media", "gallery", "timeline", "embeds", "relations", "map", "sources" };
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
            }
        }
        // 原样保存（不重新序列化，中文不会被转义，知识库搜索才能直接找到）
        return json;
    }

    private static JsonArray? Rows(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return (JsonNode.Parse(json) as JsonObject)?["rows"] as JsonArray; }
        catch (JsonException) { return null; }
    }

    private static bool IsHidden(JsonNode? block) =>
        block is JsonObject b && b["hidden"] is JsonValue v && v.TryGetValue<bool>(out var hidden) && hidden
        && KindOf(b) is "text" or "media" or "map" or "sources";

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

    /// <summary>访客能看到的自定义文本块文字（知识库搜索摘要用）</summary>
    public static string VisibleText(string? json) =>
        string.Join(" ", Blocks(json).Where(b => b.Kind == "text" && !b.Hidden).Select(b => $"{b.Title} {b.Text}"));
}
