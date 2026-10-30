using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Services.Games.Doudizhu;
using TaiChuWeb_V2.Services.Games.Go;
using TaiChuWeb_V2.Services.Games.Xiangqi;

namespace TaiChuWeb_V2.Controllers.Game
{
    /// <summary>
    /// 太虚游联机大厅（只读）。实时连接需要登录，这里给没登录、或者还没连上对战服务器的人看看现在开着哪些桌。
    /// </summary>
    [ApiController]
    [Route("api/game-lobby")]
    [AllowAnonymous]
    public class GameLobbyController(DdzManager ddz, XqManager xiangqi, GoManager go) : ControllerBase
    {
        [HttpGet("ddz")]
        public IActionResult Ddz() => Ok(ddz.Lobby());

        [HttpGet("xiangqi")]
        public IActionResult Xiangqi() => Ok(xiangqi.Lobby());

        [HttpGet("go")]
        public IActionResult Go() => Ok(go.Lobby());
    }
}
