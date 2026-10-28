using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World;

[Route("api/world/projects")]
[Authorize]
public class WorldProjectsController(IWorldProjectService projectService, AppDbContext context) : WorldControllerBase
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

    /// <summary>只保存关系图谱节点坐标（轻量端点，不触发整项目更新/重取）。</summary>
    [HttpPut("{id:guid}/graph-positions")]
    public async Task<IActionResult> SaveGraphPositions(Guid id, [FromBody] GraphPositionsDto dto)
    {
        var project = await context.WorldProjects.FirstOrDefaultAsync(p => p.Id == id);
        if (project == null) return NotFound(new { message = "项目不存在" });
        if (project.OwnerId != CurrentUserId) return Forbid();
        project.GraphPositions = dto.Positions;
        await context.SaveChangesAsync();
        return NoContent();
    }
}

public class GraphPositionsDto
{
    public string? Positions { get; set; }
}
