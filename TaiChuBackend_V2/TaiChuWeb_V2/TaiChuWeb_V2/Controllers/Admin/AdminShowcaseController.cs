using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Services.Games;

namespace TaiChuWeb_V2.Controllers.Admin
{
    public sealed class SaveShowcaseDto
    {
        /// <summary>Steam AppID，或者直接粘贴 Steam 商店链接</summary>
        public string? Steam { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Tagline { get; set; }
        public string Status { get; set; } = "released";
        public bool Featured { get; set; }
        public int SortOrder { get; set; }
        public string? CoverUrl { get; set; }
        public string? DevProjectId { get; set; }
        public string? DiscussionTag { get; set; }
        public bool Visible { get; set; } = true;
    }

    /// <summary>管理后台 · 太虚游的 Steam 游戏展示（权限：Trade_Manage，由 AdminAccessFilter 校验）。</summary>
    [Authorize]
    [ApiController]
    [Route("api/Admin/Showcase")]
    public partial class AdminShowcaseController(GameShowcaseService service) : ControllerBase
    {
        [GeneratedRegex(@"(?:app/)?(\d{3,10})")]
        private static partial Regex AppIdPattern();

        /// <summary>从 AppID 或商店链接里取出数字 AppID</summary>
        public static int? ParseAppId(string? input)
        {
            if (string.IsNullOrWhiteSpace(input)) return null;
            var m = AppIdPattern().Match(input.Trim());
            return m.Success && int.TryParse(m.Groups[1].Value, out var id) ? id : null;
        }

        [HttpGet]
        public async Task<IActionResult> List() => Ok((await service.ListAsync(visibleOnly: false)).Select(GameShowcaseService.ToView));

        /// <summary>保存前预览：按 AppID 或链接从 Steam 拉取信息</summary>
        [HttpGet("lookup")]
        public async Task<IActionResult> Lookup([FromQuery] string steam)
        {
            if (ParseAppId(steam) is not int appId) return BadRequest(new { message = "没认出 AppID，请粘贴 Steam 商店链接或数字 AppID" });
            var info = await service.FetchSteamAsync(appId);
            if (info == null) return BadRequest(new { message = $"没能从 Steam 获取 AppID {appId} 的信息（可能是网络问题，或者商店页还没公开）。可以先手动填写标题和封面保存。" });
            return Ok(new { appId, info });
        }

        [HttpPut]
        public async Task<IActionResult> Save([FromBody] SaveShowcaseDto dto, [FromQuery] int? id)
        {
            var title = (dto.Title ?? string.Empty).Trim();
            if (title.Length is < 1 or > 100) return BadRequest(new { message = "游戏名称需要 1–100 个字" });
            int? appId = null;
            if (!string.IsNullOrWhiteSpace(dto.Steam))
            {
                appId = ParseAppId(dto.Steam);
                if (appId == null) return BadRequest(new { message = "没认出 AppID，请粘贴 Steam 商店链接或数字 AppID" });
            }
            static string? Clean(string? s, int max) { var t = (s ?? string.Empty).Trim(); return t.Length == 0 ? null : t.Length > max ? t[..max] : t; }
            var game = new ShowcaseGame
            {
                Id = id ?? 0,
                AppId = appId,
                Title = title,
                Tagline = Clean(dto.Tagline, 300) ?? string.Empty,
                Status = dto.Status is "released" or "coming" or "dev" ? dto.Status : "released",
                Featured = dto.Featured,
                SortOrder = dto.SortOrder,
                CoverUrl = Clean(dto.CoverUrl, 500),
                DevProjectId = Clean(dto.DevProjectId, 64),
                DiscussionTag = Clean(dto.DiscussionTag, 40)?.TrimStart('#'),
                Visible = dto.Visible,
            };
            var savedId = await service.SaveAsync(game);
            var saved = await service.GetAsync(savedId);
            if (saved != null) await service.RefreshAsync(new[] { saved }, force: saved.SteamJson == null);
            return Ok(saved == null ? null : GameShowcaseService.ToView(saved));
        }

        /// <summary>立即重新从 Steam 拉取（价格、截图更新后用）</summary>
        [HttpPost("{id:int}/refresh")]
        public async Task<IActionResult> Refresh(int id)
        {
            var game = await service.GetAsync(id);
            if (game == null) return NotFound();
            if (game.AppId == null) return BadRequest(new { message = "这个游戏还没有填写 Steam AppID" });
            await service.RefreshAsync(new[] { game }, force: true);
            var fresh = await service.GetAsync(id);
            return Ok(fresh == null ? null : GameShowcaseService.ToView(fresh));
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> Delete(int id)
        {
            await service.DeleteAsync(id);
            return NoContent();
        }
    }
}
