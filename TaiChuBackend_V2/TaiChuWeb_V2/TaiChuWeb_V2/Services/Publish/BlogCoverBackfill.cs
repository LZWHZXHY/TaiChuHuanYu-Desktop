using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.Publish;

/// <summary>
/// 启动后补一次旧博客的列表封面：以前发布时没单独设置封面，但正文里有图的博客，
/// 列表里会没有封面。这里把正文第一张图写进摘要里的 coverUrl。只处理还没有封面的博客，重复运行不会改动已有封面。
/// </summary>
public class BlogCoverBackfill(IServiceScopeFactory scopes, ILogger<BlogCoverBackfill> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var candidates = await db.PublishedNotes.AsNoTracking()
                .Where(n => n.Type == "blog" && (n.Excerpt == null || !n.Excerpt.StartsWith("{") || n.Excerpt.Contains("\"coverUrl\":\"\"")))
                .Select(n => new { n.Id, n.Excerpt })
                .ToListAsync(stoppingToken);

            var fixedCount = 0;
            foreach (var note in candidates)
            {
                if (stoppingToken.IsCancellationRequested) break;
                var (cover, text) = Read(note.Excerpt);
                if (!string.IsNullOrWhiteSpace(cover)) continue;

                var id = note.Id.ToString();
                var blocks = await db.PublishedBlocks.AsNoTracking()
                    .Where(b => b.OwnerId == id)
                    .OrderBy(b => b.SortOrder)
                    .Select(b => new { b.Type, b.Data })
                    .ToListAsync(stoppingToken);
                var url = PublishImageFinder.FirstImageUrl(blocks.Select(b => (b.Type, b.Data)));
                if (url == null) continue;

                var excerpt = JsonSerializer.Serialize(new { coverUrl = url, text = string.IsNullOrEmpty(text) ? "深度博客，静候回响..." : text });
                await db.PublishedNotes.Where(n => n.Id == note.Id)
                    .ExecuteUpdateAsync(u => u.SetProperty(n => n.Excerpt, excerpt), stoppingToken);
                fixedCount++;
            }
            if (fixedCount > 0) logger.LogInformation("已为 {Count} 篇旧博客补上列表封面（取正文第一张图）", fixedCount);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "补博客封面失败，不影响正常使用");
        }
    }

    private static (string Cover, string Text) Read(string? excerpt)
    {
        if (string.IsNullOrWhiteSpace(excerpt)) return ("", "");
        if (!excerpt.TrimStart().StartsWith('{')) return ("", excerpt);
        try
        {
            using var doc = JsonDocument.Parse(excerpt);
            var root = doc.RootElement;
            var cover = root.TryGetProperty("coverUrl", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
            var text = root.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
            return (cover, text);
        }
        catch (JsonException)
        {
            return ("", excerpt);
        }
    }
}
