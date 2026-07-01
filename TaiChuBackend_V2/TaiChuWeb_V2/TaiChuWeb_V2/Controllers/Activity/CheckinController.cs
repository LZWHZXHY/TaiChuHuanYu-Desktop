using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.DTOs.Activity;
using TaiChuWeb_V2.Models.Activity;
using MySqlConnector;
using Microsoft.Extensions.Logging; // 添加日志命名空间

namespace TaiChuWeb_V2.Controllers.Activity;

[ApiController]
[Route("api/activities/{activityId}/checkin")]
[Authorize]
public class CheckinController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ILogger<CheckinController> _logger; // 注入日志

    public CheckinController(AppDbContext context, ILogger<CheckinController> logger)
    {
        _context = context;
        _logger = logger;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            throw new UnauthorizedAccessException("用户未认证");
        return Guid.Parse(userIdClaim);
    }

    [HttpPost]
    public async Task<IActionResult> Checkin(int activityId, CheckinDto dto)
    {
        _logger.LogInformation("开始打卡，活动ID: {ActivityId}, Day: {Day}, 用户: {UserId}",
            activityId, dto.Day, User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        var userId = GetCurrentUserId();

        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == activityId);
        if (activity == null)
        {
            _logger.LogWarning("活动不存在，ActivityId: {ActivityId}", activityId);
            return NotFound("活动不存在");
        }

        var member = await _context.Members
            .FirstOrDefaultAsync(m => m.ActivityId == activityId && m.UserId == userId);

        if (member == null)
        {
            _logger.LogWarning("用户未加入该活动，ActivityId: {ActivityId}, UserId: {UserId}", activityId, userId);
            return BadRequest("您尚未加入该活动");
        }

        _logger.LogInformation("找到成员，MemberId: {MemberId}, UserName: {UserName}", member.Id, member.User?.Username);

        var elapsedDays = Math.Min(
            (int)Math.Floor((DateTime.UtcNow - activity.CreatedAt).TotalDays) + 1,
            activity.Days
        );

        if (elapsedDays > activity.Days)
            return BadRequest("活动已结束");

        if (dto.Day != elapsedDays)
        {
            if (dto.Day > elapsedDays)
                return BadRequest($"今天是活动第 {elapsedDays} 天，不能提前打卡");
            else
                return BadRequest($"今天是活动第 {elapsedDays} 天，不能补签");
        }

        // 检查是否已打卡
        var existing = await _context.Records
            .FirstOrDefaultAsync(r => r.MemberId == member.Id && r.Day == dto.Day);
        if (existing != null)
            return BadRequest("今天已打卡，无需重复提交");

        var record = new Record
        {
            MemberId = member.Id,
            Day = dto.Day,
            IsCompleted = true,
            IsLate = false,
            Text = dto.Text ?? "",
            Image = dto.Image ?? "",
            CreatedAt = DateTime.UtcNow
        };

        _context.Records.Add(record);

        try
        {
            await _context.SaveChangesAsync();
            _logger.LogInformation("打卡成功，RecordId: {RecordId}, MemberId: {MemberId}, Day: {Day}",
                record.Id, member.Id, record.Day);
        }
        catch (DbUpdateException ex) when (ex.InnerException is MySqlException mysqlEx &&
            mysqlEx.ErrorCode == MySqlErrorCode.DuplicateKeyEntry)
        {
            _logger.LogWarning(ex, "重复打卡，MemberId: {MemberId}, Day: {Day}", member.Id, dto.Day);
            return BadRequest("今天已打卡，请勿重复提交");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "打卡失败，MemberId: {MemberId}, Day: {Day}", member.Id, dto.Day);
            throw; // 让全局处理返回500
        }

        // 返回完整记录数据
        return Ok(new
        {
            record.Id,
            record.Day,
            record.IsCompleted,
            record.IsLate,
            record.Text,
            record.Image,
            record.CreatedAt
        });
    }
}