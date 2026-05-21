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
                .Select(p => new {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.Status,
                    p.StartTime,
                    p.EndTime,
                    p.CreatedAt,
                    MemberCount = _context.ProjectMembers.Count(m => m.ProjectId == p.Id),
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
                    m.Project.Status,
                    m.Project.StartTime,
                    m.Project.EndTime,
                    m.RoleId,
                    m.Project.CreatedAt,
                    MemberCount = _context.ProjectMembers.Count(pm => pm.ProjectId == m.Project.Id)
                })
                .ToListAsync();
            return Ok(projects);
        }

        [HttpPost("create")]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

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

            // 🌟 修复：补全返回字段，供前端页面跳转后的顶层组件及看板平稳渲染
            return Ok(new
            {
                id = project.Id,
                name = project.Name,
                description = project.Description,
                isPublic = project.IsPublic,
                joinPolicy = project.JoinPolicy,
                status = project.Status,
                startTime = project.StartTime,
                endTime = project.EndTime,
                createdAt = project.CreatedAt,
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
            if (role != 0) return Forbid("只有项目所有者可以修改设置");

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