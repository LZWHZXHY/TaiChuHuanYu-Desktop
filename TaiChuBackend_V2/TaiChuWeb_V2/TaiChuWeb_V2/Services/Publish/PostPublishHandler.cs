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

        public async Task<IActionResult> ExecutePublishAsync(Guid noteId, string userId, int? categoryId)
        {
            return await _context.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
            {
                var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId);
                if (note == null) return new NotFoundObjectResult(new { message = "未找到该短动态草稿" });

                var postBlocks = await _context.Blocks
                    .Where(b => b.OwnerId == noteId.ToString() && b.OwnerType == note.Type)
                    .OrderBy(b => b.SortOrder)
                    .ToListAsync();

                // 1. 提取文字摘要
                string excerpt = ExtractPostExcerpt(postBlocks);

                // 2. 🌟【核心修复】：升级为深层网络透传扫描器，精准捕捉嵌套的图片 URL
                string? firstImageUrl = ExtractFirstImageUrl(postBlocks);

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var publishedNote = await _context.PublishedNotes
                        .FirstOrDefaultAsync(pn => pn.OriginalNoteId == noteId);

                    bool isNew = publishedNote == null;

                    Guid.TryParse(userId, out Guid parsedUserId);
                    var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == parsedUserId);
                    string authorName = dbUser?.Username ?? "匿名隐士";

                    if (isNew)
                    {
                        publishedNote = new PublishedNote
                        {
                            Id = Guid.NewGuid(),
                            OriginalNoteId = noteId,
                            SpaceId = note.SpaceId,
                            Type = NoteTypes.Post,
                            AuthorName = authorName,
                            Resonance = 0,
                            PublishedAt = DateTime.UtcNow
                        };
                        _context.PublishedNotes.Add(publishedNote);
                    }

                    // 3. ✨【打包视觉挂件】：确保绝对能生成带有 cardCover 的配置字典
                    var metaDict = new Dictionary<string, string>();
                    if (!string.IsNullOrEmpty(firstImageUrl))
                    {
                        metaDict["cardCover"] = firstImageUrl;
                    }

                    publishedNote.Title = note.Title;
                    publishedNote.Tags = note.Tags;
                    publishedNote.Excerpt = excerpt;

                    // 如果捞到了图片，塞入专属的 cardCover 键值对，否则降级回原来的额外数据
                    publishedNote.ExtraData = metaDict.Count > 0 ? JsonSerializer.Serialize(metaDict) : note.ExtraData;
                    publishedNote.PublishedAt = DateTime.UtcNow;

                    // 4. 同步物理区块表
                    var oldPubBlocks = await _context.PublishedBlocks
                        .Where(pb => pb.OwnerId == publishedNote.Id.ToString())
                        .ToListAsync();
                    _context.PublishedBlocks.RemoveRange(oldPubBlocks);

                    foreach (var block in postBlocks)
                    {
                        Guid.TryParse(block.Id, out Guid parsedBlockId);
                        _context.PublishedBlocks.Add(new PublishedBlock
                        {
                            Id = parsedBlockId != Guid.Empty ? parsedBlockId : Guid.NewGuid(),
                            OwnerId = publishedNote.Id.ToString(),
                            OwnerType = "note",
                            Type = block.Type,
                            Data = block.Data,
                            SortOrder = block.SortOrder
                        });
                    }

                    note.IsPublic = true;
                    note.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new OkObjectResult(new { success = true, publishedId = publishedNote.Id });
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
            var firstParagraph = blocks.FirstOrDefault(b => b.Type == "paragraph");
            if (firstParagraph == null || string.IsNullOrWhiteSpace(firstParagraph.Data))
                return "一语落毕，灵脉寂静...";

            try
            {
                using var doc = JsonDocument.Parse(firstParagraph.Data);
                if (doc.RootElement.TryGetProperty("content", out var contentArr))
                {
                    var text = string.Concat(contentArr.EnumerateArray()
                               .Where(i => i.TryGetProperty("text", out _))
                               .Select(i => i.GetProperty("text").GetString()));
                    return text.Length > 300 ? text.Substring(0, 300) + "..." : text;
                }
            }
            catch { }
            return "一语落毕，灵脉寂静...";
        }

        /// <summary>
        /// 🌟【重构增强版】：深层自适应递归图片链接提取引擎
        /// </summary>
        private string? ExtractFirstImageUrl(List<Block> blocks)
        {
            foreach (var block in blocks)
            {
                if (string.IsNullOrWhiteSpace(block.Data)) continue;

                try
                {
                    using var doc = JsonDocument.Parse(block.Data);
                    var root = doc.RootElement;

                    // 路线 A：如果该 Block 本身就是一个独立的展示图片块 (如前端传来的 type: "image")
                    if (block.Type == "image" || root.TryGetProperty("type", out var typeProp) && typeProp.GetString() == "image")
                    {
                        if (root.TryGetProperty("attrs", out var attrs) && attrs.TryGetProperty("src", out var src))
                        {
                            return src.GetString();
                        }
                    }

                    // 路线 B：递归穿透深度扫描内部嵌套节点（防止 Tiptap 的复合节点嵌套嵌套）
                    string? nestedUrl = FindImageUrlInJsonTree(root);
                    if (!string.IsNullOrEmpty(nestedUrl))
                    {
                        return nestedUrl;
                    }
                }
                catch { }
            }
            return null;
        }

        /// <summary>
        /// 深度递归辅助器：自适应在任意 JSON 树枝节点中搜寻 src 属性
        /// </summary>
        private string? FindImageUrlInJsonTree(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                // 探测到图片节点的特征属性
                if (element.TryGetProperty("type", out var t) && t.GetString() == "image")
                {
                    if (element.TryGetProperty("attrs", out var attrs) && attrs.TryGetProperty("src", out var src))
                    {
                        return src.GetString();
                    }
                }

                // 顺着属性节点继续深挖
                foreach (var prop in element.EnumerateObject())
                {
                    string? result = FindImageUrlInJsonTree(prop.Value);
                    if (!string.IsNullOrEmpty(result)) return result;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                // 深度遍历数组内部的所有对象块
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