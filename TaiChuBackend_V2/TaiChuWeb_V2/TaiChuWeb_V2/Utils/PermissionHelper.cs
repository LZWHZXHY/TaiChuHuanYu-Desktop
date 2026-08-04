using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Utils
{
    public static class PermissionHelper
    {
        public static async Task<bool> HasPermission(
            AppDbContext context,
            Guid userId,
            AdminPermission requiredPermission)
        {
            // 检查是否拥有 SuperAdmin（拥有所有权限）
            var isSuperAdmin = await context.UserPermissions
                .AnyAsync(p => p.UserId == userId && p.Permission == AdminPermission.SuperAdmin);
            if (isSuperAdmin)
                return true;

            // 检查是否拥有指定的具体权限
            return await context.UserPermissions
                .AnyAsync(p => p.UserId == userId && p.Permission == requiredPermission);
        }
    }
}