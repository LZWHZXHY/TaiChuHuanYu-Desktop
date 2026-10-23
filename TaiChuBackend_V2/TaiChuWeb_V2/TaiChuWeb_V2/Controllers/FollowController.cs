using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Interact;

namespace TaiChuWeb_V2.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class FollowController : ControllerBase
    {
        private readonly AppDbContext _context;
        public FollowController(AppDbContext context) { _context = context; }

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        // ===== 关注 / 取消关注 =====
        [HttpPost("{followeeId:guid}")]
        public async Task<IActionResult> Toggle(Guid followeeId)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            if (followeeId == CurrentUserId.Value) return BadRequest(new { message = "不能关注自己" });

            var existing = await _context.Follows
                .FirstOrDefaultAsync(f => f.FollowerId == CurrentUserId.Value && f.FolloweeId == followeeId);

            bool following;
            if (existing != null) { _context.Follows.Remove(existing); following = false; }
            else { _context.Follows.Add(new Follow { FollowerId = CurrentUserId.Value, FolloweeId = followeeId }); following = true; }

            await _context.SaveChangesAsync();
            var followerCount = await _context.Follows.CountAsync(f => f.FolloweeId == followeeId);
            return Ok(new { following, followerCount });
        }

        // ===== 关注状态（匿名可查） =====
        [HttpGet("status/{followeeId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> Status(Guid followeeId)
        {
            bool following = CurrentUserId.HasValue && await _context.Follows
                .AnyAsync(f => f.FollowerId == CurrentUserId.Value && f.FolloweeId == followeeId);
            var followerCount = await _context.Follows.CountAsync(f => f.FolloweeId == followeeId);
            var followingCount = await _context.Follows.CountAsync(f => f.FollowerId == followeeId);
            return Ok(new { following, followerCount, followingCount });
        }

        // ===== 我的关注列表 =====
        [HttpGet("following")]
        public async Task<IActionResult> Following()
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            var list = await _context.Follows
                .Where(f => f.FollowerId == CurrentUserId.Value)
                .Join(_context.Users, f => f.FolloweeId, u => u.Id, (f, u) => new { u.Id, u.Username })
                .OrderByDescending(x => x.Id)
                .ToListAsync();
            return Ok(list);
        }

        // ===== 某人的粉丝列表 =====
        [HttpGet("followers/{userId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> Followers(Guid userId)
        {
            var list = await _context.Follows
                .Where(f => f.FolloweeId == userId)
                .Join(_context.Users, f => f.FollowerId, u => u.Id, (f, u) => new { u.Id, u.Username })
                .OrderByDescending(x => x.Id)
                .ToListAsync();
            return Ok(list);
        }
    }
}
