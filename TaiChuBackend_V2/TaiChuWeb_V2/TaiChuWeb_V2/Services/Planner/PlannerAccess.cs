using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services.Personalization;

namespace TaiChuWeb_V2.Services.Planner;

/// <summary>
/// 生活管理谁能用（赞助特权，判断都走 SponsorPerkService.HasFeature，最低等级在那边配置）：
/// · 能用（新建 / 修改 / 提醒）= 特权 "life-planner"（默认超凡者及以上），或者 SuperAdmin；
/// · 邮件提醒 = 特权 "planner-email"（默认君者），或者 SuperAdmin；
/// · 赞助到期但以前存过安排 = 只读：能看、能导出，不能改，提醒全部暂停；续费后马上恢复。
/// </summary>
public static class PlannerAccess
{
    public const string Feature = "life-planner";
    public const string EmailFeature = "planner-email";
    /// <summary>显示给用户看的等级名：跟着当前生效的最低等级走（后台「赞助福利」里改了等级，这里的提示文字也跟着变）</summary>
    public static string NeedTierName => TierName(Feature);
    public static string EmailTierName => TierName(EmailFeature);
    private static string TierName(string feature) =>
        SponsorPerkService.Tiers[Math.Clamp(SponsorPerkService.Features.TryGetValue(feature, out var t) ? t : 1, 1, SponsorPerkService.Tiers.Length) - 1];

    /// <param name="IsAdmin">SuperAdmin，总是能用</param>
    /// <param name="CanEdit">能新建 / 修改 / 删除 / 勾选，提醒照常发</param>
    /// <param name="CanEmail">能用邮件提醒</param>
    /// <param name="Until">赞助特权到期时间（没赞助过为空）</param>
    public sealed record Result(bool IsAdmin, bool CanEdit, bool CanEmail, DateTime? Until, string? Tier);

    public static async Task<Result> CheckAsync(AppDbContext db, SponsorPerkService perks, Guid userId, CancellationToken ct = default)
    {
        // 先取出这个人的全部权限再在内存里比（只有几条，避免集合 Contains 的翻译问题）
        var mine = await db.UserPermissions.AsNoTracking().Where(p => p.UserId == userId).Select(p => p.Permission).ToListAsync(ct);
        var admin = mine.Any(p => p == AdminPermission.SuperAdmin);
        SponsorPerkService.Perk perk;
        try { perk = await perks.GetAsync(userId, ct); }
        catch { perk = new SponsorPerkService.Perk(null, null, null); } // 特权表读不出来时按没赞助处理，管理员照样能用
        var canEdit = admin || SponsorPerkService.HasFeature(perk, Feature);
        var canEmail = admin || (canEdit && SponsorPerkService.HasFeature(perk, EmailFeature));
        return new Result(admin, canEdit, canEmail, perk.Until, perk.Tier);
    }
}
