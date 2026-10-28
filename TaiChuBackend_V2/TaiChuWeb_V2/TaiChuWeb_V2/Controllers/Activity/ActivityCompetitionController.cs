using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Activity;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers.Activity;

/// <summary>活动赛事扩展：参赛作品、投票、对阵。</summary>
[ApiController]
[Route("api/activities/{activityId:int}/competition")]
public class ActivityCompetitionController : ControllerBase
{
    private readonly AppDbContext _context;
    public ActivityCompetitionController(AppDbContext context) { _context = context; }

    private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private async Task<bool> IsSuperAdmin()
    {
        if (!CurrentUserId.HasValue) return false;
        return await _context.UserPermissions.AnyAsync(p => p.UserId == CurrentUserId.Value && p.Permission == AdminPermission.SuperAdmin);
    }

    /// <summary>比赛详情：作品、对阵、我投的票</summary>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(int activityId)
    {
        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == activityId);
        if (activity == null) return NotFound(new { message = "活动不存在" });

        var entries = await _context.ActivityEntries
            .Where(e => e.ActivityId == activityId)
            .OrderByDescending(e => e.Votes).ThenBy(e => e.CreatedAt)
            .ToListAsync();

        var matches = await _context.ActivityMatches
            .Where(m => m.ActivityId == activityId)
            .OrderBy(m => m.Round).ThenBy(m => m.Slot)
            .ToListAsync();

        int? myVote = null;
        if (CurrentUserId.HasValue)
            myVote = (await _context.ActivityVotes
                .FirstOrDefaultAsync(v => v.ActivityId == activityId && v.UserId == CurrentUserId.Value))?.EntryId;

        return Ok(new
        {
            activity.Id, activity.Title, activity.Description, activity.Rules,
            activity.Kind, activity.Status, activity.Cover,
            activity.SignupDeadline, activity.StartAt, activity.EndAt,
            entries, matches, myVote
        });
    }

    /// <summary>提交参赛作品</summary>
    [HttpPost("entries")]
    [Authorize]
    public async Task<IActionResult> SubmitEntry(int activityId, [FromBody] EntryDto dto)
    {
        if (!CurrentUserId.HasValue) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest(new { message = "作品标题不能为空" });
        var activity = await _context.Activities.FirstOrDefaultAsync(a => a.Id == activityId);
        if (activity == null) return NotFound(new { message = "活动不存在" });

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId.Value);
        var entry = new ActivityEntry
        {
            ActivityId = activityId,
            UserId = CurrentUserId.Value,
            AuthorName = user?.Username ?? "参赛者",
            Title = dto.Title.Trim(),
            CoverUrl = dto.CoverUrl,
            Link = dto.Link,
            FileUrl = dto.FileUrl,
            FileName = dto.FileName,
            Description = dto.Description,
            CreatedAt = DateTime.UtcNow
        };
        _context.ActivityEntries.Add(entry);
        await _context.SaveChangesAsync();
        return Ok(new { id = entry.Id });
    }

    /// <summary>投票（同一活动每人一票，可取消或改投）</summary>
    [HttpPost("entries/{entryId:int}/vote")]
    [Authorize]
    public async Task<IActionResult> Vote(int activityId, int entryId)
    {
        if (!CurrentUserId.HasValue) return Unauthorized();
        var entry = await _context.ActivityEntries.FirstOrDefaultAsync(e => e.Id == entryId && e.ActivityId == activityId);
        if (entry == null) return NotFound(new { message = "作品不存在" });

        var existing = await _context.ActivityVotes
            .FirstOrDefaultAsync(v => v.ActivityId == activityId && v.UserId == CurrentUserId.Value);

        bool voted;
        if (existing != null && existing.EntryId == entryId)
        {
            _context.ActivityVotes.Remove(existing);
            entry.Votes = Math.Max(0, entry.Votes - 1);
            voted = false;
        }
        else
        {
            if (existing != null)
            {
                var old = await _context.ActivityEntries.FirstOrDefaultAsync(e => e.Id == existing.EntryId);
                if (old != null) old.Votes = Math.Max(0, old.Votes - 1);
                existing.EntryId = entryId;
                existing.CreatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.ActivityVotes.Add(new ActivityVote { ActivityId = activityId, EntryId = entryId, UserId = CurrentUserId.Value });
            }
            entry.Votes += 1;
            voted = true;
        }
        await _context.SaveChangesAsync();
        return Ok(new { voted, votes = entry.Votes });
    }

    /// <summary>管理员：新增对阵</summary>
    [HttpPost("matches")]
    [Authorize]
    public async Task<IActionResult> AddMatch(int activityId, [FromBody] MatchDto dto)
    {
        if (!await IsSuperAdmin()) return Forbid();
        var m = new ActivityMatch
        {
            ActivityId = activityId,
            Round = dto.Round <= 0 ? 1 : dto.Round,
            Slot = dto.Slot,
            SideA = dto.SideA,
            SideB = dto.SideB,
            ScoreA = dto.ScoreA,
            ScoreB = dto.ScoreB,
            Winner = dto.Winner,
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "pending" : dto.Status
        };
        _context.ActivityMatches.Add(m);
        await _context.SaveChangesAsync();
        return Ok(new { id = m.Id });
    }

    /// <summary>管理员：更新比分 / 胜者</summary>
    [HttpPut("matches/{matchId:int}")]
    [Authorize]
    public async Task<IActionResult> UpdateMatch(int activityId, int matchId, [FromBody] MatchDto dto)
    {
        if (!await IsSuperAdmin()) return Forbid();
        var m = await _context.ActivityMatches.FirstOrDefaultAsync(x => x.Id == matchId && x.ActivityId == activityId);
        if (m == null) return NotFound(new { message = "对阵不存在" });
        m.ScoreA = dto.ScoreA;
        m.ScoreB = dto.ScoreB;
        m.Winner = dto.Winner;
        if (!string.IsNullOrWhiteSpace(dto.Status)) m.Status = dto.Status;
        if (dto.SideA != null) m.SideA = dto.SideA;
        if (dto.SideB != null) m.SideB = dto.SideB;
        await _context.SaveChangesAsync();
        return Ok(new { id = m.Id });
    }

    /// <summary>管理员：删除对阵</summary>
    [HttpDelete("matches/{matchId:int}")]
    [Authorize]
    public async Task<IActionResult> DeleteMatch(int activityId, int matchId)
    {
        if (!await IsSuperAdmin()) return Forbid();
        var m = await _context.ActivityMatches.FirstOrDefaultAsync(x => x.Id == matchId && x.ActivityId == activityId);
        if (m == null) return NotFound();
        _context.ActivityMatches.Remove(m);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}

public class EntryDto
{
    public string Title { get; set; } = string.Empty;
    public string? CoverUrl { get; set; }
    public string? Link { get; set; }
    public string? FileUrl { get; set; }
    public string? FileName { get; set; }
    public string? Description { get; set; }
}

public class MatchDto
{
    public int Round { get; set; } = 1;
    public int Slot { get; set; }
    public string? SideA { get; set; }
    public string? SideB { get; set; }
    public int? ScoreA { get; set; }
    public int? ScoreB { get; set; }
    public string? Winner { get; set; }
    public string Status { get; set; } = "pending";
}
