using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext; // 根据你实际的命名空间调整
using TaiChuWeb_V2.Models.FinalNodes;

namespace TaiChuWeb_V2.Controllers.knowledgeBase
{
    // 1. 定义前端右侧详情面板所需的“胖节点” DTO
    public class NodeDetailDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;

        // 🌟 核心字段：存储长篇的世界观纪要
        public string Description { get; set; } = string.Empty;
    }

    [ApiController]
    // 🌟 将路由强制设定为前端请求的路径前缀
    [Route("api/knowledge-base/Node")]
    public class KnowledgeBaseController : ControllerBase
    {
        private readonly AppDbContext _context;

        public KnowledgeBaseController(AppDbContext context)
        {
            _context = context;
        }

        // 🌟 匹配前端请求： GET /api/knowledge-base/Node/{id}
        [HttpGet("{id}")]
        public async Task<ActionResult<NodeDetailDto>> GetNodeDetail(string id)
        {
            // 查询数据库，仅拉取当前这一个节点的详细文本内容
            var node = await _context.FinalNodes
                .Where(n => n.Id == id && n.Status == 0) // 确保节点有效
                .Select(n => new NodeDetailDto
                {
                    Id = n.Id,
                    Title = n.Title,
                    // 🌟 核心修复：根据真实的物理表结构进行字段映射
                    // 优先读取 HtmlCache，其次 BlocksData，最后降级到主表的 Excerpt
                    Description = n.Content != null
                                  ? (n.Content.HtmlCache ?? n.Content.BlocksData)
                                  : (n.Excerpt ?? string.Empty)
                })
                .AsNoTracking() // 提升查询性能
                .FirstOrDefaultAsync();

            if (node == null)
            {
                return NotFound(new { message = "[ 数据库警告 ]：未能在太初寰宇中定位到该实体。" });
            }

            return Ok(node);
        }
    }
}