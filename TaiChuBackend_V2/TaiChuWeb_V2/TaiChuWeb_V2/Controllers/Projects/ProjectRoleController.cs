using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Project;
using TaiChuWeb_V2.Services.Project;

namespace TaiChuWeb_V2.Controllers.Projects
{
    [Authorize]
    [ApiController]
    [Route("api/project/{projectId}/roles")]
    public class ProjectRoleController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IProjectPermissionService _permService;

        public ProjectRoleController(AppDbContext context, IProjectPermissionService permService)
        {
            _context = context;
            _permService = permService;
        }

        private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        #region --- 1. 获取当前用户在该项目中的有效权限清单 ---

        /// <summary>
        /// 获取当前登录人在当前项目中的权限列表（前端用来动态控制各种按钮与操作）
        /// </summary>
        [HttpGet("my-permissions")]
        public async Task<IActionResult> GetMyPermissions(string projectId)
        {
            var isMember = await _context.ProjectMembers
                .AnyAsync(m => m.ProjectId == projectId && m.UserId == CurrentUserId);

            var isOwner = await _permService.IsProjectOwnerAsync(projectId, CurrentUserId);

            if (!isMember && !isOwner)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "您不是该项目成员，无法获取权限信息" });
            }

            var permissions = await _permService.GetUserPermissionsAsync(projectId, CurrentUserId);

            return Ok(new
            {
                isOwner,
                permissions
            });
        }

        #endregion

        #region --- 2. 获取系统全量原子权限定义字典 ---

        /// <summary>
        /// 获取系统所有可选的权限定义节点（供管理员在新增/编辑身份时展示复选框）
        /// </summary>
        [HttpGet("permission-definitions")]
        public async Task<IActionResult> GetPermissionDefinitions(string projectId)
        {
            var definitions = await _context.ProjectPermissionDefinitions
                .OrderBy(d => d.Module)
                .Select(d => new
                {
                    d.Code,
                    d.Name,
                    d.Module,
                    d.Description
                })
                .ToListAsync();

            return Ok(definitions);
        }

        #endregion

        #region --- 3. 项目身份组 CRUD ---

        /// <summary>
        /// 获取当前项目可用的所有身份组（包含系统预置与项目专属身份）
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetProjectRoles(string projectId)
        {
            var roles = await _context.ProjectCustomRoles
                .Where(r => r.ProjectId == projectId || r.ProjectId == null)
                .OrderByDescending(r => r.IsSystemDefault)
                .ThenBy(r => r.CreatedAt)
                .ToListAsync();

            var result = roles.Select(r => new
            {
                r.Id,
                r.ProjectId,
                r.Name,
                r.ColorCode,
                r.IsSystemDefault,
                Permissions = JsonSerializer.Deserialize<List<string>>(r.PermissionsJson) ?? new List<string>(),
                r.CreatedAt
            });

            return Ok(result);
        }

        /// <summary>
        /// 为该项目创建新的自定义身份组
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateProjectRole(string projectId, [FromBody] CreateProjectRoleDto dto)
        {
            // 只有项目拥有者或有 member:manage 权限的人能管理身份组
            if (!await _permService.HasPermissionAsync(projectId, CurrentUserId, "member:manage"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "无权配置项目身份组" });
            }

            if (string.IsNullOrWhiteSpace(dto.Name))
            {
                return BadRequest("身份组名称不能为空");
            }

            var role = new ProjectCustomRole
            {
                ProjectId = projectId,
                Name = dto.Name.Trim(),
                ColorCode = dto.ColorCode ?? "#1a1a1a",
                IsSystemDefault = false,
                PermissionsJson = JsonSerializer.Serialize(dto.Permissions ?? new List<string>()),
                CreatedAt = DateTime.UtcNow
            };

            _context.ProjectCustomRoles.Add(role);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                role.Id,
                role.Name,
                role.ColorCode,
                role.PermissionsJson
            });
        }

        /// <summary>
        /// 更新项目身份组的名称、颜色与勾选的权限点
        /// </summary>
        [HttpPut("{roleId}")]
        public async Task<IActionResult> UpdateProjectRole(string projectId, string roleId, [FromBody] UpdateProjectRoleDto dto)
        {
            if (!await _permService.HasPermissionAsync(projectId, CurrentUserId, "member:manage"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "无权修改项目身份组" });
            }

            var role = await _context.ProjectCustomRoles
                .FirstOrDefaultAsync(r => r.Id == roleId && (r.ProjectId == projectId || r.ProjectId == null));

            if (role == null)
            {
                return NotFound("未寻得该身份组");
            }

            if (role.IsSystemDefault && role.ProjectId == null)
            {
                return BadRequest("全站预置的基础基准身份不可被直接篡改");
            }

            if (!string.IsNullOrWhiteSpace(dto.Name)) role.Name = dto.Name.Trim();
            if (!string.IsNullOrWhiteSpace(dto.ColorCode)) role.ColorCode = dto.ColorCode;
            if (dto.Permissions != null)
            {
                role.PermissionsJson = JsonSerializer.Serialize(dto.Permissions);
            }

            await _context.SaveChangesAsync();
            return Ok(role);
        }

        /// <summary>
        /// 删除自定义身份组
        /// </summary>
        [HttpDelete("{roleId}")]
        public async Task<IActionResult> DeleteProjectRole(string projectId, string roleId)
        {
            if (!await _permService.HasPermissionAsync(projectId, CurrentUserId, "member:manage"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "无权删除项目身份组" });
            }

            var role = await _context.ProjectCustomRoles
                .FirstOrDefaultAsync(r => r.Id == roleId && r.ProjectId == projectId);

            if (role == null)
            {
                return NotFound("未寻得该身份组或无权删除系统身份");
            }

            _context.ProjectCustomRoles.Remove(role);
            await _context.SaveChangesAsync();

            return Ok("身份组已成功抹除");
        }

        #endregion
    }

    #region --- DTOs 数据传输对象 ---

    public class CreateProjectRoleDto
    {
        public string Name { get; set; } = string.Empty;
        public string? ColorCode { get; set; } = "#1a1a1a";
        public List<string> Permissions { get; set; } = new List<string>();
    }

    public class UpdateProjectRoleDto
    {
        public string? Name { get; set; }
        public string? ColorCode { get; set; }
        public List<string>? Permissions { get; set; }
    }

    #endregion
}