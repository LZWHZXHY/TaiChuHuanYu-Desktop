using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Plugin;

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
            var userAgent = Request.Headers["User-Agent"].ToString();
            bool isDesktop = userAgent.Contains("TaiChuDesktop");

            // 根据 PlatformScope 过滤，并按照 Order 升序排列
            var plugins = await _context.Plugins
                .Where(p => p.PlatformScope == 0 || (isDesktop ? p.PlatformScope == 2 : p.PlatformScope == 1))
                .OrderBy(p => p.Order) // 确保顺序
                .ToListAsync();

            return Ok(plugins);
        }
    }
}