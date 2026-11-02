using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services.Games;
using TaiChuWeb_V2.Services.Notify;

namespace TaiChuWeb_V2.Controllers.Game
{
    /// <summary>
    /// 太虚游 · 游戏留言板（按游戏 key 区分，现在只开了斗地主 ddz）：
    /// · 谁都能看；登录后能留言（最多 500 字）、回复、删自己的；管理员（超级管理员或「太初官方」）能删任何一条；
    /// · 登录后还能给「这个游戏」点赞 / 砸番茄，可以多次，每次花土豆（花掉，不给任何人），显示总数和最近谁赞了 / 砸了。
    /// </summary>
    [ApiController]
    [Route("api/game-board")]
    public class GameBoardController(GameSocialService social, PotatoService potatoes, AppDbContext db) : ControllerBase
    {
        public sealed class MessageDto { public string? Content { get; set; } public long? ParentId { get; set; } }
        public sealed class ReactDto { public string? Kind { get; set; } }

        private const int PageSize = 20;
        private const int RecentReactions = 12;

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        /// <summary>管理员判断和岁月史书一样：超级管理员或「太初官方」权限</summary>
        private async Task<bool> IsManagerAsync()
        {
            if (CurrentUserId is not Guid me) return false;
            return await db.UserPermissions.AsNoTracking().AnyAsync(p => p.UserId == me
                && (p.Permission == AdminPermission.SuperAdmin || p.Permission == AdminPermission.TaiChu));
        }

        private sealed record UserBrief(string Name, string? Avatar);

        /// <summary>一次查出这些人的名字和头像（先拿 Id 列表再查，不跨表 join）</summary>
        private async Task<Dictionary<Guid, UserBrief>> UsersAsync(IEnumerable<Guid> ids)
        {
            var list = ids.Where(i => i != Guid.Empty).Distinct().ToList();
            if (list.Count == 0) return new Dictionary<Guid, UserBrief>();
            var users = await db.Users.AsNoTracking().Where(u => list.Contains(u.Id))
                .Select(u => new { u.Id, u.Username, Avatar = u.Profile != null ? u.Profile.Avatar : null }).ToListAsync();
            return users.ToDictionary(u => u.Id, u => new UserBrief(u.Username, u.Avatar));
        }

        private static string NameOf(Dictionary<Guid, UserBrief> users, Guid id) => users.TryGetValue(id, out var u) ? u.Name : "已注销用户";

        // ================= 留言 =================

        /// <summary>一页留言（新的在前，每页 20 条），带上每条下面的回复。beforeId：加载更早的</summary>
        [HttpGet("{game}/messages")]
        [AllowAnonymous]
        public async Task<IActionResult> Messages(string game, [FromQuery] long? beforeId = null)
        {
            if (!GameSocialService.Games.ContainsKey(game)) return NotFound(new { message = "这个游戏还没有开留言板" });
            var me = CurrentUserId;
            var manager = await IsManagerAsync();
            var (roots, replies, hasMore) = await social.ListAsync(game, beforeId, PageSize);
            var users = await UsersAsync(roots.Select(m => m.UserId)
                .Concat(replies.Select(m => m.UserId))
                .Concat(replies.Where(m => m.ReplyToUserId.HasValue).Select(m => m.ReplyToUserId!.Value)));
            var byParent = replies.GroupBy(r => r.ParentId!.Value).ToDictionary(g => g.Key, g => g.ToList());
            return Ok(new
            {
                canManage = manager,
                hasMore,
                items = roots.Select(m => new
                {
                    id = m.Id,
                    // 已删除但下面还有回复的：只留个壳，不给内容和作者
                    userId = m.Deleted ? (Guid?)null : m.UserId,
                    name = m.Deleted ? "" : NameOf(users, m.UserId),
                    avatar = m.Deleted ? null : users.GetValueOrDefault(m.UserId)?.Avatar,
                    content = m.Deleted ? "" : m.Content,
                    deleted = m.Deleted,
                    createdAt = m.CreatedAt,
                    canDelete = !m.Deleted && (manager || m.UserId == me),
                    replies = (byParent.GetValueOrDefault(m.Id) ?? new List<GameSocialService.BoardMessage>()).Select(r => new
                    {
                        id = r.Id,
                        userId = (Guid?)r.UserId,
                        name = NameOf(users, r.UserId),
                        avatar = users.GetValueOrDefault(r.UserId)?.Avatar,
                        content = r.Content,
                        replyToUserId = r.ReplyToUserId,
                        replyToName = r.ReplyToUserId is Guid rt ? NameOf(users, rt) : null,
                        createdAt = r.CreatedAt,
                        canDelete = manager || r.UserId == me,
                    }).ToList(),
                }).ToList(),
            });
        }

        /// <summary>留言或回复（parentId 是要回复的那条，可以是主留言也可以是回复；回复统一挂在主留言下面）</summary>
        [Authorize]
        [HttpPost("{game}/messages")]
        public async Task<IActionResult> Post(string game, [FromBody] MessageDto dto)
        {
            if (CurrentUserId is not Guid me) return Unauthorized();
            if (!GameSocialService.Games.TryGetValue(game, out var info)) return NotFound(new { message = "这个游戏还没有开留言板" });
            var content = (dto.Content ?? "").Trim();
            if (content.Length == 0) return BadRequest(new { message = "写点什么再发吧" });
            if (content.Length > GameSocialService.BoardMaxLength) return BadRequest(new { message = $"留言最多 {GameSocialService.BoardMaxLength} 个字" });

            long? rootId = null;
            Guid? replyTo = null;
            GameSocialService.BoardMessage? parent = null;
            if (dto.ParentId is long pid)
            {
                parent = await social.GetMessageAsync(pid);
                if (parent == null || parent.GameKey != game || parent.Deleted) return BadRequest(new { message = "要回复的留言不在了" });
                rootId = parent.ParentId ?? parent.Id;
                replyTo = parent.UserId;
            }
            if (!social.TryPass($"board-post:{me}", GameSocialService.BoardCooldownSeconds))
                return BadRequest(new { message = $"发得太快啦，{GameSocialService.BoardCooldownSeconds} 秒后再发" });

            var saved = await social.AddMessageAsync(game, me, content, rootId, replyTo);

            // 有人回复你：发站内通知（自己回复自己不通知）
            if (parent != null)
            {
                var notifier = new NotificationService(db);
                var preview = content.Length > 40 ? content[..40] + "…" : content;
                if (notifier.Add(parent.UserId, me, "reply", "game-board", game, $"在「{info.Name}」留言板回复了你：{preview}", info.Link))
                    await db.SaveChangesAsync();
            }

            var users = await UsersAsync(new[] { me }.Concat(replyTo.HasValue ? new[] { replyTo.Value } : Array.Empty<Guid>()));
            return Ok(new
            {
                id = saved.Id,
                userId = (Guid?)me,
                name = NameOf(users, me),
                avatar = users.GetValueOrDefault(me)?.Avatar,
                content = saved.Content,
                parentId = saved.ParentId,
                replyToUserId = saved.ReplyToUserId,
                replyToName = saved.ReplyToUserId is Guid rt ? NameOf(users, rt) : null,
                createdAt = saved.CreatedAt,
                canDelete = true,
            });
        }

        /// <summary>删除留言：自己的随便删，管理员能删任何一条</summary>
        [Authorize]
        [HttpDelete("{game}/messages/{id:long}")]
        public async Task<IActionResult> Delete(string game, long id)
        {
            if (CurrentUserId is not Guid me) return Unauthorized();
            var m = await social.GetMessageAsync(id);
            if (m == null || m.GameKey != game || m.Deleted) return NotFound(new { message = "这条留言已经不在了" });
            if (m.UserId != me && !await IsManagerAsync()) return StatusCode(403, new { message = "只能删除自己的留言" });
            await social.DeleteMessageAsync(id, me);
            return NoContent();
        }

        // ================= 给游戏点赞 / 砸番茄 =================

        private async Task<object> ReactionsView(string game, Guid? me)
        {
            var s = await social.GameReactionsAsync(game, me, RecentReactions);
            var users = await UsersAsync(s.Recent.Select(r => r.UserId));
            return new
            {
                likes = s.Likes,
                tomatoes = s.Tomatoes,
                myLikes = s.MyLikes,
                myTomatoes = s.MyTomatoes,
                likeCost = GameSocialService.LikeCost,
                tomatoCost = GameSocialService.TomatoCost,
                recent = s.Recent.Select(r => new
                {
                    userId = r.UserId,
                    name = NameOf(users, r.UserId),
                    avatar = users.GetValueOrDefault(r.UserId)?.Avatar,
                    kind = r.Kind,
                    at = r.At,
                }).ToList(),
            };
        }

        /// <summary>这个游戏一共被赞 / 被砸多少次，最近是谁</summary>
        [HttpGet("{game}/reactions")]
        [AllowAnonymous]
        public async Task<IActionResult> Reactions(string game)
        {
            if (!GameSocialService.Games.ContainsKey(game)) return NotFound(new { message = "这个游戏还没有开留言板" });
            return Ok(await ReactionsView(game, CurrentUserId));
        }

        /// <summary>给这个游戏点赞（like，1 土豆）或砸番茄（tomato，2 土豆），可以多次；土豆花掉，不给任何人</summary>
        [Authorize]
        [HttpPost("{game}/react")]
        public async Task<IActionResult> React(string game, [FromBody] ReactDto dto)
        {
            if (CurrentUserId is not Guid me) return Unauthorized();
            if (!GameSocialService.Games.TryGetValue(game, out var info)) return NotFound(new { message = "这个游戏还没有开留言板" });
            var kind = dto.Kind ?? "";
            if (!GameSocialService.ValidKind(kind)) return BadRequest(new { message = "没有这种互动" });
            if (!social.TryPass($"board-react:{me}", GameSocialService.BoardReactCooldownSeconds))
                return BadRequest(new { message = "点太快啦，歇一下再来" });

            var cost = GameSocialService.CostOf(kind);
            var label = GameSocialService.KindLabel(kind);
            var spent = await potatoes.SpendAsync(me, cost, $"{info.Name}留言板 · 给游戏{label}", "board-react", game);
            if (!spent.Ok) return BadRequest(new { message = "土豆不够了", balance = spent.Balance });
            await social.AddGameReactionAsync(game, me, kind, cost);
            return Ok(new { balance = spent.Balance, reactions = await ReactionsView(game, me) });
        }
    }
}
