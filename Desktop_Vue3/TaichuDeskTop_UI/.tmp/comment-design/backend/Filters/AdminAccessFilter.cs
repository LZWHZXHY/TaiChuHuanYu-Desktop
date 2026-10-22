using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Filters;

// 后台接口按数据库中的现行权限授权，不能依赖前端隐藏菜单。
public sealed class AdminAccessFilter(AppDbContext db) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.ActionDescriptor is not ControllerActionDescriptor action) return;
        var administration = action.ControllerTypeInfo.Namespace == "TaiChuWeb_V2.Controllers.Admin";
        var sharedController = action.ControllerTypeInfo.Namespace == "TaiChuWeb_V2.Controllers" && action.ControllerName switch
        {
            "News" => action.ActionName != "GetAllNews",
            "Event" => action.ActionName != "GetMonthEvents",
            "Feedback" => action.ActionName is not ("SubmitFeedback" or "GetPublicFeedbacks"),
            _ => false
        };
        if (!administration && !sharedController) return;
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true || !Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id))
        { context.Result = new UnauthorizedResult(); return; }
        if (action.ControllerName == "Admin" && action.ActionName == "CheckAdminStatus") return;
        var permissions = await db.UserPermissions.Where(p => p.UserId == id).Select(p => p.Permission).ToListAsync();
        if (permissions.Contains(AdminPermission.SuperAdmin)) return;
        AdminPermission? required = action.ControllerName switch
        {
            "AdminTrade" or "AdminProduct" => AdminPermission.Trade_Manage,
            "AdminUser" when action.ActionName != "UpdatePermissions" => AdminPermission.User_Audit,
            "AdminWiki" => AdminPermission.Wiki_Editor,
            "AdminData" => AdminPermission.System_Monitor,
            _ => null
        };
        var canNavigate = action.ControllerName == "Admin" && permissions.Any(p => p is AdminPermission.Trade_Manage or AdminPermission.User_Audit or AdminPermission.Wiki_Editor or AdminPermission.System_Monitor);
        if (!canNavigate && (required == null || !permissions.Contains(required.Value))) context.Result = new ForbidResult();
    }
}
