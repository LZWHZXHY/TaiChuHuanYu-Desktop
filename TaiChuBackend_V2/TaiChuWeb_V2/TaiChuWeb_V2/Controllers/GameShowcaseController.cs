using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Services.Games;

namespace TaiChuWeb_V2.Controllers
{
    /// <summary>太虚游大厅：太初寰宇出品的游戏（Steam 等）。公开接口，不需要登录。</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class GameShowcaseController(GameShowcaseService service, IServiceScopeFactory scopes) : ControllerBase
    {
        [HttpGet]
        public async Task<IActionResult> List()
        {
            var games = await service.ListAsync(visibleOnly: true);
            // 从没抓过 Steam 数据的游戏当场抓一次（第一次打开会稍慢）；只是过期的放到后台刷新，先返回旧数据
            var missing = games.Where(g => g.AppId.HasValue && g.SteamJson == null).ToList();
            if (missing.Count > 0) await service.RefreshAsync(missing, force: true);
            if (games.Any(g => g.AppId.HasValue && g.SteamFetchedAt.HasValue && DateTime.UtcNow - g.SteamFetchedAt.Value > GameShowcaseService.CacheFor))
            {
                var stale = games.ToList();
                _ = Task.Run(async () =>
                {
                    using var scope = scopes.CreateScope();
                    await scope.ServiceProvider.GetRequiredService<GameShowcaseService>().RefreshAsync(stale, force: false);
                });
            }
            return Ok(games.Select(GameShowcaseService.ToView));
        }
    }
}
