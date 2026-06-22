using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Models.Wiki;

namespace TaiChuWeb_V2.Services.Publish
{
    public class WikiPublishHandler : ILingMaiPublishHandler
    {
        private readonly AppDbContext _context;
        public string SupportType => "wiki";

        public WikiPublishHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> ExecutePublishAsync(Guid noteId, string userId, int? categoryId, string? projectId = null)
        {
            // 基础校验
            if (!Guid.TryParse(userId, out var userGuid))
                return new BadRequestObjectResult(new { message = "无效的用户ID格式" });

            int finalCategoryId = categoryId ?? 3;
            if (finalCategoryId <= 0)
                return new BadRequestObjectResult(new { message = "必须指定有效的分类ID" });

            // 使用 ExecutionStrategy
            return await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                // 1. 获取灵脉草稿
                var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId);
                if (note == null) return new NotFoundObjectResult(new { message = "未找到对应的灵脉草稿" });

                // 2. 🌟【组件自治对齐修复】：OwnerType 允许匹配大统一的 "wiki" 标识或通用 "note" 标识，确保数据块绝不漏捞
                string noteIdStr = noteId.ToString();
                var draftBlocks = await _context.Blocks
                    .Where(b => b.OwnerId == noteIdStr && (b.OwnerType == "wiki" || b.OwnerType == "note" || b.OwnerType == note.Type))
                    .OrderBy(b => b.SortOrder)
                    .ToListAsync();

                string excerpt = ExtractExcerpt(draftBlocks);
                var tagNames = await _context.TagAssignments
                    .Where(ta => ta.EntityId == noteIdStr && ta.EntityType == "note")
                    .Include(ta => ta.Tag)
                    .Select(ta => ta.Tag!.Name)
                    .ToListAsync();
                string joinedTags = string.Join(",", tagNames);

                // 初始化变量用于存储结果
                IActionResult actionResult;

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var wikiArticle = await _context.WikiArticles
                        .FirstOrDefaultAsync(wa => wa.SourceNoteId == noteIdStr);

                    bool isNew = wikiArticle == null;
                    if (isNew)
                    {
                        wikiArticle = new WikiArticle
                        {
                            Id = Guid.NewGuid().ToString(),
                            SourceNoteId = noteIdStr,
                            CreatorId = userId,
                            IsFromNote = true,
                            CategoryId = finalCategoryId,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.WikiArticles.Add(wikiArticle);
                    }

                    wikiArticle.Title = note.Title;
                    wikiArticle.Excerpt = excerpt;
                    wikiArticle.Tags = joinedTags;
                    wikiArticle.CategoryId = finalCategoryId;
                    wikiArticle.UpdatedAt = DateTime.UtcNow;

                    string fullContentJson = SerializeToTiptap(draftBlocks);
                    var newRevision = new WikiArticleRevision
                    {
                        ArticleId = wikiArticle.Id,
                        PreviousRevisionId = wikiArticle.CurrentRevisionId,
                        Content = fullContentJson,
                        ContributorId = userId,
                        CategoryId = finalCategoryId,
                        Title = note.Title,
                        EditSummary = isNew ? "由灵脉草稿初始固化发布" : "由灵脉空间同步更新迭代",
                        CreatedAt = DateTime.UtcNow,
                        Status = 0,
                        ReviewerId = userId,
                        ReviewedAt = DateTime.UtcNow,
                        ReviewRemarks = "太初实体分流策略自动固化"
                    };

                    _context.WikiArticleRevisions.Add(newRevision);
                    await _context.SaveChangesAsync();

                    wikiArticle.CurrentRevisionId = newRevision.Id;
                    note.IsPublic = true;
                    note.Type = NoteTypes.Wiki;
                    note.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    actionResult = new OkObjectResult(new { success = true, articleId = wikiArticle.Id });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    actionResult = new BadRequestObjectResult(new { message = $"同步失败: {ex.Message}" });
                }

                return actionResult; // 确保这里一定返回了一个 IActionResult
            });
        }

        private string ExtractExcerpt(List<Block> blocks)
        {
            var firstParagraph = blocks.FirstOrDefault(b => b.Type == "paragraph");
            if (firstParagraph == null) return "灵脉深处暂无回响...";
            try
            {
                using var doc = JsonDocument.Parse(firstParagraph.Data);
                var text = string.Concat(doc.RootElement.GetProperty("content").EnumerateArray()
                           .Where(i => i.TryGetProperty("text", out _)).Select(i => i.GetProperty("text").GetString()));
                return text.Length > 120 ? text.Substring(0, 120) + "..." : text;
            }
            catch { return "灵脉深处暂无回响..."; }
        }

        private string SerializeToTiptap(List<Block> blocks)
        {
            var tiptapDoc = new
            {
                type = "doc",
                content = blocks.Select(b => {
                    try
                    {
                        using var doc = JsonDocument.Parse(b.Data);
                        return (object)new
                        {
                            type = b.Type,
                            attrs = doc.RootElement.TryGetProperty("attrs", out var a) ? a.Clone() : (object)new { },
                            content = doc.RootElement.TryGetProperty("content", out var c) ? c.Clone() : (object?)null
                        };
                    }
                    catch { return new { type = "paragraph", content = new[] { new { type = "text", text = "" } } }; }
                }).ToList()
            };
            return JsonSerializer.Serialize(tiptapDoc);
        }
    }
}