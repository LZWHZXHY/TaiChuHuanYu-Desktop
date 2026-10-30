// TaiChuWeb_V2/Controllers/LingMai/LingMaiNodeGraphController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.LingMai;

namespace TaiChuWeb_V2.Controllers.LingMai
{
    [ApiController]
    [Route("api/[controller]")]
    public class LingMaiNodeGraphController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly LingMaiService _service;
        private string? CurrentUserId => User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        public LingMaiNodeGraphController(AppDbContext context, LingMaiService service)
        {
            _context = context;
            _service = service;
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
                    type = n.Type ?? "note",
                    // 所在文件夹：关系图里画成虚线，文件夹不再是孤零零的点
                    folderId = n.FolderId,
                    spaceId = n.SpaceId,
                    isPublic = n.IsPublic
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

        [HttpGet("notes/{noteId:guid}/backlinks")]
        public Task<IActionResult> GetBacklinks(Guid noteId) => GetLinks(noteId, incoming: true);

        [HttpGet("notes/{noteId:guid}/outlinks")]
        public Task<IActionResult> GetOutlinks(Guid noteId) => GetLinks(noteId, incoming: false);

        private async Task<IActionResult> GetLinks(Guid noteId, bool incoming)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return Unauthorized();
            var note = await _context.Notes.FindAsync(noteId);
            if (note == null) return NotFound();
            if (!await _context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == CurrentUserId))
                return Forbid();
            return Ok(await _service.GetNoteLinksAsync(noteId, incoming));
        }
    }
}
