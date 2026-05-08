// TaiChuWeb_V2/Controllers/LingMai/LingMaiController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.LingMai;
using TaiChuWeb_V2.Models.LingMai;
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

        [HttpPost("spaces")]
        public async Task<IActionResult> CreateSpace([FromBody] CreateSpaceDto dto)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var (isOverSpace, _, stats) = await GetQuotaStatus(CurrentUserId);
            // 检查：如果当前数量 >= 最大允许数量，则禁止创建
            if (await _context.Spaces.CountAsync(s => s.UserId == CurrentUserId) >= stats.MaxSpaces)
            {
                return StatusCode(403, new { message = "空间数量已达上限，请前往交易行购买扩展卡。" });
            }


            if (dto == null || string.IsNullOrWhiteSpace(dto.Name)) return BadRequest("空间名称不能为空");

            var space = new Space
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                UserId = CurrentUserId,
                CreatedAt = DateTime.UtcNow
            };

            _context.Spaces.Add(space);
            await _context.SaveChangesAsync();
            return Ok(new { id = space.Id, name = space.Name });
        }

        [HttpDelete("spaces/{id:guid}")]
        public async Task<IActionResult> DeleteSpace(Guid id)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var space = await _context.Spaces.FindAsync(id);
            if (space == null) return NotFound(new { message = "未找到指定的空间" });
            if (space.UserId != CurrentUserId) return Forbid();

            var notesInSpace = await _context.Notes.Where(n => n.SpaceId == id).ToListAsync();
            _context.Notes.RemoveRange(notesInSpace);

            _context.Spaces.Remove(space);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
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

            var notes = await _context.Notes
                .Where(n => n.SpaceId == spaceId && n.Status == 0)
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
                    n.UpdatedAt
                })
                .ToListAsync();

            return Ok(notes);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetNoteById(Guid id)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            // 1. 去掉 .Include(n => n.Blocks)
            var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == id);
            if (note == null) return NotFound(new { message = "该笔记不存在" });

            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            // 2. 🌟 手动查询该 Note 下绑定的 Blocks，使用新的 OwnerId 和 OwnerType 逻辑
            var blocks = await _context.Blocks
                .Where(b => b.OwnerId == id.ToString() && b.OwnerType == "note")
                .OrderBy(b => b.SortOrder)
                .Select(b => new { b.Id, b.Type, b.Data, b.SortOrder })
                .ToListAsync();

            return Ok(new
            {
                note.Id,
                note.Title,
                note.SpaceId,
                note.FolderId,
                note.Type,
                note.IsPublic,
                note.ShowInSidebar,
                note.SortOrder,
                note.CreatedAt,
                note.UpdatedAt,
                Blocks = blocks // 🌟 直接返回上面查出来的 blocks
            });
        }

        [HttpPost("notes")]
        public async Task<IActionResult> CreateNote([FromBody] CreateNoteDto dto)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var stats = await _context.UserStats.FirstOrDefaultAsync(s => s.UserId == Guid.Parse(CurrentUserId));
            var totalNoteCount = await _context.Notes.CountAsync(n =>
                _context.Spaces.Where(s => s.UserId == CurrentUserId).Select(s => s.Id).Contains(n.SpaceId));

            if (totalNoteCount >= (stats?.MaxNotes ?? 100))
            {
                return StatusCode(403, new { message = "灵脉节点已满，请前往交易行扩展容量。" });
            }


            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == dto.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            var note = new Note
            {
                Id = Guid.NewGuid(),
                SpaceId = dto.SpaceId,
                FolderId = dto.FolderId,
                Type = dto.Type,
                Title = dto.Title,
                IsPublic = false,
                ShowInSidebar = dto.Type == "note",
                SortOrder = dto.SortOrder ?? DateTime.UtcNow.Ticks.ToString(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Notes.Add(note);
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
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var note = await _context.Notes.FindAsync(id);
            if (note == null) return NotFound(new { message = "未找到该碎片" });

            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            _context.Notes.Remove(note);
            await _context.SaveChangesAsync();
            return Ok(new { success = true });
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

            // 🌟 如果任一维度超标，进入“只读锁死”模式
            if (isOverSpace || isOverNote)
            {
                return StatusCode(423, new
                {
                    message = "灵脉空间已淤积，编辑功能已锁定。",
                    reason = isOverNote ? "节点数溢出" : "空间数溢出"
                });
            }



            var note = await _context.Notes.FindAsync(dto.NoteId);
            if (note == null) return NotFound(new { message = "未找到对应的笔记" });

            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (!string.IsNullOrEmpty(dto.Title))
                {
                    note.Title = dto.Title;
                }
                note.UpdatedAt = DateTime.UtcNow;

                // 1. 🌟 修改点：使用多态指针 OwnerId + OwnerType 重建/清理草稿区 Blocks
                var existingBlocks = await _context.Blocks
                    .Where(b => b.OwnerId == dto.NoteId.ToString() && b.OwnerType == "note")
                    .ToListAsync();
                _context.Blocks.RemoveRange(existingBlocks);

                // 2. 准备捕获当前笔记里所有的双链引用
                var currentOutlinkIds = new HashSet<Guid>();

                if (dto.Blocks != null && dto.Blocks.Count > 0)
                {
                    foreach (var b in dto.Blocks)
                    {
                        var block = new Block
                        {
                            Id = b.Id,
                            OwnerId = dto.NoteId.ToString(), // 🌟 修改点：改用 OwnerId
                            OwnerType = "note",              // 🌟 修改点：增加 OwnerType
                            Type = b.Type,
                            Data = b.Data,
                            SortOrder = b.SortOrder ?? "0",
                            UpdatedAt = DateTime.UtcNow
                        };
                        _context.Blocks.Add(block);

                        // 🔍 自动解析双链规则：提取 data 里的 spiritLink id
                        if (!string.IsNullOrWhiteSpace(b.Data))
                        {
                            // 匹配形如 "id":"xxxx-xxxx-..." 的 GUID
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

                // 3. 增量更新 NoteLinks 表中的双链关联关系，杜绝断线！
                var existingLinks = await _context.NoteLinks.Where(nl => nl.SourceNoteId == dto.NoteId).ToListAsync();
                _context.NoteLinks.RemoveRange(existingLinks);

                foreach (var targetId in currentOutlinkIds)
                {
                    // 检查被引用的笔记是否存在
                    var targetExists = await _context.Notes.AnyAsync(n => n.Id == targetId);
                    if (targetExists)
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

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { success = true });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, $"灵脉同步异常: {ex.Message}");
            }
        }

        #endregion

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