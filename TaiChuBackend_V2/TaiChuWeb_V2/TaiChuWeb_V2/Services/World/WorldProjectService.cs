using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.World;

namespace TaiChuWeb_V2.Services.World
{
    public class WorldProjectService : IWorldProjectService
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;

        public WorldProjectService(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        // ============================================================
        //  1. 获取用户的所有项目（带缓存）
        // ============================================================
        public async Task<IEnumerable<ProjectResponseDto>> GetUserProjectsAsync(Guid userId)
        {
            var cacheKey = $"user_projects_{userId}";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<ProjectResponseDto>? cached))
                return cached ?? new List<ProjectResponseDto>();

            var projects = await _context.WorldProjects
                .AsNoTracking()
                .Where(p => p.OwnerId == userId)
                .Select(p => new ProjectResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    IsPublic = p.IsPublic,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    CardCount = p.Cards.Count,
                    OwnerName = p.Owner != null ? p.Owner.Username : null,
                    OwnerId = p.OwnerId
                })
                .ToListAsync();

            _cache.Set(cacheKey, projects, TimeSpan.FromMinutes(5));
            return projects;
        }

        // ============================================================
        //  2. 获取公开项目列表（带缓存）
        // ============================================================
        public async Task<IEnumerable<ProjectResponseDto>> GetPublicProjectsAsync()
        {
            const string cacheKey = "public_projects";

            if (_cache.TryGetValue(cacheKey, out IEnumerable<ProjectResponseDto>? cached))
                return cached ?? new List<ProjectResponseDto>();

            var projects = await _context.WorldProjects
                .AsNoTracking()
                .Where(p => p.IsPublic)
                .OrderByDescending(p => p.UpdatedAt)
                .Select(p => new ProjectResponseDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Description = p.Description,
                    IsPublic = p.IsPublic,
                    CreatedAt = p.CreatedAt,
                    UpdatedAt = p.UpdatedAt,
                    CardCount = p.Cards.Count,
                    OwnerName = p.Owner != null ? p.Owner.Username : null,
                    OwnerId = p.OwnerId
                })
                .ToListAsync();

            _cache.Set(cacheKey, projects, TimeSpan.FromMinutes(5));
            return projects;
        }

        // ============================================================
        //  3. 获取项目详情（轻量级，不含卡片列表）
        // ============================================================
        public async Task<ProjectResponseDto> GetProjectByIdAsync(Guid projectId, Guid userId)
        {
            var project = await _context.WorldProjects
                .AsNoTracking()
                .Where(p => p.Id == projectId)
                .Select(p => new
                {
                    p.Id,
                    p.Name,
                    p.Description,
                    p.IsPublic,
                    p.OwnerId,
                    p.CreatedAt,
                    p.UpdatedAt,
                    OwnerName = p.Owner != null ? p.Owner.Username : null,
                    CardCount = p.Cards.Count
                })
                .FirstOrDefaultAsync();

            if (project == null)
                return null;

            // 权限检查：公开或属于当前用户
            if (!project.IsPublic && project.OwnerId != userId)
                return null;

            return new ProjectResponseDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                IsPublic = project.IsPublic,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt,
                CardCount = project.CardCount,
                OwnerName = project.OwnerName,
                OwnerId = project.OwnerId
            };
        }

        // ============================================================
        //  4. 获取项目详情（包含卡片列表）- 用于管理页面
        // ============================================================
        public async Task<ProjectResponseDto> GetProjectWithCardsAsync(Guid projectId, Guid userId)
        {
            var project = await _context.WorldProjects
                .AsNoTracking()
                .Include(p => p.Owner)
                .Include(p => p.Cards)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return null;

            if (!project.IsPublic && project.OwnerId != userId)
                return null;

            return new ProjectResponseDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                IsPublic = project.IsPublic,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt,
                CardCount = project.Cards.Count,
                OwnerName = project.Owner?.Username,
                OwnerId = project.OwnerId
            };
        }

        // ============================================================
        //  5. 创建项目
        // ============================================================
        public async Task<ProjectResponseDto> CreateProjectAsync(Guid userId, CreateProjectDto dto)
        {
            var project = new WorldProject
            {
                Id = Guid.NewGuid(),
                Name = dto.Name,
                Description = dto.Description,
                IsPublic = dto.IsPublic,
                OwnerId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.WorldProjects.AddAsync(project);
            await _context.SaveChangesAsync();

            ClearProjectCache(userId);
            return new ProjectResponseDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                IsPublic = project.IsPublic,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt,
                CardCount = 0,
                OwnerName = null,
                OwnerId = userId
            };
        }

        // ============================================================
        //  6. 更新项目
        // ============================================================
        public async Task<ProjectResponseDto> UpdateProjectAsync(Guid projectId, Guid userId, UpdateProjectDto dto)
        {
            var project = await _context.WorldProjects
                .FirstOrDefaultAsync(p => p.Id == projectId && p.OwnerId == userId);

            if (project == null)
                return null;

            if (!string.IsNullOrEmpty(dto.Name))
                project.Name = dto.Name;

            if (dto.Description != null)
                project.Description = dto.Description;

            if (dto.IsPublic.HasValue)
                project.IsPublic = dto.IsPublic.Value;

            project.UpdatedAt = DateTime.UtcNow;

            _context.WorldProjects.Update(project);
            await _context.SaveChangesAsync();

            ClearProjectCache(userId);
            return new ProjectResponseDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                IsPublic = project.IsPublic,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt,
                CardCount = await _context.WorldCards.CountAsync(c => c.ProjectId == projectId),
                OwnerName = null,
                OwnerId = userId
            };
        }

        // ============================================================
        //  7. 删除项目
        // ============================================================
        public async Task<bool> DeleteProjectAsync(Guid projectId, Guid userId)
        {
            var project = await _context.WorldProjects
                .FirstOrDefaultAsync(p => p.Id == projectId && p.OwnerId == userId);

            if (project == null)
                return false;

            _context.WorldProjects.Remove(project);
            await _context.SaveChangesAsync();

            ClearProjectCache(userId);
            return true;
        }

        // ============================================================
        //  8. 权限验证（是否为项目所有者）
        // ============================================================
        public async Task<bool> IsProjectOwnerAsync(Guid projectId, Guid userId)
        {
            return await _context.WorldProjects
                .AsNoTracking()
                .AnyAsync(p => p.Id == projectId && p.OwnerId == userId);
        }

        // ============================================================
        //  9. 权限验证（是否有权访问 - 公开或所有者）
        // ============================================================
        public async Task<bool> IsProjectAccessibleAsync(Guid projectId, Guid userId)
        {
            return await _context.WorldProjects
                .AsNoTracking()
                .AnyAsync(p => p.Id == projectId && (p.IsPublic || p.OwnerId == userId));
        }

        // ============================================================
        //  10. 清除缓存
        // ============================================================
        private void ClearProjectCache(Guid userId)
        {
            _cache.Remove($"user_projects_{userId}");
            _cache.Remove("public_projects");
        }
    }
}