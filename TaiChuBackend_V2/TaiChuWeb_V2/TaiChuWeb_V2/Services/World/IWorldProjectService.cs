using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.World;

namespace TaiChuWeb_V2.Services.World
{
    public interface IWorldProjectService
    {
        // ============================================================
        //  1. 项目列表（带缓存）
        // ============================================================

        /// <summary>
        /// 获取用户的所有项目
        /// </summary>
        Task<IEnumerable<ProjectResponseDto>> GetUserProjectsAsync(Guid userId);

        /// <summary>
        /// 获取所有公开项目（用于首页展示）
        /// </summary>
        Task<IEnumerable<ProjectResponseDto>> GetPublicProjectsAsync();

        // ============================================================
        //  2. 项目详情
        // ============================================================

        /// <summary>
        /// 获取项目详情（轻量级，不含卡片列表）
        /// </summary>
        Task<ProjectResponseDto> GetProjectByIdAsync(Guid projectId, Guid userId);

        /// <summary>
        /// 获取项目详情（含卡片列表）- 用于管理页面
        /// </summary>
        Task<ProjectResponseDto> GetProjectWithCardsAsync(Guid projectId, Guid userId);

        // ============================================================
        //  3. 项目 CRUD
        // ============================================================

        /// <summary>
        /// 创建新项目
        /// </summary>
        Task<ProjectResponseDto> CreateProjectAsync(Guid userId, CreateProjectDto dto);

        /// <summary>
        /// 更新项目
        /// </summary>
        Task<ProjectResponseDto> UpdateProjectAsync(Guid projectId, Guid userId, UpdateProjectDto dto);

        /// <summary>
        /// 删除项目
        /// </summary>
        Task<bool> DeleteProjectAsync(Guid projectId, Guid userId);

        // ============================================================
        //  4. 权限验证
        // ============================================================

        /// <summary>
        /// 检查用户是否是项目所有者
        /// </summary>
        Task<bool> IsProjectOwnerAsync(Guid projectId, Guid userId);

        /// <summary>
        /// 检查项目是否存在且用户有权限访问（轻量级）
        /// </summary>
        Task<bool> IsProjectAccessibleAsync(Guid projectId, Guid userId);
    }
}