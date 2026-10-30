using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Reports;
using TaiChuWeb_V2.Services.Analytics;

namespace TaiChuWeb_V2.Controllers;

/// <summary>
/// 举报：登录用户可以举报帖子、博客、画作、评论、岁月史书、柴圈作品、OC、用户……
/// 同一个人对同一个内容还没处理的举报只算一条（再举报就更新说明）；每人每天最多 20 条。
/// 管理员在管理面板「举报」里处理（AdminReportsController），处理后通知举报人。
/// </summary>
[ApiController]
[Route("api/reports")]
[Authorize]
public class ReportsController(AppDbContext db) : ControllerBase
{
    public sealed class ReportDto
    {
        public string? TargetType { get; set; }
        public string? TargetId { get; set; }
        public string? TargetTitle { get; set; }
        public string? TargetUrl { get; set; }
        public string? Reason { get; set; }
        public string? Detail { get; set; }
    }

    private const int DailyLimit = 20;
    private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] ReportDto dto)
    {
        if (CurrentUserId is not Guid me) return Unauthorized();
        var type = ContentReport.TargetTypes.Contains(dto.TargetType) ? dto.TargetType! : "other";
        var targetId = (dto.TargetId ?? "").Trim();
        if (targetId.Length is 0 or > 64) return BadRequest(new { message = "举报的内容不对" });
        if (type == "user" && targetId.Equals(me.ToString(), StringComparison.OrdinalIgnoreCase)) return BadRequest(new { message = "不能举报自己" });
        var reason = ContentReport.Reasons.Contains(dto.Reason) ? dto.Reason! : "other";
        var detail = Clip(dto.Detail, 500);
        if (reason == "other" && string.IsNullOrWhiteSpace(detail)) return BadRequest(new { message = "选了「其他」的话，写一下是什么问题吧" });
        // 地址只收站内的（以 / 开头），防止塞外部链接
        var url = Clip(dto.TargetUrl, 300);
        if (url != null && (!url.StartsWith('/') || url.StartsWith("//"))) url = null;

        var existing = await db.ContentReports.FirstOrDefaultAsync(r => r.ReporterId == me && r.TargetType == type && r.TargetId == targetId && r.Status == "pending");
        if (existing != null)
        {
            existing.Reason = reason;
            existing.Detail = detail ?? existing.Detail;
            await db.SaveChangesAsync();
            return Ok(new { message = "你已经举报过这个了，管理员还在处理，补充的说明已更新" });
        }
        var dayStart = ChinaClock.DayStartUtc(ChinaClock.Today);
        if (await db.ContentReports.CountAsync(r => r.ReporterId == me && r.CreatedAt >= dayStart) >= DailyLimit)
            return BadRequest(new { message = "今天举报得太多了，明天再来吧" });

        db.ContentReports.Add(new ContentReport
        {
            ReporterId = me, TargetType = type, TargetId = targetId, TargetTitle = Clip(dto.TargetTitle, 200), TargetUrl = url,
            Reason = reason, Detail = detail, CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        return Ok(new { message = "举报已提交，管理员处理后会通知你，谢谢！" });
    }

    /// <summary>我举报过的（看处理结果）</summary>
    [HttpGet("mine")]
    public async Task<IActionResult> Mine()
    {
        if (CurrentUserId is not Guid me) return Unauthorized();
        var rows = await db.ContentReports.AsNoTracking().Where(r => r.ReporterId == me).OrderByDescending(r => r.Id).Take(100)
            .Select(r => new { r.Id, r.TargetType, r.TargetTitle, r.TargetUrl, r.Reason, r.Status, r.HandleNote, r.CreatedAt, r.HandledAt }).ToListAsync();
        return Ok(rows);
    }

    private static string? Clip(string? s, int max)
    {
        var t = (s ?? "").Trim();
        return t.Length == 0 ? null : t.Length > max ? t[..max] : t;
    }
}
