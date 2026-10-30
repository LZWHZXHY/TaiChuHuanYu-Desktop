using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Services.Analytics;

namespace TaiChuWeb_V2.Controllers
{
    /// <summary>
    /// 在线心跳：前端页面可见时每分钟调用一次。
    /// 登录用户已由 ActivityTracking 中间件记录；这里只额外记录未登录的游客（匿名编号，不含个人信息）。
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class PresenceController(PresenceTracker tracker) : ControllerBase
    {
        public sealed class PingDto
        {
            public string? VisitorId { get; set; }
        }

        [HttpPost("ping")]
        public IActionResult Ping([FromBody] PingDto? dto)
        {
            if (User.Identity?.IsAuthenticated != true && PresenceTracker.IsValidVisitorId(dto?.VisitorId))
                tracker.TouchVisitor(dto!.VisitorId!);
            return NoContent();
        }
    }
}
