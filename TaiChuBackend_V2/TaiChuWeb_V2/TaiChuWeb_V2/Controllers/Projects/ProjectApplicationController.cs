using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Project;

namespace TaiChuWeb_V2.Controllers.Projects
{
    [Authorize]
    [ApiController]
    [Route("api/project/{projectId}/applications")]
    public class ProjectApplicationController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProjectApplicationController(AppDbContext context)
        {
            _context = context;
        }

        private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        #region --- 视角 A：申请人主动加入或投递逻辑 ---

        [HttpPost("join")]
        public async Task<IActionResult> JoinProject(string projectId, [FromBody] SubmitApplicationDto dto)
        {
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null)
            {
                return NotFound("未寻得指定的项目灵脉");
            }

            var isMember = await _context.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == CurrentUserId);
            if (isMember)
            {
                return BadRequest("您已在此灵脉中共建，无需重复加入");
            }

            if (project.JoinPolicy == 0)
            {
                return BadRequest("该项目隐匿于现世（仅限邀请），无法主动申请");
            }

            if (project.JoinPolicy == 2)
            {
                _context.ProjectMembers.Add(new ProjectMember
                {
                    ProjectId = projectId,
                    UserId = CurrentUserId,
                    RoleId = 1,
                    JoinedAt = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();
                return Ok(new { status = "joined", message = "已直接融入该项目灵脉" });
            }

            if (project.JoinPolicy == 1)
            {
                var hasPending = await _context.ProjectApplications.AnyAsync(a => a.ProjectId == projectId && a.UserId == CurrentUserId && a.Status == 0);
                if (hasPending)
                {
                    return BadRequest("您的申请正在传递中，请勿重复传书");
                }

                var application = new ProjectApplication
                {
                    ProjectId = projectId,
                    UserId = CurrentUserId,
                    Message = dto.Message ?? "愿共建此项目。",
                    Status = 0,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ProjectApplications.Add(application);
                await _context.SaveChangesAsync();
                return Ok(new { status = "pending", message = "申请已传书至项目掌控者，请静候回复" });
            }

            return BadRequest("未知的准入策略");
        }

        #endregion

        #region --- 视角 B：管理者审批逻辑 ---

        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingApplications(string projectId)
        {
            if (!await IsManager(projectId))
            {
                return Forbid(); // ✅ 安全返回 403
            }

            var applications = await _context.ProjectApplications
                .Where(a => a.ProjectId == projectId && a.Status == 0)
                .Join(_context.Users,
                    app => app.UserId,
                    user => user.Id.ToString(),
                    (app, user) => new {
                        app.Id,
                        app.UserId,
                        ApplicantName = user.Username,
                        ApplicantEmail = user.Email ?? "暂无邮箱",
                        app.Message,
                        app.CreatedAt
                    })
                .OrderBy(a => a.CreatedAt)
                .ToListAsync();

            return Ok(applications);
        }

        [HttpPut("{applicationId}/handle")]
        public async Task<IActionResult> HandleApplication(string projectId, string applicationId, [FromBody] HandleApplicationDto dto)
        {
            if (!await IsManager(projectId))
            {
                return Forbid(); // ✅ 安全返回 403
            }

            var app = await _context.ProjectApplications.FirstOrDefaultAsync(a => a.Id == applicationId && a.ProjectId == projectId);
            if (app == null)
            {
                return NotFound("未寻得该申请记录");
            }
            if (app.Status != 0)
            {
                return BadRequest("该申请已被裁决，无法篡改状态");
            }

            if (dto.Approve)
            {
                app.Status = 1;

                var alreadyMember = await _context.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == app.UserId);
                if (!alreadyMember)
                {
                    _context.ProjectMembers.Add(new ProjectMember
                    {
                        ProjectId = projectId,
                        UserId = app.UserId,
                        RoleId = 1,
                        JoinedAt = DateTime.UtcNow
                    });
                }
            }
            else
            {
                app.Status = 2;
            }

            app.ProcessedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(new { message = dto.Approve ? "已接纳该共建者入脉" : "已婉拒该申请" });
        }

        #endregion

        #region --- 辅助方法 ---

        private async Task<bool> IsManager(string projectId)
        {
            var myRole = await _context.ProjectMembers
                .Where(m => m.ProjectId == projectId && m.UserId == CurrentUserId)
                .Select(m => (int?)m.RoleId)
                .FirstOrDefaultAsync();

            return myRole != null && myRole == 0;
        }

        #endregion
    }

    #region --- 配套 Dto 传输对象 ---

    public class SubmitApplicationDto
    {
        public string? Message { get; set; }
    }

    public class HandleApplicationDto
    {
        public bool Approve { get; set; }
    }

    #endregion
}