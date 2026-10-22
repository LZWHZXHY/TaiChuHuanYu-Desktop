using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Services.Wiki;

public static class WikiAccess
{
    public static async Task<bool> CanManageAsync(AppDbContext db, ClaimsPrincipal user, int? categoryId = null)
    {
        var id = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(id, out var userId)) return false;
        if (user.IsInRole("Admin") || await db.UserPermissions.AnyAsync(p => p.UserId == userId &&
            (p.Permission == AdminPermission.SuperAdmin || p.Permission == AdminPermission.TaiChu || p.Permission == AdminPermission.Wiki_Editor))) return true;
        return categoryId.HasValue && await db.WikiCategories.AnyAsync(c => c.Id == categoryId && c.OwnerId == id);
    }
}
