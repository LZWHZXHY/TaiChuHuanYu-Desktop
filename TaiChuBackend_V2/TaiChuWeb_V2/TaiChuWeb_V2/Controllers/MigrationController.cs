using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.FinalNodes;

namespace TaiChuWeb_V2.Controllers
{
    [ApiController]
    [Route("api/system/[controller]")]
    public class MigrationController : ControllerBase
    {
        private readonly AppDbContext _context;

        public MigrationController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 0. 一键清空目标表 (重新迁移前调用)
        // ==========================================
        [HttpPost("clear-migration-data")]
        public async Task<IActionResult> ClearMigrationData()
        {
            try
            {
                // ⚠️ 必须按顺序删除：先删带有外键依赖的子表，最后删主表
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM final_relations");
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM final_node_contents");
                await _context.Database.ExecuteSqlRawAsync("DELETE FROM final_nodes");

                return Ok(new { Message = "✅ 目标迁移表已全部清空，现在可以重新执行迁移接口。" });
            }
            catch (Exception ex)
            {
                var innerMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { Message = "清空表格失败", Detail = innerMessage });
            }
        }

        [HttpPost("migrate-world-cards")]
        public async Task<IActionResult> MigrateWorldCards()
        {
            var oldCards = await _context.WorldCards
                .Include(c => c.Project)
                .ThenInclude(p => p.Owner)
                .AsNoTracking()
                .ToListAsync();

            int successCount = 0;
            int skipCount = 0;

            try
            {
                foreach (var card in oldCards)
                {
                    string targetId = card.Id.ToString();

                    bool exists = await _context.FinalNodes.AnyAsync(n => n.Id == targetId);
                    if (exists)
                    {
                        skipCount++;
                        continue;
                    }

                    string safeExcerpt = string.Empty;
                    if (!string.IsNullOrWhiteSpace(card.Description))
                    {
                        safeExcerpt = card.Description.Length > 490
                            ? card.Description.Substring(0, 490) + "..."
                            : card.Description;
                    }

                    string realCreatorId = card.Project?.OwnerId.ToString() ?? "00000000-0000-0000-0000-000000000000";
                    string realCreatorName = card.Project?.Owner?.Username ?? "未知作者";

                    var newNode = new UniversalNode
                    {
                        Id = targetId,
                        SpaceId = card.ProjectId,
                        CreatorId = realCreatorId,
                        CreatorName = realCreatorName,
                        Title = card.Title ?? "未命名节点",
                        Type = string.IsNullOrWhiteSpace(card.Type) ? "unknown" : card.Type,
                        CoverImage = card.CoverImage,
                        Excerpt = safeExcerpt,
                        Tags = string.IsNullOrWhiteSpace(card.Tags) ? "[]" : card.Tags,
                        Status = 0,
                        ExtraData = JsonSerializer.Serialize(new
                        {
                            SubType = card.SubType,
                            Aliases = card.Aliases,
                            Attributes = card.Attributes,
                            TimelineEvents = card.TimelineEvents,
                            GalleryImages = card.GalleryImages,
                            EmbeddedCards = card.EmbeddedCards,
                            FullDescription = card.Description
                        }),
                        CreatedAt = card.CreatedAt,
                        UpdatedAt = card.UpdatedAt
                    };

                    var newContent = new UniversalNodeContent
                    {
                        NodeId = targetId,
                        BlocksData = string.IsNullOrWhiteSpace(card.ContentBlocks) ? "[]" : card.ContentBlocks,
                        // 🌟 核心修复：把旧卡片的真实长文本赋给 HtmlCache
                        HtmlCache = card.Description,
                        LastSavedAt = card.UpdatedAt
                    };

                    _context.FinalNodes.Add(newNode);
                    _context.FinalNodeContents.Add(newContent);

                    await _context.SaveChangesAsync();
                    successCount++;
                }

                return Ok(new
                {
                    Message = "WorldCards 数据迁移执行完毕",
                    Migrated = successCount,
                    Skipped = skipCount,
                    TotalOldRecords = oldCards.Count
                });
            }
            catch (DbUpdateException ex)
            {
                var innerMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { Message = "数据写入数据库时发生异常！", Detail = innerMessage, MigratedBeforeCrash = successCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Message = "发生了未知异常", Detail = ex.Message });
            }
        }

        [HttpPost("migrate-wiki-articles")]
        public async Task<IActionResult> MigrateWikiArticles()
        {
            var oldWikis = await _context.WikiArticles.AsNoTracking().ToListAsync();

            var userIds = oldWikis.Select(w => w.CreatorId).Distinct().ToList();
            var userDict = await _context.Users
                .Where(u => userIds.Contains(u.Id.ToString()))
                .ToDictionaryAsync(u => u.Id.ToString(), u => u.Username);

            int successCount = 0;
            int skipCount = 0;

            try
            {
                foreach (var wiki in oldWikis)
                {
                    string targetId = wiki.Id;

                    bool exists = await _context.FinalNodes.AnyAsync(n => n.Id == targetId);
                    if (exists)
                    {
                        skipCount++;
                        continue;
                    }

                    var revision = await _context.WikiArticleRevisions
                        .Where(r => r.ArticleId == wiki.Id && r.Status == 1)
                        .OrderByDescending(r => r.CreatedAt)
                        .FirstOrDefaultAsync();

                    string contentBlocks = revision?.Content ?? "[]";

                    string safeExcerpt = string.Empty;
                    if (!string.IsNullOrWhiteSpace(wiki.Excerpt))
                    {
                        safeExcerpt = wiki.Excerpt.Length > 490
                            ? wiki.Excerpt.Substring(0, 490) + "..."
                            : wiki.Excerpt;
                    }

                    string realCreatorName = userDict.ContainsKey(wiki.CreatorId)
                        ? userDict[wiki.CreatorId]
                        : "未知作者";

                    var newNode = new UniversalNode
                    {
                        Id = targetId,
                        SpaceId = Guid.Empty,
                        CreatorId = wiki.CreatorId,
                        CreatorName = realCreatorName,
                        Title = string.IsNullOrWhiteSpace(wiki.Title) ? "无标题百科" : wiki.Title,
                        Type = "wiki",
                        CategoryId = wiki.CategoryId,
                        Excerpt = safeExcerpt,
                        Tags = string.IsNullOrWhiteSpace(wiki.Tags) ? "[]" : wiki.Tags,
                        Status = wiki.IsDeleted ? 2 : 0,
                        ViewCount = wiki.ViewCount,
                        CreatedAt = wiki.CreatedAt,
                        UpdatedAt = wiki.UpdatedAt
                    };

                    var newContent = new UniversalNodeContent
                    {
                        NodeId = targetId,
                        BlocksData = contentBlocks,
                        // 🌟 备份一份纯文本到 HtmlCache
                        HtmlCache = contentBlocks,
                        LastSavedAt = wiki.UpdatedAt
                    };

                    _context.FinalNodes.Add(newNode);
                    _context.FinalNodeContents.Add(newContent);

                    await _context.SaveChangesAsync();
                    successCount++;
                }

                return Ok(new { Message = "Wiki 数据迁移完毕", Migrated = successCount, Skipped = skipCount, TotalOldRecords = oldWikis.Count });
            }
            catch (DbUpdateException ex)
            {
                var innerMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { Message = "Wiki 写入数据库时异常", Detail = innerMessage, MigratedBeforeCrash = successCount });
            }
        }

        [HttpPost("migrate-published-notes")]
        public async Task<IActionResult> MigratePublishedNotes()
        {
            var pubNotes = await _context.PublishedNotes.AsNoTracking().ToListAsync();
            int successCount = 0;
            int skipCount = 0;

            try
            {
                foreach (var note in pubNotes)
                {
                    string targetId = note.Id.ToString();

                    bool exists = await _context.FinalNodes.AnyAsync(n => n.Id == targetId);
                    if (exists)
                    {
                        skipCount++;
                        continue;
                    }

                    string safeExcerpt = string.Empty;
                    if (!string.IsNullOrWhiteSpace(note.Excerpt))
                    {
                        safeExcerpt = note.Excerpt.Length > 490
                            ? note.Excerpt.Substring(0, 490) + "..."
                            : note.Excerpt;
                    }

                    var newNode = new UniversalNode
                    {
                        Id = targetId,
                        SpaceId = note.SpaceId,
                        CreatorId = "00000000-0000-0000-0000-000000000000",
                        CreatorName = string.IsNullOrWhiteSpace(note.AuthorName) ? "匿名作者" : note.AuthorName,
                        Title = string.IsNullOrWhiteSpace(note.Title) ? "无标题" : note.Title,
                        Type = string.IsNullOrWhiteSpace(note.Type) ? "note" : note.Type,
                        Excerpt = safeExcerpt,
                        Tags = string.IsNullOrWhiteSpace(note.Tags) ? "[]" : note.Tags,
                        Status = 0,
                        ExtraData = string.IsNullOrWhiteSpace(note.ExtraData) ? "{}" : note.ExtraData,
                        CreatedAt = note.PublishedAt,
                        UpdatedAt = note.PublishedAt
                    };

                    var blocks = await _context.PublishedBlocks
                        .Where(b => b.OwnerId == targetId && b.OwnerType == note.Type)
                        .OrderBy(b => b.SortOrder)
                        .ToListAsync();

                    string contentData = blocks.Any() ? JsonSerializer.Serialize(blocks.Select(b => b.Data)) : "[]";

                    var newContent = new UniversalNodeContent
                    {
                        NodeId = targetId,
                        BlocksData = contentData,
                        LastSavedAt = note.PublishedAt
                    };

                    _context.FinalNodes.Add(newNode);
                    _context.FinalNodeContents.Add(newContent);

                    await _context.SaveChangesAsync();
                    successCount++;
                }

                return Ok(new { Message = "PublishedNotes 数据迁移完毕", Migrated = successCount, Skipped = skipCount, TotalOldRecords = pubNotes.Count });
            }
            catch (DbUpdateException ex)
            {
                var innerMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { Message = "PublishedNotes 写入数据库时异常", Detail = innerMessage, MigratedBeforeCrash = successCount });
            }
        }

        [HttpPost("migrate-relations")]
        public async Task<IActionResult> MigrateRelations()
        {
            int successCount = 0;
            int skipCount = 0;

            try
            {
                var oldWorldRelations = await _context.WorldRelations.AsNoTracking().ToListAsync();
                var validNodeIds = await _context.FinalNodes.Select(n => n.Id).ToHashSetAsync();

                foreach (var rel in oldWorldRelations)
                {
                    string targetId = rel.Id.ToString();
                    bool exists = await _context.FinalRelations.AnyAsync(r => r.Id == targetId);
                    if (exists) { skipCount++; continue; }

                    string sourceId = rel.SourceCardId.ToString();
                    string destId = rel.TargetCardId.ToString();

                    if (!validNodeIds.Contains(sourceId) || !validNodeIds.Contains(destId))
                    {
                        continue;
                    }

                    var newRel = new UniversalRelation
                    {
                        Id = targetId,
                        SourceNodeId = sourceId,
                        TargetNodeId = destId,
                        RelationType = string.IsNullOrWhiteSpace(rel.RelationType) ? "reference" : rel.RelationType,
                        ExtraData = "{}",
                        CreatedAt = rel.CreatedAt
                    };

                    _context.FinalRelations.Add(newRel);
                    successCount++;
                }

                var oldNoteLinks = await _context.NoteLinks.AsNoTracking().ToListAsync();
                foreach (var link in oldNoteLinks)
                {
                    string targetId = link.Id.ToString();
                    if (await _context.FinalRelations.AnyAsync(r => r.Id == targetId)) { skipCount++; continue; }

                    string sourceId = link.SourceNoteId.ToString();
                    string destId = link.TargetNoteId.ToString();

                    if (!validNodeIds.Contains(sourceId) || !validNodeIds.Contains(destId)) continue;

                    var newRel = new UniversalRelation
                    {
                        Id = targetId,
                        SourceNodeId = sourceId,
                        TargetNodeId = destId,
                        RelationType = "reference",
                        ExtraData = "{}",
                        CreatedAt = link.CreatedAt
                    };
                    _context.FinalRelations.Add(newRel);
                    successCount++;
                }

                await _context.SaveChangesAsync();

                return Ok(new { Message = "Relations 关系网迁移完毕", Migrated = successCount, Skipped = skipCount });
            }
            catch (DbUpdateException ex)
            {
                var innerMessage = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
                return StatusCode(500, new { Message = "关系网写入数据库时异常", Detail = innerMessage, MigratedBeforeCrash = successCount });
            }
        }
    }
}