using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.Controllers.Users;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Personalization;
using TaiChuWeb_V2.Services.Shop;

namespace TaiChuWeb_V2.Controllers.Admin
{
    public sealed class SavePresetDto
    {
        public string Title { get; set; } = string.Empty;
        public string Kind { get; set; } = "image";
        public string Url { get; set; } = string.Empty;
        public string? Poster { get; set; }
        public int SortOrder { get; set; }
        public bool Enabled { get; set; } = true;
        /// <summary>原作者（社区投稿被采用的图）：作者自动免费拥有</summary>
        public Guid? AuthorId { get; set; }
        /// <summary>主页素材用：banner 横幅 / page 整页背景</summary>
        public string? Slot { get; set; }
    }

    public sealed class GrantPerkDto
    {
        public Guid UserId { get; set; }
        public int Days { get; set; } = 30;
        public string Tier { get; set; } = "觉醒者";
        public string? Note { get; set; }
    }

    public sealed class RevokePerkDto
    {
        public Guid UserId { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>
    /// 管理后台 · 网站背景与赞助特权：维护官方网站背景、个人主页素材（横幅 / 整页背景）；给账号手动开通 / 延长 / 收回赞助特权。
    /// 素材可以填原作者，保存时自动发给作者（免费拥有，交易行出售时也不用再买）。
    /// AdminAccessFilter 里没有为它单独配权限，所以只有超级管理员能用（和赞助墙管理一致）。
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/Admin/Appearance")]
    public class AdminAppearanceController(UserAppearanceService appearance, SponsorPerkService perks, ProfileDecorService decor, ShopService shop, AppDbContext db) : ControllerBase
    {
        private Guid CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

        // ===================== 官方背景 =====================

        [HttpGet("presets")]
        public async Task<IActionResult> Presets()
        {
            var list = await appearance.ListPresetsAsync(enabledOnly: false);
            var users = await UsersAsync(list.Where(p => p.AuthorId != null).Select(p => p.AuthorId!.Value));
            return Ok(list.Select(p => new
            {
                id = p.Id, title = p.Title, kind = p.Kind, url = p.Url, poster = p.Poster, sortOrder = p.SortOrder, enabled = p.Enabled,
                authorId = p.AuthorId,
                authorName = p.AuthorId is Guid a && users.TryGetValue(a, out var u) ? u.Name : null,
            }));
        }

        private async Task<string?> CheckAuthorAsync(Guid? authorId)
            => authorId is Guid a && a != Guid.Empty && !await db.Users.AnyAsync(u => u.Id == a) ? "原作者账号不存在" : null;

        [HttpPut("presets")]
        public async Task<IActionResult> SavePreset([FromBody] SavePresetDto dto, [FromQuery] int? id)
        {
            var title = (dto.Title ?? string.Empty).Trim();
            if (title.Length is < 1 or > 60) return BadRequest(new { message = "名称需要 1–60 个字" });
            if (!UserAppearanceService.IsSafeUrl(dto.Url)) return BadRequest(new { message = "请先上传图片或视频" });
            if (!string.IsNullOrEmpty(dto.Poster) && !UserAppearanceService.IsSafeUrl(dto.Poster)) return BadRequest(new { message = "封面地址无效" });
            if (id is int existing && await appearance.GetPresetAsync(existing) == null) return NotFound(new { message = "这个背景不存在" });
            if (await CheckAuthorAsync(dto.AuthorId) is { } authorErr) return BadRequest(new { message = authorErr });
            var author = dto.AuthorId == Guid.Empty ? null : dto.AuthorId;
            var newId = await appearance.SavePresetAsync(id, title, dto.Kind, dto.Url, dto.Poster, dto.SortOrder, dto.Enabled, author);
            await shop.GrantToAuthorAsync(author, ShopService.EffectBackground, newId.ToString());
            return Ok(new { id = newId });
        }

        [HttpDelete("presets/{id:int}")]
        public async Task<IActionResult> DeletePreset(int id)
        {
            await appearance.DeletePresetAsync(id);
            return NoContent();
        }

        // ===================== 个人主页素材（横幅 / 整页背景，独立素材库） =====================

        [HttpGet("profile-presets")]
        public async Task<IActionResult> ProfilePresets()
        {
            var list = await decor.ListPresetsAsync(enabledOnly: false);
            var users = await UsersAsync(list.Where(p => p.AuthorId != null).Select(p => p.AuthorId!.Value));
            return Ok(list.Select(p => new
            {
                id = p.Id, slot = p.Slot, title = p.Title, kind = p.Kind, url = p.Url, poster = p.Poster, sortOrder = p.SortOrder, enabled = p.Enabled,
                authorId = p.AuthorId,
                authorName = p.AuthorId is Guid a && users.TryGetValue(a, out var u) ? u.Name : null,
            }));
        }

        [HttpPut("profile-presets")]
        public async Task<IActionResult> SaveProfilePreset([FromBody] SavePresetDto dto, [FromQuery] int? id)
        {
            var title = (dto.Title ?? string.Empty).Trim();
            if (title.Length is < 1 or > 60) return BadRequest(new { message = "名称需要 1–60 个字" });
            if (!ProfileDecorService.Slots.Contains(dto.Slot ?? "")) return BadRequest(new { message = "请选择用途：横幅还是整页背景" });
            if (!UserAppearanceService.IsSafeUrl(dto.Url)) return BadRequest(new { message = "请先上传图片或视频" });
            if (!string.IsNullOrEmpty(dto.Poster) && !UserAppearanceService.IsSafeUrl(dto.Poster)) return BadRequest(new { message = "封面地址无效" });
            if (id is int existing && await decor.GetPresetAsync(existing) == null) return NotFound(new { message = "这个素材不存在" });
            if (await CheckAuthorAsync(dto.AuthorId) is { } authorErr) return BadRequest(new { message = authorErr });
            var author = dto.AuthorId == Guid.Empty ? null : dto.AuthorId;
            var newId = await decor.SavePresetAsync(id, dto.Slot!, title, dto.Kind, dto.Url, dto.Poster, author, dto.SortOrder, dto.Enabled);
            await shop.GrantToAuthorAsync(author, ShopService.EffectProfileBg, newId.ToString());
            return Ok(new { id = newId });
        }

        [HttpDelete("profile-presets/{id:int}")]
        public async Task<IActionResult> DeleteProfilePreset(int id)
        {
            await decor.DeletePresetAsync(id);
            return NoContent();
        }

        // ===================== 赞助特权 =====================

        private async Task<Dictionary<Guid, (string Name, string? Avatar)>> UsersAsync(IEnumerable<Guid> ids)
        {
            var list = ids.Distinct().ToList();
            var users = await db.Users.AsNoTracking().Where(u => list.Contains(u.Id))
                .Select(u => new { u.Id, u.Username, Avatar = u.Profile != null ? u.Profile.Avatar : null }).ToListAsync();
            return users.ToDictionary(u => u.Id, u => (u.Username, (string?)u.Avatar));
        }

        [HttpGet("perks")]
        public async Task<IActionResult> Perks()
        {
            var rows = await perks.ListAsync();
            var users = await UsersAsync(rows.Select(r => r.UserId));
            var now = DateTime.UtcNow;
            return Ok(new
            {
                tiers = SponsorPerkService.Tiers,
                features = SponsorPerkService.Features,
                days = SponsorPerkService.DaysPerMonth,
                minAmount = perks.MinAmount,
                items = rows.Select(r => new
                {
                    userId = r.UserId,
                    name = users.TryGetValue(r.UserId, out var u) ? u.Name : "（已注销）",
                    avatar = users.TryGetValue(r.UserId, out var u2) ? u2.Avatar : null,
                    until = r.Until,
                    active = r.Until is DateTime t && t > now,
                    tier = r.Tier,
                    afdianBound = r.AfdianUserId != null,
                    updatedAt = r.UpdatedAt,
                }),
            });
        }

        [HttpGet("perks/log")]
        public async Task<IActionResult> PerkLog([FromQuery] Guid? userId, [FromQuery] int limit = 100)
        {
            var rows = await perks.LogAsync(userId, limit);
            var users = await UsersAsync(rows.Select(r => r.UserId));
            return Ok(rows.Select(r => new
            {
                id = r.Id,
                userId = r.UserId,
                name = users.TryGetValue(r.UserId, out var u) ? u.Name : "（已注销）",
                source = r.Source,
                refId = r.RefId,
                days = r.Days,
                tier = r.Tier,
                untilAfter = r.UntilAfter,
                note = r.Note,
                createdAt = r.CreatedAt,
            }));
        }

        /// <summary>按用户名查账号（开通特权时用）</summary>
        [HttpGet("users")]
        public async Task<IActionResult> FindUsers([FromQuery] string q)
        {
            q = (q ?? string.Empty).Trim();
            if (q.Length < 1) return Ok(Array.Empty<object>());
            var users = await db.Users.AsNoTracking()
                .Where(u => u.Username.Contains(q))
                .OrderBy(u => u.Username.Length)
                .Take(10)
                .Select(u => new { userId = u.Id, name = u.Username, avatar = u.Profile != null ? u.Profile.Avatar : null })
                .ToListAsync();
            return Ok(users);
        }

        [HttpPost("perks/grant")]
        public async Task<IActionResult> Grant([FromBody] GrantPerkDto dto)
        {
            if (dto.Days is < 1 or > 3650) return BadRequest(new { message = "天数需要在 1–3650 之间" });
            if (!await db.Users.AnyAsync(u => u.Id == dto.UserId)) return NotFound(new { message = "账号不存在" });
            var until = await perks.GrantAsync(dto.UserId, dto.Days, dto.Tier, dto.Note, CurrentUserId);
            return Ok(new { until });
        }

        [HttpPost("perks/revoke")]
        public async Task<IActionResult> Revoke([FromBody] RevokePerkDto dto)
        {
            await perks.RevokeAsync(dto.UserId, dto.Note, CurrentUserId);
            return Ok(new { message = "已收回" });
        }
    }
}
