using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Services.Paint;

namespace TaiChuWeb_V2.Controllers
{
    /// <summary>
    /// 茶绘图层底图：图片比较大，不走 SignalR（默认单条消息只有 32KB），用普通 HTTP 上传 / 下载。
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/Paint")]
    public class PaintController(PaintManager manager) : ControllerBase
    {
        private Guid UserId => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        /// <summary>后进画室的人取图层底图（PNG）</summary>
        [HttpGet("{roomId}/layers/{layerId:int}/base")]
        public IActionResult GetBase(string roomId, int layerId, [FromQuery] int? v)
        {
            var (png, error, stale) = manager.GetBase(UserId, roomId, layerId, v);
            if (stale) return Conflict(new { message = error });
            if (png == null) return NotFound(new { message = error });
            Response.Headers.CacheControl = "no-store";
            return File(png, "image/png");
        }

        /// <summary>绘画过程回放的数据（笔画可能很多，走 HTTP）</summary>
        [HttpGet("{roomId}/history")]
        public async Task<IActionResult> History(string roomId)
        {
            var (data, error) = await manager.HistoryAsync(UserId, roomId);
            return data == null ? BadRequest(new { message = error }) : Ok(data);
        }

        /// <summary>油漆桶等工具的结果图（PNG，multipart，字段名 file），返回哈希</summary>
        [HttpPost("{roomId}/patches")]
        [RequestSizeLimit(PaintManager.MaxPatchBytes + 64 * 1024)]
        public async Task<IActionResult> UploadPatch(string roomId, IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest(new { message = "没有收到图片" });
            if (file.Length > PaintManager.MaxPatchBytes) return BadRequest(new { message = "图片太大" });
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var (hash, error) = await manager.AddPatchAsync(UserId, roomId, ms.ToArray());
            return hash == null ? BadRequest(new { message = error }) : Ok(new { hash });
        }

        /// <summary>取贴图。内容按哈希寻址，不会变，可以长时间缓存</summary>
        [HttpGet("{roomId}/patches/{hash}")]
        public IActionResult GetPatch(string roomId, string hash)
        {
            var (png, error) = manager.GetPatch(UserId, roomId, hash);
            if (png == null) return NotFound(new { message = error });
            Response.Headers.CacheControl = "private, max-age=86400, immutable";
            return File(png, "image/png");
        }

        /// <summary>客户端按服务器的请求把旧笔画合成 PNG 底图上传（multipart，字段名 file）</summary>
        [HttpPost("{roomId}/layers/{layerId:int}/base")]
        [RequestSizeLimit(PaintManager.MaxBaseBytes + 64 * 1024)]
        public async Task<IActionResult> UploadBase(string roomId, int layerId, [FromQuery] long upto, IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest(new { message = "没有收到图片" });
            if (file.Length > PaintManager.MaxBaseBytes) return BadRequest(new { message = "图片太大" });
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();
            // 只收 PNG
            if (bytes.Length < 8 || bytes[0] != 0x89 || bytes[1] != 0x50 || bytes[2] != 0x4E || bytes[3] != 0x47) return BadRequest(new { message = "只接受 PNG" });
            var error = await manager.AcceptBakeAsync(UserId, roomId, layerId, upto, bytes);
            return error == null ? Ok(new { ok = true }) : BadRequest(new { message = error });
        }
    }
}
