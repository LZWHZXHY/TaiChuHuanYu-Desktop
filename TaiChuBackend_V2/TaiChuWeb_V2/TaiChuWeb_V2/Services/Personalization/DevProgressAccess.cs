using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Services.Personalization
{
    /// <summary>
    /// 开发进度详情页谁能看：
    /// · 管理员：能进管理面板的权限（SuperAdmin / Trade_Manage / User_Audit / Wiki_Editor / System_Monitor）和官方研发（TaiChu）；
    /// · 赞助会员：赞助特权在有效期内，且等级达到 SponsorPerkService.Features["dev-progress"]（超凡者及以上，觉醒者不行）。赞助到期、没续费就看不了了，续费后马上恢复。
    /// 其他人（包括没登录的）只能在首页看到简单的进度（项目名 + 百分比）。
    /// </summary>
    public static class DevProgressAccess
    {
        public const string Feature = "dev-progress";

        /// <summary>算「管理员」的权限。和前端 utils/adminAccess.ts 的 adminPermissions 一致，另加官方研发 TaiChu</summary>
        public static readonly List<AdminPermission> StaffPermissions = new()
        {
            AdminPermission.SuperAdmin, AdminPermission.Trade_Manage, AdminPermission.User_Audit,
            AdminPermission.Wiki_Editor, AdminPermission.System_Monitor, AdminPermission.TaiChu,
        };

        /// <param name="CanView">能不能看详情</param>
        /// <param name="Reason">admin 管理员 / sponsor 赞助会员 / null 不能看</param>
        /// <param name="SponsorUntil">赞助特权到期时间（没赞助过为空；过期了也会返回，用来提示「已于某天到期」）</param>
        /// <param name="LoggedIn">有没有登录</param>
        /// <param name="Tier">当前赞助等级（没有为空）</param>
        /// <param name="TierTooLow">赞助还在有效期，但等级不够（比如觉醒者）</param>
        public sealed record Result(bool CanView, string? Reason, DateTime? SponsorUntil, bool LoggedIn, string? Tier = null, bool TierTooLow = false);

        /// <summary>需要的最低等级名（超凡者）</summary>
        public static string NeedTier => SponsorPerkService.Tiers[Math.Clamp(SponsorPerkService.Features[Feature], 1, SponsorPerkService.Tiers.Length) - 1];

        public static async Task<Result> CheckAsync(AppDbContext db, SponsorPerkService perks, ClaimsPrincipal user, CancellationToken ct = default)
        {
            if (!Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var uid))
                return new Result(false, null, null, false);
            // 先取出这个人的全部权限再在内存里比（权限只有几条；避免集合 Contains 在 MySQL 上的翻译问题）
            var mine = await db.UserPermissions.AsNoTracking().Where(p => p.UserId == uid).Select(p => p.Permission).ToListAsync(ct);
            var staff = mine.Any(StaffPermissions.Contains);
            SponsorPerkService.Perk perk;
            try { perk = await perks.GetAsync(uid, ct); }
            catch { perk = new SponsorPerkService.Perk(null, null, null); } // 特权表读不出来时按没赞助处理，管理员照样能看
            if (staff) return new Result(true, "admin", perk.Until, true, perk.Tier);
            if (SponsorPerkService.HasFeature(perk, Feature)) return new Result(true, "sponsor", perk.Until, true, perk.Tier);
            return new Result(false, null, perk.Until, true, perk.Tier, TierTooLow: perk.Active);
        }
    }
}
