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
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers.Projects
{
    [Authorize]
    [ApiController]
    [Route("api/project/{projectId}/members")]
    public class ProjectMemberController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProjectMemberController(AppDbContext context)
        {
            _context = context;
        }

        private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        #region --- 1. 获取团队成员列表 ---

        [HttpGet]
        public async Task<IActionResult> GetMembers(string projectId)
        {
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null) return NotFound("未寻得指定的项目灵脉");

            // 权限检查：当前用户必须在项目中，或者当前用户就是该项目所有者
            bool isProjectOwner = string.Equals(project.OwnerId.ToString(), CurrentUserId, StringComparison.OrdinalIgnoreCase);
            bool isMember = await _context.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == CurrentUserId);

            if (!isMember && !isProjectOwner)
                return Forbid();

            var membersList = await _context.ProjectMembers
                .Where(m => m.ProjectId == projectId)
                .Join(
                    _context.Users,
                    member => member.UserId,
                    user => user.Id.ToString(),
                    (member, user) => new
                    {
                        Member = member,
                        Username = user.Username,
                        Email = user.Email ?? "暂无邮箱"
                    }
                )
                .ToListAsync();

            var ownerIdStr = project.OwnerId.ToString();

            // 🌟 核心防错加固：在内存中统一使用 OrdinalIgnoreCase 比对 Guid 字符串
            var result = membersList.Select(x =>
            {
                bool isOwner = string.Equals(ownerIdStr, x.Member.UserId, StringComparison.OrdinalIgnoreCase);

                // 如果是所有者且原本角色列表为空，自动追加 owner 标识供前端识别
                var roles = x.Member.RoleIds ?? new List<string>();
                if (isOwner && !roles.Contains("role_system_owner"))
                {
                    roles.Insert(0, "role_system_owner");
                }

                return new
                {
                    Id = x.Member.UserId,
                    Name = x.Username,
                    Email = x.Email,
                    RoleIds = roles,
                    IsOwner = isOwner,
                    Role = isOwner ? "owner" : "member"
                };
            }).ToList();

            return Ok(result);
        }

        #endregion

        #region --- 2. 变更成员角色身份组 ---

        [HttpPut("{memberId}/role")]
        public async Task<IActionResult> UpdateMemberRole(string projectId, string memberId, [FromBody] UpdateMemberRolesDto dto)
        {
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null) return NotFound("未寻得指定的项目灵脉");

            // 只有项目拥有者有权调整其他成员的身份组 (忽略大小写比对)
            if (!string.Equals(project.OwnerId.ToString(), CurrentUserId, StringComparison.OrdinalIgnoreCase))
                return Forbid("权限不足，只有项目掌舵人可以调整成员身份组");

            var targetMember = await _context.ProjectMembers
                .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == memberId);

            if (targetMember == null) return NotFound("未在项目中找到该成员");

            // 禁止修改项目所有者自身的身份
            if (string.Equals(project.OwnerId.ToString(), memberId, StringComparison.OrdinalIgnoreCase))
                return BadRequest("超级管理员（创建者）的权限不可被调整");

            // 更新身份组 ID 列表
            targetMember.RoleIds = dto.RoleIds ?? new List<string>();
            await _context.SaveChangesAsync();

            return Ok();
        }

        #endregion

        #region --- 3. 移除团队成员 ---

        [HttpDelete("{memberId}")]
        public async Task<IActionResult> RemoveMember(string projectId, string memberId)
        {
            var project = await _context.Projects.FindAsync(projectId);
            if (project == null) return NotFound("未寻得指定的项目灵脉");

            // 只有项目所有者有权移除成员
            if (!string.Equals(project.OwnerId.ToString(), CurrentUserId, StringComparison.OrdinalIgnoreCase))
                return Forbid("只有项目管理员/所有者有权移除成员");

            if (string.Equals(memberId, CurrentUserId, StringComparison.OrdinalIgnoreCase))
                return BadRequest("无法自我放逐，请通过解散项目或转让完成");

            var targetMember = await _context.ProjectMembers
                .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == memberId);

            if (targetMember == null) return NotFound("该成员不在此灵脉中");

            if (string.Equals(project.OwnerId.ToString(), memberId, StringComparison.OrdinalIgnoreCase))
                return BadRequest("无法驱逐项目所有者");

            _context.ProjectMembers.Remove(targetMember);

            // 成员移出后，将其指派的任务重置为未指派
            var linkedTasks = await _context.ProjectTasks
                .Where(t => t.ProjectId == projectId && t.AssigneeId == memberId)
                .ToListAsync();

            foreach (var task in linkedTasks)
            {
                task.AssigneeId = null;
            }

            await _context.SaveChangesAsync();
            return Ok("成员已成功移出项目");
        }

        #endregion


        #region --- 3.5 实时模糊检索候选共建者 ---

        [HttpGet("search-candidates")]
        public async Task<IActionResult> SearchCandidates(string projectId, [FromQuery] string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword) || keyword.Trim().Length < 1)
            {
                return Ok(new List<object>());
            }

            var query = keyword.Trim();

            // 查出该项目已经存在的成员 ID 列表，用于排除
            var existingMemberIds = await _context.ProjectMembers
                .Where(m => m.ProjectId == projectId)
                .Select(m => m.UserId)
                .ToListAsync();

            // 实时联查 Users 表，匹配用户名或 GUID，排除已在项目中的成员，最多返回 8 条
            var candidates = await _context.Users
                .Where(u => !existingMemberIds.Contains(u.Id.ToString()) &&
                            (EF.Functions.Like(u.Username, $"%{query}%") || u.Id.ToString() == query))
                .Select(u => new
                {
                    Id = u.Id.ToString(),
                    Username = u.Username,
                    Email = u.Email ?? "暂无邮箱"
                })
                .Take(8)
                .ToListAsync();

            return Ok(candidates);
        }

        #endregion















        #region --- 4. 邀请新成员 ---

        [HttpPost("invite")]
        public async Task<IActionResult> InviteMember(string projectId, [FromBody] InviteMemberDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.UsernameOrId))
                return BadRequest("邀请目标（用户名或ID）不能为空");

            var input = dto.UsernameOrId.Trim();
            User? targetUser = null;

            // Step 1: 尝试解析为 Guid
            if (Guid.TryParse(input, out Guid parsedGuid))
            {
                targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == parsedGuid);
            }

            // Step 2: 按用户名检索
            if (targetUser == null)
            {
                targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == input);
            }

            if (targetUser == null)
                return NotFound("太初之中未寻得该用户名或用户ID对应的共建者");

            bool isAlreadyMember = await _context.ProjectMembers
                .AnyAsync(m => m.ProjectId == projectId && m.UserId == targetUser.Id.ToString());

            if (isAlreadyMember)
                return BadRequest("该用户已在共建者团队中，无需重复引入");

            // 新成员默认赋予观察者身份模板
            var newMember = new ProjectMember
            {
                ProjectId = projectId,
                UserId = targetUser.Id.ToString(),
                RoleIds = new List<string> { "role_system_viewer" },
                JoinedAt = DateTime.UtcNow
            };

            _context.ProjectMembers.Add(newMember);
            await _context.SaveChangesAsync();

            return Ok("成功将该共建者纳入灵脉");
        }

        #endregion
    }

    #region --- DTOs 数据传输对象 ---

    public class UpdateMemberRolesDto
    {
        public List<string> RoleIds { get; set; } = new List<string>();
    }

    public class InviteMemberDto
    {
        public string UsernameOrId { get; set; } = string.Empty;
    }

    #endregion
}