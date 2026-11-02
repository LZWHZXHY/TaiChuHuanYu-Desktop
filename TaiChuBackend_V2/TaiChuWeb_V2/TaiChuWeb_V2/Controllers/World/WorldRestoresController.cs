using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World;

[Authorize, Route("api/world/restores")]
public class WorldRestoresController(WorldRestoreService service) : WorldControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(Guid projectId) => Ok(await service.ListAsync(CurrentUserId, projectId));

    [HttpPost]
    public async Task<IActionResult> Start(StartWorldRestoreDto dto) => Ok(await service.StartAsync(CurrentUserId, dto));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Status(Guid id) => Ok(await service.StatusAsync(CurrentUserId, id));

    [HttpPut("{id:guid}/images/{name}")]
    [RequestSizeLimit(34 * 1024 * 1024), RequestFormLimits(MultipartBodyLengthLimit = 34 * 1024 * 1024)]
    public async Task<IActionResult> Upload(Guid id, string name, IFormFile file) =>
        Ok(await service.UploadAsync(CurrentUserId, id, name, file));

    // 提交时可以带上编年史（时间线、事件、历法）；不带也照常恢复条目（兼容老页面）
    [HttpPost("{id:guid}/commit"), RequestSizeLimit(128 * 1024 * 1024)]
    public async Task<IActionResult> Commit(Guid id, CommitWorldRestoreWithChronicleDto dto) =>
        Ok(await service.CommitAsync(CurrentUserId, id, dto));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id) => Ok(await service.CancelAsync(CurrentUserId, id));
}
