using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World;

[Route("api/world/search")]
[Authorize]
public class WorldSearchController(IWorldCardService cardService) : WorldControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Search([FromQuery] string q, [FromQuery] int limit = 20)
        => Ok(await cardService.SearchCardsAsync(ReaderUserId, q, limit));
}
