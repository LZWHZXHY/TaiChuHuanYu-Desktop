using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.World;

namespace TaiChuWeb_V2.Services.World
{
    public interface IWorldProjectService
    {
        Task<IEnumerable<ProjectResponseDto>> GetUserProjectsAsync(Guid userId);
        Task<ProjectResponseDto> GetProjectByIdAsync(Guid projectId, Guid userId);
        Task<ProjectResponseDto> CreateProjectAsync(Guid userId, CreateProjectDto dto);
        Task<ProjectResponseDto> UpdateProjectAsync(Guid projectId, Guid userId, UpdateProjectDto dto);
        Task<bool> DeleteProjectAsync(Guid projectId, Guid userId);
        Task<bool> IsProjectOwnerAsync(Guid projectId, Guid userId);
    }
}