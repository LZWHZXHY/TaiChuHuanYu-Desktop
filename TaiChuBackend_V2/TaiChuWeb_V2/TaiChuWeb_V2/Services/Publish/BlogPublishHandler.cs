using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.LingMai;

namespace TaiChuWeb_V2.Services.Publish
{
    public class BlogPublishHandler : ILingMaiPublishHandler
    {
        private readonly AppDbContext _context;

        // 🌟 必须严格对应前端的多态形态 type: "blog"
        public string SupportType => "blog";

        public BlogPublishHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> ExecutePublishAsync(Guid noteId, string userId, int? categoryId, string? projectId = null)
        {
            // 使用数据库执行策略，确保高并发下的弹性容错
            return await _context.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
            {
                // 1. 获取灵脉草稿
                var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId);
                if (note == null) return new NotFoundObjectResult(new { message = "草稿不存在" });

                // 2. 🌟 组件自治配套：允许 OwnerType == "blog" 或 "note" 合流抓取正文积木块，确保数据完美固化
                string noteIdStr = noteId.ToString();
                var blogBlocks = await _context.Blocks
                    .Where(b => b.OwnerId == noteIdStr && (b.OwnerType == "blog" || b.OwnerType == "note"))
                    .OrderBy(b => b.SortOrder)
                    .ToListAsync();

                // 3. 🌟 核心升级：调用针对自治架构的专属摘要榨取函数
                string excerpt = ExtractBlogExcerpt(blogBlocks);

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 4. 查找或创建广场固化实体 (PublishedNotes)
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
                            Type = "blog", // 标记发布类型为博客
                            AuthorName = authorName,
                            Resonance = 0,
                            PublishedAt = DateTime.UtcNow
                        };
                        _context.PublishedNotes.Add(publishedNote);
                    }

                    // 5. 同步更新发布实体的核心信息
                    publishedNote.Title = note.Title;
                    publishedNote.Tags = note.Tags;
                    publishedNote.Excerpt = excerpt;
                    publishedNote.ExtraData = note.ExtraData;
                    publishedNote.PublishedAt = DateTime.UtcNow;

                    // 6. 物理同步发布块 PublishedBlocks（先清空旧发布，再无缝覆写）
                    var oldPubBlocks = await _context.PublishedBlocks
                        .Where(pb => pb.OwnerId == publishedNote.Id.ToString())
                        .ToListAsync();
                    _context.PublishedBlocks.RemoveRange(oldPubBlocks);

                    // 遍历存入发布专属的区块表
                    foreach (var block in blogBlocks)
                    {
                        Guid.TryParse(block.Id, out Guid parsedBlockId);

                        _context.PublishedBlocks.Add(new PublishedBlock
                        {
                            Id = parsedBlockId != Guid.Empty ? parsedBlockId : Guid.NewGuid(),
                            OwnerId = publishedNote.Id.ToString(),
                            // 🌟 配合组件自治契约：发布后的固化区段也标记为大统一的 "blog" 标识，方便大厅详情页拉取
                            OwnerType = "blog",
                            Type = block.Type,
                            Data = block.Data,
                            SortOrder = block.SortOrder
                        });
                    }

                    // 7. 更改原始草稿的公开状态
                    note.IsPublic = true;
                    note.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new OkObjectResult(new { success = true, publishedId = publishedNote.Id });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return new BadRequestObjectResult(new { message = $"博客发布失败: {ex.Message}" });
                }
            });
        }

        private string ExtractBlogExcerpt(List<Block> blocks)
        {
            string coverUrl = "";
            string textExcerpt = "";

            // 1. 提取封面图 URL
            var fixedCoverBlock = blocks.FirstOrDefault(b => b.Type == "blog_fixed_cover");
            if (fixedCoverBlock != null && !string.IsNullOrWhiteSpace(fixedCoverBlock.Data))
            {
                try
                {
                    using var doc = JsonDocument.Parse(fixedCoverBlock.Data);
                    if (doc.RootElement.TryGetProperty("url", out var urlProp))
                    {
                        coverUrl = urlProp.GetString() ?? "";
                    }
                }
                catch { }
            }

            // 2. 提取摘要文本 (策略 1：固定摘要块)
            var fixedExcerptBlock = blocks.FirstOrDefault(b => b.Type == "blog_fixed_excerpt");
            if (fixedExcerptBlock != null && !string.IsNullOrWhiteSpace(fixedExcerptBlock.Data))
            {
                try
                {
                    using var doc = JsonDocument.Parse(fixedExcerptBlock.Data);
                    if (doc.RootElement.TryGetProperty("text", out var textProp))
                    {
                        textExcerpt = textProp.GetString()?.Trim() ?? "";
                    }
                }
                catch { }
            }

            // 策略 2：自动向正文截取兜底[cite: 11]
            if (string.IsNullOrEmpty(textExcerpt))
            {
                var firstTextParagraph = blocks.FirstOrDefault(b => b.Type == "paragraph" && b.SortOrder >= 2); 
        if (firstTextParagraph != null && !string.IsNullOrWhiteSpace(firstTextParagraph.Data))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(firstTextParagraph.Data);
                        if (doc.RootElement.TryGetProperty("content", out var contentArr) && contentArr.ValueKind == JsonValueKind.Array)
                        {
                            var text = string.Concat(contentArr.EnumerateArray()
                                       .Where(i => i.TryGetProperty("text", out _))
                                       .Select(i => i.GetProperty("text").GetString()));
                            textExcerpt = text.Length > 150 ? text.Substring(0, 150) + "..." : text;
                        }
                    }
                    catch { }
                }
            }

            if (string.IsNullOrEmpty(textExcerpt)) textExcerpt = "深度博客，静候回响..."; 

    // 3. 🌟【核心闭环】：将封面与摘要打包成一段结构化 JSON 塞进 Excerpt 字段
    // 这样既不污染 ExtraData，首页列表接口拉取时又能同时拿到图片和纯文字
    var payload = new
    {
        coverUrl = coverUrl,
        text = textExcerpt
    };

            return JsonSerializer.Serialize(payload);
        }
    }
}