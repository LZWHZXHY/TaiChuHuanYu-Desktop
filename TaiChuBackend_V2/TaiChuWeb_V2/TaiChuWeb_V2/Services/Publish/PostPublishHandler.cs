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

        // 🌟 严格对应前端动态多态形态 type: "post"
        public string SupportType => NoteTypes.Post; // 即 "post"

        public PostPublishHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> ExecutePublishAsync(Guid noteId, string userId, int? categoryId)
        {
            // 使用 ExecutionStrategy 确保高并发下的弹性容错
            return await _context.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
            {
                // 1. 获取原始草稿
                var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId);
                if (note == null) return new NotFoundObjectResult(new { message = "未找到该短动态草稿" });

                // 2. 批量拉取该动态关联的所有富文本区块
                var postBlocks = await _context.Blocks
                    .Where(b => b.OwnerId == noteId.ToString() && b.OwnerType == "note")
                    .OrderBy(b => b.SortOrder)
                    .ToListAsync();

                // 3. 榨取动态全文的第一段话，直接作为广场瀑布流展示的平铺摘要
                string excerpt = ExtractPostExcerpt(postBlocks);

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 4. 寻找或创建广场固化实体
                    var publishedNote = await _context.PublishedNotes
                        .FirstOrDefaultAsync(pn => pn.OriginalNoteId == noteId);

                    bool isNew = publishedNote == null;

                    // 转换 Guid 去 Users 表查询创作者
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
                            Type = NoteTypes.Post, // 固化类型为 post
                            AuthorName = authorName,
                            Resonance = 0,
                            PublishedAt = DateTime.UtcNow
                        };
                        _context.PublishedNotes.Add(publishedNote);
                    }

                    // 5. 同步元数据到发布表 (短动态的 Title 通常是你前端截取的前15个字)
                    publishedNote.Title = note.Title;
                    publishedNote.Tags = note.Tags;
                    publishedNote.Excerpt = excerpt; // 🌟 关键：短动态内容直接平铺在 Excerpt 字段，方便广场秒级渲染
                    publishedNote.ExtraData = note.ExtraData; // 透传角色雷达或其余 JSON 配置
                    publishedNote.PublishedAt = DateTime.UtcNow;

                    // 6. 增量覆写物理发布块 PublishedBlocks 
                    // 🔒 迎合你的底层强类型：OwnerId 是 string，右侧用 ToString() 对齐
                    var oldPubBlocks = await _context.PublishedBlocks
                        .Where(pb => pb.OwnerId == publishedNote.Id.ToString())
                        .ToListAsync();
                    _context.PublishedBlocks.RemoveRange(oldPubBlocks);

                    foreach (var block in postBlocks)
                    {
                        // 🔒 迎合你的底层强类型：block.Id (string) 转换为 Guid
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

                    // 7. 更改原始草稿公开状态
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

        /// <summary>
        /// 针对短动态优化的文本段落榨取引擎
        /// </summary>
        private string ExtractPostExcerpt(List<Block> blocks)
        {
            var firstParagraph = blocks.FirstOrDefault(b => b.Type == "paragraph");
            if (firstParagraph == null || string.IsNullOrWhiteSpace(firstParagraph.Data))
                return "一语落毕，灵脉寂静...";

            try
            {
                using var doc = JsonDocument.Parse(firstParagraph.Data);
                if (doc.RootElement.TryGetProperty("content", out var contentArr) && contentArr.ValueKind == JsonValueKind.Array)
                {
                    var text = string.Concat(contentArr.EnumerateArray()
                               .Where(i => i.TryGetProperty("text", out _))
                               .Select(i => i.GetProperty("text").GetString()));

                    // 短动态不需要截取太短，尽量展示全貌（放宽到 300 字限制）
                    return text.Length > 300 ? text.Substring(0, 300) + "..." : text;
                }
            }
            catch { }

            return "一语落毕，灵脉寂静...";
        }
    }
}