// TaiChuWeb_V2/Controllers/LingMai/LingMaiNodeGraphController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.LingMai;

namespace TaiChuWeb_V2.Controllers.LingMai
{
    [ApiController]
    [Route("api/[controller]")]
    public class LingMaiNodeGraphController : ControllerBase
    {
        private readonly AppDbContext _context;
        private string? CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        public LingMaiNodeGraphController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 🌟 完美兼容：获取空间图谱数据，支持单空间与跨空间模式
        /// 严格匹配前端：GET api/LingMaiNodeGraph/spaces/{spaceId}/graph?scope=all
        /// </summary>
        [HttpGet("spaces/{spaceId:guid}/graph")]
        public async Task<IActionResult> GetGraphData([FromRoute] Guid spaceId, [FromQuery] string? scope)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            // 1. 获取当前用户所拥有的全部空间 ID
            var userSpaceIds = await _context.Spaces
                .Where(s => s.UserId == CurrentUserId)
                .Select(s => s.Id)
                .ToListAsync();

            if (userSpaceIds.Count == 0)
            {
                return Ok(new { nodes = new List<object>(), links = new List<object>() });
            }

            List<Guid> targetSpaceIds;

            // 🌟 核心判断逻辑：当前端请求中 scope == "all" 时，开启跨空间全屏谱图模式
            if (scope == "all")
            {
                // 只能看见用户自己的全部空间
                targetSpaceIds = userSpaceIds;
            }
            else
            {
                // 单空间隔离模式：严格校验并锁定当前传入的空间 ID
                if (!userSpaceIds.Contains(spaceId)) return Forbid();
                targetSpaceIds = new List<Guid> { spaceId };
            }

            // 2. 提取该范围内所有状态正常的节点（包含文件夹和笔记）
            var nodes = await _context.Notes
                .Where(n => targetSpaceIds.Contains(n.SpaceId) && n.Status == 0)
                .Select(n => new
                {
                    id = n.Id.ToString(),
                    title = string.IsNullOrWhiteSpace(n.Title) ? "无标题碎片" : n.Title,
                    type = n.Type ?? "note"
                })
                .ToListAsync();

            // 3. 提取这些节点之间的双链连线关系
            var noteIds = nodes.Select(n => Guid.Parse(n.id)).ToList();
            var links = await _context.NoteLinks
                .Where(nl => noteIds.Contains(nl.SourceNoteId) && noteIds.Contains(nl.TargetNoteId))
                .Select(nl => new
                {
                    source = nl.SourceNoteId.ToString(),
                    target = nl.TargetNoteId.ToString()
                })
                .ToListAsync();

            return Ok(new { nodes, links });
        }

        /// <summary>
        /// 反向链接
        /// </summary>
        [HttpGet("notes/{noteId:guid}/backlinks")]
        public async Task<IActionResult> GetBacklinks(Guid noteId)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var note = await _context.Notes.FindAsync(noteId);
            if (note == null) return NotFound();

            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            var backlinks = await _context.NoteLinks
                .Where(nl => nl.TargetNoteId == noteId)
                .Include(nl => nl.SourceNote)
                .Select(nl => new
                {
                    id = nl.SourceNoteId,
                    title = nl.SourceNote != null ? nl.SourceNote.Title : "无标题碎片",
                    excerpt = nl.Excerpt,
                    updatedAt = nl.SourceNote != null ? nl.SourceNote.UpdatedAt : DateTime.UtcNow
                })
                .ToListAsync();

            return Ok(backlinks);
        }

        /// <summary>
        /// 正向链接
        /// </summary>
        [HttpGet("notes/{noteId:guid}/outlinks")]
        public async Task<IActionResult> GetOutlinks(Guid noteId)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();

            var note = await _context.Notes.FindAsync(noteId);
            if (note == null) return NotFound();

            var isOwner = await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId);
            if (!isOwner) return Forbid();

            var outlinks = await _context.NoteLinks
                .Where(nl => nl.SourceNoteId == noteId)
                .Include(nl => nl.TargetNote)
                .Select(nl => new
                {
                    id = nl.TargetNoteId,
                    title = nl.TargetNote != null ? nl.TargetNote.Title : "无标题碎片",
                    excerpt = nl.Excerpt,
                    updatedAt = nl.TargetNote != null ? nl.TargetNote.UpdatedAt : DateTime.UtcNow
                })
                .ToListAsync();

            return Ok(outlinks);
        }
    }
}