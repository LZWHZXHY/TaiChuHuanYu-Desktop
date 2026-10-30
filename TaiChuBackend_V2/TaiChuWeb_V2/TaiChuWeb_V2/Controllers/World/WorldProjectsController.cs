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
        // 创建者和「编写者」协作者都能调整图谱布局
        if (!await WorldAccess.CanEditProjectAsync(context, id, CurrentUserId)) return Forbid();
        // 必须携带读取时的坐标。旧页面没有此字段时拒绝写入，避免绕过并发保护。
        if (dto.ExpectedPositions == null)
            return StatusCode(428, new { message = "图谱页面版本过旧，请刷新后重试。" });
        if (dto.Positions == null) return BadRequest(new { message = "节点坐标不能为空" });

        // 比较和写入在同一条 SQL 内完成，两个请求不能同时通过旧值校验。
        var changed = await context.WorldProjects
            .EditableBy(CurrentUserId).Where(p => p.Id == id && (p.GraphPositions ?? "{}") == dto.ExpectedPositions)
            .ExecuteUpdateAsync(update => update.SetProperty(p => p.GraphPositions, dto.Positions));
        if (changed > 0) return NoContent();
        var current = await context.WorldProjects.AsNoTracking().EditableBy(CurrentUserId).Where(p => p.Id == id)
            .Select(p => new { p.GraphPositions }).FirstOrDefaultAsync();
        if (current == null) return NotFound(new { message = "项目不存在或无权修改" });
        // 丢失成功回包后的相同请求可安全重试，不再写入。
        if (current.GraphPositions == dto.Positions) return NoContent();
        return Conflict(new { message = "布局已在其他页面更新，请载入最新布局后再调整。", positions = current.GraphPositions ?? "{}" });
    }
}

public class GraphPositionsDto
{
    public string? Positions { get; set; }
    public string? ExpectedPositions { get; set; }
}
