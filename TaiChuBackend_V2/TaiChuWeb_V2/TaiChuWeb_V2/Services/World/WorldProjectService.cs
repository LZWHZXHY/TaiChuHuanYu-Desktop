using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.World;

namespace TaiChuWeb_V2.Services.World;

public class WorldProjectService(AppDbContext context, IMemoryCache cache, IWorldQuotaService quotaService) : IWorldProjectService
{
    public async Task<IEnumerable<ProjectResponseDto>> GetUserProjectsAsync(Guid userId)
    {
        var key = $"user_projects_{userId}";
        if (cache.TryGetValue(key, out IEnumerable<ProjectResponseDto>? cached)) return cached ?? [];
        var projects = await context.WorldProjects.AsNoTracking().OwnedBy(userId)
            .Select(WorldProjectMapper.Projection).ToListAsync();
        cache.Set(key, projects, TimeSpan.FromMinutes(5));
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

    public Task<ProjectResponseDto?> GetProjectByIdAsync(Guid projectId, Guid userId) =>
        context.WorldProjects.AsNoTracking().ReadableBy(userId).Where(p => p.Id == projectId)
            .Select(WorldProjectMapper.Projection).FirstOrDefaultAsync();

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
        return result;
    }

    public async Task<ProjectResponseDto?> UpdateProjectAsync(Guid projectId, Guid userId, UpdateProjectDto dto)
    {
        WorldValidation.Validate(dto);
        var project = await context.WorldProjects.FirstOrDefaultAsync(p => p.Id == projectId);
        WorldAccess.RequireReadable(project, userId);
        if (!WorldAccess.IsOwner(project!.OwnerId, userId)) return null;

        if (!string.IsNullOrEmpty(dto.Name)) project.Name = dto.Name;
        if (dto.Description != null) project.Description = dto.Description;
        if (dto.CoverImage != null) project.CoverImage = dto.CoverImage;
        if (dto.GraphPositions != null) project.GraphPositions = dto.GraphPositions;
        if (dto.IsPublic.HasValue) project.IsPublic = dto.IsPublic.Value;
        project.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        WorldCache.InvalidateProject(cache, projectId, userId);

        // 写接口沿用 OwnerName=null，CardCount 从数据库计数，不依赖导航实体是否被加载。
        var result = WorldProjectMapper.FromEntity(project);
        result.OwnerName = null;
        result.CardCount = await context.WorldCards.CountAsync(c => c.ProjectId == projectId);
        return result;
    }

    public async Task<bool> DeleteProjectAsync(Guid projectId, Guid userId)
    {
        var project = await context.WorldProjects.FirstOrDefaultAsync(p => p.Id == projectId);
        WorldAccess.RequireReadable(project, userId);
        if (!WorldAccess.IsOwner(project!.OwnerId, userId)) return false;

        // 关系的外键为 Restrict，先删除项目内卡片的双向关系，再删除项目。
        var cardIds = await context.WorldCards.Where(c => c.ProjectId == projectId).Select(c => c.Id).ToArrayAsync();
        var relations = await context.WorldRelations
            .Where(r => cardIds.Contains(r.SourceCardId) || cardIds.Contains(r.TargetCardId)).ToListAsync();
        var affectedIds = relations.SelectMany(r => new[] { r.SourceCardId, r.TargetCardId }).Concat(cardIds).ToArray();
        context.WorldRelations.RemoveRange(relations);
        context.WorldProjects.Remove(project);
        await context.SaveChangesAsync();
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
