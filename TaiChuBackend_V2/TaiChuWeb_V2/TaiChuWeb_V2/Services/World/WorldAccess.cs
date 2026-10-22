using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.World;

namespace TaiChuWeb_V2.Services.World;

public static class WorldAccess
{
    public static bool IsOwner(Guid ownerId, Guid userId) => userId != Guid.Empty && ownerId == userId;
    public static bool CanRead(bool isPublic, Guid ownerId, Guid userId) => isPublic || IsOwner(ownerId, userId);

    // 查询表达式保持可翻译成 SQL；已加载实体使用同样的公开/所有者规则。
    public static IQueryable<WorldProject> ReadableBy(this IQueryable<WorldProject> projects, Guid userId) =>
        projects.Where(project => project.IsPublic || (userId != Guid.Empty && project.OwnerId == userId));

    public static IQueryable<WorldProject> OwnedBy(this IQueryable<WorldProject> projects, Guid userId) =>
        projects.Where(project => userId != Guid.Empty && project.OwnerId == userId);

    public static void RequireReadable(WorldProject? project, Guid userId)
    {
        if (project == null || !CanRead(project.IsPublic, project.OwnerId, userId))
            throw new WorldOperationException(WorldErrorKind.NotFound, "项目不存在或无权访问");
    }

    public static async Task RequireReadableProjectAsync(AppDbContext context, Guid projectId, Guid userId)
    {
        if (!await context.WorldProjects.AsNoTracking().ReadableBy(userId).AnyAsync(p => p.Id == projectId))
            throw new WorldOperationException(WorldErrorKind.NotFound, "项目不存在或无权访问");
    }

    public static Task<bool> CanReadCardAsync(AppDbContext context, Guid cardId, Guid userId) =>
        context.WorldCards.AsNoTracking().AnyAsync(card => card.Id == cardId && card.Project != null &&
            (card.Project.IsPublic || (userId != Guid.Empty && card.Project.OwnerId == userId)));

    public static async Task RequireReadableCardAsync(AppDbContext context, Guid cardId, Guid userId)
    {
        if (!await CanReadCardAsync(context, cardId, userId))
            throw new WorldOperationException(WorldErrorKind.NotFound, "卡片不存在或无权访问");
    }
}
