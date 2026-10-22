using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers.Admin
{
    [Authorize] // 基础门槛：必须登录
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 获取当前用户有权访问的后台导航菜单
        /// </summary>
        [HttpGet("navigation")]
        public async Task<IActionResult> GetAdminNavigation()
        {
            var userId = GetUserId();
            // 1. 从数据库获取该用户拥有的所有独立权限
            var permissions = await _context.UserPermissions
                .Where(p => p.UserId == userId)
                .Select(p => p.Permission)
                .ToListAsync();

            // 如果没有任何管理权限，直接返回 403
            if (!permissions.Any()) return Forbid();

            // 2. 定义原始导航数据 (你可以从配置文件读取，这里为了演示直接硬编码)
            var allMenus = GetRawNavigationData();

            // 3. 🌟 核心：权限过滤逻辑
            var filteredMenus = allMenus.Where(menu =>
            {
                // 超级管理员通行一切
                if (permissions.Contains(AdminPermission.SuperAdmin)) return true;

                // 针对不同模块的匹配
                return menu.Url switch
                {
                    "/admin/trade" => permissions.Contains(AdminPermission.Trade_Manage),
                    "/admin/user" => permissions.Contains(AdminPermission.User_Audit),
                    "/admin/lingmai" => permissions.Contains(AdminPermission.System_Monitor),
                    "/admin/wiki" => permissions.Contains(AdminPermission.Wiki_Editor),
                    _ => true // 默认首页或概览通常允许所有级别管理员查看
                };
            }).OrderBy(m => m.Order);

            return Ok(filteredMenus);
        }

        /// <summary>
        /// 检查当前用户是否具备进入管理系统的资格
        /// </summary>
        [HttpGet("check-status")]
        public async Task<IActionResult> CheckAdminStatus()
        {
            var userId = GetUserId();
            var hasPermission = await _context.UserPermissions.AnyAsync(p => p.UserId == userId);

            return Ok(new
            {
                isAdmin = hasPermission,
                timestamp = DateTime.Now
            });
        }

        private Guid GetUserId()
        {
            var idClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(idClaim, out var guid) ? guid : Guid.Empty;
        }

        private List<AdminMenuDto> GetRawNavigationData()
        {
            // 这里对应你之前给我的 JSON 结构
            return new List<AdminMenuDto>
            {
                new() { Id = 1, Name = "系统概览", Url = "/admin/overview", Order = 1 },
                new() { Id = 2, Name = "资源交易管理", Url = "/admin/trade", Order = 2 },
                new() { Id = 3, Name = "用户审计中心", Url = "/admin/user", Order = 3 },
                new() { Id = 4, Name = "灵脉逻辑监控", Url = "/admin/lingmai", Order = 4 },
                new() { Id = 5, Name = "知识库修订", Url = "/admin/wiki", Order = 5 }
            };
        }
    }

    public class AdminMenuDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public int Order { get; set; }
    }
}