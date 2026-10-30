using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Personalization;
using TaiChuWeb_V2.Services.Shop;

namespace TaiChuWeb_V2.Controllers.Users
{
    /// <summary>
    /// 个人主页装扮：顶部横幅 + 整页背景（别人访问你的主页时看到）。
    /// 官方素材（独立素材库）所有人可选，交易行在卖的要先兑换（可以先预览）；自己上传是赞助特权 custom-profile。
    /// 图片和视频由前端直传 COS，这里只保存地址和显示参数。
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/Users/ProfileDecor")]
    public class ProfileDecorController(ProfileDecorService decor, SponsorPerkService perks, ShopService shop) : ControllerBase
    {
        public const string CustomFeature = "custom-profile";
        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        public sealed class PartDto
        {
            public string Source { get; set; } = "default";
            public int? PresetId { get; set; }
            public string? Url { get; set; }
            public string Kind { get; set; } = "image";
            public string? Poster { get; set; }
            public string Focus { get; set; } = "center";
            public int Veil { get; set; } = 45;
            public int Blur { get; set; }
        }

        public sealed class SaveDto
        {
            public PartDto Banner { get; set; } = new();
            public PartDto Page { get; set; } = new();
        }

        private static object PartView(ProfileDecorService.Part p) => new
        {
            source = p.Source,
            presetId = p.PresetId,
            custom = p.Url == null ? null : new { url = p.Url, kind = p.Kind, poster = p.Poster },
            focus = p.Focus,
            veil = p.Veil,
            blur = p.Blur,
        };

        private async Task<object> ViewAsync(Guid uid, ProfileDecorService.Decor d)
        {
            var perk = await perks.GetAsync(uid);
            var canCustom = SponsorPerkService.HasFeature(perk, CustomFeature);
            return new
            {
                banner = PartView(d.Banner),
                page = PartView(d.Page),
                updatedAt = d.UpdatedAt,
                canCustom,
                sponsor = AppearanceController.SponsorView(perk, perks.MinAmount),
            };
        }

        /// <summary>我的主页装扮设置</summary>
        [HttpGet]
        public async Task<IActionResult> Get()
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            return Ok(await ViewAsync(uid, await decor.GetAsync(uid)));
        }

        /// <summary>官方素材（横幅、整页背景）。交易行在卖的带 locked、storeItemId；登录用户已拥有的带 owned</summary>
        [AllowAnonymous]
        [HttpGet("presets")]
        public async Task<IActionResult> Presets([FromServices] AppDbContext db)
        {
            var list = await decor.ListPresetsAsync(enabledOnly: true);
            var authorIds = list.Where(p => p.AuthorId != null).Select(p => p.AuthorId!.Value).Distinct().ToList();
            var authors = authorIds.Count == 0 ? new Dictionary<Guid, string>()
                : await db.Users.AsNoTracking().Where(u => authorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
            var forSale = await shop.ItemsForEffectAsync(ShopService.EffectProfileBg);
            var owned = CurrentUserId is Guid uid
                ? (await shop.CosmeticsAsync(uid)).Where(c => c.Kind == ShopService.EffectProfileBg).Select(c => c.Key).ToHashSet()
                : new HashSet<string>();
            return Ok(list.Select(p => new
            {
                id = p.Id,
                slot = p.Slot,
                title = p.Title,
                kind = p.Kind,
                url = p.Url,
                poster = p.Poster,
                sortOrder = p.SortOrder,
                locked = forSale.ContainsKey(p.Id),
                owned = owned.Contains(p.Id.ToString()),
                storeItemId = forSale.TryGetValue(p.Id, out var itemId) ? itemId : (int?)null,
                author = p.AuthorId is Guid a ? authors.GetValueOrDefault(a) : null,
            }));
        }

        /// <summary>检查一项设置能不能保存，返回错误提示（null 表示可以）</summary>
        private async Task<string?> CheckPartAsync(Guid uid, string slot, PartDto dto, ProfileDecorService.Part current, bool canCustom)
        {
            var name = slot == ProfileDecorService.SlotBanner ? "横幅" : "主页背景";
            if (dto.Source == "preset")
            {
                if (dto.PresetId is not int pid || await decor.GetPresetAsync(pid) is not { Enabled: true } preset)
                    return $"这个{name}素材已经下架了，换一个吧";
                if (preset.Slot != slot) return $"这个素材不能用作{name}";
                var keepCurrent = current.Source == "preset" && current.PresetId == pid;
                if (!keepCurrent && (await shop.ItemsForEffectAsync(ShopService.EffectProfileBg)).ContainsKey(pid)
                    && !await shop.OwnsAsync(uid, ShopService.EffectProfileBg, pid.ToString()))
                    return $"这个{name}需要先在交易行用经验或土豆兑换";
            }
            if (dto.Source == "custom")
            {
                if (!canCustom) return $"自定义{name}是赞助用户的特权，可以先选官方素材";
                if (!string.IsNullOrEmpty(dto.Url) && !UserAppearanceService.IsSafeUrl(dto.Url)) return $"{name}地址无效，请重新上传";
                if (!string.IsNullOrEmpty(dto.Poster) && !UserAppearanceService.IsSafeUrl(dto.Poster)) return "视频封面地址无效，请重新上传";
                if (string.IsNullOrEmpty(dto.Url) && current.Url == null) return $"请先上传{name}图片或视频";
            }
            return null;
        }

        /// <summary>自己上传的素材：有特权时才能换；没有特权时保留原来的（续费后还能接着用）</summary>
        private static ProfileDecorService.Part Merge(PartDto dto, ProfileDecorService.Part current, bool canCustom)
        {
            string? url = current.Url, poster = current.Poster;
            var kind = current.Kind;
            if (canCustom && !string.IsNullOrEmpty(dto.Url))
            {
                url = dto.Url;
                kind = dto.Kind ?? "image";
                poster = dto.Poster;
            }
            var source = UserAppearanceService.Sources.Contains(dto.Source) ? dto.Source : "default";
            return new ProfileDecorService.Part(source, source == "preset" ? dto.PresetId : null, url, kind, poster, dto.Focus ?? "center", dto.Veil, dto.Blur);
        }

        [HttpPut]
        public async Task<IActionResult> Save([FromBody] SaveDto dto)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            dto.Banner ??= new PartDto();
            dto.Page ??= new PartDto();
            var current = await decor.GetAsync(uid);
            var canCustom = SponsorPerkService.HasFeature(await perks.GetAsync(uid), CustomFeature);
            if (await CheckPartAsync(uid, ProfileDecorService.SlotBanner, dto.Banner, current.Banner, canCustom) is { } e1) return BadRequest(new { message = e1 });
            if (await CheckPartAsync(uid, ProfileDecorService.SlotPage, dto.Page, current.Page, canCustom) is { } e2) return BadRequest(new { message = e2 });
            var saved = await decor.SaveAsync(uid, Merge(dto.Banner, current.Banner, canCustom), Merge(dto.Page, current.Page, canCustom));
            return Ok(await ViewAsync(uid, saved));
        }

        /// <summary>
        /// 某人主页实际显示的横幅和整页背景（不用登录）。没设置、素材下架、自定义的赞助已到期时对应项为 null。
        /// </summary>
        [AllowAnonymous]
        [HttpGet("{userId:guid}")]
        public async Task<IActionResult> Public(Guid userId)
        {
            var d = await decor.GetAsync(userId);
            if (d.Banner.Source == "default" && d.Page.Source == "default") return Ok(new { banner = (object?)null, page = (object?)null });
            var canCustom = SponsorPerkService.HasFeature(await perks.GetAsync(userId), CustomFeature);

            async Task<object?> Resolve(ProfileDecorService.Part p)
            {
                string? url = null, poster = null;
                var kind = "image";
                if (p.Source == "preset" && p.PresetId is int pid && await decor.GetPresetAsync(pid) is { Enabled: true } preset)
                {
                    url = preset.Url; kind = preset.Kind; poster = preset.Poster;
                }
                else if (p.Source == "custom" && canCustom && p.Url != null)
                {
                    url = p.Url; kind = p.Kind; poster = p.Poster;
                }
                return url == null ? null : new { url, kind, poster, focus = p.Focus, veil = p.Veil, blur = p.Blur };
            }

            return Ok(new { banner = await Resolve(d.Banner), page = await Resolve(d.Page) });
        }
    }
}
