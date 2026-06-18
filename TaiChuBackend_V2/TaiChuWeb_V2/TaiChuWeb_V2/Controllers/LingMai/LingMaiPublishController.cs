// TaiChuWeb_V2/Controllers/LingMai/LingMaiPublishController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.LingMai;
using TaiChuWeb_V2.Models.LingMai;
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

        // 获取当前登录用户 ID
        private string? CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        // 🌟 通过构造函数依赖注入所有的处理器，并自动编排为路由映射字典
        public LingMaiPublishController(AppDbContext context, IEnumerable<ILingMaiPublishHandler> handlers)
        {
            _context = context;
            _publishHandlers = handlers.ToDictionary(h => h.SupportType, h => h);
        }

        [HttpPost("notes/{id:guid}/publish")]
        public async Task<IActionResult> PublishNote([FromRoute] Guid id, [FromBody] PublishRequest req)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            if (req.type == "note")
            {
                return BadRequest(new { message = "普通随笔碎片属于私密草稿，无法直接发布" });
            }

            // 🌟 纯粹的多态派发：前端发布面板传来什么 type (例如 "post" 或 "blog")，大厅直接交由对应的 Handler 处理
            if (!_publishHandlers.TryGetValue(req.type, out var handler))
            {
                return BadRequest(new { message = $"暂未编织该多态碎片形态 [{req.type}] 的分流大厅逻辑" });
            }

            return await handler.ExecutePublishAsync(id, CurrentUserId, req.categoryId);
        }

        #region --- 1. 取消发布 ---

        [HttpDelete("notes/{id:guid}/unpublish")]
        public async Task<IActionResult> UnpublishNote(Guid id)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

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

        #region --- 2. 广场流与阅读 ---

        [HttpGet("stream")]
        public async Task<IActionResult> GetPublicStream([FromQuery] string? type = "wiki", [FromQuery] string? spaceId = null)
        {
            var query = _context.PublishedNotes.AsNoTracking();

            if (!string.IsNullOrEmpty(type))
            {
                query = query.Where(pn => pn.Type == type);
            }

            Guid.TryParse(CurrentUserId, out Guid userIdGuid);

            var dbUser = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userIdGuid);

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
            [FromQuery] int pageSize = 20)
        {
            var query = _context.PublishedNotes.AsNoTracking();

            if (string.IsNullOrEmpty(type)) //[cite: 1]
            {
                // 🌟 修复：把 "blog" 加上去，让它默认并流展示！
                query = query.Where(pn => pn.Type == "note" || pn.Type == NoteTypes.Post || pn.Type == "blog");
            }
            else //[cite: 1]
            {
                query = query.Where(pn => pn.Type == type); //[cite: 1]
            }

            var safePageSize = pageSize > 50 ? 50 : pageSize;
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
                    // ✨【核心兼性能修复点】：直接返回平铺好的本表 Excerpt，不要再去跨表查 PublishedBlocks！
                    Excerpt = pn.Excerpt ?? "灵脉深处暂无回响..."
                })
                .ToListAsync();

            return Ok(stream);
        }

        [HttpGet("published/{id:guid}")]
        public async Task<IActionResult> GetPublishedDetail(Guid id)
        {
            var publishedNote = await _context.PublishedNotes
                .FirstOrDefaultAsync(pn => pn.Id == id);

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

        [HttpGet("blog/{id:guid}")]
        public async Task<IActionResult> GetPublicBlog(Guid id)
        {
            var publishedNote = await _context.PublishedNotes
                .AsNoTracking()
                .FirstOrDefaultAsync(pn => pn.Id == id);

            if (publishedNote == null) return NotFound(new { message = "内容不存在" });

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
                blocks = blocksList
            });
        }

        #endregion
    }
}