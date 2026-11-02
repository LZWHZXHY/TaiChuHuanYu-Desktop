using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Analytics;

namespace TaiChuWeb_V2.Controllers
{
    /// <summary>
    /// 在线心跳：前端页面可见时每分钟调用一次。
    /// 登录用户已由 ActivityTracking 中间件记录；这里额外记录未登录的游客（匿名编号，不含个人信息），
    /// 以及登录用户「正在看哪个板块」（社区成员页显示，隐身的人不显示）。
    /// 另外：GET / PUT api/presence/me 读取、切换自己的隐身设置。
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [AllowAnonymous]
    public class PresenceController(PresenceTracker tracker, AppDbContext db) : ControllerBase
    {
        public sealed class PingDto
        {
            public string? VisitorId { get; set; }
            /// <summary>正在看的板块（chat、gallery、world……），前端按地址算出来</summary>
            public string? Section { get; set; }
        }
        public sealed class PrefsDto { public bool Invisible { get; set; } }

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        [HttpPost("ping")]
        public IActionResult Ping([FromBody] PingDto? dto)
        {
            if (CurrentUserId is Guid me) tracker.SetSection(me, dto?.Section);
            else if (PresenceTracker.IsValidVisitorId(dto?.VisitorId))
                tracker.TouchVisitor(dto!.VisitorId!);
            return NoContent();
        }

        [HttpGet("me")]
        [Authorize]
        public IActionResult Me() => CurrentUserId is Guid me ? Ok(new { invisible = tracker.IsInvisible(me) }) : Unauthorized();

        /// <summary>隐身开关：存进 user_presence_prefs 表，同时更新内存（马上生效）</summary>
        [HttpPut("me")]
        [Authorize]
        public async Task<IActionResult> SetMe([FromBody] PrefsDto dto)
        {
            if (CurrentUserId is not Guid me) return Unauthorized();
            var id = me.ToString();
            var flag = dto.Invisible ? 1 : 0;
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"INSERT INTO user_presence_prefs (UserId, Invisible, UpdatedAt) VALUES ({id}, {flag}, UTC_TIMESTAMP(6)) ON DUPLICATE KEY UPDATE Invisible = VALUES(Invisible), UpdatedAt = VALUES(UpdatedAt)");
            tracker.SetInvisible(me, dto.Invisible);
            return Ok(new { invisible = dto.Invisible, message = dto.Invisible ? "已隐身：别人在社区成员里看不到你在线，也看不到你最近来过" : "已取消隐身" });
        }
    }
}
