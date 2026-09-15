using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.FinalNodes;

namespace TaiChuWeb_V2.Controllers.knowledgeBase
{
    // 1. 定义前端所需的极简图谱 DTO
    public class GraphDataDto
    {
        public List<GraphNodeDto> Nodes { get; set; } = new();
        public List<GraphEdgeDto> Edges { get; set; } = new();
    }

    public class GraphNodeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public int Size { get; set; } = 30; // 默认节点大小，前端可覆盖
        public string? CoverImage { get; set; }
    }

    public class GraphEdgeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Source { get; set; } = string.Empty; // 必须叫 source，匹配主流图表库
        public string Target { get; set; } = string.Empty; // 必须叫 target
        public string RelationType { get; set; } = string.Empty;
    }

    [ApiController]
    [Route("api/knowledge-base/[controller]")] // 路由调整为匹配新的模块划分
    public class GraphController : ControllerBase
    {
        private readonly AppDbContext _context;

        public GraphController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("space/{spaceId}")]
        public async Task<ActionResult<GraphDataDto>> GetSpaceGraph(Guid spaceId)
        {
            // 1. 极速拉取瘦节点（规避了沉重的胖表，查询极快）
            var nodesQuery = _context.FinalNodes
                .Where(n => n.Status == 0); // 只拉取正常状态的节点

            // 如果传入的不是空 Guid，则按空间进行隔离查询
            if (spaceId != Guid.Empty)
            {
                nodesQuery = nodesQuery.Where(n => n.SpaceId == spaceId);
            }

            var nodes = await nodesQuery
                .Select(n => new GraphNodeDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    Type = n.Type,
                    CoverImage = n.CoverImage
                })
                .AsNoTracking() // 只读查询，极大降低内存开销
                .ToListAsync();

            // 提取出所有节点的 ID 集合，用于精确过滤关系连线
            var nodeIds = nodes.Select(n => n.Id).ToHashSet();

            // 2. 拉取这些节点之间的连线 (Edges)
            var edges = await _context.FinalRelations
                .Where(r => nodeIds.Contains(r.SourceNodeId) && nodeIds.Contains(r.TargetNodeId))
                .Select(r => new GraphEdgeDto
                {
                    Id = r.Id,
                    Source = r.SourceNodeId,
                    Target = r.TargetNodeId,
                    RelationType = r.RelationType
                })
                .AsNoTracking()
                .ToListAsync();

            // 3. 组装返回给 Vue 3
            return Ok(new GraphDataDto
            {
                Nodes = nodes,
                Edges = edges
            });
        }
    }
}