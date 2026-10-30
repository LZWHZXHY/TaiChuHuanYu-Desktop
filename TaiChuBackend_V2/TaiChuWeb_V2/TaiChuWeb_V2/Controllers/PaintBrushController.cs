using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Paint;

namespace TaiChuWeb_V2.Controllers
{
    public sealed class PaintBrushSaveDto
    {
        public string? Name { get; set; }
        public PaintBrushParams? Params { get; set; }
        public bool IsPublic { get; set; }
    }

    /// <summary>
    /// 茶绘笔刷库：自己的笔刷（跟账号保存）、公开笔刷（知识库「茶绘笔刷」分类 / 画室里的笔刷库）、收藏、笔尖图。
    /// </summary>
    [ApiController]
    [Route("api/PaintBrush")]
    public class PaintBrushController(PaintBrushService brushes, AppDbContext db) : ControllerBase
    {
        private Guid? CurrentUser => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        private async Task<List<object>> ViewsAsync(IEnumerable<PaintBrushRow> rows, Guid? me)
        {
            var list = rows.ToList();
            var ownerIds = list.Select(r => r.OwnerId).Distinct().ToList();
            var names = await db.Users.AsNoTracking().Where(u => ownerIds.Contains(u.Id)).Select(u => new { u.Id, u.Username }).ToDictionaryAsync(u => u.Id, u => u.Username);
            return list.Select(r => (object)new
            {
                id = r.Id, ownerId = r.OwnerId, ownerName = names.GetValueOrDefault(r.OwnerId) ?? "画师",
                name = r.Name, @params = r.Params, isPublic = r.IsPublic, favorites = r.Favorites,
                favorited = r.Favorited, mine = me == r.OwnerId, createdAt = r.CreatedAt, updatedAt = r.UpdatedAt,
            }).ToList();
        }

        /// <summary>我的笔刷：自己做的 + 收藏的</summary>
        [Authorize]
        [HttpGet("mine")]
        public async Task<IActionResult> Mine()
        {
            var me = CurrentUser!.Value;
            return Ok(await ViewsAsync(await brushes.MineAsync(me), me));
        }

        /// <summary>公开笔刷：sort = hot / new，q 按名字搜索。不登录也能看</summary>
        [AllowAnonymous]
        [HttpGet("square")]
        public async Task<IActionResult> Square([FromQuery] string? sort, [FromQuery] string? q, [FromQuery] int offset = 0, [FromQuery] int limit = 24)
        {
            if (q?.Length > 50) return BadRequest(new { message = "搜索词太长" });
            var me = CurrentUser;
            var (items, total) = await brushes.SquareAsync(me, sort, q, offset, limit);
            return Ok(new { items = await ViewsAsync(items, me), total });
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] PaintBrushSaveDto dto)
        {
            var me = CurrentUser!.Value;
            var (row, error) = await brushes.CreateAsync(me, dto.Name, dto.Params, dto.IsPublic);
            return row == null ? BadRequest(new { message = error }) : Ok((await ViewsAsync(new[] { row }, me))[0]);
        }

        [Authorize]
        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] PaintBrushSaveDto dto)
        {
            var me = CurrentUser!.Value;
            var (row, error) = await brushes.UpdateAsync(me, id, dto.Name, dto.Params, dto.IsPublic);
            return row == null ? BadRequest(new { message = error }) : Ok((await ViewsAsync(new[] { row }, me))[0]);
        }

        [Authorize]
        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var error = await brushes.DeleteAsync(CurrentUser!.Value, id);
            return error == null ? Ok(new { ok = true }) : BadRequest(new { message = error });
        }

        [Authorize]
        [HttpPost("{id:guid}/favorite")]
        public async Task<IActionResult> Favorite(Guid id)
        {
            var (count, error) = await brushes.FavoriteAsync(CurrentUser!.Value, id, true);
            return error == null ? Ok(new { favorites = count, favorited = true }) : BadRequest(new { message = error });
        }

        [Authorize]
        [HttpDelete("{id:guid}/favorite")]
        public async Task<IActionResult> Unfavorite(Guid id)
        {
            var (count, error) = await brushes.FavoriteAsync(CurrentUser!.Value, id, false);
            return error == null ? Ok(new { favorites = count, favorited = false }) : BadRequest(new { message = error });
        }

        /// <summary>上传笔尖图（PNG，multipart 字段 file），返回内容哈希</summary>
        [Authorize]
        [HttpPost("tips")]
        [RequestSizeLimit(PaintBrushService.MaxTipBytes + 16 * 1024)]
        public async Task<IActionResult> UploadTip(IFormFile file)
        {
            if (file == null || file.Length == 0) return BadRequest(new { message = "没有收到图片" });
            if (file.Length > PaintBrushService.MaxTipBytes) return BadRequest(new { message = "笔尖图太大了" });
            using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var (hash, error) = await brushes.SaveTipAsync(ms.ToArray());
            return hash == null ? BadRequest(new { message = error }) : Ok(new { hash });
        }

        /// <summary>取笔尖图。地址由内容决定，永远不变，可以长期缓存</summary>
        [AllowAnonymous]
        [HttpGet("tips/{hash}")]
        public async Task<IActionResult> Tip(string hash)
        {
            var png = await brushes.GetTipAsync(hash);
            if (png == null) return NotFound();
            Response.Headers.CacheControl = "public, max-age=31536000, immutable";
            return File(png, "image/png");
        }
    }
}
