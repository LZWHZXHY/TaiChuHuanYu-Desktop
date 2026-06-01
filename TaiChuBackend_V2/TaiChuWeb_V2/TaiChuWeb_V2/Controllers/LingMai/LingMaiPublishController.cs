// TaiChuWeb_V2/Controllers/LingMai/LingMaiPublishController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
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
        [HttpGet("stream")]
        public async Task<IActionResult> GetPublicStream([FromQuery] string? type = "wiki", [FromQuery] string? spaceId = null)
        {
            var query = _context.PublishedNotes.AsNoTracking();

            if (!string.IsNullOrEmpty(type))
                query = query.Where(pn => pn.Type == type);

            // 1. 先转换
            Guid.TryParse(CurrentUserId, out Guid userIdGuid);

            // 2. 比较
            var dbUser = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userIdGuid); // Guid == Guid

            var stream = await query
                .OrderByDescending(pn => pn.PublishedAt)
                .Select(pn => new {
                    pn.Id,
                    pn.Title,
                    pn.Type,
                    pn.SpaceId,
                    pn.PublishedAt,
                    pn.Tags,
                    Excerpt = _context.PublishedBlocks
                        .Where(pb => pb.OwnerId == pn.Id.ToString() && pb.Type == "paragraph")
                        .OrderBy(pb => pb.SortOrder)
                        .Select(pb => pb.Data)
                        .FirstOrDefault() ?? "灵脉深处暂无回响..."
                })
                .ToListAsync();

            return Ok(stream);
        }
        #region --- 1. 发布与取消发布 ---

        // TaiChuWeb_V2/Controllers/LingMai/LingMaiPublishController.cs











        [HttpPost("notes/{id:guid}/publish")]
        public async Task<IActionResult> PublishNote([FromRoute] Guid id, [FromQuery] string type = "note")
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
                return await strategy.ExecuteAsync<IActionResult>(async () =>
                {
                    var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == id);
                    if (note == null) return NotFound(new { message = "未找到该草稿" });

                    var dbUser = await _context.Users.AsNoTracking()
                        .FirstOrDefaultAsync(u => u.Id == Guid.Parse(CurrentUserId));
                    var authorName = dbUser?.Username ?? "未知编织者";

                    var tagNames = await _context.TagAssignments
                        .Where(ta => ta.EntityId == id.ToString() && ta.EntityType == "note")
                        .Include(ta => ta.Tag)
                        .Select(ta => ta.Tag!.Name)
                        .ToListAsync();

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

                        publishedNote.Title = note.Title;
                        publishedNote.Type = type;
                        publishedNote.PublishedAt = DateTime.UtcNow;
                        publishedNote.AuthorName = authorName;
                        publishedNote.Tags = string.Join(",", tagNames);

                        // ================================================================
                        // 🌟🌟🌟 新方案：同时生成 steps 和 content
                        // ================================================================
                        var sortedBlocks = draftBlocks
                        .OrderBy(b => b.SortOrder) // 🌟 已经是 int 了，直接OrderBy，干干净净！
                        .ToList();

                        var steps = new List<dynamic>();
                        string currentImageUrl = null;

                        // 收集全部非空段落文本（用于 article 区全文展示）
                        var fullContent = new List<object>();

                        for (int i = 0; i < sortedBlocks.Count; i++)
                        {
                            var block = sortedBlocks[i];

                            if (block.Type == "image")
                            {
                                // 提取图片 src
                                string src = null;
                                try
                                {
                                    using var doc = System.Text.Json.JsonDocument.Parse(block.Data);
                                    if (doc.RootElement.TryGetProperty("attrs", out var attrs) &&
                                        attrs.TryGetProperty("src", out var s))
                                        src = s.GetString();
                                }
                                catch { }

                                if (!string.IsNullOrEmpty(src))
                                {
                                    // 若之前已有未配对的图片，作为无描述步骤存入
                                    if (currentImageUrl != null)
                                    {
                                        steps.Add(new
                                        {
                                            imageUrl = currentImageUrl,
                                            title = $"步骤 {steps.Count + 1}",
                                            description = ""
                                        });
                                    }
                                    currentImageUrl = src;
                                }
                            }
                            else if (block.Type == "paragraph")
                            {
                                // 提取段落完整文本
                                string text = "";
                                try
                                {
                                    using var doc = System.Text.Json.JsonDocument.Parse(block.Data);
                                    if (doc.RootElement.TryGetProperty("content", out var content) &&
                                        content.ValueKind == System.Text.Json.JsonValueKind.Array)
                                    {
                                        foreach (var item in content.EnumerateArray())
                                        {
                                            if (item.TryGetProperty("text", out var t))
                                                text += t.GetString();
                                        }
                                    }
                                }
                                catch { }

                                if (!string.IsNullOrWhiteSpace(text))
                                {
                                    // 收集到全局文本列表
                                    fullContent.Add(new { text = text, type = "text" });

                                    // 如果当前有待处理的图片，则进行配对
                                    if (currentImageUrl != null)
                                    {
                                        // 尝试提取阶段名（冒号前）
                                        string title = null;
                                        string description = text;
                                        int colonIndex = text.IndexOf('：'); // 中文冒号
                                        if (colonIndex == -1) colonIndex = text.IndexOf(':');
                                        if (colonIndex > 0)
                                        {
                                            title = text.Substring(0, colonIndex).Trim();
                                            description = text.Substring(colonIndex + 1).Trim();
                                        }
                                        else
                                        {
                                            title = $"步骤 {steps.Count + 1}";
                                        }

                                        steps.Add(new
                                        {
                                            imageUrl = currentImageUrl,
                                            title = title,
                                            description = description
                                        });
                                        currentImageUrl = null; // 配对完成
                                    }
                                }
                            }
                        }

                        // 处理最后一张未配对的图片
                        if (currentImageUrl != null)
                        {
                            steps.Add(new
                            {
                                imageUrl = currentImageUrl,
                                title = $"步骤 {steps.Count + 1}",
                                description = ""
                            });
                        }

                        // 序列化为包含 steps 和 content 的 JSON
                        var descriptionObj = new
                        {
                            steps = steps,
                            content = fullContent
                        };
                        string descriptionJson = System.Text.Json.JsonSerializer.Serialize(descriptionObj);
                        publishedNote.Excerpt = descriptionJson;
                        // ================================================================
                        // 结束核心修改
                        // ================================================================

                        await _context.SaveChangesAsync();

                        // 同步 PublishedBlocks 快照（保持不变）
                        var oldPubBlocks = await _context.PublishedBlocks
                            .Where(pb => pb.OwnerId == publishedNote.Id.ToString() && pb.OwnerType == "note")
                            .ToListAsync();
                        _context.PublishedBlocks.RemoveRange(oldPubBlocks);

                        var pubBlocks = sortedBlocks.Select(db => new PublishedBlock
                        {
                            Id = Guid.NewGuid(),
                            OwnerId = publishedNote.Id.ToString(),
                            OwnerType = "note",
                            Type = db.Type,
                            Data = db.Data,
                            SortOrder = db.SortOrder,
                        }).ToList();
                        _context.PublishedBlocks.AddRange(pubBlocks);

                        note.IsPublic = true;
                        note.Type = type;
                        note.UpdatedAt = DateTime.UtcNow;

                        // 画廊桥接（art 类型）
                        if (type == "art")
                        {
                            var uploaderId = Guid.Parse(CurrentUserId);

                            var existingArtwork = await _context.Artworks
                                .Include(a => a.Images)
                                .FirstOrDefaultAsync(a => a.OriginalNoteId == id
                                    || (a.Title == note.Title && a.UploaderId == uploaderId));

                            // 提取所有图片 URL（保持顺序）
                            var imageUrls = steps.Select(s => (string)s.imageUrl).ToList();

                            if (imageUrls.Any())
                            {
                                if (existingArtwork == null)
                                {
                                    var newArtwork = new TaiChuWeb_V2.Models.Artwork.Artwork
                                    {
                                        OriginalNoteId = id,
                                        Title = note.Title,
                                        Description = descriptionJson,  // 现在包含 steps + content
                                        UploaderId = uploaderId,
                                        UploadAt = DateTime.UtcNow,
                                        Images = imageUrls.Select((url, index) => new TaiChuWeb_V2.Models.Artwork.ArtworkImage
                                        {
                                            ImageUrl = url,
                                            IsCover = index == 0
                                        }).ToList()
                                    };
                                    _context.Artworks.Add(newArtwork);
                                }
                                else
                                {
                                    existingArtwork.OriginalNoteId = id;
                                    existingArtwork.Title = note.Title;
                                    existingArtwork.Description = descriptionJson;
                                    existingArtwork.UploadAt = DateTime.UtcNow;

                                    if (existingArtwork.Images != null && existingArtwork.Images.Any())
                                        _context.RemoveRange(existingArtwork.Images.AsEnumerable());

                                    existingArtwork.Images = imageUrls.Select((url, index) => new TaiChuWeb_V2.Models.Artwork.ArtworkImage
                                    {
                                        ImageUrl = url,
                                        IsCover = index == 0
                                    }).ToList();
                                }
                            }
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        return Ok(new { success = true, publishedNoteId = publishedNote.Id });
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
                return StatusCode(500, new { message = $"发布失败: {ex.Message}" });
            }
        }























        // LingMaiPublishController.cs

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

            // 🌟 核心修复：重新编织 Tiptap 文档树，平铺 attrs 和 content
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
                            // 提取 attrs，如果没有则给空对象
                            attrs = root.TryGetProperty("attrs", out var a) ? a.Clone() : (object)new { },
                            // 提取 content，如果没有则不返回该字段
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



        [HttpDelete("notes/{id:guid}/unpublish")]
        public async Task<IActionResult> UnpublishNote(Guid id)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
                // 🌟 加上 <IActionResult> 泛型
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

        #region --- 2. 广场与阅读 ---



        [HttpGet("public-stream")]
        public async Task<IActionResult> GetPublicStream(
            [FromQuery] string? type,       // 🌟 配合前端改成单选类型 (note 或 thought)
            [FromQuery] int page = 1,       // 🌟 新增：当前页码，默认第一页
            [FromQuery] int pageSize = 20)  // 🌟 新增：每页数量，默认 20 条
        {
            var query = _context.PublishedNotes.AsNoTracking();

            // 1. 类型过滤
            if (string.IsNullOrEmpty(type))
            {
                // 如果前端传了 'all' (undefined/null)，默认只出长文和短动态，屏蔽百科或角色档案
                query = query.Where(pn => pn.Type == "note" || pn.Type == "thought");
            }
            else
            {
                // 过滤指定的类型
                query = query.Where(pn => pn.Type == type);
            }

            // 2. 分页安全计算
            // 防止前端恶意传一个巨大的 pageSize (比如 10000) 导致数据库 OOM
            var safePageSize = pageSize > 50 ? 50 : pageSize;
            var skipCount = (page - 1) * safePageSize;

            // 3. 执行分页查询 (Skip + Take)
            var stream = await query
                .OrderByDescending(pn => pn.PublishedAt)
                .Skip(skipCount)               // 🌟 核心：跳过前面的数据
                .Take(safePageSize)            // 🌟 核心：只抓取当前页的数据
                .Select(pn => new
                {
                    pn.Id,
                    pn.Title,
                    pn.Type,
                    pn.SpaceId,
                    pn.PublishedAt,
                    pn.Resonance,
                    pn.AuthorName, // 🌟 记得把这行加上！前端卡片需要展示作者名字
                    Excerpt = _context.PublishedBlocks
                        .Where(pb => pb.OwnerId == pn.Id.ToString() && pb.OwnerType == "note" && pb.Type == "paragraph")
                        .OrderBy(pb => pb.SortOrder)
                        .Select(pb => pb.Data)
                        .FirstOrDefault()
                })
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