using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Project; // 确保包含 ProjectMember 所在的命名空间
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

        private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        #region --- 1. 获取团队成员列表 ---

        [HttpGet]
        public async Task<IActionResult> GetMembers(string projectId)
        {
            // 🔒 权限检查
            if (!await _context.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == CurrentUserId))
                return Forbid();

            // 🌟 核心修改：确保返回了 RoleId 原始值或明确的 Owner 标识
            var members = await _context.ProjectMembers
                .Where(m => m.ProjectId == projectId)
                .Join(
                    _context.Users,
                    member => member.UserId,
                    user => user.Id.ToString(),
                    (member, user) => new
                    {
                        Id = member.UserId,
                        Name = user.Username,
                        Email = user.Email ?? "暂无邮箱",
                        RoleId = member.RoleId, // 🌟 返回原始 RoleId，方便前端判断
                        IsOwner = member.RoleId == 0, // 🌟 显式返回 Boolean，前端直接 v-if="member.IsOwner"
                        Role = member.RoleId == 0 ? "owner" :
                               member.RoleId == 1 ? "admin" :
                               member.RoleId == 2 ? "editor" :
                               member.RoleId == 3 ? "executor" : "viewer"
                    }
                )
                .ToListAsync();

            return Ok(members);
        }

        #endregion

        #region --- 2. 变更成员角色 ---

        [HttpPut("{memberId}/role")]
        public async Task<IActionResult> UpdateMemberRole(string projectId, string memberId, [FromBody] UpdateRoleDto dto)
        {
            // 🔒 权限检查：只有 Owner(0) 或 Admin(1) 可以调整他人权限
            var myRole = await _context.ProjectMembers
                .Where(m => m.ProjectId == projectId && m.UserId == CurrentUserId)
                .Select(m => (int?)m.RoleId)
                .FirstOrDefaultAsync();

            if (myRole == null || myRole > 1) return Forbid("权限不足，无法更改成员角色");

            var targetMember = await _context.ProjectMembers
                .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == memberId);

            if (targetMember == null) return NotFound("未在项目中找到该成员");
            if (targetMember.RoleId == 0) return BadRequest("超级管理员（创建者）的权限不可被动摇");

            // 更新角色编码
            targetMember.RoleId = dto.RoleValue;
            await _context.SaveChangesAsync();

            return Ok();
        }

        #endregion

        #region --- 3. 移除团队成员 ---

        [HttpDelete("{memberId}")]
        public async Task<IActionResult> RemoveMember(string projectId, string memberId)
        {
            // 🔒 权限检查：只有管理层可以移除成员，且不能移除 Owner
            var myRole = await _context.ProjectMembers
                .Where(m => m.ProjectId == projectId && m.UserId == CurrentUserId)
                .Select(m => (int?)m.RoleId)
                .FirstOrDefaultAsync();

            if (myRole == null || myRole > 1) return Forbid("只有管理员有权移除成员");
            if (memberId == CurrentUserId) return BadRequest("无法自我放逐，请通过解散项目或转让完成");

            var targetMember = await _context.ProjectMembers
                .FirstOrDefaultAsync(m => m.ProjectId == projectId && m.UserId == memberId);

            if (targetMember == null) return NotFound("该成员不在此灵脉中");
            if (targetMember.RoleId == 0) return BadRequest("无法驱逐项目所有者");

            _context.ProjectMembers.Remove(targetMember);

            // 🌟 进阶优雅逻辑：顺便把该成员在看板上被指派的任务改回“未指派”
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

        #region --- 4. 邀请新成员 (通过邮箱) ---

        [HttpPost("invite")]
        public async Task<IActionResult> InviteMember(string projectId, [FromBody] InviteMemberDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.UsernameOrId))
                return BadRequest("邀请目标（用户名或ID）不能为空");

            var input = dto.UsernameOrId.Trim();
            User targetUser = null;

            // 🌟 核心路由双向匹配逻辑
            // Step 1: 尝试解析为 Guid (判断输入的是否是系统分配的 string 型唯一主键 Guid)
            if (Guid.TryParse(input, out Guid parsedGuid))
            {
                targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == parsedGuid);
            }

            // Step 2: 如果输入不是符合格式的 Guid，或者按 Guid 没查出来，则将输入视为 Username 点对点检索
            if (targetUser == null)
            {
                // 对应 User.cs 中的 Username 属性（区分大小写或不区分，取决于你的数据库排序规则）
                targetUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == input);
            }

            if (targetUser == null)
                return NotFound("太初之中未寻得该用户名或用户ID对应的共建者");

            // 验证用户是否已身在此灵脉中
            bool isAlreadyMember = await _context.ProjectMembers
                .AnyAsync(m => m.ProjectId == projectId && m.UserId == targetUser.Id.ToString());
            if (isAlreadyMember)
                return BadRequest("该用户已在共建者团队中，无需重复引入");

            // 生成关联记录，初始赋予最低观察者(4)权限，由管理员后续按需调整
            var newMember = new ProjectMember
            {
                ProjectId = projectId,
                UserId = targetUser.Id.ToString(), // 统一转为 string 型持久化
                JoinedAt = DateTime.UtcNow
            };

            _context.ProjectMembers.Add(newMember);
            await _context.SaveChangesAsync();

            return Ok("成功将该共建者纳入灵脉");
        }

        // 别忘了更新配套的 Dto 类
        public class InviteMemberDto
        {
            public string UsernameOrId { get; set; } = string.Empty;
        }

        #endregion
    }

    #region --- DTOs 数据传输对象 ---

    public class UpdateRoleDto
    {
        public int RoleValue { get; set; }
    }

    public class InviteMemberDto
    {
        public string Email { get; set; } = string.Empty;
    }

    #endregion
}