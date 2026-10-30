using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.World;
using TaiChuWeb_V2.Services.Notify;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World;

/// <summary>
/// 世界观协作者：创建者选择谁可以一起编写世界观。
/// editor = 编写者（可新建、修改、删除条目和关系，调整图谱布局）；viewer = 只读成员（能看私有世界）。
/// 管理协作者只有创建者能做；协作者可以自己退出。
/// </summary>
[Route("api/world/projects/{projectId:guid}/collaborators")]
[Authorize]
public class WorldCollaboratorsController(AppDbContext context, IMemoryCache cache) : WorldControllerBase
{
    public sealed class AddCollaboratorDto
    {
        public Guid UserId { get; set; }
        public string? Role { get; set; }
    }

    public sealed class UpdateCollaboratorDto
    {
        public string? Role { get; set; }
    }

    public sealed record CollaboratorView(Guid UserId, string Username, string? Avatar, string Role, DateTime? CreatedAt);

    /// <summary>成员列表：能看到这个世界的人都能看到谁参与了编写</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List(Guid projectId)
    {
        var userId = ReaderUserId;
        var project = await context.WorldProjects.AsNoTracking().ReadableBy(userId).Where(p => p.Id == projectId)
            .Select(p => new { p.OwnerId, OwnerName = p.Owner != null ? p.Owner.Username : "", OwnerAvatar = p.Owner != null && p.Owner.Profile != null ? p.Owner.Profile.Avatar : null })
            .FirstOrDefaultAsync();
        if (project == null) return NotFound(new { message = "项目不存在或无权访问" });

        var items = await context.WorldCollaborators.AsNoTracking().Where(c => c.ProjectId == projectId)
            .OrderBy(c => c.CreatedAt)
            .Select(c => new CollaboratorView(c.UserId, c.User != null ? c.User.Username : "", c.User != null && c.User.Profile != null ? c.User.Profile.Avatar : null, c.Role, c.CreatedAt))
            .ToListAsync();
        var myRole = project.OwnerId == userId && userId != Guid.Empty ? WorldAccess.RoleOwner
            : items.FirstOrDefault(c => c.UserId == userId)?.Role;
        return Ok(new
        {
            owner = new CollaboratorView(project.OwnerId, project.OwnerName, project.OwnerAvatar, WorldAccess.RoleOwner, null),
            items,
            myRole,
            max = WorldAccess.MaxCollaborators
        });
    }

    /// <summary>按用户名找人（只有创建者能用）</summary>
    [HttpGet("candidates")]
    public async Task<IActionResult> Candidates(Guid projectId, [FromQuery] string? keyword)
    {
        var ownerId = await RequireOwnerAsync(projectId);
        var key = (keyword ?? "").Trim();
        if (key.Length == 0) return Ok(Array.Empty<object>());
        if (key.Length > 50) key = key[..50];
        var existing = context.WorldCollaborators.Where(c => c.ProjectId == projectId).Select(c => c.UserId);
        var users = await context.Users.AsNoTracking()
            .Where(u => u.Id != ownerId && u.Username.Contains(key) && !existing.Contains(u.Id))
            .OrderBy(u => u.Username == key ? 0 : u.Username.StartsWith(key) ? 1 : 2).ThenBy(u => u.Username.Length)
            .Take(8)
            .Select(u => new { userId = u.Id, username = u.Username, avatar = u.Profile != null ? u.Profile.Avatar : null })
            .ToListAsync();
        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> Add(Guid projectId, [FromBody] AddCollaboratorDto dto)
    {
        var ownerId = await RequireOwnerAsync(projectId);
        var role = string.IsNullOrWhiteSpace(dto.Role) ? WorldAccess.RoleEditor : dto.Role.Trim().ToLowerInvariant();
        if (!WorldAccess.IsValidCollaboratorRole(role)) return BadRequest(new { message = "身份只能是编写者或只读成员" });
        if (dto.UserId == Guid.Empty) return BadRequest(new { message = "请选择要邀请的用户" });
        if (dto.UserId == ownerId) return BadRequest(new { message = "你本来就是这个世界的创建者" });

        var user = await context.Users.AsNoTracking().Where(u => u.Id == dto.UserId).Select(u => new { u.Id, u.Username }).FirstOrDefaultAsync();
        if (user == null) return NotFound(new { message = "没有找到这位用户" });

        var existing = await context.WorldCollaborators.FirstOrDefaultAsync(c => c.ProjectId == projectId && c.UserId == dto.UserId);
        if (existing != null)
        {
            if (existing.Role != role)
                new NotificationService(context).Add(dto.UserId, ownerId, "world_collab", "world", projectId.ToString(),
                    $"把你在世界观《{await WorldNameAsync(projectId)}》的身份改成了「{RoleLabel(role)}」", $"/world/project/{projectId}");
            existing.Role = role;
        }
        else
        {
            var count = await context.WorldCollaborators.CountAsync(c => c.ProjectId == projectId);
            if (count >= WorldAccess.MaxCollaborators)
                return BadRequest(new { message = $"每个世界最多 {WorldAccess.MaxCollaborators} 位协作者" });
            context.WorldCollaborators.Add(new WorldCollaborator
            {
                ProjectId = projectId, UserId = dto.UserId, Role = role, AddedBy = ownerId, CreatedAt = DateTime.UtcNow
            });
            var worldName = await context.WorldProjects.AsNoTracking().Where(p => p.Id == projectId).Select(p => p.Name).FirstAsync();
            var content = role == WorldAccess.RoleEditor ? $"邀请你一起编写世界观《{worldName}》" : $"把你加入了世界观《{worldName}》的阅读成员";
            new NotificationService(context).Add(dto.UserId, ownerId, "world_collab", "world", projectId.ToString(), content, $"/world/project/{projectId}");
        }
        await context.SaveChangesAsync();
        Invalidate(projectId, ownerId);
        return Ok(new { userId = user.Id, username = user.Username, role });
    }

    [HttpPut("{userId:guid}")]
    public async Task<IActionResult> UpdateRole(Guid projectId, Guid userId, [FromBody] UpdateCollaboratorDto dto)
    {
        var ownerId = await RequireOwnerAsync(projectId);
        var role = (dto.Role ?? "").Trim().ToLowerInvariant();
        if (!WorldAccess.IsValidCollaboratorRole(role)) return BadRequest(new { message = "身份只能是编写者或只读成员" });
        var member = await context.WorldCollaborators.FirstOrDefaultAsync(c => c.ProjectId == projectId && c.UserId == userId);
        if (member == null) return NotFound(new { message = "这位用户不是协作者" });
        if (member.Role != role)
        {
            member.Role = role;
            // 身份变了告诉本人
            new NotificationService(context).Add(userId, ownerId, "world_collab", "world", projectId.ToString(),
                $"把你在世界观《{await WorldNameAsync(projectId)}》的身份改成了「{RoleLabel(role)}」", $"/world/project/{projectId}");
            await context.SaveChangesAsync();
        }
        Invalidate(projectId, ownerId);
        return Ok(new { userId, role });
    }

    /// <summary>创建者移除协作者；协作者也可以自己退出</summary>
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> Remove(Guid projectId, Guid userId)
    {
        var me = CurrentUserId;
        var ownerId = await context.WorldProjects.AsNoTracking().Where(p => p.Id == projectId).Select(p => (Guid?)p.OwnerId).FirstOrDefaultAsync();
        if (ownerId == null) return NotFound(new { message = "项目不存在" });
        if (me != ownerId && me != userId) return Forbid();
        var removed = await context.WorldCollaborators.Where(c => c.ProjectId == projectId && c.UserId == userId).ExecuteDeleteAsync();
        if (removed == 0) return NotFound(new { message = "这位用户不是协作者" });
        // 被移出时告诉本人；自己退出时告诉创建者
        var name = await WorldNameAsync(projectId);
        if (me == ownerId)
            new NotificationService(context).Add(userId, me, "world_collab", "world", projectId.ToString(), $"把你移出了世界观《{name}》的协作者", $"/world/project/{projectId}");
        else
            new NotificationService(context).Add(ownerId.Value, me, "world_collab", "world", projectId.ToString(), $"退出了世界观《{name}》的协作", $"/world/project/{projectId}");
        await context.SaveChangesAsync();
        Invalidate(projectId, ownerId.Value);
        return NoContent();
    }

    private static string RoleLabel(string role) => role == WorldAccess.RoleEditor ? "编写者" : "只读成员";

    private async Task<string> WorldNameAsync(Guid projectId)
    {
        var name = await context.WorldProjects.AsNoTracking().Where(p => p.Id == projectId).Select(p => p.Name).FirstOrDefaultAsync() ?? "世界观";
        return name.Length > 40 ? name[..40] + "…" : name;
    }

    private async Task<Guid> RequireOwnerAsync(Guid projectId)
    {
        var me = CurrentUserId;
        var ownerId = await context.WorldProjects.AsNoTracking().Where(p => p.Id == projectId).Select(p => (Guid?)p.OwnerId).FirstOrDefaultAsync();
        if (ownerId == null) throw new WorldOperationException(WorldErrorKind.NotFound, "项目不存在");
        if (ownerId != me) throw new WorldOperationException(WorldErrorKind.Forbidden, "只有世界的创建者可以管理协作者");
        return ownerId.Value;
    }

    private void Invalidate(Guid projectId, Guid ownerId) => WorldCache.InvalidateProject(cache, projectId, ownerId);
}
