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
    [Route("api/[controller]")]
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
            // 逻辑：
            // 1. 必须是公开项目 (IsPublic == true)
            // 2. (可选) 排除掉用户已经是成员的项目，或者在前端标记“已加入”
            var projects = await _context.Projects
                .Where(p => p.IsPublic)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Status,
                    p.StartTime,
                    p.EndTime,
                    p.CreatedAt,
                    // 统计人数
                    MemberCount = _context.ProjectMembers.Count(m => m.ProjectId == p.Id),
                    // 标记当前用户是否已经是成员
                    IsJoined = _context.ProjectMembers.Any(m => m.ProjectId == p.Id && m.UserId == CurrentUserId)
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
                    m.Project.Status,    // 🌟 补全状态
                    m.Project.StartTime, // 🌟 补全开始时间
                    m.Project.EndTime,   // 🌟 补全结束时间
                    m.RoleId,
                    m.Project.CreatedAt,

                    // 🌟 实时统计参与人数
                    MemberCount = _context.ProjectMembers.Count(pm => pm.ProjectId == m.Project.Id)
                })
                .ToListAsync();
            return Ok(projects);
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto dto)
        {
            var project = new Project
            {
                Id = Guid.NewGuid().ToString(),
                Name = dto.Name,
                Description = dto.Description,
                IsPublic = dto.IsPublic,
                JoinPolicy = 0, // 默认仅限邀请
                CreatedAt = DateTime.UtcNow,
                StartTime = dto.StartTime,
                EndTime = dto.EndTime,
                Status = 1 // 创建即默认进入活跃状态
            };

            _context.Projects.Add(project);

            // 自动设为 Owner
            _context.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = CurrentUserId,
                RoleId = 0,
                JoinedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();
            return Ok(new
            {
                id = project.Id,
                name = project.Name,
                description = project.Description,
                createdAt = project.CreatedAt
            });
        }

        #endregion

        #region --- 管理：设置与属性修改 ---

        // 获取项目基础信息与统计
        [HttpGet("{projectId}/settings")]
        public async Task<IActionResult> GetProjectSettings(string projectId)
        {
            var project = await _context.Projects
                .Select(p => new {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.IsPublic,
                    p.JoinPolicy,
                    p.CreatedAt,
                    MemberCount = _context.ProjectMembers.Count(m => m.ProjectId == p.Id),
                    TaskCount = _context.ProjectTasks.Count(t => t.ProjectId == p.Id)
                })
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null) return NotFound();
            if (!await IsMember(projectId)) return Forbid();

            return Ok(project);
        }

        // 🌟 核心：一站式修改项目属性 (名字, 描述, 公开性, 准入策略)
        [HttpPatch("{projectId}")]
        public async Task<IActionResult> UpdateProject(string projectId, [FromBody] UpdateProjectDto dto)
        {
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null) return NotFound();

            // 🔒 权限检查
            var role = await GetUserRole(projectId);
            if (role != 0) return Forbid("只有项目所有者可以修改设置");

            // 更新字段
            if (dto.Name != null) project.Name = dto.Name;
            if (dto.Description != null) project.Description = dto.Description;
            if (dto.IsPublic.HasValue) project.IsPublic = dto.IsPublic.Value;
            if (dto.JoinPolicy.HasValue) project.JoinPolicy = dto.JoinPolicy.Value;
            if (dto.StartTime.HasValue) project.StartTime = dto.StartTime;
            if (dto.EndTime.HasValue) project.EndTime = dto.EndTime;
            if (dto.Status.HasValue) project.Status = dto.Status.Value;

            await _context.SaveChangesAsync();

            // 🌟 关键修改：只返回前端需要的扁平化数据，避开 Members 导航属性
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

            _context.Projects.Remove(project); // 依赖于 DbContext 中的级联删除配置
            await _context.SaveChangesAsync();
            return Ok("项目已从灵脉中抹除");
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