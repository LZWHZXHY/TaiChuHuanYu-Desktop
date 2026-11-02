using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using System.Text.Json;
using System.Text.RegularExpressions;
using TaiChuWeb_V2.Controllers.LingMai;
using TaiChuWeb_V2.Models.FinalNodes;
using TaiChuWeb_V2.Services.Personalization;

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
        /// <summary>简介（纯文字，全站图谱用）</summary>
        public string? Description { get; set; }
        /// <summary>站内地址：点「打开原内容」时跳过去（全站图谱用）</summary>
        public string? Route { get; set; }
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
            /// <summary>
        /// 全站公开内容的关系图谱：图谱资料 + 博客 / 帖子 / 画廊作品 / 公开世界观 / 世界观小说 / 柴圈作品 / 柴圈人设 / 百科，
        /// 再加「创作者」节点把同一个人的内容串起来；小说连到所属世界，柴圈作品连到出场的人设。
        /// 每类最多取最近更新的 PerType 个，避免节点太多拖慢星图。
        /// </summary>
        [HttpGet("public")]
        public async Task<ActionResult<GraphDataDto>> GetPublicGraph()
        {
            const int PerType = 150;
            var nodes = new List<GraphNodeDto>();
            var edges = new List<GraphEdgeDto>();
            var authors = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // 名字 → 节点 ID

            static string Clip(string? text, int max = 160)
            {
                if (string.IsNullOrWhiteSpace(text)) return "";
                var plain = Regex.Replace(text, @"\{CARD:[^}]+\}|!\[[^\]]*\]\([^)]*\)|[#*>`]", "").Trim();
                return plain.Length > max ? plain[..max] + "…" : plain;
            }
            void Author(string nodeId, string? name)
            {
                if (string.IsNullOrWhiteSpace(name)) return;
                name = name.Trim();
                if (!authors.TryGetValue(name, out var authorId))
                {
                    authorId = "author:" + name;
                    authors[name] = authorId;
                    nodes.Add(new GraphNodeDto { Id = authorId, Title = name, Type = "author", Size = 12, Description = $"{name} 的公开创作" });
                }
                edges.Add(new GraphEdgeDto { Id = $"{authorId}->{nodeId}", Source = authorId, Target = nodeId, RelationType = "AUTHOR" });
            }

            // 1. 图谱资料（灵脉里整理的设定节点）和它们之间的关系
            var finalNodes = await _context.FinalNodes.AsNoTracking().Where(n => n.Status == 0)
                .Select(n => new GraphNodeDto { Id = n.Id, Title = n.Title, Type = n.Type, CoverImage = n.CoverImage }).ToListAsync();
            nodes.AddRange(finalNodes);
            var finalIds = finalNodes.Select(n => n.Id).ToHashSet();
            edges.AddRange((await _context.FinalRelations.AsNoTracking()
                    .Select(r => new GraphEdgeDto { Id = r.Id, Source = r.SourceNodeId, Target = r.TargetNodeId, RelationType = r.RelationType }).ToListAsync())
                .Where(e => finalIds.Contains(e.Source) && finalIds.Contains(e.Target)));

            // 2. 博客 / 帖子（和论坛同一套可见规则）
            var notes = await LingMaiPublishController.VisiblePublishedNotes(_context)
                .Where(n => n.Type == "blog" || n.Type == "note" || n.Type == "post")
                .OrderByDescending(n => n.PublishedAt).Take(PerType * 2)
                .Select(n => new { n.Id, n.Type, n.Title, n.Excerpt, n.ExtraData, n.AuthorName }).ToListAsync();
            foreach (var n in notes)
            {
                var type = n.Type == "post" ? "post" : "blog";
                string text = n.Excerpt ?? "", cover = "";
                try
                {
                    if (text.TrimStart().StartsWith('{'))
                    {
                        using var doc = JsonDocument.Parse(text);
                        text = doc.RootElement.TryGetProperty("text", out var t) && t.ValueKind == JsonValueKind.String ? t.GetString() ?? "" : "";
                        cover = doc.RootElement.TryGetProperty("coverUrl", out var c) && c.ValueKind == JsonValueKind.String ? c.GetString() ?? "" : "";
                    }
                    if (type == "post" && !string.IsNullOrWhiteSpace(n.ExtraData) && n.ExtraData.TrimStart().StartsWith('{'))
                    {
                        using var doc = JsonDocument.Parse(n.ExtraData);
                        if (doc.RootElement.TryGetProperty("cardCover", out var c) && c.ValueKind == JsonValueKind.String) cover = c.GetString() ?? "";
                    }
                }
                catch (JsonException) { }
                var id = $"{type}:{n.Id}";
                nodes.Add(new GraphNodeDto
                {
                    Id = id, Type = type, Title = string.IsNullOrWhiteSpace(n.Title) ? (type == "post" ? "短帖" : "无标题文章") : n.Title!,
                    CoverImage = string.IsNullOrWhiteSpace(cover) ? null : cover, Description = Clip(text),
                    Route = $"/knowledge?detail={n.Id}&detailType={type}"
                });
                Author(id, n.AuthorName);
            }

            // 3. 画廊作品
            var artworks = await _context.Artworks.AsNoTracking()
                .Where(a => a.IsApproved && (a.Status == "published" || a.Status == "" || a.Status == null))
                .OrderByDescending(a => a.UploadAt).Take(PerType)
                .Select(a => new { a.Id, a.Title, a.Description, Author = a.Uploader.Username,
                    Cover = a.Images.OrderByDescending(i => i.IsCover).ThenBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault() })
                .ToListAsync();
            foreach (var a in artworks)
            {
                var id = $"artwork:{a.Id}";
                nodes.Add(new GraphNodeDto { Id = id, Type = "artwork", Title = a.Title, CoverImage = a.Cover, Description = Clip(a.Description),
                    Route = $"/knowledge?detail={a.Id}&detailType=artwork" });
                Author(id, a.Author);
            }

            // 4. 公开世界观
            var worlds = await _context.WorldProjects.AsNoTracking().Where(p => p.IsPublic)
                .OrderByDescending(p => p.UpdatedAt).Take(PerType)
                .Select(p => new { p.Id, p.Name, p.Description, p.CoverImage, Author = p.Owner != null ? p.Owner.Username : null, Cards = p.Cards.Count })
                .ToListAsync();
            var worldIds = new HashSet<Guid>();
            foreach (var w in worlds)
            {
                var id = $"world:{w.Id}";
                worldIds.Add(w.Id);
                nodes.Add(new GraphNodeDto { Id = id, Type = "world", Title = w.Name, CoverImage = w.CoverImage, Size = 20,
                    Description = Clip($"{w.Cards} 个设定条目。{w.Description}"), Route = $"/world/project/{w.Id}" });
                Author(id, w.Author);
            }

            // 5. 世界观小说（连到所属世界）
            var novels = await _context.WorldNovels.AsNoTracking().Where(n => n.IsPublic)
                .OrderByDescending(n => n.UpdatedAt).Take(PerType)
                .Select(n => new { n.Id, n.Title, n.Description, n.CoverUrl, n.AuthorName, n.ProjectId }).ToListAsync();
            foreach (var n in novels)
            {
                var id = $"novel:{n.Id}";
                nodes.Add(new GraphNodeDto { Id = id, Type = "novel", Title = n.Title, CoverImage = n.CoverUrl, Description = Clip(n.Description), Route = $"/novels/{n.Id}" });
                Author(id, n.AuthorName);
                if (n.ProjectId is Guid pid && worldIds.Contains(pid))
                    edges.Add(new GraphEdgeDto { Id = $"{id}->world", Source = id, Target = $"world:{pid}", RelationType = "IN_WORLD" });
            }

            // 6. 柴圈人设
            var ocs = await _context.StickmanCharacters.AsNoTracking().Where(c => c.Status == "published")
                .OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt).Take(PerType)
                .Select(c => new { c.Id, c.Title, c.Description, c.CoverUrl, c.AuthorName }).ToListAsync();
            var ocIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in ocs)
            {
                var id = $"oc:{c.Id}";
                ocIds.Add(c.Id.ToString());
                nodes.Add(new GraphNodeDto { Id = id, Type = "oc", Title = c.Title, CoverImage = c.CoverUrl, Description = Clip(c.Description), Route = $"/ocs/{c.Id}" });
                Author(id, c.AuthorName);
            }

            // 7. 柴圈作品（连到出场的人设）
            var works = await _context.ChaiWorks.AsNoTracking().Where(w => w.Status == "published")
                .OrderByDescending(w => w.UpdatedAt ?? w.CreatedAt).Take(PerType)
                .Select(w => new { w.Id, w.Title, w.Description, w.CoverUrl, w.AuthorName, w.RelatedCharacterIds }).ToListAsync();
            foreach (var w in works)
            {
                var id = $"work:{w.Id}";
                nodes.Add(new GraphNodeDto { Id = id, Type = "work", Title = w.Title, CoverImage = w.CoverUrl, Description = Clip(w.Description), Route = $"/works/{w.Id}" });
                Author(id, w.AuthorName);
                try
                {
                    using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(w.RelatedCharacterIds) ? "[]" : w.RelatedCharacterIds);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                        foreach (var item in doc.RootElement.EnumerateArray())
                        {
                            var cid = item.ValueKind == JsonValueKind.String ? item.GetString() : item.ToString();
                            if (cid != null && ocIds.Contains(cid))
                                edges.Add(new GraphEdgeDto { Id = $"{id}->oc:{cid}", Source = id, Target = $"oc:{cid.ToLowerInvariant()}", RelationType = "FEATURES" });
                        }
                }
                catch (JsonException) { }
            }

            // 8. 公共百科
            var wikis = await _context.WikiArticles.AsNoTracking().Where(a => !a.IsDeleted && a.CurrentRevisionId != null)
                .OrderByDescending(a => a.UpdatedAt).Take(PerType)
                .Select(a => new { a.Id, a.Title, a.Excerpt }).ToListAsync();
            foreach (var a in wikis)
                nodes.Add(new GraphNodeDto { Id = $"wiki:{a.Id}", Type = "wiki", Title = a.Title, Description = Clip(a.Excerpt), Route = $"/codex/{a.Id}" });

            // 创作者节点大小按作品数
            var authorCounts = edges.Where(e => e.RelationType == "AUTHOR").GroupBy(e => e.Source).ToDictionary(g => g.Key, g => g.Count());
            foreach (var n in nodes.Where(n => n.Type == "author"))
                n.Size = 10 + Math.Min(20, authorCounts.GetValueOrDefault(n.Id) * 2);

            return Ok(new GraphDataDto { Nodes = nodes, Edges = edges });
        }
        /// <summary>
        /// 关系图谱的搜索：和知识库列表（catalog）用同一套匹配规则（KnowledgeSearch.FilterAsync，同样的类型范围、字段和「每个词都要出现」），
        /// 再把列表条目的（类型, ID）换成图谱节点 ID（KnowledgeSearch.GraphNodeId）。
        /// listTotal = 列表一共能搜到多少条：前端用它算「还有几条在列表里、图谱里没有对应节点」（设定条目、公告、必吃榜……，
        /// 或者太旧没进图谱的内容），提示用户去列表看。创作者、文件夹、资源文件等前端自己拼的节点由前端按同样的规则匹配。
        /// </summary>
        [HttpGet("search")]
        public async Task<IActionResult> SearchPublicGraph([FromQuery] string? q)
        {
            var terms = KnowledgeSearch.Terms(q?.Length > 100 ? q[..100] : q);
            if (terms.Count == 0) return Ok(new { ids = Array.Empty<string>(), listTotal = 0 });
            // 开发进度和列表一样：只有管理员和赞助会员能搜到
            var perks = HttpContext.RequestServices.GetService<SponsorPerkService>();
            var devlogOk = perks != null && (await DevProgressAccess.CheckAsync(_context, perks, User)).CanView;
            var query = await KnowledgeSearch.FilterAsync(_context,
                CatalogController.Query(_context, "all", includeSettings: true, includeDevlog: devlogOk).AsNoTracking(), terms);
            var total = await query.CountAsync();
            var hits = await query.Select(i => new { i.Type, i.Id }).Take(5000).ToListAsync();
            var ids = hits.Select(h => KnowledgeSearch.GraphNodeId(h.Type, h.Id)).Where(id => id != null)
                .Select(id => id!.ToLowerInvariant()).Distinct().ToList();
            return Ok(new { ids, listTotal = total });
        }
    }
}