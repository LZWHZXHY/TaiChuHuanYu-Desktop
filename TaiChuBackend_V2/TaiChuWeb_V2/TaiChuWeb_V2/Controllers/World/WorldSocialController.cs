using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Interact;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World;

/// <summary>
/// 世界观 / 小说的点赞、收藏、评论数（fix171）。
/// 不建新表：点赞收藏复用 UserInteractions（TargetType = WorldProject / WorldNovel，ActionType = Like / Favorite），
/// 评论复用 comments 表（ChaiComment 接口，TargetType = world / world-card / novel）。
/// 看不到的世界观 / 小说（私有、草稿）一律当作不存在，计数、点赞、收藏都不给。
/// </summary>
[ApiController]
[Route("api/world/social")]
public class WorldSocialController : ControllerBase
{
    private const string WorldTarget = "WorldProject", NovelTarget = "WorldNovel";
    private readonly AppDbContext _context;
    public WorldSocialController(AppDbContext context) { _context = context; }

    private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    /// <summary>kind：world 世界观 / card 设定条目（只有评论数）/ novel 小说</summary>
    private async Task<bool> CanSeeAsync(string kind, Guid id) => kind switch
    {
        "world" => await _context.WorldProjects.AsNoTracking().ReadableBy(Me).AnyAsync(p => p.Id == id),
        "card" => await WorldAccess.CanReadCardAsync(_context, id, Me),
        "novel" => await _context.WorldNovels.AsNoTracking().AnyAsync(n => n.Id == id && (n.IsPublic || (Me != Guid.Empty && n.AuthorId == Me))),
        _ => false,
    };

    private static string? InteractionTarget(string kind) => kind switch { "world" => WorldTarget, "novel" => NovelTarget, _ => null };
    private static string CommentTarget(string kind) => kind switch { "world" => "world", "card" => "world-card", _ => "novel" };

    /// <summary>点赞数、收藏数、评论数，以及我点过没有</summary>
    [HttpGet("{kind}/{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Stats(string kind, Guid id)
    {
        if (kind is not ("world" or "card" or "novel")) return BadRequest(new { message = "不支持的类型" });
        if (!await CanSeeAsync(kind, id)) return NotFound(new { message = "内容不存在或无权查看" });

        var key = id.ToString();
        var commentType = CommentTarget(kind);
        var commentCount = await _context.Comments.CountAsync(c => c.TargetType == commentType && c.TargetId == key && c.Content != "");
        var target = InteractionTarget(kind);
        if (target == null) return Ok(new { likeCount = 0, favoriteCount = 0, commentCount, liked = false, favorited = false });

        var rows = await _context.UserInteractions.AsNoTracking()
            .Where(i => i.TargetType == target && i.TargetId == key && (i.ActionType == "Like" || i.ActionType == "Favorite"))
            .Select(i => new { i.ActionType, i.UserId }).ToListAsync();
        var me = Me;
        return Ok(new
        {
            likeCount = rows.Count(r => r.ActionType == "Like"),
            favoriteCount = rows.Count(r => r.ActionType == "Favorite"),
            commentCount,
            liked = me != Guid.Empty && rows.Any(r => r.ActionType == "Like" && r.UserId == me),
            favorited = me != Guid.Empty && rows.Any(r => r.ActionType == "Favorite" && r.UserId == me),
        });
    }

    /// <summary>点赞 / 收藏（再点一次取消）。只有世界观和小说能点</summary>
    [HttpPost("{kind}/{id:guid}/{action}")]
    [Authorize]
    public async Task<IActionResult> Toggle(string kind, Guid id, string action)
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized(new { message = "请先登录" });
        var target = InteractionTarget(kind);
        if (target == null) return BadRequest(new { message = "不支持的类型" });
        if (action is not ("like" or "favorite")) return BadRequest(new { message = "不支持的操作" });
        if (!await CanSeeAsync(kind, id)) return NotFound(new { message = "内容不存在或无权查看" });

        var key = id.ToString();
        var actionType = action == "like" ? "Like" : "Favorite";
        var existing = await _context.UserInteractions.FirstOrDefaultAsync(i =>
            i.TargetType == target && i.TargetId == key && i.ActionType == actionType && i.UserId == me);
        bool active;
        if (existing != null) { _context.UserInteractions.Remove(existing); active = false; }
        else { _context.UserInteractions.Add(new UserInteraction { UserId = me, TargetId = key, TargetType = target, ActionType = actionType }); active = true; }
        await _context.SaveChangesAsync();
        var count = await _context.UserInteractions.CountAsync(i => i.TargetType == target && i.TargetId == key && i.ActionType == actionType);
        return Ok(new { active, count });
    }

    /// <summary>我收藏的世界观和小说（个人中心「我的收藏」用）。已经看不到的（转私有、删除）不显示</summary>
    [HttpGet("favorites")]
    [Authorize]
    public async Task<IActionResult> Favorites()
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized(new { message = "请先登录" });
        var favs = await _context.UserInteractions.AsNoTracking()
            .Where(i => i.UserId == me && i.ActionType == "Favorite" && (i.TargetType == WorldTarget || i.TargetType == NovelTarget))
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => new { i.TargetType, i.TargetId, i.CreatedAt })
            .Take(300).ToListAsync();

        var worldIds = favs.Where(f => f.TargetType == WorldTarget).Select(f => Guid.TryParse(f.TargetId, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();
        var novelIds = favs.Where(f => f.TargetType == NovelTarget).Select(f => Guid.TryParse(f.TargetId, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).ToList();

        var worlds = await _context.WorldProjects.AsNoTracking().ReadableBy(me)
            .Where(p => worldIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.Description, p.CoverImage, p.IsPublic, p.OwnerId, OwnerName = p.Owner != null ? p.Owner.Username : null })
            .ToListAsync();
        var novels = await _context.WorldNovels.AsNoTracking()
            .Where(n => novelIds.Contains(n.Id) && (n.IsPublic || n.AuthorId == me))
            .Select(n => new { n.Id, n.Title, n.Description, n.CoverUrl, n.AuthorId, n.AuthorName, n.WordCount, n.Status })
            .ToListAsync();

        var at = favs.GroupBy(f => f.TargetType + ":" + f.TargetId.ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First().CreatedAt);
        DateTime When(string type, Guid id) => at.GetValueOrDefault(type + ":" + id.ToString(), DateTime.MinValue);

        return Ok(new
        {
            worlds = worlds.Select(w => new { w.Id, w.Name, w.Description, w.CoverImage, w.IsPublic, w.OwnerId, w.OwnerName, FavoritedAt = When(WorldTarget, w.Id) })
                .OrderByDescending(w => w.FavoritedAt),
            novels = novels.Select(n => new { n.Id, n.Title, n.Description, n.CoverUrl, n.AuthorId, n.AuthorName, n.WordCount, n.Status, FavoritedAt = When(NovelTarget, n.Id) })
                .OrderByDescending(n => n.FavoritedAt),
        });
    }
}
