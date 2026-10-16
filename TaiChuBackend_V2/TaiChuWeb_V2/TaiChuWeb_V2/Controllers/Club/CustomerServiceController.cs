using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers.Club
{
    /// <summary>
    /// 客服公开接口
    /// 路由前缀：api/club/customer-services
    /// </summary>
    [ApiController]
    [Route("api/club/customer-services")]
    public class CustomerServiceController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CustomerServiceController(AppDbContext db) => _db = db;

        /// <summary>
        /// 客服列表（含在线状态、当前载单）
        /// GET /api/club/customer-services
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetList()
        {
            var list = await _db.CustomerServices
                .AsNoTracking()
               .Where(c => c.IsActive && c.AuditStatus == "approved")
                .OrderBy(c => c.SortOrder).ThenBy(c => c.CreatedAt)
                .Select(c => new
                {
                    id = c.Id,
                    name = c.Name,
                    wechat = c.Wechat,
                    avatar = c.Avatar,
                    status = c.Status,                // online / busy / offline
                    currentLoad = c.CurrentLoad,
                    capacity = c.Capacity,
                    isAssignable = c.IsAssignable
                })
                .ToListAsync();

            return Ok(list);
        }
    }
}