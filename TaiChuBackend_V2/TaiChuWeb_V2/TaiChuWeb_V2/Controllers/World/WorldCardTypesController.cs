using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World;

[Route("api/world/card-types")]
[Authorize(Roles = "Admin")]
public class WorldCardTypesController(ICardTypeService typeService) : WorldControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    [WorldExceptionFilter(FailureMessage = "获取卡片类型失败")]
    public async Task<IActionResult> GetCardTypes() => Ok(await typeService.GetAllActiveTypesAsync());

    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCardType(string id)
    {
        var type = await typeService.GetTypeByIdAsync(id);
        return type == null || !type.IsActive
            ? NotFound(new { message = $"卡片类型 '{id}' 不存在" }) : Ok(type);
    }

    [HttpPost]
    public async Task<IActionResult> CreateCardType([FromBody] CreateCardTypeDto dto)
    {
        var type = await typeService.CreateTypeAsync(dto);
        return CreatedAtAction(nameof(GetCardType), new { id = type.Id }, type);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCardType(string id, [FromBody] UpdateCardTypeDto dto)
    {
        var type = await typeService.UpdateTypeAsync(id, dto);
        return type == null ? NotFound(new { message = $"卡片类型 '{id}' 不存在" }) : Ok(type);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteCardType(string id)
    {
        var deleted = await typeService.DeleteTypeAsync(id);
        return deleted ? NoContent() : NotFound(new { message = $"卡片类型 '{id}' 不存在" });
    }
}
