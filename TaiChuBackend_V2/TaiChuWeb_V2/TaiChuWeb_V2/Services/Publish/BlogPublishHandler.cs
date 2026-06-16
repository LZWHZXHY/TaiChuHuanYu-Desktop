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

        public async Task<IActionResult> ExecutePublishAsync(Guid noteId, string userId, int? categoryId)
        {
            // 使用数据库执行策略，确保高并发下的弹性容错
            return await _context.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
            {
                // 1. 获取灵脉草稿
                var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId);
                if (note == null) return new NotFoundObjectResult(new { message = "草稿不存在" });

                // 2. 批量拉取该笔记关联的所有富文本正文数据块
                var blogBlocks = await _context.Blocks
                    .Where(b => b.OwnerId == noteId.ToString() && b.OwnerType == "note")
                    .OrderBy(b => b.SortOrder)
                    .ToListAsync();

                // 3. 提取第一段文字作为摘要
                string excerpt = ExtractFirstParagraph(blogBlocks);

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 4. 查找或创建广场固化实体 (PublishedNotes)
                    var publishedNote = await _context.PublishedNotes
                        .FirstOrDefaultAsync(pn => pn.OriginalNoteId == noteId);

                    bool isNew = publishedNote == null;

                    // 🌟 强转换 Guid 去 Users 查询
                    Guid.TryParse(userId, out Guid parsedUserId);
                    var dbUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == parsedUserId);

                    // 🌟 针对你的数据库 User 实体属性名匹配
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
                            Resonance = 0, // 初始共鸣度
                            PublishedAt = DateTime.UtcNow
                        };
                        _context.PublishedNotes.Add(publishedNote);
                    }

                    // 5. 同步更新发布实体的核心信息
                    publishedNote.Title = note.Title;
                    publishedNote.Tags = note.Tags;       // 继承同步过来的标签快照
                    publishedNote.Excerpt = excerpt;       // 🌟 完美同步到 PublishedNote 的 Excerpt 冗余字段中
                    publishedNote.ExtraData = note.ExtraData; // 传递包含封面图在内的配置数据
                    publishedNote.PublishedAt = DateTime.UtcNow;

                    // 6. 物理同步发布块 PublishedBlocks（先清空旧发布，再无缝覆写）
                    // 🌟 解决旧的编译器报错：OwnerId 是 string，右侧必须用 ToString() 对齐
                    var oldPubBlocks = await _context.PublishedBlocks
                        .Where(pb => pb.OwnerId == publishedNote.Id.ToString())
                        .ToListAsync();
                    _context.PublishedBlocks.RemoveRange(oldPubBlocks);

                    // 遍历存入发布专属的区块表
                    foreach (var block in blogBlocks)
                    {
                        // 🌟 解决 block.Id 是 string，向 PublishedBlock.Id (Guid) 转换的问题
                        Guid.TryParse(block.Id, out Guid parsedBlockId);

                        _context.PublishedBlocks.Add(new PublishedBlock
                        {
                            // 如果前端传过来的 Block.Id 是合法的 Guid 字符串，则原样奉还，否则自动生成
                            Id = parsedBlockId != Guid.Empty ? parsedBlockId : Guid.NewGuid(),

                            // 🌟 解决 OwnerId 强类型匹配：必须转成字符串存储
                            OwnerId = publishedNote.Id.ToString(),
                            OwnerType = "note",
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

        /// <summary>
        /// 从 Blocks 中榨取第一段文字作为摘要
        /// </summary>
        private string ExtractFirstParagraph(List<Block> blocks)
        {
            var firstParagraph = blocks.FirstOrDefault(b => b.Type == "paragraph");
            if (firstParagraph == null || string.IsNullOrWhiteSpace(firstParagraph.Data))
                return "灵脉深处暂无回响...";

            try
            {
                using var doc = JsonDocument.Parse(firstParagraph.Data);
                if (doc.RootElement.TryGetProperty("content", out var contentArr) && contentArr.ValueKind == JsonValueKind.Array)
                {
                    var text = string.Concat(contentArr.EnumerateArray()
                               .Where(i => i.TryGetProperty("text", out _))
                               .Select(i => i.GetProperty("text").GetString()));

                    return text.Length > 150 ? text.Substring(0, 150) + "..." : text;
                }
            }
            catch { }

            return "灵脉深处暂无回响...";
        }
    }
}