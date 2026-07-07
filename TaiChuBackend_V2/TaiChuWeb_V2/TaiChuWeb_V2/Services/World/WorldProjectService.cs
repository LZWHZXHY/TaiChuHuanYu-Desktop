using Microsoft.EntityFrameworkCore;
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

        public WorldProjectService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IEnumerable<ProjectResponseDto>> GetUserProjectsAsync(Guid userId)
        {
            var projects = await _context.WorldProjects
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
                    OwnerName = p.Owner != null ? p.Owner.Username : null
                })
                .ToListAsync();

            return projects;
        }

        public async Task<ProjectResponseDto> GetProjectByIdAsync(Guid projectId, Guid userId)
        {
            var project = await _context.WorldProjects
                .Include(p => p.Owner)
                .Include(p => p.Cards)
                .FirstOrDefaultAsync(p => p.Id == projectId);

            if (project == null)
                return null;

            // 检查权限：公开或属于当前用户
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
                OwnerName = project.Owner?.Username
            };
        }

        public async Task<ProjectResponseDto> CreateProjectAsync(Guid userId, CreateProjectDto dto)
        {
            var project = new WorldProject
            {
                Name = dto.Name,
                Description = dto.Description,
                IsPublic = dto.IsPublic,
                OwnerId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _context.WorldProjects.AddAsync(project);
            await _context.SaveChangesAsync();

            return new ProjectResponseDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                IsPublic = project.IsPublic,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt,
                CardCount = 0,
                OwnerName = null // 后续可通过查询获取
            };
        }

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

            return new ProjectResponseDto
            {
                Id = project.Id,
                Name = project.Name,
                Description = project.Description,
                IsPublic = project.IsPublic,
                CreatedAt = project.CreatedAt,
                UpdatedAt = project.UpdatedAt,
                CardCount = await _context.WorldCards.CountAsync(c => c.ProjectId == projectId),
                OwnerName = null
            };
        }

        public async Task<bool> DeleteProjectAsync(Guid projectId, Guid userId)
        {
            var project = await _context.WorldProjects
                .FirstOrDefaultAsync(p => p.Id == projectId && p.OwnerId == userId);

            if (project == null)
                return false;

            _context.WorldProjects.Remove(project);
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<bool> IsProjectOwnerAsync(Guid projectId, Guid userId)
        {
            return await _context.WorldProjects
                .AnyAsync(p => p.Id == projectId && p.OwnerId == userId);
        }
    }
}