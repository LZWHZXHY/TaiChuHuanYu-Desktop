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

        // 获取当前登录用户 ID，确保越权隔离
        private string? CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        public LingMaiPublishController(AppDbContext context)
        {
            _context = context;
        }

        #region --- 1. 发布与取消发布 (双表物理隔离 + 独立主键快照化) ---

        [HttpPost("notes/{id:guid}/publish")]
        public async Task<IActionResult> PublishNote([FromRoute] Guid id, [FromQuery] string type = "note")
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            // 1. 获取草稿
            var note = await _context.Notes.FindAsync(id);
            if (note == null) return NotFound(new { message = "未找到该草稿" });

            // 🌟 2. 根据多态条件获取草稿的所有块
            var draftBlocks = await _context.Blocks
                .Where(b => b.OwnerId == id.ToString() && b.OwnerType == "note")
                .ToListAsync();

            // 3. 检查是否已经发布过
            var publishedNote = await _context.PublishedNotes
                .FirstOrDefaultAsync(pn => pn.SpaceId == note.SpaceId && pn.OriginalNoteId == id);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (publishedNote == null)
                {
                    // 新建发布
                    publishedNote = new PublishedNote
                    {
                        Id = Guid.NewGuid(),
                        SpaceId = note.SpaceId,
                        OriginalNoteId = id,
                        Title = note.Title,
                        Type = note.Type,
                        PublishedAt = DateTime.UtcNow,
                        Resonance = 0
                    };
                    _context.PublishedNotes.Add(publishedNote);
                }
                else
                {
                    // 更新发布
                    publishedNote.Title = note.Title;
                    publishedNote.Type = note.Type;
                    publishedNote.PublishedAt = DateTime.UtcNow;

                    // 🌟 清理该发布文档之前的旧发布块（使用多态标识清理）
                    var oldPubBlocks = await _context.PublishedBlocks
                        .Where(pb => pb.OwnerId == publishedNote.Id.ToString() && pb.OwnerType == "note")
                        .ToListAsync();
                    _context.PublishedBlocks.RemoveRange(oldPubBlocks);
                }

                await _context.SaveChangesAsync();

                // 🌟 4. 插入新的发布块（使用多态 OwnerId 和 OwnerType）
                var pubBlocks = draftBlocks.Select(db => new PublishedBlock
                {
                    Id = Guid.NewGuid(),
                    OwnerId = publishedNote.Id.ToString(), // 对应已发布笔记的 ID
                    OwnerType = "note",
                    Type = db.Type,
                    Data = db.Data,
                    SortOrder = int.Parse(db.SortOrder) // 转换排序号为 int
                }).ToList();

                _context.PublishedBlocks.AddRange(pubBlocks);
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
            if (note == null) return NotFound(new { message = "未找到指定的灵脉碎片" });

            // 🔒 越权校验
            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. 通过 OriginalNoteId 查找发布区中的快照
                var existingPublish = await _context.PublishedNotes
                    .FirstOrDefaultAsync(pn => pn.OriginalNoteId == id);

                if (existingPublish != null)
                {
                    // 🌟 核心修改 1：使用多态条件显式查出属于该发布笔记的内容块并物理移除
                    var pubBlocks = await _context.PublishedBlocks
                        .Where(pb => pb.OwnerId == existingPublish.Id.ToString() && pb.OwnerType == "note")
                        .ToListAsync();

                    _context.PublishedBlocks.RemoveRange(pubBlocks);
                    _context.PublishedNotes.Remove(existingPublish);
                }

                // 2. 更新原草稿笔记状态
                note.IsPublic = false;
                note.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "已从广场下线", isPublic = false });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "取消发布异常", error = ex.Message });
            }
        }

        #endregion

        #region --- 2. 广场信息流与公开阅读 ---

        [HttpGet("public-stream")]
        public async Task<IActionResult> GetPublicStream([FromQuery] string? type, [FromQuery] int limit = 20)
        {
            var query = _context.PublishedNotes.AsNoTracking();

            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(pn => pn.Type == type);
            }

            var stream = await query
                .OrderByDescending(pn => pn.PublishedAt)
                .Select(pn => new
                {
                    pn.Id, // 返回的是独立的发布表 Id
                    pn.Title,
                    pn.Type,
                    pn.SpaceId,
                    pn.PublishedAt,
                    pn.Resonance,
                    // 🌟 核心修改 2：把 PublishedNoteId 改为多态指针字段 OwnerId 和 OwnerType
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
            // 1. 先查出发布笔记的基本信息
            var publishedNote = await _context.PublishedNotes
                .AsNoTracking()
                .FirstOrDefaultAsync(pn => pn.Id == id);

            if (publishedNote == null) return NotFound(new { message = "此思维碎片未发布或不存在" });

            // 2. 🌟 通过多态 OwnerId 获取对应的发布内容块
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
                publishedNote.Resonance,
                Blocks = blocks
            });
        }

        #endregion
    }
}