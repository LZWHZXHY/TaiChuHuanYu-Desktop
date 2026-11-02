using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services;
using TaiChuWeb_V2.Services.Personalization;

namespace TaiChuWeb_V2.Controllers
{
    /// <summary>
    /// 赞助福利目录：赞助墙上公开展示「每个等级有什么福利」，超级管理员可以随时修改（管理面板 → 赞助福利）。
    /// 带 Key 的福利就是网站真正判断的特权，改它的最低等级马上生效；不带 Key 的只是展示文字。
    /// </summary>
    [ApiController]
    [Route("api/SponsorPerks")]
    public class SponsorPerksController(AppDbContext db, SystemConfigService config, SponsorPerkService perks) : ControllerBase
    {
        private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

        private async Task<bool> IsSuperAdminAsync()
        {
            var me = Me;
            if (me == Guid.Empty) return false;
            return await db.UserPermissions.AnyAsync(p => p.UserId == me && p.Permission == AdminPermission.SuperAdmin);
        }

        /// <summary>
        /// 公开的福利目录。普通人只看到公开的福利；超级管理员拿到全部（包括隐藏的）和可编辑标记。
        /// 登录了的顺便返回自己的赞助状态（等级、到期时间），赞助墙上显示「你现在是 X，到 Y 为止」。
        /// </summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Get()
        {
            var catalog = await SponsorPerkCatalog.LoadAsync(config);
            var admin = await IsSuperAdminAsync();
            object? mine = null;
            if (Me != Guid.Empty)
            {
                var perk = await perks.GetAsync(Me);
                mine = new { active = perk.Active, tier = perk.Active ? perk.Tier : null, rank = perk.Active ? SponsorPerkService.TierRank(perk.Tier) : 0, until = perk.Until };
            }
            return Ok(Dto(catalog, admin, mine));
        }

        /// <summary>超级管理员保存福利目录。带 Key 的福利不能删（被删的会自动加回来），只能隐藏</summary>
        [HttpPut]
        [Authorize]
        public async Task<IActionResult> Save([FromBody] JsonElement body)
        {
            if (!await IsSuperAdminAsync()) return StatusCode(403, new { message = "只有超级管理员能修改赞助福利" });
            // 自己解析：新加的福利 id / key 是空的，自动模型绑定会当成必填项没填而拒绝
            PerkCatalog? dto;
            try { dto = JsonSerializer.Deserialize<PerkCatalog>(body.GetRawText(), SponsorPerkCatalog.Json); }
            catch (JsonException) { return BadRequest(new { message = "内容格式不正确" }); }
            var current = await SponsorPerkCatalog.LoadAsync(config);
            var clean = SponsorPerkCatalog.Clean(dto, current, out var error);
            if (clean == null) return BadRequest(new { message = error });
            await SponsorPerkCatalog.SaveAsync(db, config, clean, Me);
            return Ok(Dto(clean, true, null));
        }

        private static object Dto(PerkCatalog c, bool admin, object? mine) => new
        {
            version = c.Version,
            intro = c.Intro,
            tiers = c.Tiers.Select(t => new { rank = t.Rank, name = SponsorPerkService.Tiers[t.Rank - 1], price = t.Price, blurb = t.Blurb }),
            perks = c.Perks.Where(p => admin || p.Visible).Select(p => new
            {
                id = p.Id,
                key = p.Key,
                title = p.Title,
                description = p.Description,
                minTier = p.MinTier,
                visible = p.Visible,
            }),
            // 管理员编辑时用：哪些 Key 是代码里真正判断的特权、默认等级是多少
            builtIns = admin ? SponsorPerkCatalog.BuiltIns.Select(b => new { key = b.Key, title = b.Title, defaultTier = SponsorPerkService.DefaultFeatures.TryGetValue(b.Key, out var t) ? t : 1 }) : null,
            canEdit = admin,
            mine,
        };
    }
}
