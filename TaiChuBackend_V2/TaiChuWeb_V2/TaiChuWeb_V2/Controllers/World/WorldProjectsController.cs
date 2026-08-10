using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World
{
    [Route("api/world/projects")]
    [ApiController]
    [Authorize]
    public class WorldProjectsController : ControllerBase
    {
        private readonly IWorldProjectService _projectService;

        public WorldProjectsController(IWorldProjectService projectService)
        {
            _projectService = projectService;
        }

        /// <summary>
        /// 获取当前用户的所有项目
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetMyProjects()
        {
            var userId = GetCurrentUserId();
            var projects = await _projectService.GetUserProjectsAsync(userId);
            return Ok(projects);
        }

        /// <summary>
        /// 获取单个项目详情
        /// </summary>
        [HttpGet("{id}")]
        public async Task<IActionResult> GetProject(Guid id)
        {
            var userId = GetCurrentUserId();

            // ✅ 优化：使用轻量级权限验证
            if (!await _projectService.IsProjectAccessibleAsync(id, userId))
                return NotFound(new { message = "项目不存在或无权访问" });

            var project = await _projectService.GetProjectByIdAsync(id, userId);
            return Ok(project);
        }

        /// <summary>
        /// 获取所有公开项目（无需登录）
        /// </summary>
        [HttpGet("public")]
        [AllowAnonymous]
        public async Task<IActionResult> GetPublicProjects()
        {
            var projects = await _projectService.GetPublicProjectsAsync();
            return Ok(projects);
        }

        /// <summary>
        /// 创建新项目
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var userId = GetCurrentUserId();
            var project = await _projectService.CreateProjectAsync(userId, dto);
            return CreatedAtAction(nameof(GetProject), new { id = project.Id }, project);
        }

        /// <summary>
        /// 更新项目
        /// </summary>
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateProject(Guid id, [FromBody] UpdateProjectDto dto)
        {
            var userId = GetCurrentUserId();

            if (!await _projectService.IsProjectAccessibleAsync(id, userId))
                return NotFound(new { message = "项目不存在或无权访问" });

            var project = await _projectService.UpdateProjectAsync(id, userId, dto);
            if (project == null)
                return NotFound(new { message = "项目不存在或无权修改" });

            return Ok(project);
        }

        /// <summary>
        /// 删除项目
        /// </summary>
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteProject(Guid id)
        {
            var userId = GetCurrentUserId();

            if (!await _projectService.IsProjectAccessibleAsync(id, userId))
                return NotFound(new { message = "项目不存在或无权访问" });

            var result = await _projectService.DeleteProjectAsync(id, userId);
            if (!result)
                return NotFound(new { message = "项目不存在或无权删除" });

            return NoContent();
        }

        /// <summary>
        /// 从 JWT 中获取当前用户 ID
        /// </summary>
        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("用户未认证");

            return Guid.Parse(userIdClaim);
        }
    }
}