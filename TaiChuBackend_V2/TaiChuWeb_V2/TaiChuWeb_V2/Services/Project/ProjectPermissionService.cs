using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Admin;
using TaiChuWeb_V2.Models.Project;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Services.Project
{
    public interface IProjectPermissionService
    {
        Task<bool> IsProjectOwnerAsync(string projectId, string userId);
        Task<bool> HasPermissionAsync(string projectId, string userId, string permissionCode);
        Task<List<string>> GetUserPermissionsAsync(string projectId, string userId);
    }

    public class ProjectPermissionService : IProjectPermissionService
    {
        private readonly AppDbContext _context;

        public ProjectPermissionService(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 判定当前用户是否为该项目的所有者 (Owner)
        /// </summary>
        public async Task<bool> IsProjectOwnerAsync(string projectId, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(projectId))
                return false;

            return await _context.Projects
                .AnyAsync(p => p.Id == projectId && p.OwnerId.ToString() == userId);
        }

        /// <summary>
        /// 判定用户在当前项目中是否具备某一特定的原子权限 Code（如: task:manage, report:submit）
        /// </summary>
        public async Task<bool> HasPermissionAsync(string projectId, string userId, string permissionCode)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(projectId))
                return false;

            // 1. 项目所有者 (Owner) 拥有最高绝对通行权
            if (await IsProjectOwnerAsync(projectId, userId))
                return true;

            // 2. 全局特权管理员通行（SuperAdmin / TaiChu 身份直通）
            if (Guid.TryParse(userId, out var userGuid))
            {
                var isGlobalAdmin = await _context.UserPermissions.AnyAsync(up =>
                    up.UserId == userGuid &&
                    (up.Permission == AdminPermission.SuperAdmin || up.Permission == AdminPermission.TaiChu));

                if (isGlobalAdmin)
                    return true;
            }

            // 3. 读取该成员记录绑定的身份组列表
            var member = await _context.ProjectMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);

            if (member == null)
                return false;

            var roleIds = member.RoleIds;
            if (roleIds == null || roleIds.Count == 0)
                return false;

            // 4. 查询绑定的所有身份组实体
            var roles = await _context.ProjectCustomRoles
                .AsNoTracking()
                .Where(r => roleIds.Contains(r.Id))
                .ToListAsync();

            // 5. 遍历比对权限点
            foreach (var role in roles)
            {
                if (string.IsNullOrWhiteSpace(role.PermissionsJson))
                    continue;

                var perms = JsonSerializer.Deserialize<List<string>>(role.PermissionsJson);
                if (perms != null && (perms.Contains(permissionCode) || perms.Contains("*")))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 汇总用户在当前项目中的所有原子权限清单（用于前端按权限渲染按钮/视图）
        /// </summary>
        public async Task<List<string>> GetUserPermissionsAsync(string projectId, string userId)
        {
            if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(projectId))
                return new List<string>();

            // 如果是项目创建者，直接返回通配符 *
            if (await IsProjectOwnerAsync(projectId, userId))
            {
                return new List<string> { "*" };
            }

            var member = await _context.ProjectMembers
                .AsNoTracking()
                .FirstOrDefaultAsync(pm => pm.ProjectId == projectId && pm.UserId == userId);

            if (member == null)
                return new List<string>();

            var roleIds = member.RoleIds;
            if (roleIds == null || roleIds.Count == 0)
                return new List<string>();

            var roles = await _context.ProjectCustomRoles
                .AsNoTracking()
                .Where(r => roleIds.Contains(r.Id))
                .ToListAsync();

            var resultSet = new HashSet<string>();
            foreach (var role in roles)
            {
                if (string.IsNullOrWhiteSpace(role.PermissionsJson))
                    continue;

                var perms = JsonSerializer.Deserialize<List<string>>(role.PermissionsJson);
                if (perms != null)
                {
                    foreach (var p in perms)
                    {
                        resultSet.Add(p);
                    }
                }
            }

            return resultSet.ToList();
        }
    }
}