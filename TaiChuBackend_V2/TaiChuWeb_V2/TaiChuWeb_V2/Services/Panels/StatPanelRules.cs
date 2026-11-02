using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace TaiChuWeb_V2.Services.Panels;

/// <summary>
/// 「能力面板」（多维雷达图，像 JOJO 替身面板）的检查和整理。柴圈 OC 的「展示面板」和世界观条目的「能力面板」区块共用。
/// 格式（前端 utils/statPanel.ts，两边的上限要一致）：
/// { "v": 1, "title": "能力面板", "scale": "grade" | "number", "max": 100,
///   "dims": [ { "name": "破坏力", "value": "A" } 或 { "name": "力量", "value": 80 } ],
///   "note": "备注", "overall": true, "template": "jojo" | "six" | "custom" }
/// 维度最少 3 个、不设实际上限；只留一个很宽的防滥用上限（100 个维度、整个面板 64KB），防止有人直接调接口塞垃圾数据。
/// </summary>
public static class StatPanelRules
{
    public const int MinDims = 3;
    public const int MaxDims = 100;
    public const int MaxTitle = 20;
    public const int MaxName = 12;
    public const int MaxNote = 300;
    public const int MaxMax = 10000;
    public const int MaxJsonLength = 64 * 1024;
    public const string DefaultTitle = "能力面板";

    /// <summary>字母等级：S（比 A 高一档）、A～E，∞ = 超出上限，? = 未知，— = 无（前端 utils/statPanel.ts 的 ALL_GRADES 要一致）</summary>
    public static readonly List<string> Grades = new() { "S", "A", "B", "C", "D", "E", "∞", "?", "—" };
    private static readonly List<string> Templates = new() { "jojo", "six", "custom" };

    /// <summary>输出时中文不转义（数据库里直接能看懂，知识库搜索也能搜到）</summary>
    public static readonly JsonSerializerOptions Raw = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    /// <summary>检查面板；没问题返回 null，有问题返回中文原因（直接给用户看）</summary>
    public static string? Check(JsonNode? node)
    {
        if (node is not JsonObject p) return "能力面板格式不正确";
        if (p.ToJsonString(Raw).Length > MaxJsonLength) return "能力面板内容太多了（整个面板不能超过 64KB），请精简一下维度或备注";

        if (!TryString(p, "title", out var title)) return "能力面板的标题格式不正确";
        if (title != null && title.Length > MaxTitle) return $"能力面板的标题最多 {MaxTitle} 个字";

        if (!TryString(p, "scale", out var scale)) return "能力面板的刻度格式不正确";
        scale ??= "grade";
        if (scale != "grade" && scale != "number") return "能力面板的刻度只能是字母等级或数值";

        var max = 100.0;
        if (p["max"] != null)
        {
            if (!TryNumber(p["max"], out max) || max < 1 || max > MaxMax) return $"能力面板的满分要在 1～{MaxMax} 之间";
        }

        if (p["dims"] is not JsonArray dims) return "能力面板没有维度";
        if (dims.Count < MinDims) return $"能力面板最少要 {MinDims} 个维度";
        if (dims.Count > MaxDims) return $"能力面板的维度太多了（最多 {MaxDims} 个）";
        foreach (var item in dims)
        {
            if (item is not JsonObject d) return "能力面板的维度格式不正确";
            if (!TryString(d, "name", out var name)) return "能力面板的维度名字格式不正确";
            if (name != null && name.Length > MaxName) return $"能力面板的维度名字最多 {MaxName} 个字";
            if (scale == "grade")
            {
                if (!TryString(d, "value", out var grade) || grade == null || !Grades.Contains(grade))
                    return "字母等级只能选 A、B、C、D、E、∞、?、—";
            }
            else if (!TryNumber(d["value"], out var value) || value < 0 || value > max)
                return "能力面板的数值要在 0 到满分之间";
        }

        if (!TryString(p, "note", out var note)) return "能力面板的备注格式不正确";
        if (note != null && note.Length > MaxNote) return $"能力面板的备注最多 {MaxNote} 个字";
        if (p["overall"] != null && !(p["overall"] is JsonValue ov && ov.TryGetValue<bool>(out _))) return "能力面板格式不正确";
        if (!TryString(p, "template", out var template) || (template != null && template.Length > 20)) return "能力面板格式不正确";
        return null;
    }

    /// <summary>
    /// 整理成干净的面板（先 Check 通过再调用）：去掉首尾空格、空名字补成「维度 N」、数值保留一位小数、
    /// 只留认识的字段（和前端 cleanStatPanel 的结果一致）。
    /// </summary>
    public static JsonObject Normalize(JsonObject p)
    {
        TryString(p, "title", out var title);
        TryString(p, "scale", out var scale);
        scale = scale == "number" ? "number" : "grade";
        var max = 100.0;
        if (p["max"] != null) TryNumber(p["max"], out max);
        var maxInt = (int)Math.Round(max);

        var dims = new JsonArray();
        var i = 0;
        foreach (var d in ((JsonArray)p["dims"]!).OfType<JsonObject>())
        {
            i++;
            TryString(d, "name", out var name);
            name = string.IsNullOrWhiteSpace(name) ? $"维度{i}" : name.Trim();
            JsonNode value;
            if (scale == "grade")
            {
                TryString(d, "value", out var grade);
                value = JsonValue.Create(grade ?? "?")!;
            }
            else
            {
                TryNumber(d["value"], out var number);
                value = JsonValue.Create(Math.Round(Math.Clamp(number, 0, maxInt), 1))!;
            }
            dims.Add(new JsonObject { ["name"] = name, ["value"] = value });
        }

        var result = new JsonObject
        {
            ["v"] = 1,
            ["title"] = string.IsNullOrWhiteSpace(title) ? DefaultTitle : title.Trim(),
            ["scale"] = scale,
            ["max"] = maxInt,
            ["dims"] = dims,
        };
        TryString(p, "note", out var note);
        if (!string.IsNullOrWhiteSpace(note)) result["note"] = note.Trim();
        if (p["overall"] is JsonValue ov && ov.TryGetValue<bool>(out var overall) && overall) result["overall"] = true;
        TryString(p, "template", out var template);
        if (template != null && Templates.Contains(template)) result["template"] = template;
        return result;
    }

    /// <summary>取字符串字段：没有（或 null）算成功、value = null；有但不是字符串算失败</summary>
    public static bool TryString(JsonObject obj, string field, out string? value)
    {
        value = null;
        var node = obj[field];
        if (node == null) return true;
        if (node is JsonValue v && v.TryGetValue<string>(out var text)) { value = text; return true; }
        return false;
    }

    private static bool TryNumber(JsonNode? node, out double value)
    {
        value = 0;
        return node is JsonValue v && v.TryGetValue<double>(out value) && double.IsFinite(value);
    }
}
