using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World;

/// <summary>
/// 世界观修改记录：看谁在什么时候改了哪个条目、改了什么；撤销某次修改；
/// 创建者可以一键撤销某位协作者一段时间内的全部修改。
/// 查看：创建者和协作者；撤销单次修改：创建者和编写者；一键撤销某人：只有创建者。
/// </summary>
[Route("api/world/projects/{projectId:guid}/history")]
[Authorize]
public class WorldHistoryController(AppDbContext context, WorldHistoryService history) : WorldControllerBase
{
    public sealed class RevertUserDto
    {
        public Guid UserId { get; set; }
        /// <summary>撤销最近多少小时内的修改；0 或不填 = 全部</summary>
        public int Hours { get; set; }
    }

    private async Task RequireMemberAsync(Guid projectId)
    {
        if (!await context.WorldProjects.AsNoTracking().MemberOf(CurrentUserId).AnyAsync(p => p.Id == projectId))
            throw new WorldOperationException(WorldErrorKind.Forbidden, "只有世界的创建者和协作者能查看修改记录");
    }

    [HttpGet]
    public async Task<IActionResult> List(Guid projectId, [FromQuery] Guid? cardId, [FromQuery] Guid? userId,
        [FromQuery] string? action, [FromQuery] long? before, [FromQuery] int take = 30)
    {
        await RequireMemberAsync(projectId);
        take = Math.Clamp(take, 1, 100);
        var query = context.WorldCardRevisions.AsNoTracking().Where(r => r.ProjectId == projectId);
        // 「记录起点」只在看单个条目时显示
        query = cardId.HasValue ? query.Where(r => r.CardId == cardId.Value) : query.Where(r => r.Action != WorldHistoryService.Baseline);
        if (userId.HasValue) query = query.Where(r => r.UserId == userId.Value);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(r => r.Action == action);
        if (before.HasValue) query = query.Where(r => r.Id < before.Value);
        var rows = await query.OrderByDescending(r => r.Id).Take(take + 1)
            .Select(r => new { r.Id, r.CardId, r.UserId, r.Action, r.Title, r.Note, r.CreatedAt }).ToListAsync();
        var userIds = rows.Where(r => r.UserId.HasValue).Select(r => r.UserId!.Value).Distinct().ToList();
        var users = await context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Username, Avatar = u.Profile != null ? u.Profile.Avatar : null }).ToDictionaryAsync(u => u.Id);
        var alive = await context.WorldCards.AsNoTracking().Where(c => c.ProjectId == projectId).Select(c => c.Id).ToListAsync();
        var aliveSet = alive.ToHashSet();
        return Ok(new
        {
            items = rows.Take(take).Select(r => new
            {
                r.Id, r.CardId, r.UserId, r.Action, r.Title, r.Note, r.CreatedAt,
                Username = r.UserId.HasValue && users.TryGetValue(r.UserId.Value, out var u) ? u.Username : null,
                Avatar = r.UserId.HasValue && users.TryGetValue(r.UserId.Value, out var u2) ? u2.Avatar : null,
                CardExists = aliveSet.Contains(r.CardId),
            }),
            hasMore = rows.Count > take,
        });
    }

    /// <summary>某次修改的详情：这次之后的内容 + 这次之前的内容（前端对比出改了什么）</summary>
    [HttpGet("{revisionId:long}")]
    public async Task<IActionResult> Detail(Guid projectId, long revisionId)
    {
        await RequireMemberAsync(projectId);
        var revision = await context.WorldCardRevisions.AsNoTracking().FirstOrDefaultAsync(r => r.Id == revisionId && r.ProjectId == projectId);
        if (revision == null) return NotFound(new { message = "修改记录不存在" });
        var previous = revision.Action is WorldHistoryService.Create or WorldHistoryService.Baseline ? null
            : await context.WorldCardRevisions.AsNoTracking().Where(r => r.CardId == revision.CardId && r.Id < revision.Id)
                .OrderByDescending(r => r.Id).Select(r => r.Snapshot).FirstOrDefaultAsync();
        // 删除记录存的是删除前的内容：对比时「之前」= 它，「之后」= 空
        return revision.Action == WorldHistoryService.Delete
            ? Ok(new { revision.Id, revision.Action, before = WorldHistoryService.Parse(revision.Snapshot), after = (WorldCardSnapshot?)null })
            : Ok(new { revision.Id, revision.Action, before = WorldHistoryService.Parse(previous), after = WorldHistoryService.Parse(revision.Snapshot) });
    }

    /// <summary>撤销某次修改</summary>
    [HttpPost("{revisionId:long}/undo")]
    public async Task<IActionResult> Undo(Guid projectId, long revisionId)
    {
        if (!await WorldAccess.CanEditProjectAsync(context, projectId, CurrentUserId))
            return StatusCode(403, new { message = "只有世界的创建者和编写者能撤销修改" });
        return Ok(new { message = await history.UndoAsync(projectId, revisionId, CurrentUserId) });
    }

    /// <summary>一键撤销某人一段时间内的全部修改（只有创建者能用）</summary>
    [HttpPost("revert-user")]
    public async Task<IActionResult> RevertUser(Guid projectId, [FromBody] RevertUserDto dto)
    {
        var ownerId = await context.WorldProjects.AsNoTracking().Where(p => p.Id == projectId).Select(p => (Guid?)p.OwnerId).FirstOrDefaultAsync();
        if (ownerId == null) return NotFound(new { message = "项目不存在" });
        if (ownerId != CurrentUserId) return StatusCode(403, new { message = "只有世界的创建者能一键撤销别人的修改" });
        if (dto.UserId == Guid.Empty) return BadRequest(new { message = "请选择要撤销谁的修改" });
        var since = dto.Hours > 0 ? DateTime.UtcNow.AddHours(-dto.Hours) : DateTime.MinValue;
        var count = await history.RevertUserAsync(projectId, dto.UserId, since, CurrentUserId);
        return Ok(new { count, message = count > 0 ? $"已撤销，{count} 个条目恢复到了修改之前的样子" : "这段时间内没有需要撤销的修改" });
    }

    /// <summary>每个条目最后一次是谁改的（条目页显示「最后修改：xxx」）</summary>
    [HttpGet("last-editors")]
    [AllowAnonymous]
    public async Task<IActionResult> LastEditors(Guid projectId)
    {
        await WorldAccess.RequireReadableProjectAsync(context, projectId, ReaderUserId);
        var latestIds = await context.WorldCardRevisions.AsNoTracking()
            .Where(r => r.ProjectId == projectId && r.Action != WorldHistoryService.Baseline)
            .GroupBy(r => r.CardId).Select(g => g.Max(r => r.Id)).ToListAsync();
        var rows = await context.WorldCardRevisions.AsNoTracking().Where(r => latestIds.Contains(r.Id))
            .Select(r => new { r.CardId, r.UserId, r.Action, r.CreatedAt }).ToListAsync();
        var userIds = rows.Where(r => r.UserId.HasValue).Select(r => r.UserId!.Value).Distinct().ToList();
        var names = await context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
        return Ok(rows.Select(r => new
        {
            r.CardId, r.UserId, r.Action, r.CreatedAt,
            Username = r.UserId.HasValue ? names.GetValueOrDefault(r.UserId.Value) : null,
        }));
    }

    /// <summary>条目每一项是谁、什么时候改的，改过几次（看设定时直接显示）。能看这个世界的人都能看。</summary>
    [HttpGet("card/{cardId:guid}/blame")]
    [AllowAnonymous]
    public async Task<IActionResult> Blame(Guid projectId, Guid cardId)
    {
        await WorldAccess.RequireReadableProjectAsync(context, projectId, ReaderUserId);
        var blame = await history.BlameAsync(projectId, cardId);
        var ids = blame.Editors.Select(e => e.UserId).Concat(blame.Fields.Values.Where(f => f.UserId.HasValue).Select(f => f.UserId!.Value))
            .Concat(blame.CreatedBy.HasValue ? new[] { blame.CreatedBy.Value } : Array.Empty<Guid>()).Distinct().ToList();
        var names = await context.Users.AsNoTracking().Where(u => ids.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
        string? Name(Guid? id) => id.HasValue ? names.GetValueOrDefault(id.Value) : null;
        return Ok(new
        {
            createdBy = Name(blame.CreatedBy), blame.CreatedAt, blame.TotalEdits, blame.TrackedFromStart,
            editors = blame.Editors.Select(e => new { e.UserId, username = Name(e.UserId), e.Count, e.LastAt }),
            fields = blame.Fields.ToDictionary(f => f.Key, f => new { username = Name(f.Value.UserId), at = f.Value.At, count = f.Value.Count, editors = f.Value.Editors }),
        });
    }
}
