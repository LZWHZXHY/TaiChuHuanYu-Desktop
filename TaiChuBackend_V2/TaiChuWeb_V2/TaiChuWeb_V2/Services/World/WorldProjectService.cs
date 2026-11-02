using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.World;

namespace TaiChuWeb_V2.Services.World;

public class WorldProjectService(AppDbContext context, IMemoryCache cache, IWorldQuotaService quotaService) : IWorldProjectService
{
    // 「我的世界」= 自己创建的 + 被拉进来协作的。协作者的写入也会改变卡片数，所以这里不做按用户缓存。
    public async Task<IEnumerable<ProjectResponseDto>> GetUserProjectsAsync(Guid userId)
    {
        var projects = await context.WorldProjects.AsNoTracking().MemberOf(userId)
            .Select(WorldProjectMapper.Projection).ToListAsync();
        var roles = await context.WorldCollaborators.AsNoTracking().Where(c => c.UserId == userId)
            .Select(c => new { c.ProjectId, c.Role }).ToDictionaryAsync(c => c.ProjectId, c => c.Role);
        foreach (var project in projects)
            project.MyRole = project.OwnerId == userId ? WorldAccess.RoleOwner : roles.GetValueOrDefault(project.Id);
        return projects;
    }

    public async Task<IEnumerable<ProjectResponseDto>> GetPublicProjectsAsync()
    {
        const string key = "public_projects";
        if (cache.TryGetValue(key, out IEnumerable<ProjectResponseDto>? cached)) return cached ?? [];
        var projects = await context.WorldProjects.AsNoTracking().Where(p => p.IsPublic)
            .OrderByDescending(p => p.UpdatedAt).Select(WorldProjectMapper.Projection).ToListAsync();
        cache.Set(key, projects, TimeSpan.FromMinutes(5));
        return projects;
    }

    public async Task<ProjectResponseDto?> GetProjectByIdAsync(Guid projectId, Guid userId)
    {
        var project = await context.WorldProjects.AsNoTracking().ReadableBy(userId).Where(p => p.Id == projectId)
            .Select(WorldProjectMapper.Projection).FirstOrDefaultAsync();
        if (project != null) project.MyRole = await WorldAccess.GetRoleAsync(context, projectId, userId);
        return project;
    }

    // 此 DTO 只有 CardCount，不需要加载完整卡片集合。
    public Task<ProjectResponseDto?> GetProjectWithCardsAsync(Guid projectId, Guid userId) =>
        GetProjectByIdAsync(projectId, userId);

    public async Task<ProjectResponseDto> CreateProjectAsync(Guid userId, CreateProjectDto dto)
    {
        WorldValidation.Validate(dto);
        var quota = await quotaService.CanCreateProjectAsync(userId);
        if (!quota.CanCreate)
            throw new WorldOperationException(WorldErrorKind.Invalid, quota.Message, new Dictionary<string, object?>
            {
                ["success"] = false, ["code"] = "QUOTA_EXCEEDED", ["used"] = quota.Used, ["max"] = quota.Max
            });

        var project = new WorldProject
        {
            Id = Guid.NewGuid(), Name = dto.Name, Description = dto.Description, CoverImage = dto.CoverImage, IsPublic = dto.IsPublic,
            OwnerId = userId, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };
        context.WorldProjects.Add(project);
        await context.SaveChangesAsync();
        WorldCache.InvalidateProject(cache, project.Id, userId);
        await quotaService.IncrementUsedWorldCountAsync(userId);
        var result = WorldProjectMapper.FromEntity(project);
        result.OwnerName = null;
        result.MyRole = WorldAccess.RoleOwner;
        return result;
    }

    // 改名、简介、封面、公开状态只有创建者能改；图谱坐标走 graph-positions 接口（编写者也能保存）。
    public async Task<ProjectResponseDto?> UpdateProjectAsync(Guid projectId, Guid userId, UpdateProjectDto dto)
    {
        WorldValidation.Validate(dto);
        var project = await context.WorldProjects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) throw new WorldOperationException(WorldErrorKind.NotFound, "项目不存在或无权访问");
        if (!WorldAccess.IsOwner(project.OwnerId, userId)) return null;

        var name = dto.Name?.Trim();
        if (!string.IsNullOrEmpty(name)) project.Name = name;
        if (dto.Description != null) project.Description = dto.Description;
        if (dto.CoverImage != null) project.CoverImage = dto.CoverImage;
        if (dto.GraphPositions != null) project.GraphPositions = dto.GraphPositions;
        if (dto.IsPublic.HasValue) project.IsPublic = dto.IsPublic.Value;
        project.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        WorldCache.InvalidateProject(cache, projectId, userId);

        // 写接口沿用 OwnerName=null，计数从数据库取，不依赖导航实体是否被加载。
        var result = WorldProjectMapper.FromEntity(project);
        result.OwnerName = null;
        result.CardCount = await context.WorldCards.CountAsync(c => c.ProjectId == projectId);
        result.CollaboratorCount = await context.WorldCollaborators.CountAsync(c => c.ProjectId == projectId);
        result.MyRole = WorldAccess.RoleOwner;
        return result;
    }

    public async Task<bool> DeleteProjectAsync(Guid projectId, Guid userId)
    {
        var project = await context.WorldProjects.FirstOrDefaultAsync(p => p.Id == projectId);
        if (project == null) throw new WorldOperationException(WorldErrorKind.NotFound, "项目不存在或无权访问");
        if (!WorldAccess.IsOwner(project.OwnerId, userId)) return false;

        // 关系的外键为 Restrict，先删除项目内卡片的双向关系，再删除项目。
        var cardIds = await context.WorldCards.Where(c => c.ProjectId == projectId).Select(c => c.Id).ToArrayAsync();
        var relations = await context.WorldRelations
            .Where(r => Enumerable.Contains(cardIds, r.SourceCardId) || Enumerable.Contains(cardIds, r.TargetCardId)).ToListAsync();
        var affectedIds = relations.SelectMany(r => new[] { r.SourceCardId, r.TargetCardId }).Concat(cardIds).ToArray();
        context.WorldRelations.RemoveRange(relations);
        context.WorldProjects.Remove(project);
        await context.SaveChangesAsync();
        // 协作者表没有数据库外键，项目删掉后手动清理
        await context.WorldCollaborators.Where(c => c.ProjectId == projectId).ExecuteDeleteAsync();
        // 编年史（fix102）也是手动建的表，一起清掉
        await context.WorldTimelineEvents.Where(e => e.ProjectId == projectId).ExecuteDeleteAsync();
        await context.WorldTimelines.Where(t => t.ProjectId == projectId).ExecuteDeleteAsync();
        await context.WorldCalendars.Where(c => c.ProjectId == projectId).ExecuteDeleteAsync();
        // 编年史修改记录（fix121）：世界都删了，记录也没用了；清不掉（比如表还没建出来）也不影响删除
        try { await context.WorldChronicleRevisions.Where(r => r.ProjectId == projectId).ExecuteDeleteAsync(); }
        catch (Exception) { /* 忽略：只是留下几条没人看得到的旧记录 */ }
        // 时间线颜色图例（fix121）：表不存在也不影响删除世界
        try { await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM WorldColorLegends WHERE ProjectId = {projectId.ToString()}"); }
        catch (Exception) { /* 图例表还没建好时忽略 */ }
        WorldCache.InvalidateCards(cache, affectedIds);
        WorldCache.InvalidateProject(cache, projectId, userId);
        await quotaService.DecrementUsedWorldCountAsync(userId);
        return true;
    }

    public Task<bool> IsProjectOwnerAsync(Guid projectId, Guid userId) =>
        context.WorldProjects.AsNoTracking().OwnedBy(userId).AnyAsync(p => p.Id == projectId);

    public Task<bool> IsProjectAccessibleAsync(Guid projectId, Guid userId) =>
        context.WorldProjects.AsNoTracking().ReadableBy(userId).AnyAsync(p => p.Id == projectId);
}
