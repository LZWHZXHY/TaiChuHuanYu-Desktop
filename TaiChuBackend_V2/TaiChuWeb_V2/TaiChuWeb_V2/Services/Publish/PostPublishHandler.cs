using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.LingMai;

namespace TaiChuWeb_V2.Services.Publish
{
    public class PostPublishHandler : ILingMaiPublishHandler
    {
        private readonly AppDbContext _context;
        public string SupportType => NoteTypes.Post;

        public PostPublishHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> ExecutePublishAsync(Guid noteId, string userId, int? categoryId, string? projectId = null)
        {
            return await _context.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
            {
                var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId);
                if (note == null) return new NotFoundObjectResult(new { message = "未找到该短动态草稿" });

                // 只按 OwnerId 取正文块，不按 OwnerType 过滤，避免类型迁移导致正文块漏取。
                var postBlocks = await _context.Blocks
                    .Where(b => b.OwnerId == noteId.ToString())
                    .OrderBy(b => b.SortOrder)
                    .ToListAsync();

                // 1. 提取文字摘要
                string excerpt = ExtractPostExcerpt(postBlocks);

                // 2. 升级为深层网络透传扫描器，精准捕捉嵌套的图片 URL
                string? firstImageUrl = ExtractFirstImageUrl(postBlocks);

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var defaultTitles = new[] { "默认标题", "新灵感碎片", "灵感碎片", "未命名笔记" };
                    var title = string.IsNullOrWhiteSpace(note.Title) || defaultTitles.Contains(note.Title)
                        ? (excerpt.Length > 15 ? excerpt[..15] + "..." : excerpt)
                        : note.Title;
                    var extraData = string.IsNullOrEmpty(firstImageUrl) ? note.ExtraData
                        : JsonSerializer.Serialize(new Dictionary<string, string> { ["cardCover"] = firstImageUrl });
                    var publishedId = await PublishedNoteStore.SaveAsync(_context, note, postBlocks, userId,
                        SupportType, title, excerpt, extraData);
                    await transaction.CommitAsync();

                    return new OkObjectResult(new { success = true, publishedId });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return new BadRequestObjectResult(new { message = $"短动态发布失败: {ex.Message}" });
                }
            });
        }

        private string ExtractPostExcerpt(List<Block> blocks)
        {
            // 1. 尝试从所有类型的块中提取文字，不仅仅是 paragraph
            foreach (var block in blocks)
            {
                if (string.IsNullOrWhiteSpace(block.Data)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(block.Data);
                    if (doc.RootElement.TryGetProperty("content", out var contentArr))
                    {
                        var text = string.Concat(contentArr.EnumerateArray()
                                   .Where(i => i.TryGetProperty("text", out _))
                                   .Select(i => i.GetProperty("text").GetString()));

                        if (!string.IsNullOrWhiteSpace(text))
                        {
                            return text.Length > 300 ? text.Substring(0, 300) + "..." : text;
                        }
                    }
                }
                catch { continue; }
            }

            return "灵感碎片已捕获..."; // 或者你喜欢的简洁占位符
        }

        private string? ExtractFirstImageUrl(List<Block> blocks)
        {
            foreach (var block in blocks)
            {
                if (string.IsNullOrWhiteSpace(block.Data)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(block.Data);
                    var root = doc.RootElement;

                    if (block.Type == "image" || root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "image")
                    {
                        if (root.TryGetProperty("attrs", out var attrs) && attrs.TryGetProperty("src", out var src))
                        {
                            return src.GetString();
                        }
                    }

                    string? nestedUrl = FindImageUrlInJsonTree(root);
                    if (!string.IsNullOrEmpty(nestedUrl)) return nestedUrl;
                }
                catch { }
            }
            return null;
        }

        private string? FindImageUrlInJsonTree(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("type", out var t) && t.GetString() == "image")
                {
                    if (element.TryGetProperty("attrs", out var attrs) && attrs.TryGetProperty("src", out var src))
                    {
                        return src.GetString();
                    }
                }

                foreach (var prop in element.EnumerateObject())
                {
                    string? result = FindImageUrlInJsonTree(prop.Value);
                    if (!string.IsNullOrEmpty(result)) return result;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in element.EnumerateArray())
                {
                    string? result = FindImageUrlInJsonTree(item);
                    if (!string.IsNullOrEmpty(result)) return result;
                }
            }
            return null;
        }
    }
}