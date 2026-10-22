// TaiChuWeb_V2/Controllers/LingMai/LingMaiPublishController.cs
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.LingMai;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Services.LingMai;
using TaiChuWeb_V2.Services.Publish; // 引入策略命名空间

namespace TaiChuWeb_V2.Controllers.LingMai
{
    [ApiController]
    [Route("api/[controller]")]
    public class LingMaiPublishController : ControllerBase
    {
        private readonly AppDbContext _context;

        // 🌟 多态分流策略处理器字典
        private readonly Dictionary<string, ILingMaiPublishHandler> _publishHandlers;

        private readonly NoteCommentService _comments;

        // 获取当前登录用户 ID
        private string? CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        private Guid? CurrentUserIdGuid => Guid.TryParse(CurrentUserId, out var id) ? id : null;

        // 🌟 通过构造函数依赖注入所有的处理器，并自动编排为路由映射字典
        public LingMaiPublishController(AppDbContext context, IEnumerable<ILingMaiPublishHandler> handlers, NoteCommentService comments)
        {
            _context = context;
            _publishHandlers = handlers.ToDictionary(h => h.SupportType, h => h);
            _comments = comments;
        }





        [HttpPost("notes/{id:guid}/publish")]
        public async Task<IActionResult> PublishNote([FromRoute] Guid id, [FromBody] PublishRequest req)
        {
            if (!Guid.TryParse(CurrentUserId, out _)) return Unauthorized();

            if (req == null || string.IsNullOrWhiteSpace(req.type)) return BadRequest(new { message = "请选择发布类型" });
            var denied = await CheckOwnerAsync(id, req.type);
            if (denied != null) return denied;

            if (req.type == "note")
            {
                return BadRequest(new { message = "普通随笔碎片属于私密草稿，无法直接发布" });
            }

            // 🌟 纯粹的多态派发：前端发布面板传来什么 type (例如 "doc")，大厅直接交由对应的 Handler 处理
            if (!_publishHandlers.TryGetValue(req.type, out var handler))
            {
                return BadRequest(new { message = $"暂未编织该多态碎片形态 [{req.type}] 的分流大厅逻辑" });
            }

            // 🌟【核心修复点】：补上第四个参数 req.projectId！
            // 只有这样，前端传过来的项目 GUID 才能真正抵达 DocPublishHandler 内部，破除 400 校验拦截！
            return await handler.ExecutePublishAsync(id, CurrentUserId, req.categoryId, req.projectId);
        }

        #region --- 1. 取消发布 ---

        [HttpDelete("notes/{id:guid}/unpublish")]
        public async Task<IActionResult> UnpublishNote(Guid id)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var denied = await CheckOwnerAsync(id);
            if (denied != null) return denied;
            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
                return await strategy.ExecuteAsync<IActionResult>(async () =>
                {
                    var note = await _context.Notes.FindAsync(id);
                    if (note == null) return NotFound(new { message = "未找到草稿" });

                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        if (note.Type == NoteTypes.Doc)
                        {
                            var noteId = id.ToString();
                            var links = await _context.ProjectDocuments.Where(d => d.NoteId == noteId).ToListAsync();
                            _context.ProjectDocuments.RemoveRange(links);
                            note.UpdatedAt = DateTime.UtcNow;
                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();
                            return Ok(new { success = true, message = "已取消所有项目分享" });
                        }

                        var existingPublish = await _context.PublishedNotes
                            .FirstOrDefaultAsync(pn => pn.OriginalNoteId == id);

                        if (existingPublish != null)
                        {
                            var pubBlocks = await _context.PublishedBlocks
                                .Where(pb => pb.OwnerId == existingPublish.Id.ToString() && (pb.OwnerType == "note" || pb.OwnerType == existingPublish.Type))
                                .ToListAsync();

                            _context.PublishedBlocks.RemoveRange(pubBlocks);
                            _context.PublishedNotes.Remove(existingPublish);
                        }

                        // Serialize withdrawal with publication through the source note row.
                        await _context.Notes.Where(n => n.Id == id).ExecuteUpdateAsync(s => s.SetProperty(n => n.UpdatedAt, n => n.UpdatedAt));
                        await _context.Artworks.Where(a => a.OriginalNoteId == id).ExecuteUpdateAsync(s => s.SetProperty(a => a.IsApproved, false));
                        note.IsPublic = false;
                        note.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        return Ok(new { success = true, message = "已取消发布" });
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync();
                        throw;
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "取消发布异常", error = ex.Message });
            }
        }

        #endregion

        private async Task<IActionResult?> CheckOwnerAsync(Guid id, string? type = null)
        {
            var note = await _context.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id);
            if (note == null) return NotFound(new { message = "未找到草稿" });
            if (!await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId)) return Forbid();
            if (type != null && (note.Type != type || note.Status == 2))
                return BadRequest(new { message = "草稿类型不匹配或已被下架" });
            return null;
        }

        #region --- 2. 广场流与阅读 ---

        [HttpGet("stream")]
        public async Task<IActionResult> GetPublicStream([FromQuery] string? type = "wiki", [FromQuery] string? spaceId = null)
        {
            var query = PublicNotes();

            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(pn => pn.Type == type);
            }

            var stream = await query
                .OrderByDescending(pn => pn.PublishedAt)
                .Select(pn => new {
                    pn.Id,
                    pn.Title,
                    pn.Type,
                    pn.SpaceId,
                    pn.PublishedAt,
                    pn.Tags,
                    pn.ExtraData,
                    // ✨【性能修复点】：摒弃跨表子查询，直接读取本表已格式化好的摘要字段
                    Excerpt = pn.Excerpt ?? "灵脉深处暂无回响..."
                })
                .ToListAsync();

            return Ok(stream);
        }

        [HttpGet("public-stream")]
        public async Task<IActionResult> GetPublicStream(
            [FromQuery] string? type,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? tag = null)
        {
            var query = PublicNotes();

            if (string.IsNullOrEmpty(type)) //[cite: 1]
            {
                // 🌟 修复：把 "blog" 加上去，让它默认并流展示！
                query = query.Where(pn => pn.Type == "note" || pn.Type == NoteTypes.Post || pn.Type == "blog");
            }
            else //[cite: 1]
            {
                query = query.Where(pn => pn.Type == type); //[cite: 1]
            }

            if (!string.IsNullOrWhiteSpace(tag))
            {
                tag = tag.Trim();
                if (tag.Length > 50) return BadRequest(new { message = "分类标签不能超过 50 字" });
                // 使用发布时的标签快照，兼容 Unicode 转义和未转义的旧 JSON。
                var encoded = JsonSerializer.Serialize(tag);
                var literal = JsonSerializer.Serialize(tag, new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
                query = query.Where(pn => pn.Tags != null && (pn.Tags.Contains(encoded) || pn.Tags.Contains(literal) || pn.Tags == tag));
            }

            var safePageSize = Math.Clamp(pageSize, 1, 50);
            page = Math.Clamp(page, 1, int.MaxValue / safePageSize);
            var skipCount = (page - 1) * safePageSize;

            var stream = await query
                .OrderByDescending(pn => pn.PublishedAt)
                .Skip(skipCount)
                .Take(safePageSize)
                .Select(pn => new
                {
                    pn.Id,
                    pn.Title,
                    pn.Type,
                    pn.SpaceId,
                    pn.PublishedAt,
                    pn.Resonance,
                    pn.AuthorName,
                    pn.ExtraData,
                    // 🌟 评论数：评论锚定在发布内容的源草稿 Note 上。
                    //    comments.NoteId 与 PublishedNotes.OriginalNoteId 两列排序规则不同，
                    //    必须用 Collate 显式对齐，否则 MySQL 会抛「Illegal mix of collations」。
                    CommentsCount = _context.Comments.Count(c => c.NoteId != null
                        && EF.Functions.Collate(c.NoteId.ToString(), "utf8mb4_general_ci") == EF.Functions.Collate(pn.OriginalNoteId.ToString(), "utf8mb4_general_ci")
                        && c.Content != ""),
                    // ✨【核心兼性能修复点】：直接返回平铺好的本表 Excerpt，不要再去跨表查 PublishedBlocks！
                    Excerpt = pn.Excerpt ?? "灵脉深处暂无回响..."
                })
                .ToListAsync();

            return Ok(stream);
        }

        [HttpGet("published/{id:guid}")]
        public async Task<IActionResult> GetPublishedDetail(Guid id)
        {
            var publishedNote = await PublicNotes().FirstOrDefaultAsync(pn => pn.Id == id);

            if (publishedNote == null)
            {
                return NotFound(new { message = "该词条已进入虚空（未找到）" });
            }

            var blocks = await _context.PublishedBlocks
                .Where(pb => pb.OwnerId == id.ToString())
                .OrderBy(pb => pb.SortOrder)
                .ToListAsync();

            var content = new
            {
                type = "doc",
                content = blocks.Select(b => {
                    try
                    {
                        using var doc = JsonDocument.Parse(b.Data);
                        var root = doc.RootElement;

                        return (object)new
                        {
                            type = b.Type,
                            attrs = root.TryGetProperty("attrs", out var a) ? a.Clone() : (object)new { },
                            content = root.TryGetProperty("content", out var c) ? c.Clone() : (object?)null
                        };
                    }
                    catch
                    {
                        return new { type = "paragraph", content = new[] { new { type = "text", text = "碎片解析异常" } } };
                    }
                }).ToList()
            };

            return Ok(new
            {
                id = publishedNote.Id,
                title = publishedNote.Title,
                authorName = publishedNote.AuthorName,
                publishedAt = publishedNote.PublishedAt,
                tags = publishedNote.Tags,
                content = content
            });
        }

        private IQueryable<PublishedNote> PublicNotes() => _context.PublishedNotes.AsNoTracking().Where(p => p.OriginalNoteId == null || !_context.Notes.Any(n => n.Id.ToString() == EF.Functions.Collate(p.OriginalNoteId.ToString(), "utf8mb4_general_ci")) || _context.Notes.Any(n => n.Id.ToString() == EF.Functions.Collate(p.OriginalNoteId.ToString(), "utf8mb4_general_ci") && n.IsPublic && n.Status != 2));

        [HttpGet("blog/{id:guid}")]
        public async Task<IActionResult> GetPublicBlog(Guid id)
        {
            var publishedNote = await PublicNotes().FirstOrDefaultAsync(pn => pn.Id == id);

            if (publishedNote == null) return NotFound(new { message = "内容不存在" });

            var canEdit = publishedNote.OriginalNoteId != null && await _context.Notes.AnyAsync(n => n.Id == publishedNote.OriginalNoteId && _context.Spaces.Any(s => s.Id == n.SpaceId && s.UserId == CurrentUserId));
            // 在内存中先转化为标准格式的字符串（全小写），确保 SQL 查询能完美命中
            string ownerIdStr = id.ToString().ToLower();

            var blocksList = await _context.PublishedBlocks
                .AsNoTracking()
                .Where(pb => pb.OwnerId == ownerIdStr)
                .OrderBy(pb => pb.SortOrder)
                .Select(pb => new {
                    id = pb.Id,
                    type = pb.Type,
                    data = pb.Data,
                    sortOrder = pb.SortOrder
                })
                .ToListAsync();

            // 🌟 修复核心：去掉产生冲突的双属性，仅保留单个属性。
            // .NET 默认的 CamelCase 会自动将这里的 "blocks" 在网络传输时转换为小写 "blocks"
            return Ok(new
            {
                id = publishedNote.Id,
                title = publishedNote.Title,
                type = publishedNote.Type,
                publishedAt = publishedNote.PublishedAt,
                authorName = publishedNote.AuthorName, tags = publishedNote.Tags, canEdit, originalNoteId = canEdit ? publishedNote.OriginalNoteId : null,
                blocks = blocksList
            });
        }

        #endregion

        #region --- 3. 博客与帖子的评论区 ---

        [HttpGet("blog/{id:guid}/comments")]
        public Task<IActionResult> GetBlogComments(Guid id, [FromQuery] int offset = 0, [FromQuery] int limit = 20)
            => CommentResult(() => _comments.GetCommentsAsync(id, CurrentUserIdGuid, offset, limit));

        [Authorize, HttpPost("blog/{id:guid}/comments")]
        public Task<IActionResult> AddBlogComment(Guid id, [FromBody] CreateNoteCommentDto dto)
            => CurrentUserIdGuid is Guid userId ? CommentResult(() => _comments.AddCommentAsync(id, userId, dto))
                : Task.FromResult<IActionResult>(Unauthorized());

        [Authorize, HttpDelete("blog/{id:guid}/comments/{commentId:guid}")]
        public Task<IActionResult> DeleteBlogComment(Guid id, Guid commentId)
            => CurrentUserIdGuid is Guid userId ? CommentResult(() => _comments.DeleteCommentAsync(id, commentId, userId))
                : Task.FromResult<IActionResult>(Unauthorized());

        private async Task<IActionResult> CommentResult(Func<Task<object>> action)
        {
            try { return Ok(await action()); }
            catch (NoteCommentRequestException ex) { return StatusCode(ex.Status, new { message = ex.Message }); }
        }

        #endregion
    }
}