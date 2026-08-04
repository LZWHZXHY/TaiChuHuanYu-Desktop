using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Plugin;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PluginsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public PluginsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<Plugin>>> GetPlugins()
        {
            // 1. 识别平台环境 (TaiChuDesktop 为桌面端)
            var userAgent = Request.Headers["User-Agent"].ToString();
            bool isDesktop = userAgent.Contains("TaiChuDesktop");

            // 2. 获取当前用户信息与权限
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            bool isAuthenticated = Guid.TryParse(userIdClaim, out var userId);

            // 如果已登录，预加载该用户的所有权限
            var userPermissions = isAuthenticated
                ? await _context.UserPermissions
                    .Where(p => p.UserId == userId)
                    .Select(p => p.Permission)
                    .ToListAsync()
                : new List<AdminPermission>();

            // 3. 构建基础查询：平台过滤 + 排序
            var query = _context.Plugins
                .Where(p => p.PlatformScope == 0 || (isDesktop ? p.PlatformScope == 2 : p.PlatformScope == 1))
                .OrderBy(p => p.Order);

            var allPlugins = await query.ToListAsync();

            // ✅ 判断是否是 SuperAdmin
            var isSuperAdmin = userPermissions.Contains(AdminPermission.SuperAdmin);

            // 4. 核心过滤：根据身份和权限剔除无权查看的菜单
            var filteredPlugins = allPlugins.Where(p =>
            {
                // 如果该项不需要登录，直接显示
                if (!p.RequiresAuth) return true;

                // 如果需要登录但未登录，直接剔除
                if (!isAuthenticated) return false;

                // ✅ 特殊处理：管理面板 (URL 包含 /admin)
                if (p.Url.StartsWith("/admin") || p.Name == "管理面板")
                {
                    // ✅ 只有 SuperAdmin 才能看见管理面板
                    return isSuperAdmin;
                }

                // 其他需要登录的项（如个人中心、交易行）
                return true;
            });

            return Ok(filteredPlugins);
        }
    }
}