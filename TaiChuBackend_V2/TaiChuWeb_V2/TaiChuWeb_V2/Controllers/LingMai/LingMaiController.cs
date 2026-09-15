// TaiChuWeb_V2/Controllers/LingMai/LingMaiController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.LingMai;
using TaiChuWeb_V2.Models.FinalNodes;   // 🌟 新增：BlockIndex 所在命名空间
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Models.Tag;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services.LingMai;

namespace TaiChuWeb_V2.Controllers.LingMai
{
    [ApiController]
    [Route("api/[controller]")]
    public class LingMaiController : ControllerBase
    {
        private readonly LingMaiService _lingMaiService;
        private readonly AppDbContext _context;
        private string? CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        public LingMaiController(LingMaiService lingMaiService, AppDbContext context)
        {
            _lingMaiService = lingMaiService;
            _context = context;
        }

        #region --- 1. 空间管理 ---

        [HttpGet("spaces")]
        public async Task<IActionResult> GetSpaces()
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var spaces = await _context.Spaces
                .Where(s => s.UserId == CurrentUserId)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => new { s.Id, s.Name, s.UserId, s.CreatedAt })
                .ToListAsync();

            return Ok(spaces);
        }




        [HttpPatch("notes/{id}/meta")]
        public async Task<IActionResult> UpdateNoteMeta(Guid id, [FromBody] System.Text.Json.JsonElement updates)
        {
            // 1. 寻找对应的灵脉碎片
            var note = await _context.Notes.FindAsync(id);
            if (note == null) return NotFound(new { message = "未找到该碎片" });

            // 2. 动态感应并更新元数据
            // 更新位面归属
            if (updates.TryGetProperty("spaceId", out var spaceIdProp))
            {
                if (Guid.TryParse(spaceIdProp.GetString(), out var newSpaceId))
                {
                    note.SpaceId = newSpaceId;
                    note.FolderId = null;   // 🌟 迁移时清空 folderId
                }
            }

            // 更新侧边栏显示状态
            if (updates.TryGetProperty("showInSidebar", out var sidebarProp))
            {
                note.ShowInSidebar = sidebarProp.GetBoolean();
            }

            // 更新私密/公开状态
            if (updates.TryGetProperty("isPrivate", out var privateProp))
            {
                note.IsPrivate = privateProp.GetBoolean();
            }

            // 🌟【核心修复点】：更新类型并同步迁移底层 Blocks 数据的 OwnerType
            if (updates.TryGetProperty("type", out var typeProp))
            {
                var newType = typeProp.GetString();
                if (!string.IsNullOrEmpty(newType) && note.Type != newType)
                {
                    string oldType = note.Type; // 记录旧的类型 (例如 "note")

                    // 找到所有旧形态名下的 Blocks
                    var relatedBlocks = await _context.Blocks
                        .Where(b => b.OwnerId == id.ToString() && b.OwnerType == oldType)
                        .ToListAsync();

                    // 批量将它们的从属标识迁移到新形态下 (例如改为 "doc")
                    foreach (var block in relatedBlocks)
                    {
                        block.OwnerType = newType;
                    }

                    // 更新笔记主体的类型
                    note.Type = newType;
                }
            }

            note.UpdatedAt = DateTime.UtcNow;

            try
            {
                await _context.SaveChangesAsync();
                return Ok(new { success = true, message = "元数据感应同步成功" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "同步失败", error = ex.Message });
            }
        }




        [HttpPost("spaces")]
        public async Task<IActionResult> CreateSpace([FromBody] CreateSpaceDto dto)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            // 1. 获取配额状态和 stats 对象
            var (isOverSpace, _, stats) = await GetQuotaStatus(CurrentUserId);

            // 检查上限
            if (await _context.Spaces.CountAsync(s => s.UserId == CurrentUserId) >= stats.MaxSpaces)
            {
                return StatusCode(403, new { message = "空间数量已达上限，请前往交易行购买扩展卡。" });
            }

            if (dto == null || string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("空间名称不能为空");

            // 2. 创建空间实体
            var space = new Space
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                UserId = CurrentUserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Spaces.Add(space);

            // 🌟 核心修复：同步更新 UserStats 表中的计数器
            stats.UsedSpaces++;
            _context.Entry(stats).State = EntityState.Modified;

            // 3. 统一保存更改
            await _context.SaveChangesAsync();

            return Ok(new { id = space.Id, name = space.Name });
        }

        [HttpDelete("spaces/{id:guid}")]
        public async Task<IActionResult> DeleteSpace(Guid id)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var space = await _context.Spaces.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
            if (space == null) return NotFound(new { message = "未找到指定的空间" });
            if (space.UserId != CurrentUserId) return Forbid();

            // 该空间下所有笔记的 ID
            var noteIds = await _context.Notes
                .AsNoTracking()
                .Where(n => n.SpaceId == id)
                .Select(n => n.Id)
                .ToListAsync();

            var noteIdStrs = noteIds.Select(x => x.ToString()).ToList();
            int notesCount = noteIds.Count;

            var user = await _context.Users
                .Include(u => u.Stats)
                .FirstOrDefaultAsync(u => u.Id == Guid.Parse(CurrentUserId));
            if (user?.Stats == null) return BadRequest("无法更新账户审计数据");

            // 🌟 用 ExecutionStrategy 包裹整个事务
            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
                return await strategy.ExecuteAsync<IActionResult>(async () =>
                {
                    using var transaction = await _context.Database.BeginTransactionAsync();

                    try
                    {
                        if (noteIds.Count > 0)
                        {
                            // 全部用 ExecuteDeleteAsync，绕过 EF 跟踪
                            await _context.NoteLinks
                                .Where(l => noteIds.Contains(l.SourceNoteId) || noteIds.Contains(l.TargetNoteId))
                                .ExecuteDeleteAsync();

                            await _context.NoteHistories
                                .Where(h => noteIds.Contains(h.NoteId))
                                .ExecuteDeleteAsync();

                            await _context.Comments
                                .Where(c => c.NoteId != null && noteIds.Contains(c.NoteId.Value))
                                .ExecuteDeleteAsync();

                            await _context.Blocks
                                .Where(b => noteIdStrs.Contains(b.OwnerId))
                                .ExecuteDeleteAsync();

                            await _context.BlockIndexes
                                .Where(b => noteIdStrs.Contains(b.NodeId))
                                .ExecuteDeleteAsync();

                            await _context.TagAssignments
                                .Where(ta => ta.EntityType == "Note" && noteIdStrs.Contains(ta.EntityId))
                                .ExecuteDeleteAsync();
                        }

                        // PublishedNotes / PublishedBlocks
                        var pubIds = await _context.PublishedNotes
                            .AsNoTracking()
                            .Where(pn => pn.SpaceId == id)
                            .Select(pn => pn.Id.ToString())
                            .ToListAsync();

                        if (pubIds.Count > 0)
                        {
                            await _context.PublishedBlocks
                                .Where(pb => pubIds.Contains(pb.OwnerId))
                                .ExecuteDeleteAsync();

                            await _context.PublishedNotes
                                .Where(pn => pn.SpaceId == id)
                                .ExecuteDeleteAsync();
                        }

                        // 删笔记
                        await _context.Notes
                            .Where(n => n.SpaceId == id)
                            .ExecuteDeleteAsync();

                        // 删空间
                        await _context.Spaces
                            .Where(s => s.Id == id)
                            .ExecuteDeleteAsync();

                        // 更新计数
                        user.Stats.UsedSpaces = Math.Max(0, user.Stats.UsedSpaces - 1);
                        user.Stats.UsedNotes = Math.Max(0, user.Stats.UsedNotes - notesCount);
                        await _context.SaveChangesAsync();

                        await transaction.CommitAsync();
                        return Ok(new { success = true, deletedNotes = notesCount });
                    }
                    catch (Exception ex)
                    {
                        await transaction.RollbackAsync();
                        var inner = ex.InnerException?.Message ?? "(无内部异常)";
                        return StatusCode(500, $"删除失败: {ex.Message} || 内部: {inner}");
                    }
                });
            }
            catch (Exception ex)
            {
                var inner = ex.InnerException?.Message ?? "(无内部异常)";
                return StatusCode(500, $"删除失败(重试后): {ex.Message} || 内部: {inner}");
            }
        }





















        [HttpPatch("spaces/{id:guid}")]
        public async Task<IActionResult> UpdateSpaceName(Guid id, [FromBody] string name)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();
            if (string.IsNullOrWhiteSpace(name)) return BadRequest("空间名称不能为空");

            var space = await _context.Spaces.FindAsync(id);
            if (space == null) return NotFound(new { message = "未找到指定的空间" });
            if (space.UserId != CurrentUserId) return Forbid();

            space.Name = name;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        #endregion

        #region --- 2. 文件夹管理 (🌟 补齐之前漏掉的 API) ---
        #region --- 2. 文件夹管理 ---

        [HttpPost("folders")]
        public async Task<IActionResult> CreateFolder([FromBody] CreateFolderDto dto)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();
            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == dto.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            var folder = new Note
            {
                Id = Guid.NewGuid(),
                SpaceId = dto.SpaceId,
                FolderId = null, // 🌟 文件夹本身的 FolderId 为 null
                Type = "folder",
                Title = dto.Name,
                ShowInSidebar = true, // 确保在侧边栏显示
                IsPublic = false,
                Status = 0,
                SortOrder = DateTime.UtcNow.Ticks.ToString(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Notes.Add(folder);
            await _context.SaveChangesAsync();
            return Ok(new { success = true, id = folder.Id, title = folder.Title });
        }

        [HttpPatch("folders/{id:guid}")]
        public async Task<IActionResult> UpdateFolder(Guid id, [FromBody] string name)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();
            var folder = await _context.Notes.FindAsync(id);
            if (folder == null || folder.Type != "folder") return NotFound("未找到该文件夹");

            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == folder.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            folder.Title = name;
            folder.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        #endregion

        #endregion

        #region --- 3. 笔记管理与双链解析同步 ---

        [HttpGet("all")]
        public async Task<IActionResult> GetAllNotes([FromQuery] Guid? spaceId)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();
            if (spaceId == null || spaceId == Guid.Empty) return BadRequest("必须指定空间 ID");

            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == spaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            // 1. 拉取所有活跃的 Notes，包含 BlocksData (为画布准备)
            var notes = await _context.Notes
                .Where(n => n.SpaceId == spaceId && n.Status == (int)NoteStatus.Active)
                .OrderByDescending(n => n.UpdatedAt)
                .Select(n => new {
                    n.Id,
                    n.Title,
                    n.SpaceId,
                    n.FolderId,
                    n.Type,
                    n.IsPublic,
                    n.ShowInSidebar,
                    n.SortOrder,
                    n.CreatedAt,
                    n.UpdatedAt,
                    n.Tags,
                    n.ExtraData,
                    n.BlocksData // 🌟 拉取画布专属数据
                })
                .ToListAsync();

            // 2. 仅提取“普通笔记”的 ID
            var normalNoteIds = notes
                .Where(n => n.Type != NoteTypes.Canvas && n.Type != NoteTypes.Map)
                .Select(n => n.Id.ToString())
                .ToList();

            // 3. 批量查询普通笔记的 Blocks (🌟 完美解决 CS0173 报错：统一返回 List<Block>)
            var allBlocks = normalNoteIds.Any()
                ? await _context.Blocks
                    .Where(b => normalNoteIds.Contains(b.OwnerId))
                    .ToListAsync()
                : new List<Block>();

            // 4. 在内存中智能分流拼装数据
            var result = notes.Select(n =>
            {
                // --- 标签平滑兼容解析 ---
                string[] tagsArray = Array.Empty<string>();
                if (!string.IsNullOrWhiteSpace(n.Tags))
                {
                    try
                    {
                        tagsArray = JsonSerializer.Deserialize<string[]>(n.Tags) ?? Array.Empty<string>();
                    }
                    catch (System.Text.Json.JsonException)
                    {
                        tagsArray = n.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                          .Select(t => t.Trim())
                                          .ToArray();
                    }
                }

                // --- 万能块双轨分流处理 ---
                object finalBlocks;
                if (n.Type == NoteTypes.Canvas || n.Type == NoteTypes.Map)
                {
                    // 路线 A：画布直接反序列化 JSON
                    if (!string.IsNullOrWhiteSpace(n.BlocksData))
                    {
                        try
                        {
                            finalBlocks = JsonSerializer.Deserialize<System.Text.Json.JsonElement>(n.BlocksData);
                        }
                        catch
                        {
                            finalBlocks = Array.Empty<object>();
                        }
                    }
                    else
                    {
                        finalBlocks = Array.Empty<object>();
                    }
                }
                else
                {
                    // 路线 B：普通笔记在内存匹配，并转换为前端所需的匿名格式
                    finalBlocks = allBlocks
                        .Where(b => b.OwnerId == n.Id.ToString())
                        .OrderBy(b => b.SortOrder)
                        .Select(b => new
                        {
                            id = b.Id,
                            type = b.Type,
                            data = b.Data,
                            sortOrder = b.SortOrder
                        })
                        .ToList();
                }

                return new
                {
                    n.Id,
                    n.Title,
                    n.SpaceId,
                    n.FolderId,
                    n.Type,
                    n.IsPublic,
                    n.ShowInSidebar,
                    n.SortOrder,
                    n.CreatedAt,
                    n.UpdatedAt,
                    tags = tagsArray,
                    extraData = n.ExtraData,
                    blocks = finalBlocks // 🌟 最终无缝拼装
                };
            });

            return Ok(result);
        }



        // 🌟 恢复动作：从归档库放回侧边栏
        [HttpPatch("notes/{id:guid}/restore")]
        public async Task<IActionResult> RestoreNote(Guid id)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var note = await _context.Notes.FindAsync(id);
            if (note == null) return NotFound();

            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            note.Status = (int)NoteStatus.Active;   // 设为 0
            note.ShowInSidebar = true;
            note.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = "内容已回归活跃视界" });
        }






        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetNoteById(Guid id)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            // 1. 获取核心 Note 实体
            var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == id);
            if (note == null) return NotFound(new { message = "该笔记不存在" });

            // 2. 权限校验
            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            // ==========================================
            // 🌟 核心修复 1：分流读取万能块数据
            // ==========================================
            object finalBlocks; // 声明一个 object 用来动态承载两种不同来源的数据

            if (note.Type == NoteTypes.Canvas || note.Type == NoteTypes.Map)
            {
                // 🚀 路线 A：如果是白板或地图，直接读取 BlocksData 字段
                if (!string.IsNullOrWhiteSpace(note.BlocksData))
                {
                    try
                    {
                        // 反序列化为 JsonElement，这样在最后 Ok() 返回时，ASP.NET 框架会正确把它渲染成 JSON 数组，而不是带转义的字符串
                        finalBlocks = JsonSerializer.Deserialize<System.Text.Json.JsonElement>(note.BlocksData);
                    }
                    catch
                    {
                        finalBlocks = Array.Empty<object>(); // 解析失败的兜底
                    }
                }
                else
                {
                    finalBlocks = Array.Empty<object>();
                }
            }
            else
            {
                // 🛣️ 路线 B：如果是普通笔记或 Wiki，继续从 Blocks 表查询
                finalBlocks = await _context.Blocks
                    .Where(b => b.OwnerId == id.ToString() && b.OwnerType == note.Type)
                    .OrderBy(b => b.SortOrder) // 保证前端渲染顺序
                    .Select(b => new
                    {
                        id = b.Id,
                        type = b.Type,
                        data = b.Data,
                        sortOrder = b.SortOrder
                    })
                    .ToListAsync();
            }

            // ==========================================
            // 🌟 核心修复 2：JSON 反序列化解析 Tags (保持不变)
            // ==========================================
            string[] tagsArray = Array.Empty<string>();
            if (!string.IsNullOrWhiteSpace(note.Tags))
            {
                try
                {
                    tagsArray = JsonSerializer.Deserialize<string[]>(note.Tags) ?? Array.Empty<string>();
                }
                catch (System.Text.Json.JsonException)
                {
                    tagsArray = note.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                         .Select(t => t.Trim())
                                         .ToArray();
                }
            }

            // 3. 组装多态数据返回给前端
            return Ok(new
            {
                id = note.Id,
                title = note.Title,
                spaceId = note.SpaceId,
                folderId = note.FolderId,
                type = note.Type,
                isPublic = note.IsPublic,
                extraData = note.ExtraData,
                tags = tagsArray,
                showInSidebar = note.ShowInSidebar,
                sortOrder = note.SortOrder,
                createdAt = note.CreatedAt,
                updatedAt = note.UpdatedAt,
                blocks = finalBlocks          // 🌟 无缝塞入刚才分流获取的块数据
            });
        }











        [HttpPost("notes")]
        public async Task<IActionResult> CreateNote([FromBody] CreateNoteDto dto)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            // 1. 获取当前用户的统计数据
            var stats = await _context.UserStats.FirstOrDefaultAsync(s => s.UserId == Guid.Parse(CurrentUserId));
            if (stats == null) return BadRequest("无法感应账户审计数据");

            // 🌟 优化：直接从 stats 字段判断，而不是去 Notes 表里重算，提高灵脉响应速度
            if (stats.UsedNotes >= stats.MaxNotes)
            {
                return StatusCode(403, new { message = "灵脉节点已满，请前往交易行扩展容量。" });
            }

            // 2. 权限校验
            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == dto.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            // 3. 创建实体
            var note = new Note
            {
                Id = Guid.NewGuid(),
                SpaceId = dto.SpaceId,
                FolderId = dto.FolderId,
                Type = dto.Type,
                Title = dto.Title,
                IsPublic = false,

                // ✅ 修复：调用你写好的多态方法，根据类型自动判断是否显示在侧边栏
                ShowInSidebar = NoteTypes.ShouldShowInSidebarByDefault(dto.Type),

                SortOrder = dto.SortOrder ?? DateTime.UtcNow.Ticks.ToString(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Notes.Add(note);

            // 🌟 核心修复：同步增加 UsedNotes 计数器
            stats.UsedNotes++;
            _context.Entry(stats).State = EntityState.Modified; // 强制标记为已修改

            await _context.SaveChangesAsync();

            return Ok(new { success = true, id = note.Id, type = note.Type });
        }

        [HttpPatch("notes/{id:guid}")]
        public async Task<IActionResult> UpdateNoteTitle(Guid id, [FromBody] string title)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var note = await _context.Notes.FindAsync(id);
            if (note == null) return NotFound(new { message = "未找到该碎片" });

            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            note.Title = title;
            note.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { success = true, title = note.Title });
        }

        [HttpDelete("notes/{id:guid}")]
        public async Task<IActionResult> DeleteNote(Guid id)
        {
            // 1. 仅查询 Note 本身（去掉会报错的 Include）
            var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == id);
            if (note == null) return NotFound();

            // 2. 🌟 核心修改：手动查询关联的多态 Blocks (将 Guid 转为 string 进行匹配)
            var noteIdStr = id.ToString();
            var relatedBlocks = await _context.Blocks
                .Where(b => b.OwnerId == noteIdStr)
                .ToListAsync();

            // 如果有 Blocks，将其标记为删除
            if (relatedBlocks.Any())
            {
                _context.Blocks.RemoveRange(relatedBlocks);
            }

            // 3. 删除关联的星图连线 (建议加上 ToListAsync，避免在执行 SaveChanges 前触发并发读写限制)
            var links = await _context.NoteLinks
                .Where(l => l.TargetNoteId == id || l.SourceNoteId == id)
                .ToListAsync();

            if (links.Any())
            {
                _context.NoteLinks.RemoveRange(links);
            }

            // 4. 物理删除：从数据库中彻底移除 Note 本身
            _context.Notes.Remove(note);

            // 5. 更新配额（如果配额统计表存在）
            // 注意：确保 CurrentUserId 确实存在并且是合法的 Guid 格式
            if (!string.IsNullOrEmpty(CurrentUserId) && Guid.TryParse(CurrentUserId, out Guid parsedUserId))
            {
                var stats = await _context.UserStats.FirstOrDefaultAsync(s => s.UserId == parsedUserId);
                if (stats != null)
                {
                    stats.UsedNotes = Math.Max(0, stats.UsedNotes - 1);
                }
            }

            // 6. 统一提交到数据库（EF Core 会在一个隐式事务中安全地执行上述所有 Delete 操作）
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "碎片及其关联的所有块和星图连线已永久粉碎" });
        }



        [HttpPatch("notes/{id:guid}/move")]
        public async Task<IActionResult> MoveNote(Guid id, [FromBody] MoveNoteDto dto)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var note = await _context.Notes.FindAsync(id);
            if (note == null) return NotFound(new { message = "未找到该碎片" });

            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            note.FolderId = dto.FolderId;
            note.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { success = true });
        }

        #endregion

        #region --- 4. 实时数据同步与自动提取双链 (🌟 修复双链丢失) ---

        [HttpPost("sync")]
        public async Task<IActionResult> SyncNote([FromBody] NoteSyncDto dto)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var (isOverSpace, isOverNote, _) = await GetQuotaStatus(CurrentUserId);
            if (isOverSpace || isOverNote)
            {
                return StatusCode(423, new { message = "灵脉空间已淤积，编辑功能已锁定。" });
            }

            var strategy = _context.Database.CreateExecutionStrategy();

            try
            {
                return await strategy.ExecuteAsync<IActionResult>(async () =>
                {
                    var note = await _context.Notes.FindAsync(dto.NoteId);
                    if (note == null) return NotFound(new { message = "未找到对应的笔记" });

                    var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId);
                    if (!isOwner) return Forbid();

                    using var transaction = await _context.Database.BeginTransactionAsync();
                    try
                    {
                        // 1. 更新基本元数据
                        if (!string.IsNullOrEmpty(dto.Title)) note.Title = dto.Title;
                        if (dto.ExtraData != null) note.ExtraData = dto.ExtraData;
                        note.UpdatedAt = DateTime.UtcNow;

                        // 准备收集所有的双链 ID
                        var currentOutlinkIds = new HashSet<Guid>();

                        // ========================================================================
                        // 分流处理 blocks
                        // ========================================================================
                        bool isCanvasOrMap = note.Type == "canvas" || note.Type == "map";
                        bool isSchedule = note.Type == "schedule";

                        if (isCanvasOrMap)
                        {
                            // 路线 A：白板/地图 -> 大 JSON 存储
                            var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
                            string blocksJson = dto.Blocks != null && dto.Blocks.Any()
                                ? JsonSerializer.Serialize(dto.Blocks, jsonOptions)
                                : "[]";
                            note.BlocksData = blocksJson;

                            if (blocksJson.Length > 2)
                            {
                                var matches = Regex.Matches(blocksJson, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
                                foreach (Match match in matches)
                                {
                                    if (Guid.TryParse(match.Value, out Guid linkedId) && linkedId != dto.NoteId)
                                        currentOutlinkIds.Add(linkedId);
                                }
                            }

                            var obsoleteBlocks = await _context.Blocks.Where(b => b.OwnerId == dto.NoteId.ToString()).ToListAsync();
                            if (obsoleteBlocks.Any()) _context.Blocks.RemoveRange(obsoleteBlocks);
                        }
                        else if (isSchedule)
                        {
                            // 路线 C：Schedule，物理抹除 + 干净落库
                            var noteIdStr = dto.NoteId.ToString();
                            var oldBlocks = await _context.Blocks.Where(b => b.OwnerId == noteIdStr).ToListAsync();
                            if (oldBlocks.Any())
                            {
                                _context.Blocks.RemoveRange(oldBlocks);
                            }

                            if (dto.Blocks != null && dto.Blocks.Any())
                            {
                                foreach (var (b, index) in dto.Blocks.Select((item, i) => (item, i)))
                                {
                                    _context.Blocks.Add(new Block
                                    {
                                        Id = b.Id,
                                        OwnerId = noteIdStr,
                                        OwnerType = "schedule",
                                        Type = b.Type,
                                        Data = b.Data ?? string.Empty,
                                        SortOrder = b.SortOrder ?? index,
                                        UpdatedAt = DateTime.UtcNow
                                    });

                                    if (!string.IsNullOrWhiteSpace(b.Data))
                                    {
                                        var matches = Regex.Matches(b.Data, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
                                        foreach (Match match in matches)
                                        {
                                            if (Guid.TryParse(match.Value, out Guid linkedId) && linkedId != dto.NoteId)
                                                currentOutlinkIds.Add(linkedId);
                                        }
                                    }
                                }
                            }
                        }
                        else
                        {
                            // ================================================================
                            // 🛣️ 路线 B（重写）：普通笔记也改成「全删全建」
                            // 用 ExecuteDeleteAsync 绕过 ChangeTracker，直接发 SQL DELETE
                            // 从根上杜绝与后续 INSERT 撞主键（包括并发、复制、中间插入等场景）
                            // ================================================================
                            await _context.Blocks
                                .Where(b => b.OwnerId == dto.NoteId.ToString())
                                .ExecuteDeleteAsync();

                            if (dto.Blocks != null && dto.Blocks.Any())
                            {
                                var seenIds = new HashSet<string>();   // 请求内去重
                                int blockOrder = 0;

                                foreach (var b in dto.Blocks)
                                {
                                    if (string.IsNullOrEmpty(b.Id) || !seenIds.Add(b.Id)) continue;

                                    _context.Blocks.Add(new Block
                                    {
                                        Id = b.Id,
                                        OwnerId = dto.NoteId.ToString(),
                                        OwnerType = note.Type,
                                        Type = b.Type,
                                        Data = b.Data ?? string.Empty,
                                        SortOrder = b.SortOrder ?? blockOrder,
                                        UpdatedAt = DateTime.UtcNow
                                    });
                                    blockOrder++;

                                    // 顺手解析双链
                                    if (!string.IsNullOrWhiteSpace(b.Data))
                                    {
                                        var matches = Regex.Matches(b.Data, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
                                        foreach (Match match in matches)
                                        {
                                            if (Guid.TryParse(match.Value, out Guid linkedId) && linkedId != dto.NoteId)
                                                currentOutlinkIds.Add(linkedId);
                                        }
                                    }
                                }
                            }
                        }

                        // ========================================================================
                        // 3. 增量更新 NoteLinks 表 (双链) 与标签系统
                        // ========================================================================
                        var existingLinks = await _context.NoteLinks
                            .Where(nl => nl.SourceNoteId == note.Id)
                            .ToListAsync();

                        var existingTargetIds = existingLinks.Select(nl => nl.TargetNoteId).ToHashSet();
                        var linksToRemove = existingLinks.Where(nl => !currentOutlinkIds.Contains(nl.TargetNoteId)).ToList();
                        if (linksToRemove.Any()) _context.NoteLinks.RemoveRange(linksToRemove);

                        var targetIdsToAdd = currentOutlinkIds.Where(id => !existingTargetIds.Contains(id)).ToList();
                        if (targetIdsToAdd.Any())
                        {
                            var validTargetIds = await _context.Notes.Where(n => targetIdsToAdd.Contains(n.Id)).Select(n => n.Id).ToListAsync();
                            foreach (var targetId in validTargetIds)
                            {
                                _context.NoteLinks.Add(new NoteLink
                                {
                                    Id = Guid.NewGuid(),
                                    SourceNoteId = note.Id,
                                    TargetNoteId = targetId,
                                    Excerpt = dto.Title ?? note.Title
                                });
                            }
                        }

                        if (dto.Tags != null)
                        {
                            note.Tags = dto.Tags.Any() ? JsonSerializer.Serialize(dto.Tags) : null;
                            var oldTags = await _context.TagAssignments.Where(ta => ta.EntityId == note.Id.ToString() && ta.EntityType == "Note").ToListAsync();
                            _context.TagAssignments.RemoveRange(oldTags);

                            if (dto.Tags.Any())
                            {
                                var cleanTags = dto.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList();
                                var normalizedTags = cleanTags.Select(t => t.ToLower()).ToList();
                                var existingDbTags = await _context.Tags.Where(t => t.SpaceId == note.SpaceId && normalizedTags.Contains(t.NormalizedName)).ToDictionaryAsync(t => t.NormalizedName);

                                foreach (var cleanName in cleanTags)
                                {
                                    var normalizedName = cleanName.ToLower();
                                    if (!existingDbTags.TryGetValue(normalizedName, out var tag))
                                    {
                                        tag = new Tag { Id = Guid.NewGuid(), SpaceId = note.SpaceId, Name = cleanName, NormalizedName = normalizedName, CreatedAt = DateTime.UtcNow };
                                        _context.Tags.Add(tag);
                                        existingDbTags[normalizedName] = tag;
                                    }
                                    _context.TagAssignments.Add(new TagAssignment { Id = Guid.NewGuid(), TagId = tag.Id, EntityId = note.Id.ToString(), EntityType = "Note", CreatedAt = DateTime.UtcNow });
                                }
                            }
                        }
                        // ========================================================================
                        // 🌟 3.5 投影 block_links（块级引用的边）
                        // ========================================================================
                        var sourceBlockIds = dto.Blocks?
                            .Select(b => b.Id)
                            .Where(x => !string.IsNullOrEmpty(x))
                            .Cast<string>()
                            .ToList() ?? new List<string>();

                        // 3.5.1 删掉之前以这些块为 source 的边
                        if (sourceBlockIds.Any())
                        {
                            await _context.BlockLinks
                                .Where(l => sourceBlockIds.Contains(l.SourceBlockId))
                                .ExecuteDeleteAsync();
                        }

                        // 3.5.2 从 dto.Blocks 里重新解析
                        if (dto.Blocks != null)
                        {
                            var newBlockLinks = new List<BlockLink>();
                            var seenEdges = new HashSet<string>();

                            foreach (var b in dto.Blocks)
                            {
                                if (string.IsNullOrEmpty(b.Id)) continue;
                                var targets = ExtractBlockRefTargets(b.Data);
                                foreach (var tgt in targets)
                                {
                                    if (tgt == b.Id) continue; // 自己引用自己跳过
                                    var edgeKey = $"{b.Id}|{tgt}";
                                    if (!seenEdges.Add(edgeKey)) continue;

                                    newBlockLinks.Add(new BlockLink
                                    {
                                        Id = Guid.NewGuid().ToString(),
                                        SourceBlockId = b.Id,
                                        TargetBlockId = tgt,
                                        RelationType = "reference",
                                        CreatedAt = DateTime.UtcNow,
                                    });
                                }
                            }

                            if (newBlockLinks.Any())
                            {
                                _context.BlockLinks.AddRange(newBlockLinks);
                            }
                        }
                        // ========================================================================
                        // 4. 投影 block_index
                        // ========================================================================
                        var indexNodeId = dto.NoteId.ToString();

                        await _context.BlockIndexes
                            .Where(x => x.NodeId == indexNodeId)
                            .ExecuteDeleteAsync();

                        if (dto.Blocks != null && dto.Blocks.Any())
                        {
                            int blockOrder = 0;
                            var seenIds = new HashSet<string>();

                            foreach (var b in dto.Blocks)
                            {
                                if (string.IsNullOrEmpty(b.Id) || !seenIds.Add(b.Id)) continue;

                                string? text = null;
                                string? latex = null;
                                string? assetId = null;

                                try
                                {
                                    using var doc = JsonDocument.Parse(b.Data ?? "{}");
                                    var root = doc.RootElement;

                                    if (root.ValueKind == JsonValueKind.Object &&
                                        root.TryGetProperty("attrs", out var attrs) &&
                                        attrs.ValueKind == JsonValueKind.Object)
                                    {
                                        if (attrs.TryGetProperty("latex", out var lx) &&
                                            lx.ValueKind == JsonValueKind.String)
                                            latex = lx.GetString();

                                        if (attrs.TryGetProperty("assetId", out var aid) &&
                                            aid.ValueKind == JsonValueKind.String)
                                            assetId = aid.GetString();
                                    }

                                    text = ExtractBlockText(root);
                                }
                                catch { }

                                _context.BlockIndexes.Add(new BlockIndex
                                {
                                    Id = b.Id,
                                    NodeId = indexNodeId,
                                    BlockType = b.Type ?? "paragraph",
                                    ParentBlockId = null,
                                    SortOrder = blockOrder++,
                                    TextContent = text,
                                    Latex = latex,
                                    AssetId = assetId,
                                    UpdatedAt = DateTime.UtcNow,
                                });
                            }
                        }

                        await _context.SaveChangesAsync();
                        await transaction.CommitAsync();

                        return Ok(new { success = true });
                    }
                    catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException mySqlEx && mySqlEx.Number == 1213)
                    {
                        await transaction.RollbackAsync();
                        return Ok(new { success = true, message = "并发重叠已由新版本覆盖" });
                    }
                    catch (DbUpdateException ex) when (ex.InnerException is MySqlConnector.MySqlException mySqlEx2 && mySqlEx2.Number == 1062)
                    {
                        // 🌟 主键冲突兜底：即使并发写入，也不报 500，让前端下次保存覆盖
                        await transaction.RollbackAsync();
                        return Ok(new { success = true, message = "主键冲突已忽略，下次保存将覆盖" });
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        await transaction.RollbackAsync();
                        return Ok(new { success = true, message = "并发重叠已无痕覆盖" });
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
                var inner = ex.InnerException?.Message ?? "(无内部异常)";
                var deeper = ex.InnerException?.InnerException?.Message ?? "(无更深异常)";
                return StatusCode(500, $"灵脉同步异常: {ex.Message} || 内部: {inner} || 更深: {deeper}");
            }
        }

        /// <summary>
        /// 🌟 全局搜索：笔记标题 + 块文本
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 20)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            if (string.IsNullOrWhiteSpace(q))
                return Ok(new { notes = new List<object>(), blocks = new List<object>() });

            // 用户的空间
            var userSpaceIds = await _context.Spaces
                .Where(s => s.UserId == CurrentUserId)
                .Select(s => s.Id)
                .ToListAsync();

            if (userSpaceIds.Count == 0)
                return Ok(new { notes = new List<object>(), blocks = new List<object>() });

            var pattern = $"%{q}%";

            // 用户的所有笔记
            var userNoteIds = await _context.Notes
                .Where(n => userSpaceIds.Contains(n.SpaceId) && n.Status == 0)
                .Select(n => n.Id.ToString())
                .ToListAsync();

            if (userNoteIds.Count == 0)
                return Ok(new { notes = new List<object>(), blocks = new List<object>() });

            // 搜笔记
            var notesRaw = await _context.Notes
                .Where(n => userSpaceIds.Contains(n.SpaceId) && n.Status == 0)
                .Where(n => EF.Functions.Like(n.Title, pattern))
                .OrderByDescending(n => n.UpdatedAt)
                .Take(limit)
                .Select(n => new { n.Id, n.Title, n.Type })
                .ToListAsync();

            // 搜块
            var blocksRaw = await _context.BlockIndexes
                .Where(b => userNoteIds.Contains(b.NodeId))
                .Where(b => b.TextContent != null && EF.Functions.Like(b.TextContent, pattern))
                .OrderByDescending(b => b.UpdatedAt)
                .Take(limit)
                .Select(b => new { b.Id, b.NodeId, b.BlockType, b.TextContent, b.Latex })
                .ToListAsync();

            // 补 noteTitle
            var blockNoteIds = blocksRaw.Select(b => b.NodeId).Distinct().ToList();
            var noteTitles = await _context.Notes
                .Where(n => blockNoteIds.Contains(n.Id.ToString()))
                .ToDictionaryAsync(n => n.Id.ToString(), n => n.Title);

            return Ok(new
            {
                notes = notesRaw.Select(n => new { id = n.Id, title = n.Title, type = n.Type }),
                blocks = blocksRaw.Select(b => new
                {
                    id = b.Id,
                    noteId = b.NodeId,
                    noteTitle = noteTitles.ContainsKey(b.NodeId) ? noteTitles[b.NodeId] : "无标题",
                    type = b.BlockType,
                    text = b.TextContent,
                    latex = b.Latex,
                }),
            });
        }

        /// <summary>
        /// 🌟 块预览：拿单个块的文本，供悬浮卡片显示
        /// </summary>
        [HttpGet("blocks/{blockId}/preview")]
        public async Task<IActionResult> GetBlockPreview(string blockId)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var block = await _context.BlockIndexes.FirstOrDefaultAsync(b => b.Id == blockId);
            if (block == null) return NotFound();

            var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id.ToString() == block.NodeId);
            if (note == null) return NotFound();

            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            var backlinkCount = await _context.BlockLinks
    .Where(l => l.TargetBlockId == blockId)
    .CountAsync();

            return Ok(new
            {
                id = block.Id,
                noteId = block.NodeId,
                noteTitle = note.Title,
                type = block.BlockType,
                text = block.TextContent,
                latex = block.Latex,
                assetId = block.AssetId,
                backlinkCount,
            });
        }
        /// <summary>
        /// 🌟 笔记预览：拿标题 + 前几块文本，供悬浮卡片显示
        /// </summary>
        [HttpGet("notes/{id:guid}/preview")]
        public async Task<IActionResult> GetNotePreview(Guid id)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == id);
            if (note == null) return NotFound();

            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            // 拿前 5 个块，抽纯文本
            var blocks = await _context.Blocks
                .Where(b => b.OwnerId == id.ToString())
                .OrderBy(b => b.SortOrder)
                .Take(30)
                .ToListAsync();

            var sb = new System.Text.StringBuilder();
            foreach (var b in blocks)
            {
                var t = ExtractTextFromBlockData(b.Data);
                if (!string.IsNullOrEmpty(t))
                {
                    sb.Append(t);
                    sb.Append('\n');
                }
                if (sb.Length > 300) break;
            }

            var excerpt = sb.ToString().Trim();
            if (excerpt.Length > 200) excerpt = excerpt.Substring(0, 200) + "…";

            // 🌟 统计这个笔记里所有块被引用的总次数
            var blockIds = await _context.BlockIndexes
                .Where(bi => bi.NodeId == id.ToString())
                .Select(bi => bi.Id)
                .ToListAsync();

            int backlinkCount = 0;
            if (blockIds.Any())
            {
                backlinkCount = await _context.BlockLinks
                    .Where(l => blockIds.Contains(l.TargetBlockId))
                    .CountAsync();
            }

            return Ok(new
            {
                id = note.Id,
                title = note.Title,
                type = note.Type,
                excerpt,
                backlinkCount,
            });
        }

        /// <summary>
        /// 从 Block.Data JSON 里抽纯文本
        /// </summary>
        private static string ExtractTextFromBlockData(string? data)
        {
            if (string.IsNullOrEmpty(data)) return "";
            try
            {
                using var doc = JsonDocument.Parse(data);
                var sb = new System.Text.StringBuilder();
                WalkTextForPreview(doc.RootElement, sb);
                return sb.ToString().Trim();
            }
            catch { return ""; }
        }

        private static void WalkTextForPreview(JsonElement node, System.Text.StringBuilder sb)
        {
            if (node.ValueKind != JsonValueKind.Object) return;

            // 普通文本节点
            if (node.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String)
            {
                sb.Append(t.GetString());
                sb.Append(' ');
            }

            // 🌟 schedule-item 之类的 title 字段
            if (node.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.String)
            {
                sb.Append(title.GetString());
                sb.Append(' ');
            }

            // 递归子节点
            if (node.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in c.EnumerateArray())
                    WalkTextForPreview(child, sb);
            }
        }





























        #endregion
        [HttpPatch("spaces/{id}")]
        public async Task<IActionResult> UpdateSpace(Guid id, [FromBody] JsonElement updates)
        {
            var space = await _context.Spaces.FindAsync(id);
            if (space == null) return NotFound();

            // 感应并更新位面名
            if (updates.TryGetProperty("name", out var nameProp))
                space.Name = nameProp.GetString() ?? space.Name;

            // 感应并更新公开性
            if (updates.TryGetProperty("isPublic", out var publicProp))
                space.IsPublic = publicProp.GetBoolean();

            await _context.SaveChangesAsync();
            return Ok();
        }
        #region --- 5. 历史快照与穿梭 ---

        [HttpGet("notes/{id:guid}/history")]
        public async Task<IActionResult> GetHistory(Guid id)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();
            var note = await _context.Notes.FindAsync(id);
            if (note == null) return NotFound();

            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            var history = await _context.NoteHistories
                .Where(h => h.NoteId == id)
                .OrderByDescending(h => h.CreatedAt)
                .Select(h => new { h.Id, h.Remark, h.CreatedAt })
                .ToListAsync();

            return Ok(history);
        }

        [HttpPost("notes/{id:guid}/snapshot")]
        public async Task<IActionResult> CreateSnapshot(Guid id, [FromBody] SnapshotDto dto)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();
            var note = await _context.Notes.FindAsync(id);
            if (note == null) return NotFound();

            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            await _lingMaiService.CreateSnapshotAsync(id, dto.ContentJson, dto.Remark);
            return Ok(new { success = true });
        }

        [HttpPost("history/{historyId:guid}/rollback")]
        public async Task<IActionResult> Rollback(Guid historyId) // 🌟 只需要 historyId 即可！
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            // 1. 通过 historyId 查出快照
            var history = await _context.NoteHistories.FirstOrDefaultAsync(h => h.Id == historyId);
            if (history == null) return NotFound(new { message = "未找到该历史快照" });

            // 2. 通过快照记录拿到关联的 Note
            var note = await _context.Notes.FindAsync(history.NoteId);
            if (note == null) return NotFound(new { message = "快照对应的原始笔记已不存在" });

            // 3. 越权校验：确保这个笔记属于当前用户
            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            // 4. 执行回滚
            await _lingMaiService.RollbackToSnapshotAsync(note.Id, history.Id);
            return Ok(new { success = true });
        }

        // 在 LingMaiController 内部添加
        private async Task<(bool isOverSpace, bool isOverNote, UserStats stats)> GetQuotaStatus(string userId)
        {
            // 1. 获取用户统计数据（如果不存在则赋予默认初值）
            var stats = await _context.UserStats.FirstOrDefaultAsync(s => s.UserId == Guid.Parse(userId));
            if (stats == null)
            {
                stats = new UserStats { UserId = Guid.Parse(userId), MaxSpaces = 1, MaxNotes = 100 };
            }

            // 2. 统计当前空间数
            var spaceCount = await _context.Spaces.CountAsync(s => s.UserId == userId);

            // 3. 统计全账户总节点数（跨空间统计）
            var totalNoteCount = await _context.Notes.CountAsync(n =>
                _context.Spaces.Where(s => s.UserId == userId).Select(s => s.Id).Contains(n.SpaceId));

            // 🌟 逻辑判断：
            // 创建时用 >= 拦截；编辑锁死用 > 拦截
            return (spaceCount > stats.MaxSpaces, totalNoteCount > stats.MaxNotes, stats);
        }


        [HttpGet("quota")]
        public async Task<IActionResult> GetQuotaUsage()
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            // 1. 获取用户的配额设定 (UserStats)
            var stats = await _context.UserStats
                .FirstOrDefaultAsync(s => s.UserId == Guid.Parse(CurrentUserId));

            // 如果没有 stats 记录，使用默认值
            var maxSpaces = stats?.MaxSpaces ?? 1;
            var maxNotes = stats?.MaxNotes ?? 100;

            // 2. 统计已使用的空间数量
            var usedSpaces = await _context.Spaces
                .CountAsync(s => s.UserId == CurrentUserId);

            // 3. 统计全账户已使用的节点总数 (无关空间)
            var usedNotes = await _context.Notes
                .CountAsync(n => _context.Spaces
                    .Where(s => s.UserId == CurrentUserId)
                    .Select(s => s.Id)
                    .Contains(n.SpaceId));

            // 4. 组装返回
            var result = new QuotaUsageDto
            {
                UsedSpaces = usedSpaces,
                MaxSpaces = maxSpaces,
                UsedNotes = usedNotes,
                MaxNotes = maxNotes
            };

            return Ok(result);
        }

        #endregion

        // ========================================================================
        // 🌟 block_index 投影辅助方法
        // ========================================================================

        /// <summary>
        /// 从 Tiptap 节点 JSON 里榨取纯文本，用于 block_index.TextContent
        /// </summary>
        private static string? ExtractBlockText(JsonElement node)
        {
            var sb = new System.Text.StringBuilder();
            WalkText(node, sb);
            var result = sb.ToString().Trim();
            return string.IsNullOrEmpty(result) ? null : result;
        }

        /// <summary>
        /// 递归遍历 JSON 节点，把所有 text 字段拼起来
        /// </summary>
        private static void WalkText(JsonElement node, System.Text.StringBuilder sb)
        {
            if (node.ValueKind != JsonValueKind.Object) return;

            if (node.TryGetProperty("text", out var t) &&
                t.ValueKind == JsonValueKind.String)
            {
                sb.Append(t.GetString());
                sb.Append(' ');
            }

            if (node.TryGetProperty("content", out var c) &&
                c.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in c.EnumerateArray())
                {
                    WalkText(child, sb);
                }
            }
        }

        /// <summary>
        /// 🌟 从 Block.Data JSON 里提取所有 spirit-link 指向的 blockId
        /// </summary>
        private static List<string> ExtractBlockRefTargets(string? data)
        {
            var result = new List<string>();
            if (string.IsNullOrWhiteSpace(data)) return result;
            try
            {
                using var doc = JsonDocument.Parse(data);
                WalkForBlockRefs(doc.RootElement, result);
            }
            catch { }
            return result;
        }

        private static void WalkForBlockRefs(JsonElement node, List<string> output)
        {
            if (node.ValueKind != JsonValueKind.Object) return;

            // 是 spirit-link 节点？
            if (node.TryGetProperty("type", out var t) &&
                t.ValueKind == JsonValueKind.String &&
                t.GetString() == "spirit-link")
            {
                if (node.TryGetProperty("attrs", out var attrs) &&
                    attrs.ValueKind == JsonValueKind.Object &&
                    attrs.TryGetProperty("blockId", out var bid) &&
                    bid.ValueKind == JsonValueKind.String)
                {
                    var b = bid.GetString();
                    if (!string.IsNullOrEmpty(b)) output.Add(b);
                }
            }

            // 递归
            if (node.TryGetProperty("content", out var c) && c.ValueKind == JsonValueKind.Array)
            {
                foreach (var child in c.EnumerateArray())
                    WalkForBlockRefs(child, output);
            }
        }


    }
}