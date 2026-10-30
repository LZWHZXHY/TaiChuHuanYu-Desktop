using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services;
using TaiChuWeb_V2.Services.Afdian;
using TaiChuWeb_V2.Services.Personalization;
using TaiChuWeb_V2.Services.Shop;

namespace TaiChuWeb_V2.Controllers.Users
{
    /// <summary>
    /// 个人外观：网站背景。官方背景默认所有人可选；后台把某个官方背景设成交易行商品后，要先用经验或土豆兑换才能保存使用（可以先预览）。
    /// 自己上传图片 / 视频是赞助特权（赞助后 30 天，最低等级见 SponsorPerkService.Features）。
    /// 图片和视频由前端直传 COS，这里只保存地址和显示参数。
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/Users/Appearance")]
    public class AppearanceController(UserAppearanceService appearance, SponsorPerkService perks, ShopService shop) : ControllerBase
    {
        private const string CustomFeature = "custom-background";
        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        public sealed class AppearanceDto
        {
            public string Source { get; set; } = "default";
            public int? PresetId { get; set; }
            public string? Url { get; set; }
            public string Kind { get; set; } = "image";
            public string? Poster { get; set; }
            public int Veil { get; set; } = 55;
            public int Blur { get; set; }
            public bool Grid { get; set; } = true;
            public string Focus { get; set; } = "center";
            /// <summary>左侧导航栏不透明度 30–100（%）；不传按 80</summary>
            public int? Nav { get; set; }
        }

        public sealed class BindDto
        {
            public string OrderNo { get; set; } = string.Empty;
        }

        public static object PresetView(UserAppearanceService.Preset p) => new
        {
            id = p.Id,
            title = p.Title,
            kind = p.Kind,
            url = p.Url,
            poster = p.Poster,
            sortOrder = p.SortOrder,
            enabled = p.Enabled,
        };

        /// <summary>赞助特权概况（前端据此显示等级、到期时间和哪些特权可用）</summary>
        public static object SponsorView(SponsorPerkService.Perk perk, double minAmount) => new
        {
            active = perk.Active,
            until = perk.Until,
            tier = perk.Active ? perk.Tier : null,
            tierRank = perk.Active ? SponsorPerkService.TierRank(perk.Tier) : 0,
            tiers = SponsorPerkService.Tiers,
            afdianBound = perk.AfdianUserId != null,
            days = SponsorPerkService.DaysPerMonth,
            minAmount,
            features = SponsorPerkService.Features.ToDictionary(f => f.Key, f => new { minRank = f.Value, unlocked = SponsorPerkService.HasFeature(perk, f.Key) }),
        };

        private async Task<object> ViewAsync(Guid uid, UserAppearanceService.Appearance a)
        {
            var perk = await perks.GetAsync(uid);
            return new
            {
                source = a.Source,
                presetId = a.PresetId,
                custom = a.Url == null ? null : new { url = a.Url, kind = a.Kind, poster = a.Poster },
                veil = a.Veil,
                blur = a.Blur,
                grid = a.Grid,
                focus = a.Focus,
                nav = a.Nav,
                updatedAt = a.UpdatedAt,
                sponsor = SponsorView(perk, perks.MinAmount),
                // 选的是自定义背景但特权已到期：暂时显示默认，续费后自动恢复
                customLocked = a.Source == "custom" && !SponsorPerkService.HasFeature(perk, CustomFeature),
            };
        }

        [HttpGet]
        public async Task<IActionResult> Get()
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            return Ok(await ViewAsync(uid, await appearance.GetAsync(uid)));
        }

        /// <summary>
        /// 官方背景列表（所有人可见）。设成交易行商品的背景带 locked = true、storeItemId；
        /// 登录用户已经兑换过的带 owned = true。
        /// </summary>
        [AllowAnonymous]
        [HttpGet("presets")]
        public async Task<IActionResult> Presets([FromServices] AppDbContext db)
        {
            var list = await appearance.ListPresetsAsync(enabledOnly: true);
            var authorIds = list.Where(p => p.AuthorId != null).Select(p => p.AuthorId!.Value).Distinct().ToList();
            var authors = authorIds.Count == 0 ? new Dictionary<Guid, string>()
                : await db.Users.AsNoTracking().Where(u => authorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
            var forSale = await shop.BackgroundItemsAsync();
            var owned = CurrentUserId is Guid uid
                ? (await shop.CosmeticsAsync(uid)).Where(c => c.Kind == ShopService.EffectBackground).Select(c => c.Key).ToHashSet()
                : new HashSet<string>();
            return Ok(list.Select(p => new
            {
                id = p.Id,
                title = p.Title,
                kind = p.Kind,
                url = p.Url,
                poster = p.Poster,
                sortOrder = p.SortOrder,
                enabled = p.Enabled,
                locked = forSale.ContainsKey(p.Id),
                owned = owned.Contains(p.Id.ToString()),
                storeItemId = forSale.TryGetValue(p.Id, out var itemId) ? itemId : (int?)null,
                author = p.AuthorId is Guid a ? authors.GetValueOrDefault(a) : null,
            }));
        }

        [HttpPut]
        public async Task<IActionResult> Save([FromBody] AppearanceDto dto)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var current = await appearance.GetAsync(uid);
            var canCustom = SponsorPerkService.HasFeature(await perks.GetAsync(uid), CustomFeature);
            var source = UserAppearanceService.Sources.Contains(dto.Source) ? dto.Source : "default";

            if (source == "preset")
            {
                if (dto.PresetId is not int pid || await appearance.GetPresetAsync(pid) is not { Enabled: true })
                    return BadRequest(new { message = "这个官方背景已经下架了，换一个吧" });
                // 交易行出售的背景：兑换过才能用（之前已经在用的不受影响）
                var keepCurrent = current.Source == "preset" && current.PresetId == pid;
                if (!keepCurrent && (await shop.BackgroundItemsAsync()).ContainsKey(pid)
                    && !await shop.OwnsAsync(uid, ShopService.EffectBackground, pid.ToString()))
                    return StatusCode(403, new { message = "这个背景需要先在交易行用经验或土豆兑换" });
            }
            if (source == "custom" && !canCustom)
                return StatusCode(403, new { message = "自定义背景是赞助用户的特权。赞助后就能上传自己的图片或视频。" });

            // 自己上传的素材：只有有特权时才能更换；没有特权时保留原来的（续费后还能接着用）
            string? url = current.Url, poster = current.Poster;
            var kind = current.Kind;
            if (canCustom && !string.IsNullOrEmpty(dto.Url))
            {
                if (!UserAppearanceService.IsSafeUrl(dto.Url)) return BadRequest(new { message = "背景地址无效，请重新上传" });
                if (!string.IsNullOrEmpty(dto.Poster) && !UserAppearanceService.IsSafeUrl(dto.Poster)) return BadRequest(new { message = "视频封面地址无效，请重新上传" });
                url = dto.Url;
                kind = dto.Kind ?? "image";
                poster = dto.Poster;
            }
            if (source == "custom" && url == null) return BadRequest(new { message = "请先上传一张图片或一段视频" });

            var saved = await appearance.SaveAsync(uid, new UserAppearanceService.Appearance(source, source == "preset" ? dto.PresetId : null, url, kind, poster, dto.Veil, dto.Blur, dto.Grid, dto.Focus ?? "center", null, dto.Nav ?? current.Nav));
            return Ok(await ViewAsync(uid, saved));
        }

        /// <summary>恢复默认（纯色坐标纸背景），同时清掉自己上传的素材</summary>
        [HttpDelete]
        public async Task<IActionResult> Reset()
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            await appearance.ResetAsync(uid);
            return Ok(await ViewAsync(uid, UserAppearanceService.Default));
        }

        // ===================== 绑定爱发电（自动开通赞助特权） =====================

        private static readonly ConcurrentDictionary<Guid, (DateTime WindowStart, int Count)> BindAttempts = new();

        /// <summary>
        /// 填一笔自己的爱发电订单号，绑定爱发电账号。
        /// 绑定后这个爱发电账号最近的赞助会折算成特权天数，以后的新赞助也会在同步时自动顺延。
        /// </summary>
        [HttpPost("sponsor/bind")]
        public async Task<IActionResult> Bind([FromBody] BindDto dto, [FromServices] AppDbContext db, [FromServices] AfdianService afdian, [FromServices] IConfiguration config)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var orderNo = (dto.OrderNo ?? string.Empty).Trim();
            if (orderNo.Length is < 6 or > 64 || !orderNo.All(char.IsLetterOrDigit))
                return BadRequest(new { message = "订单号格式不对。请在爱发电「我的订单」里复制完整的订单号。" });

            // 防止猜订单号：每个账号每小时最多试 10 次
            var now = DateTime.UtcNow;
            var attempt = BindAttempts.AddOrUpdate(uid, _ => (now, 1), (_, v) => now - v.WindowStart > TimeSpan.FromHours(1) ? (now, 1) : (v.WindowStart, v.Count + 1));
            if (attempt.Count > 10) return StatusCode(429, new { message = "尝试次数太多，请一小时后再试" });

            var cfg = await db.AfdianConfigs.AsNoTracking().FirstOrDefaultAsync();
            var secret = config["Ai:EncryptionKey"] ?? config["Jwt:Key"] ?? "TaichuAiDefaultSecret";
            var token = cfg == null ? null : AiKeyCrypto.Decrypt(cfg.EncryptedToken, secret);
            if (cfg == null || string.IsNullOrEmpty(cfg.UserId) || string.IsNullOrEmpty(token))
                return BadRequest(new { message = "网站还没有接入爱发电，暂时只能由管理员手动开通。" });

            AfdianOrderDto? order;
            List<AfdianOrderDto> recent = [];
            Dictionary<string, string> tierByUser = new();
            try
            {
                order = await afdian.QueryOrderAsync(cfg.UserId, token, orderNo);
                if (order != null)
                {
                    recent = await afdian.QueryOrdersAsync(cfg.UserId, token, 1);
                    var sponsors = await afdian.QuerySponsorsAsync(cfg.UserId, token, 1);
                    tierByUser = sponsors.Where(s => !string.IsNullOrWhiteSpace(s.UserId) && !string.IsNullOrWhiteSpace(s.TierName))
                        .GroupBy(s => s.UserId).ToDictionary(g => g.Key, g => g.First().TierName);
                }
            }
            catch (Exception)
            {
                return StatusCode(502, new { message = "暂时连不上爱发电，请稍后再试" });
            }
            if (order == null || order.Status != 2 || string.IsNullOrWhiteSpace(order.UserId))
                return BadRequest(new { message = "没有找到这笔已支付的订单。请确认是赞助本站的订单，订单号复制完整。" });

            var (ok, message) = await perks.BindAfdianAsync(uid, order.UserId);
            if (!ok) return Conflict(new { message });

            var applied = await perks.ApplyOrdersAsync(recent.Append(order), tierByUser);
            var perk = await perks.GetAsync(uid);
            return Ok(new
            {
                message = perk.Active
                    ? $"绑定成功！当前等级「{perk.Tier}」，特权有效期到 {perk.Until!.Value.AddHours(8):yyyy-MM-dd HH:mm}（北京时间）。以后再赞助会自动顺延。"
                    : applied > 0 ? "绑定成功，但最近的赞助已经过了有效期。再次赞助后会自动开通。" : "绑定成功。以后在爱发电赞助会自动开通特权（同步可能有几分钟延迟）。",
                sponsor = SponsorView(perk, perks.MinAmount),
            });
        }

        [HttpDelete("sponsor/bind")]
        public async Task<IActionResult> Unbind()
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            await perks.UnbindAfdianAsync(uid);
            return Ok(new { message = "已解除绑定。已经开通的特权天数不受影响。", sponsor = SponsorView(await perks.GetAsync(uid), perks.MinAmount) });
        }
    }
}
