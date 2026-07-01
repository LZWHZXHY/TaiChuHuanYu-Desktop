using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Project;
using TaiChuWeb_V2.Dtos.Project;

namespace TaiChuWeb_V2.Controllers.Projects
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")] // 🌟 提示：请确保前端 baseUrl 包含了 api 前缀，或与前端请求路径对齐
    public class ProjectController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProjectController(AppDbContext context)
        {
            _context = context;
        }

        private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        #region --- 广场：发现公开项目 ---

        [HttpGet("public")]
        public async Task<IActionResult> GetPublicProjects()
        {
            var projects = await _context.Projects
                .Where(p => p.IsPublic)
                .OrderByDescending(p => p.CreatedAt)
                // 🌟 核心：联查 Users 表以获取所有者用户名
                .Join(
                    _context.Users,
                    p => p.OwnerId,
                    u => u.Id,
                    (p, u) => new { Project = p, OwnerName = u.Username }
                )
                .Select(x => new {
                    x.Project.Id,
                    x.Project.Name,
                    x.Project.Description,
                    x.Project.Status,
                    x.Project.StartTime,
                    x.Project.EndTime,
                    x.Project.JoinPolicy,
                    x.Project.CreatedAt,
                    x.OwnerName, // 🌟 返回所有者名字供前端渲染
                    x.Project.OwnerId, // 🌟 返回 OwnerId，供前端判断当前用户是否就是该项目所有者
                    MemberCount = _context.ProjectMembers.Count(m => m.ProjectId == x.Project.Id),
                    IsJoined = _context.ProjectMembers.Any(m => m.ProjectId == x.Project.Id && m.UserId == CurrentUserId),
                    HasApplied = _context.ProjectApplications.Any(a => a.ProjectId == x.Project.Id && a.UserId == CurrentUserId && a.Status == 0)
                })
                .ToListAsync();

            return Ok(projects);
        }

        #endregion

        #region --- 核心：项目列表与创建 ---

        [HttpGet("my")]
        public async Task<IActionResult> GetMyProjects()
        {
            var projects = await _context.ProjectMembers
                .Where(m => m.UserId == CurrentUserId)
                .Include(m => m.Project)
                .Select(m => new {
                    m.Project.Id,
                    m.Project.Name,
                    m.Project.Description,
                    m.Project.IsPublic,
                    m.Project.JoinPolicy,
                    m.Project.Status,
                    m.Project.StartTime,
                    m.Project.EndTime,
                    m.RoleId,
                    m.Project.CreatedAt,

                    // 🌟 新增：返回所有者 ID
                    OwnerId = m.Project.OwnerId,

                    // 🌟 新增：联查所有者昵称 (通过 OwnerId 匹配 Users 表)
                    OwnerName = _context.Users
                        .Where(u => u.Id.ToString() == m.Project.OwnerId.ToString())
                        .Select(u => u.Username)
                        .FirstOrDefault(),

                    MemberCount = _context.ProjectMembers.Count(pm => pm.ProjectId == m.Project.Id)
                })
                .ToListAsync();

            return Ok(projects);
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // 🌟 1. 查询当前登录用户的 Stats 数据
            var userStats = await _context.UserStats
                .FirstOrDefaultAsync(s => s.UserId == Guid.Parse(CurrentUserId));

            if (userStats == null) return Unauthorized("未寻得您的太初数据，无法校验额度");

            // 🌟 2. 动态统计活跃项目数量
            var activeProjectCount = await _context.ProjectMembers
                .CountAsync(m => m.UserId == CurrentUserId && m.RoleId == 0 && m.Project.Status != 3);

            // 🌟 3. 拦截
            if (activeProjectCount >= userStats.MaxProjectCount)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = $"您的活跃灵脉负载已达上限（{activeProjectCount}/{userStats.MaxProjectCount}）。请前往项目配置封存闲置项目释放额度，或去交易行购置更多空间。"
                });
            }

            // 🌟 为了在返回值里带上正确的 OwnerName，我们查一下当前用户的名字
            var currentUser = await _context.Users.FindAsync(Guid.Parse(CurrentUserId));
            var ownerName = currentUser?.Username ?? "未知创造者";

            // --- 4. 创建项目 ---
            var project = new Project
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name,
                Description = dto.Description,
                IsPublic = dto.IsPublic,
                JoinPolicy = 0,
                CreatedAt = DateTime.UtcNow,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Status = 1,
                OwnerId = Guid.Parse(CurrentUserId) // 绑定所有者 ID
            };

            _context.Projects.Add(project);

            _context.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = CurrentUserId,
                RoleId = 0,
                JoinedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

            // 🌟 完美对齐前端的数据结构
            return Ok(new
            {
                project.Id,
                project.Name,
                project.Description,
                project.IsPublic,
                project.JoinPolicy,
                project.Status,
                project.StartTime,
                project.EndTime,
                project.CreatedAt,

                // 👇 补全这三个字段，前端拿到数据后直接 push 进列表就能完美显示！
                ownerId = project.OwnerId,
                ownerName = ownerName,
                memberCount = 1
            });
        }

        #endregion

        #region --- 管理：设置与属性修改 ---

        // 获取项目基础信息与统计
        [HttpGet("{projectId}/settings")]
        public async Task<IActionResult> GetProjectSettings(string projectId)
        {
            // 🔒 权限检查
            if (!await IsMember(projectId)) return Forbid();

            // 🌟 修复：补全 status, startTime, endTime 字段查询，供前端表单赋初始值
            var project = await _context.Projects
                .Where(p => p.Id == projectId)
                .Select(p => new {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.IsPublic,
                    p.JoinPolicy,
                    p.Status,
                    p.StartTime,
                    p.EndTime,
                    p.CreatedAt,
                    MemberCount = _context.ProjectMembers.Count(m => m.ProjectId == p.Id),
                    TaskCount = _context.ProjectTasks.Count(t => t.ProjectId == p.Id)
                })
                .FirstOrDefaultAsync();

            if (project == null) return NotFound();

            return Ok(project);
        }

        // 🌟 一站式修改项目属性
        [HttpPatch("{projectId}")]
        public async Task<IActionResult> UpdateProject(string projectId, [FromBody] UpdateProjectDto dto)
        {
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null) return NotFound();

            // 🔒 权限检查
            var role = await GetUserRole(projectId);
            if (role != 0) return StatusCode(StatusCodes.Status403Forbidden, new { message = "只有项目所有者可以修改设置" });

            // 更新字段
            if (dto.Name != null) project.Name = dto.Name;
            if (dto.Description != null) project.Description = dto.Description;
            if (dto.IsPublic.HasValue) project.IsPublic = dto.IsPublic.Value;
            if (dto.JoinPolicy.HasValue) project.JoinPolicy = dto.JoinPolicy.Value;

            // 🌟 允许前台显式更新时间维度的空值 (比如清除结束时间)
            if (dto.StartTime.HasValue) project.StartTime = dto.StartTime;
            if (dto.EndTime.HasValue) project.EndTime = dto.EndTime;
            if (dto.Status.HasValue) project.Status = dto.Status.Value;

            await _context.SaveChangesAsync();

            // 返回扁平化数据
            return Ok(new
            {
                project.Id,
                project.Name,
                project.Description,
                project.IsPublic,
                project.JoinPolicy,
                project.Status,
                project.StartTime,
                project.EndTime
            });
        }

        // 彻底解散项目
        [HttpDelete("{projectId}")]
        public async Task<IActionResult> DeleteProject(string projectId)
        {
            var role = await GetUserRole(projectId);
            if (role != 0) return Forbid();

            var project = await _context.Projects.FindAsync(projectId);
            if (project == null) return NotFound();

            _context.Projects.Remove(project);

            await _context.SaveChangesAsync();
            return Ok("项目已从灵脉中抹除");
        }


        // 🌟 新增：获取指定项目下的所有公开/协作归档文档大纲
        [HttpGet("{projectId}/documents")]
        public async Task<IActionResult> GetProjectDocuments(string projectId)
        {
            // 1. 安全拦截：如果用户不是该项目的成员，无权查看项目内部文档长卷
            if (!await IsMember(projectId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "您尚未加入该协作位面，无法窥探项目长卷" });
            }

            // 2. 核心联动查询：
            // 从 ProjectDocuments 关联表出发，通过 NoteId 去把原始的 Notes 表联动查出来
            var documents = await _context.ProjectDocuments
                .Where(pd => pd.ProjectId == projectId)
                .Join(
                    _context.Notes,
                    pd => pd.NoteId,
                    n => n.Id.ToString(),
                    (pd, n) => new { pd, n }
                )
                // 🌟 进一步联查 Users 表，把当年 Pin 这篇文档的共建者用户名顺手捞出来
                .Join(
                    _context.Users,
                    combined => combined.pd.PinnedByUserId,
                    u => u.Id.ToString(),
                    (combined, u) => new { combined.pd, combined.n, PinnedByUserName = u.Username }
                )
                .OrderByDescending(x => x.pd.PinnedAt) // 按归档时间倒序排列
                .Select(x => new
                {
                    id = x.n.Id,                      // 文档草稿的真实 NoteId，供前端右侧沉浸阅读器去读 Blocks
                    title = string.IsNullOrWhiteSpace(x.n.Title) ? "未命名项目长卷" : x.n.Title,
                    type = x.n.Type,
                    pinnedAt = x.pd.PinnedAt,          // 归档同步时间
                    pinnedByUserName = x.PinnedByUserName // 🌟 完美的贡献者昵称，映射前端的 doc.pinnedByUserName
                })
                .ToListAsync();

            return Ok(documents);
        }


        #endregion

        #region --- 辅助校验 ---

        private async Task<bool> IsMember(string projectId) =>
            await _context.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == CurrentUserId);

        private async Task<int?> GetUserRole(string projectId)
        {
            var member = await _context.ProjectMembers
                .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == CurrentUserId);
            return member?.RoleId;
        }

        #endregion
    }
}