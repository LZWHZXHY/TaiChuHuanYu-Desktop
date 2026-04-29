using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Interact;

namespace TaiChuWeb_V2.Controllers
{
    [Authorize] // 只有登录道友方可点赞/收藏
    [ApiController]
    [Route("api/[controller]")]
    public class InteractionController : ControllerBase
    {
        private readonly AppDbContext _context;

        public InteractionController(AppDbContext context)
        {
            _context = context;
        }

        [HttpPost("toggle-action")]
        public async Task<IActionResult> ToggleAction([FromQuery] string targetId, [FromQuery] string targetType, [FromQuery] string actionType)
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim)) return Unauthorized();
            var userId = Guid.Parse(userIdClaim);

            // 1. 动态定位目标对象并更新计数器
            // 我们定义两个变量来存储结果
            bool isActive = false;
            int newCount = 0;

            // 开启事务处理，保证交互记录和计数器同步
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 检查是否已存在记录
                var existing = await _context.UserInteractions
                    .FirstOrDefaultAsync(x => x.UserId == userId &&
                                             x.TargetId == targetId &&
                                             x.TargetType == targetType &&
                                             x.ActionType == actionType);

                // 处理计数器更新逻辑 (根据不同类型找不同的表)
                if (targetType == "Artwork")
                {
                    var id = int.Parse(targetId);
                    var artwork = await _context.Artworks.FindAsync(id);
                    if (artwork == null) return NotFound("画卷不存在");

                    if (existing == null)
                    {
                        artwork.LikesCount++;
                        isActive = true;
                    }
                    else
                    {
                        artwork.LikesCount = Math.Max(0, artwork.LikesCount - 1);
                        isActive = false;
                    }
                    newCount = artwork.LikesCount;
                }
                else if (targetType == "Post")
                {
                    // 等你以后有了 Posts 表，代码直接加在这里
                    // var post = await _context.Posts.FindAsync(int.Parse(targetId));
                    // ... 处理逻辑同上
                }
                else if (targetType == "Blog")
                {
                    // 处理博客逻辑...
                }

                // 2. 更新交互记录表
                if (existing == null)
                {
                    _context.UserInteractions.Add(new UserInteraction
                    {
                        UserId = userId,
                        TargetId = targetId,
                        TargetType = targetType,
                        ActionType = actionType
                    });
                }
                else
                {
                    _context.UserInteractions.Remove(existing);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { isActive, newCount });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                // 这样前端就能看到到底是哪一行代码报错，以及报了什么错
                return BadRequest(new
                {
                    message = "灵力紊乱",
                    error = ex.Message,
                    stack = ex.StackTrace
                });
            }
        }




    }
}
