using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Project;
using TaiChuWeb_V2.Dtos.Project;
using ProjectEntity = TaiChuWeb_V2.Models.Project.Project;

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

        private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        #region --- 广场：发现公开项目 ---

        [HttpGet("public")]
        public async Task<IActionResult> GetPublicProjects()
        {
            var projects = await _context.Projects
                .Where(p => p.IsPublic)
                .OrderByDescending(p => p.CreatedAt)
                // 联查 Users 表以获取所有者用户名
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
                    x.OwnerName,
                    x.Project.OwnerId,
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
                    RoleIds = m.RoleIds,
                    m.Project.CreatedAt,
                    OwnerId = m.Project.OwnerId,
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

            if (!Guid.TryParse(CurrentUserId, out var currentUserGuid))
            {
                return Unauthorized("无效的用户身份");
            }

            var userStats = await _context.UserStats
                .FirstOrDefaultAsync(s => s.UserId == currentUserGuid);

            if (userStats == null) return Unauthorized("未寻得您的太初数据，无法校验额度");

            var activeProjectCount = await _context.Projects
                .CountAsync(p => p.OwnerId == currentUserGuid && p.Status != 3);

            if (activeProjectCount >= userStats.MaxProjectCount)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = $"您的活跃灵脉负载已达上限（${activeProjectCount}/${userStats.MaxProjectCount}）。请前往项目配置封存闲置项目释放额度，或去交易行购置更多空间。"
                });
            }

            var currentUser = await _context.Users.FindAsync(currentUserGuid);
            var ownerName = currentUser?.Username ?? "未知创造者";

            var project = new ProjectEntity
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
                OwnerId = currentUserGuid
            };

            _context.Projects.Add(project);

            _context.ProjectMembers.Add(new ProjectMember
            {
                ProjectId = project.Id,
                UserId = CurrentUserId,
                RoleIds = new List<string> { "role_system_owner" },
                JoinedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync();

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
                ownerId = project.OwnerId,
                ownerName = ownerName,
                memberCount = 1
            });
        }

        #endregion

        #region --- 管理：设置与属性修改 ---

        [HttpGet("{projectId}/settings")]
        public async Task<IActionResult> GetProjectSettings(string projectId)
        {
            if (!await IsMember(projectId)) return Forbid();

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
                    // 🌟 新增：读取汇报时间限制
                    p.WeeklyReportDeadline,
                    p.MonthlyReportDeadline,
                    MemberCount = _context.ProjectMembers.Count(m => m.ProjectId == p.Id),
                    TaskCount = _context.ProjectTasks.Count(t => t.ProjectId == p.Id)
                })
                .FirstOrDefaultAsync();

            if (project == null) return NotFound();

            return Ok(project);
        }

        [HttpPatch("{projectId}")]
        public async Task<IActionResult> UpdateProject(string projectId, [FromBody] UpdateProjectDto dto)
        {
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null) return NotFound();

            if (!await IsProjectOwner(projectId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "只有项目所有者可以修改设置" });
            }

            if (dto.Name != null) project.Name = dto.Name;
            if (dto.Description != null) project.Description = dto.Description;
            if (dto.IsPublic.HasValue) project.IsPublic = dto.IsPublic.Value;
            if (dto.JoinPolicy.HasValue) project.JoinPolicy = dto.JoinPolicy.Value;

            if (dto.StartTime.HasValue) project.StartTime = dto.StartTime;
            if (dto.EndTime.HasValue) project.EndTime = dto.EndTime;
            if (dto.Status.HasValue) project.Status = dto.Status.Value;

            // 🌟 新增：更新汇报时间限制
            if (dto.WeeklyReportDeadline.HasValue) project.WeeklyReportDeadline = dto.WeeklyReportDeadline.Value;
            if (dto.MonthlyReportDeadline.HasValue) project.MonthlyReportDeadline = dto.MonthlyReportDeadline.Value;

            await _context.SaveChangesAsync();

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
                // 🌟 新增：返回更新后的时间限制
                project.WeeklyReportDeadline,
                project.MonthlyReportDeadline
            });
        }

        [HttpDelete("{projectId}")]
        public async Task<IActionResult> DeleteProject(string projectId)
        {
            if (!await IsProjectOwner(projectId)) return Forbid();

            var project = await _context.Projects.FindAsync(projectId);
            if (project == null) return NotFound();

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync();

            return Ok("项目已从灵脉中抹除");
        }

        [HttpGet("{projectId}/documents")]
        public async Task<IActionResult> GetProjectDocuments(string projectId)
        {
            if (!await IsMember(projectId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "您尚未加入该协作位面，无法窥探项目长卷" });
            }

            var documents = await _context.ProjectDocuments
                .Where(pd => pd.ProjectId == projectId)
                .Join(
                    _context.Notes,
                    pd => pd.NoteId,
                    n => n.Id.ToString(),
                    (pd, n) => new { pd, n }
                )
                .Join(
                    _context.Users,
                    combined => combined.pd.PinnedByUserId,
                    u => u.Id.ToString(),
                    (combined, u) => new { combined.pd, combined.n, PinnedByUserName = u.Username }
                )
                .OrderByDescending(x => x.pd.PinnedAt)
                .Select(x => new
                {
                    id = x.n.Id,
                    title = string.IsNullOrWhiteSpace(x.n.Title) ? "未命名项目长卷" : x.n.Title,
                    type = x.n.Type,
                    pinnedAt = x.pd.PinnedAt,
                    pinnedByUserName = x.PinnedByUserName
                })
                .ToListAsync();

            return Ok(documents);
        }

        #endregion

        #region --- 辅助校验 ---

        private async Task<bool> IsMember(string projectId) =>
            await _context.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == CurrentUserId);

        private async Task<bool> IsProjectOwner(string projectId)
        {
            if (string.IsNullOrEmpty(CurrentUserId)) return false;

            return await _context.Projects
                .AnyAsync(p => p.Id == projectId && p.OwnerId.ToString() == CurrentUserId);
        }

        #endregion
    }
}