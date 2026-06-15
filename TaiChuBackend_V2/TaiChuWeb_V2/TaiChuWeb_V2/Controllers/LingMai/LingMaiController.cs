// TaiChuWeb_V2/Controllers/LingMai/LingMaiController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.LingMai;
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




        // 在 LingMaiController.cs 中添加
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

            // 更新类型（如从 art 转为 wiki）
            if (updates.TryGetProperty("type", out var typeProp))
            {
                note.Type = typeProp.GetString() ?? note.Type;
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

            // 1. 查找空间
            var space = await _context.Spaces.FindAsync(id);
            if (space == null) return NotFound(new { message = "未找到指定的空间" });
            if (space.UserId != CurrentUserId) return Forbid();

            // 2. 获取该空间下的所有笔记（为了统计需要扣减的 UsedNotes 数量）
            var notesInSpace = await _context.Notes.Where(n => n.SpaceId == id).ToListAsync();
            int notesCount = notesInSpace.Count;

            // 3. 获取用户统计数据对象
            // 假设你有一个 GetQuotaStatus 或直接查询 stats
            var user = await _context.Users
                .Include(u => u.Stats)
                .FirstOrDefaultAsync(u => u.Id == Guid.Parse(CurrentUserId));

            if (user?.Stats == null) return BadRequest("无法更新账户审计数据");

            // --- 🌟 执行删除与计数器同步 ---

            // 删除笔记和空间
            _context.Notes.RemoveRange(notesInSpace);
            _context.Spaces.Remove(space);

            // 同步更新计数器
            // 使用 Math.Max 确保不会因为意外变成负数
            user.Stats.UsedSpaces = Math.Max(0, user.Stats.UsedSpaces - 1);
            user.Stats.UsedNotes = Math.Max(0, user.Stats.UsedNotes - notesCount);

            // 显式标记状态已改变
            _context.Entry(user.Stats).State = EntityState.Modified;

            await _context.SaveChangesAsync();

            return Ok(new { success = true, deletedNotes = notesCount });
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

            // 1. 拉取所有活跃的 Notes
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
                    n.ExtraData
                })
                .ToListAsync();

            // 2. 提取所有 Note 的 ID
            var noteIds = notes.Select(n => n.Id.ToString()).ToList();

            // 3. 🌟 核心修复：批量查询这些 Note 下属的所有 Blocks (避免 N+1 性能黑洞)
            var allBlocks = await _context.Blocks
                .Where(b => noteIds.Contains(b.OwnerId))
                .Select(b => new { b.Id, b.OwnerId, b.Type, b.Data, b.SortOrder })
                .ToListAsync();

            // 4. 在内存中将 Blocks 拼接到对应的 Note 身上
            var result = notes.Select(n => new {
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
                tags = string.IsNullOrWhiteSpace(n.Tags)
                        ? Array.Empty<string>()
                        : n.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(t => t.Trim()).ToArray(),
                extraData = n.ExtraData,
                // 👇 将归属于此卡片的万能块组装进去
                blocks = allBlocks.Where(b => b.OwnerId == n.Id.ToString()).OrderBy(b => b.SortOrder).ToList()
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
            // 🌟 核心修复 1：拉取该碎片绑定的所有万能块
            // ==========================================
            var blocks = await _context.Blocks
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

            // ==========================================
            // 🌟 核心修复 2：JSON 反序列化解析 Tags
            // ==========================================
            string[] tagsArray = Array.Empty<string>();
            if (!string.IsNullOrWhiteSpace(note.Tags))
            {
                try
                {
                    // 尝试按新的 JSON 格式解析
                    tagsArray = JsonSerializer.Deserialize<string[]>(note.Tags) ?? Array.Empty<string>();
                }
                catch (JsonException)
                {
                    // 🌟 平滑过渡：如果解析失败，说明是老数据（逗号分隔的），回退到 Split 模式
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
                extraData = note.ExtraData,   // 包含地图底图等扩展属性
                tags = tagsArray,             // 完美的数组格式
                showInSidebar = note.ShowInSidebar,
                sortOrder = note.SortOrder,
                createdAt = note.CreatedAt,
                updatedAt = note.UpdatedAt,
                blocks = blocks               // 🌟 塞入万能块大军
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

                        // ========================================================================
                        // 2. 块数据的增量 UPSERT (万能块核心逻辑)
                        // ========================================================================
                        var existingBlocks = await _context.Blocks
                            .Where(b => b.OwnerId == dto.NoteId.ToString() && b.OwnerType == note.Type)
                            .ToDictionaryAsync(b => b.Id);

                        var currentOutlinkIds = new HashSet<Guid>();

                        if (dto.Blocks != null)
                        {
                            // 遍历前端传来的块，带上索引 i 提供兜底 SortOrder
                            foreach (var (b, index) in dto.Blocks.Select((item, i) => (item, i)))
                            {
                                if (existingBlocks.TryGetValue(b.Id, out var dbBlock))
                                {
                                    bool isChanged = false;
                                    var newData = b.Data ?? string.Empty;
                                    var newSortOrder = b.SortOrder ?? index;

                                    if (dbBlock.Data != newData) { dbBlock.Data = newData; isChanged = true; }
                                    if (dbBlock.SortOrder != newSortOrder) { dbBlock.SortOrder = newSortOrder; isChanged = true; }
                                    if (dbBlock.Type != b.Type) { dbBlock.Type = b.Type; isChanged = true; }

                                    if (isChanged) dbBlock.UpdatedAt = DateTime.UtcNow;

                                    existingBlocks.Remove(b.Id);
                                }
                                else
                                {
                                    _context.Blocks.Add(new Block
                                    {
                                        Id = b.Id,
                                        OwnerId = dto.NoteId.ToString(),
                                        OwnerType = note.Type, // 继承宿主Type，命中复合索引
                                        Type = b.Type,
                                        Data = b.Data ?? string.Empty,
                                        SortOrder = b.SortOrder ?? index,
                                        UpdatedAt = DateTime.UtcNow
                                    });
                                }

                                // 提取双链 GUID
                                if (!string.IsNullOrWhiteSpace(b.Data))
                                {
                                    var matches = Regex.Matches(b.Data, @"[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}");
                                    foreach (Match match in matches)
                                    {
                                        if (Guid.TryParse(match.Value, out Guid linkedId) && linkedId != dto.NoteId)
                                        {
                                            currentOutlinkIds.Add(linkedId);
                                        }
                                    }
                                }
                            }
                        }

                        // 清理前端已删除的旧区块
                        if (existingBlocks.Any())
                        {
                            _context.Blocks.RemoveRange(existingBlocks.Values);
                        }

                        // ========================================================================
                        // 3. 增量更新 NoteLinks 表 (双链)
                        // ========================================================================
                        var existingLinks = await _context.NoteLinks
                            .Where(nl => nl.SourceNoteId == dto.NoteId)
                            .ToListAsync();

                        var existingTargetIds = existingLinks.Select(nl => nl.TargetNoteId).ToHashSet();

                        var linksToRemove = existingLinks.Where(nl => !currentOutlinkIds.Contains(nl.TargetNoteId)).ToList();
                        if (linksToRemove.Any()) _context.NoteLinks.RemoveRange(linksToRemove);

                        var targetIdsToAdd = currentOutlinkIds.Where(id => !existingTargetIds.Contains(id)).ToList();
                        if (targetIdsToAdd.Any())
                        {
                            var validTargetIds = await _context.Notes
                                .Where(n => targetIdsToAdd.Contains(n.Id))
                                .Select(n => n.Id)
                                .ToListAsync();

                            foreach (var targetId in validTargetIds)
                            {
                                _context.NoteLinks.Add(new NoteLink
                                {
                                    Id = Guid.NewGuid(),
                                    SourceNoteId = dto.NoteId,
                                    TargetNoteId = targetId,
                                    Excerpt = dto.Title ?? note.Title
                                });
                            }
                        }

                        // ========================================================================
                        // 4. 同步标签系统
                        // ========================================================================
                        if (dto.Tags != null)
                        {
                            note.Tags = dto.Tags.Any() ? JsonSerializer.Serialize(dto.Tags) : null;

                            var oldTags = await _context.TagAssignments
                                .Where(ta => ta.EntityId == dto.NoteId.ToString() && ta.EntityType == "Note")
                                .ToListAsync();
                            _context.TagAssignments.RemoveRange(oldTags);

                            if (dto.Tags.Any())
                            {
                                var cleanTags = dto.Tags.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToList();
                                var normalizedTags = cleanTags.Select(t => t.ToLower()).ToList();

                                var existingDbTags = await _context.Tags
                                    .Where(t => t.SpaceId == note.SpaceId && normalizedTags.Contains(t.NormalizedName))
                                    .ToDictionaryAsync(t => t.NormalizedName);

                                foreach (var cleanName in cleanTags)
                                {
                                    var normalizedName = cleanName.ToLower();

                                    if (!existingDbTags.TryGetValue(normalizedName, out var tag))
                                    {
                                        tag = new Tag
                                        {
                                            Id = Guid.NewGuid(),
                                            SpaceId = note.SpaceId,
                                            Name = cleanName,
                                            NormalizedName = normalizedName,
                                            CreatedAt = DateTime.UtcNow
                                        };
                                        _context.Tags.Add(tag);
                                        existingDbTags[normalizedName] = tag;
                                    }

                                    _context.TagAssignments.Add(new TagAssignment
                                    {
                                        Id = Guid.NewGuid(),
                                        TagId = tag.Id,
                                        EntityId = note.Id.ToString(),
                                        EntityType = "Note",
                                        CreatedAt = DateTime.UtcNow
                                    });
                                }
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
                return StatusCode(500, $"灵脉同步异常: {ex.Message}");
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
    }

    
}