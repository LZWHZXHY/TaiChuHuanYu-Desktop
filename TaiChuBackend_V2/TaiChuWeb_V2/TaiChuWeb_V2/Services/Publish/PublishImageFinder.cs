using System.Text.Json;

namespace TaiChuWeb_V2.Services.Publish;

/// <summary>在正文积木块（Tiptap 节点 JSON）里找第一张图，用作列表封面。</summary>
public static class PublishImageFinder
{
    // 这些系统块不是正文，跳过
    private static readonly HashSet<string> SkipTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "blog_fixed_cover", "blog_fixed_excerpt", "canvas-node", "canvas-edge", "char-layout-block", "map-layout-block"
    };

    public static string? FirstImageUrl(IEnumerable<(string Type, string? Data)> blocks)
    {
        foreach (var (type, data) in blocks)
        {
            if (SkipTypes.Contains(type) || string.IsNullOrWhiteSpace(data)) continue;
            try
            {
                using var doc = JsonDocument.Parse(data);
                var url = Find(doc.RootElement, 0);
                if (!string.IsNullOrWhiteSpace(url)) return url;
            }
            catch (JsonException) { /* 坏数据跳过 */ }
        }
        return null;
    }

    private static string? Find(JsonElement element, int depth)
    {
        if (depth > 40) return null;
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() == "image"
                && element.TryGetProperty("attrs", out var attrs) && attrs.ValueKind == JsonValueKind.Object
                && attrs.TryGetProperty("src", out var src) && src.ValueKind == JsonValueKind.String)
            {
                var url = src.GetString();
                if (IsUsable(url)) return url;
            }
            foreach (var prop in element.EnumerateObject())
            {
                var found = Find(prop.Value, depth + 1);
                if (found != null) return found;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var found = Find(item, depth + 1);
                if (found != null) return found;
            }
        }
        return null;
    }

    // 只要网络地址或站内地址；base64 内嵌图太大，不放进摘要
    private static bool IsUsable(string? url) =>
        !string.IsNullOrWhiteSpace(url) && url.Length <= 1000 &&
        (url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || url.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || url.StartsWith('/'));
}
