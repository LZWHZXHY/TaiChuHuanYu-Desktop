using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World;

[Route("api/world/projects")]
[Authorize]
public class WorldProjectsController(IWorldProjectService projectService) : WorldControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetMyProjects() => Ok(await projectService.GetUserProjectsAsync(CurrentUserId));

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetProject(Guid id)
    {
        var project = await projectService.GetProjectByIdAsync(id, ReaderUserId);
        return project == null ? NotFound(new { message = "项目不存在或无权访问" }) : Ok(project);
    }

    [HttpGet("public")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPublicProjects() => Ok(await projectService.GetPublicProjectsAsync());

    [HttpPost]
    public async Task<IActionResult> CreateProject([FromBody] CreateProjectDto dto)
    {
        var project = await projectService.CreateProjectAsync(CurrentUserId, dto);
        return CreatedAtAction(nameof(GetProject), new { id = project.Id }, project);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateProject(Guid id, [FromBody] UpdateProjectDto dto)
    {
        var project = await projectService.UpdateProjectAsync(id, CurrentUserId, dto);
        return project == null ? NotFound(new { message = "项目不存在或无权修改" }) : Ok(project);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProject(Guid id)
    {
        var deleted = await projectService.DeleteProjectAsync(id, CurrentUserId);
        return deleted ? NoContent() : NotFound(new { message = "项目不存在或无权删除" });
    }
}
