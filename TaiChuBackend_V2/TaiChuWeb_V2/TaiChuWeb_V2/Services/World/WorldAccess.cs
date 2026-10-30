using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.World;

namespace TaiChuWeb_V2.Services.World;

/// <summary>
/// 世界观权限：
/// - 读：公开世界任何人；私有世界只有创建者和协作者（编写者、只读成员）。
/// - 写条目 / 关系 / 图谱布局：创建者和「编写者」协作者。
/// - 改名、简介、封面、公开状态、删除、导入备份、管理协作者：只有创建者。
/// </summary>
public static class WorldAccess
{
    public const string RoleOwner = "owner";
    public const string RoleEditor = "editor";
    public const string RoleViewer = "viewer";
    public const int MaxCollaborators = 30;

    public static bool IsValidCollaboratorRole(string? role) => role is RoleEditor or RoleViewer;

    public static bool IsOwner(Guid ownerId, Guid userId) => userId != Guid.Empty && ownerId == userId;

    // 查询表达式保持可翻译成 SQL。
    public static IQueryable<WorldProject> ReadableBy(this IQueryable<WorldProject> projects, Guid userId) =>
        projects.Where(project => project.IsPublic || (userId != Guid.Empty &&
            (project.OwnerId == userId || project.Collaborators.Any(c => c.UserId == userId))));

    public static IQueryable<WorldProject> EditableBy(this IQueryable<WorldProject> projects, Guid userId) =>
        projects.Where(project => userId != Guid.Empty &&
            (project.OwnerId == userId || project.Collaborators.Any(c => c.UserId == userId && c.Role == RoleEditor)));

    public static IQueryable<WorldProject> OwnedBy(this IQueryable<WorldProject> projects, Guid userId) =>
        projects.Where(project => userId != Guid.Empty && project.OwnerId == userId);

    /// <summary>创建者和所有协作者（不含只看公开内容的路人）</summary>
    public static IQueryable<WorldProject> MemberOf(this IQueryable<WorldProject> projects, Guid userId) =>
        projects.Where(project => userId != Guid.Empty &&
            (project.OwnerId == userId || project.Collaborators.Any(c => c.UserId == userId)));

    /// <summary>当前用户在这个世界里的身份：owner / editor / viewer，路人返回 null。</summary>
    public static async Task<string?> GetRoleAsync(AppDbContext context, Guid projectId, Guid userId)
    {
        if (userId == Guid.Empty) return null;
        var ownerId = await context.WorldProjects.AsNoTracking().Where(p => p.Id == projectId)
            .Select(p => (Guid?)p.OwnerId).FirstOrDefaultAsync();
        if (ownerId == null) return null;
        if (ownerId == userId) return RoleOwner;
        return await context.WorldCollaborators.AsNoTracking()
            .Where(c => c.ProjectId == projectId && c.UserId == userId).Select(c => c.Role).FirstOrDefaultAsync();
    }

    public static async Task RequireReadableProjectAsync(AppDbContext context, Guid projectId, Guid userId)
    {
        if (!await context.WorldProjects.AsNoTracking().ReadableBy(userId).AnyAsync(p => p.Id == projectId))
            throw new WorldOperationException(WorldErrorKind.NotFound, "项目不存在或无权访问");
    }

    public static Task<bool> CanEditProjectAsync(AppDbContext context, Guid projectId, Guid userId) =>
        context.WorldProjects.AsNoTracking().EditableBy(userId).AnyAsync(p => p.Id == projectId);

    public static Task<bool> CanReadCardAsync(AppDbContext context, Guid cardId, Guid userId) =>
        context.WorldCards.AsNoTracking().AnyAsync(card => card.Id == cardId && card.Project != null &&
            (card.Project.IsPublic || (userId != Guid.Empty &&
                (card.Project.OwnerId == userId || card.Project.Collaborators.Any(c => c.UserId == userId)))));

    public static async Task RequireReadableCardAsync(AppDbContext context, Guid cardId, Guid userId)
    {
        if (!await CanReadCardAsync(context, cardId, userId))
            throw new WorldOperationException(WorldErrorKind.NotFound, "卡片不存在或无权访问");
    }
}
