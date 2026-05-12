// TaiChuWeb_V2/Controllers/LingMai/LingMaiPublishController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.LingMai;

namespace TaiChuWeb_V2.Controllers.LingMai
{
    [ApiController]
    [Route("api/[controller]")]
    public class LingMaiPublishController : ControllerBase
    {
        private readonly AppDbContext _context;

        // 获取当前登录用户 ID
        private string? CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        public LingMaiPublishController(AppDbContext context)
        {
            _context = context;
        }

        #region --- 1. 发布与取消发布 ---

        // TaiChuWeb_V2/Controllers/LingMai/LingMaiPublishController.cs

        [HttpPost("notes/{id:guid}/publish")]
        public async Task<IActionResult> PublishNote([FromRoute] Guid id, [FromQuery] string type = "note")
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            // 1. 获取原始草稿
            var note = await _context.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);
            if (note == null) return NotFound(new { message = "未找到该草稿" });

            // 🌟 2. 准备快照数据：获取作者名称
            // 1. 建议将变量名改为 dbUser，避免和系统的 User 属性混淆
            var dbUser = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == Guid.Parse(CurrentUserId));

            // 2. 🌟 修正点：将 .UserName 改为 .Username (注意大小写)
            var authorName = dbUser?.Username ?? "未知编织者";

            // 🌟 3. 准备快照数据：从通用标签表中拉取当前笔记的所有标签
            var tagNames = await _context.TagAssignments
                .Where(ta => ta.EntityId == id.ToString() && ta.EntityType == "note")
                .Include(ta => ta.Tag)
                .Select(ta => ta.Tag!.Name)
                .ToListAsync();

            // 4. 获取草稿的所有内容块
            var draftBlocks = await _context.Blocks
                .Where(b => b.OwnerId == id.ToString() && b.OwnerType == "note")
                .OrderBy(b => b.SortOrder)
                .ToListAsync();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var publishedNote = await _context.PublishedNotes
                    .FirstOrDefaultAsync(pn => pn.OriginalNoteId == id);

                if (publishedNote == null)
                {
                    publishedNote = new PublishedNote
                    {
                        Id = Guid.NewGuid(),
                        SpaceId = note.SpaceId,
                        OriginalNoteId = id,
                        Resonance = 0
                    };
                    _context.PublishedNotes.Add(publishedNote);
                }

                // 🌟 5. 同步快照字段到发布表
                publishedNote.Title = note.Title;
                publishedNote.Type = type; // 决定它是世界观、角色还是社区知识
                publishedNote.PublishedAt = DateTime.UtcNow;
                publishedNote.AuthorName = authorName;
                publishedNote.Tags = string.Join(",", tagNames); // 拍扁成字符串存储，方便前端 index.vue 读取

                // 🌟 6. 提取摘要：取第一个段落的前 100 个字
                var firstParagraph = draftBlocks
                    .FirstOrDefault(b => b.Type == "paragraph")?.Data;

                if (!string.IsNullOrEmpty(firstParagraph))
                {
                    // 简单处理：截取前100字作为摘要
                    publishedNote.Excerpt = firstParagraph.Length > 100
                        ? firstParagraph.Substring(0, 100) + "..."
                        : firstParagraph;
                }

                await _context.SaveChangesAsync();

                // 7. 同步内容块快照 (PublishedBlocks)
                var oldPubBlocks = await _context.PublishedBlocks
                    .Where(pb => pb.OwnerId == publishedNote.Id.ToString() && pb.OwnerType == "note")
                    .ToListAsync();
                _context.PublishedBlocks.RemoveRange(oldPubBlocks);

                var pubBlocks = draftBlocks.Select(db => new PublishedBlock
                {
                    Id = Guid.NewGuid(),
                    OwnerId = publishedNote.Id.ToString(),
                    OwnerType = "note",
                    Type = db.Type,
                    Data = db.Data,
                    SortOrder = int.TryParse(db.SortOrder, out var order) ? order : 0
                }).ToList();

                _context.PublishedBlocks.AddRange(pubBlocks);

                // 8. 标记原笔记为已公开
                note.IsPublic = true;
                _context.Entry(note).Property(n => n.IsPublic).IsModified = true;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, publishedNoteId = publishedNote.Id });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = $"发布失败: {ex.Message}" });
            }
        }

        [HttpDelete("notes/{id:guid}/unpublish")]
        public async Task<IActionResult> UnpublishNote(Guid id)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var note = await _context.Notes.FindAsync(id);
            if (note == null) return NotFound(new { message = "未找到草稿" });

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var existingPublish = await _context.PublishedNotes
                    .FirstOrDefaultAsync(pn => pn.OriginalNoteId == id);

                if (existingPublish != null)
                {
                    var pubBlocks = await _context.PublishedBlocks
                        .Where(pb => pb.OwnerId == existingPublish.Id.ToString() && pb.OwnerType == "note")
                        .ToListAsync();

                    _context.PublishedBlocks.RemoveRange(pubBlocks);
                    _context.PublishedNotes.Remove(existingPublish);
                }

                note.IsPublic = false;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "已取消发布" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "取消发布异常", error = ex.Message });
            }
        }

        #endregion

        #region --- 2. 广场与阅读 ---

        [HttpGet("public-stream")]
        public async Task<IActionResult> GetPublicStream([FromQuery] string? type, [FromQuery] int limit = 20)
        {
            var query = _context.PublishedNotes.AsNoTracking();

            // 如果传了 type，这里就能正确过滤 blog 或 post 了
            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(pn => pn.Type == type);
            }

            var stream = await query
                .OrderByDescending(pn => pn.PublishedAt)
                .Select(pn => new
                {
                    pn.Id,
                    pn.Title,
                    pn.Type, // 这里的 type 会返回 blog/post 等
                    pn.SpaceId,
                    pn.PublishedAt,
                    pn.Resonance,
                    // 提取第一段文字作为摘要
                    Excerpt = _context.PublishedBlocks
                        .Where(pb => pb.OwnerId == pn.Id.ToString() && pb.OwnerType == "note" && pb.Type == "paragraph")
                        .OrderBy(pb => pb.SortOrder)
                        .Select(pb => pb.Data)
                        .FirstOrDefault()
                })
                .Take(limit > 100 ? 100 : limit)
                .ToListAsync();

            return Ok(stream);
        }

        [HttpGet("blog/{id:guid}")]
        public async Task<IActionResult> GetPublicBlog(Guid id)
        {
            var publishedNote = await _context.PublishedNotes
                .AsNoTracking()
                .FirstOrDefaultAsync(pn => pn.Id == id);

            if (publishedNote == null) return NotFound(new { message = "内容不存在" });

            var blocks = await _context.PublishedBlocks
                .Where(pb => pb.OwnerId == id.ToString() && pb.OwnerType == "note")
                .OrderBy(pb => pb.SortOrder)
                .Select(pb => new { pb.Id, pb.Type, pb.Data, pb.SortOrder })
                .ToListAsync();

            return Ok(new
            {
                publishedNote.Id,
                publishedNote.Title,
                publishedNote.Type,
                publishedNote.PublishedAt,
                Blocks = blocks
            });
        }

        #endregion
    }
}