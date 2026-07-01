using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.DTOs.Activity;
using ActivityModel = TaiChuWeb_V2.Models.Activity.Activity;
using MemberModel = TaiChuWeb_V2.Models.Activity.Member;

namespace TaiChuWeb_V2.Controllers.Activity;

[ApiController]
[Route("api/activities")]
[Authorize]
public class ActivitiesController : ControllerBase
{
    private readonly AppDbContext _context;

    public ActivitiesController(AppDbContext context)
    {
        _context = context;
    }

    private Guid GetCurrentUserId()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userIdClaim))
            throw new UnauthorizedAccessException("用户未认证");
        return Guid.Parse(userIdClaim);
    }

    // ----- 活动列表（允许未登录查看） -----
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetActivities([FromQuery] ActivityQueryParams query)
    {
        var activities = _context.Activities
            .Include(a => a.Owner)
            .Include(a => a.Type)          // 必须 Include Type
            .Include(a => a.Members)
            .AsQueryable();

        // 过滤（修正 Type 比较）
        if (!string.IsNullOrEmpty(query.Status))
            activities = activities.Where(a => a.Status == query.Status);
        if (!string.IsNullOrEmpty(query.Type))
            activities = activities.Where(a => a.Type.Name == query.Type);  // 比较 Name
        if (!string.IsNullOrEmpty(query.Keyword))
        {
            var keyword = query.Keyword.ToLower();
            activities = activities.Where(a =>
                a.Title.ToLower().Contains(keyword) ||
                (a.Description != null && a.Description.ToLower().Contains(keyword)));
        }

        activities = activities.OrderByDescending(a => a.CreatedAt);

        var result = await activities.Select(a => new ActivityResponseDto
        {
            Id = a.Id,
            Title = a.Title,
            Description = a.Description,
            Type = a.Type.Name,            // 取名称
            Status = a.Status,
            Cover = a.Cover,
            Days = a.Days,
            Participants = a.Members.Count,
            CompletedRate = a.Members.Count > 0
                ? a.Members.SelectMany(m => m.Records).Count(r => r.IsCompleted) * 100 / (a.Members.Count * a.Days)
                : 0,
            Owner = a.Owner.Username,
            CreatedAt = a.CreatedAt
        }).ToListAsync();

        return Ok(result);
    }

    // ----- 获取单个活动详情 -----
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetActivity(int id)
    {
        var activity = await _context.Activities
            .Include(a => a.Owner)
            .Include(a => a.Type)          // 必须 Include Type
            .Include(a => a.Members)
                .ThenInclude(m => m.Records)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound("活动不存在");

        var response = new ActivityResponseDto
        {
            Id = activity.Id,
            Title = activity.Title,
            Description = activity.Description,
            Type = activity.Type.Name,     // 取名称
            Status = activity.Status,
            Cover = activity.Cover,
            Days = activity.Days,
            Participants = activity.Members.Count,
            CompletedRate = activity.Members.Count > 0
                ? activity.Members.SelectMany(m => m.Records).Count(r => r.IsCompleted) * 100 / (activity.Members.Count * activity.Days)
                : 0,
            Owner = activity.Owner.Username,
            CreatedAt = activity.CreatedAt
        };

        return Ok(response);
    }

    [HttpGet("{id}/members")]
    public async Task<IActionResult> GetMembers(int id)
    {
        var activity = await _context.Activities
            .Include(a => a.Members)
                .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound("活动不存在");

        var memberIds = activity.Members.Select(m => m.Id).ToList();

        // 直接通过 MemberId 列表查询 Records
        var records = await _context.Records
            .Where(r => memberIds.Contains(r.MemberId))
            .ToListAsync();

        var recordsByMember = records.GroupBy(r => r.MemberId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var members = activity.Members.Select(m => new MemberDto
        {
            Id = m.Id,
            Name = m.User.Username,
            JoinedAt = m.JoinedAt,
            Records = recordsByMember.TryGetValue(m.Id, out var recs)
                ? recs.Select(r => new RecordDto
                {
                    Day = r.Day,
                    IsCompleted = r.IsCompleted,
                    IsLate = r.IsLate,
                    Text = r.Text,
                    Image = r.Image
                }).ToList()
                : new List<RecordDto>()
        }).ToList();

        return Ok(members);
    }

    // ----- 获取当前用户在该活动中的打卡状态 -----
    [HttpGet("{id}/my-status")]
    public async Task<IActionResult> GetMyStatus(int id)
    {
        var userId = GetCurrentUserId();

        var member = await _context.Members
            .Include(m => m.Records)
            .FirstOrDefaultAsync(m => m.ActivityId == id && m.UserId == userId);

        if (member == null)
            return Ok(new { isJoined = false });

        var activity = await _context.Activities
            .Include(a => a.Type)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (activity == null)
            return NotFound("活动不存在");

        var totalDays = activity.Days;
        var elapsedDays = Math.Min(
            (int)Math.Floor((DateTime.UtcNow - activity.CreatedAt).TotalDays) + 1,
            totalDays
        );

        var records = member.Records.ToList();
        var completedDays = records.Count(r => r.IsCompleted);

        return Ok(new
        {
            isJoined = true,
            totalDays,
            elapsedDays,
            completedDays,
            completionRate = totalDays > 0 ? completedDays * 100 / totalDays : 0,
            consecutiveDays = 0,
            records = records.Select(r => new RecordDto
            {
                Day = r.Day,
                IsCompleted = r.IsCompleted,
                IsLate = r.IsLate,
                Text = r.Text,
                Image = r.Image
            })
        });
    }

    // ----- 创建活动 -----
    [HttpPost]
    public async Task<IActionResult> CreateActivity(CreateActivityDto dto)
    {
        var userId = GetCurrentUserId();

        // 验证 TypeId 是否存在
        var type = await _context.ActivityTypes.FindAsync(dto.TypeId);
        if (type == null)
            return BadRequest("无效的活动类型");

        var activity = new ActivityModel
        {
            Title = dto.Title,
            Description = dto.Description,
            TypeId = dto.TypeId,          // 使用 TypeId（int）
            Cover = dto.Cover,
            Days = dto.Days,
            OwnerId = userId,
            Status = "招募中",
            CreatedAt = DateTime.UtcNow
        };

        _context.Activities.Add(activity);
        await _context.SaveChangesAsync();

        // 创建者自动加入活动
        var member = new MemberModel
        {
            ActivityId = activity.Id,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        };
        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetActivity), new { id = activity.Id }, new
        {
            activity.Id,
            activity.Title,
            message = "活动创建成功，您已自动加入"
        });
    }

    // ----- 更新活动 -----
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateActivity(int id, UpdateActivityDto dto)
    {
        var userId = GetCurrentUserId();

        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound("活动不存在");

        if (activity.OwnerId != userId)
            return Forbid("只有活动创建者可以编辑");

        if (!string.IsNullOrEmpty(dto.Title))
            activity.Title = dto.Title;
        if (!string.IsNullOrEmpty(dto.Description))
            activity.Description = dto.Description;
        if (dto.TypeId.HasValue)  // 如果提供 TypeId，则更新
        {
            var type = await _context.ActivityTypes.FindAsync(dto.TypeId.Value);
            if (type == null)
                return BadRequest("无效的活动类型");
            activity.TypeId = dto.TypeId.Value;
        }
        if (!string.IsNullOrEmpty(dto.Status))
            activity.Status = dto.Status;
        if (!string.IsNullOrEmpty(dto.Cover))
            activity.Cover = dto.Cover;
        if (dto.Days.HasValue)
            activity.Days = dto.Days.Value;

        await _context.SaveChangesAsync();

        return Ok(new { message = "活动已更新" });
    }


    // 在 ActivitiesController.cs 中添加
    [HttpGet("{id}/records")]
    [AllowAnonymous]
    public async Task<IActionResult> GetRecords(int id)
    {
        var records = await _context.Records
            .Include(r => r.Member)
                .ThenInclude(m => m.User)
            .Where(r => r.Member.ActivityId == id)
            .Select(r => new
            {
                r.Id,
                MemberName = r.Member.User.Username,
                r.Day,
                r.IsCompleted,
                r.IsLate,
                r.Text,
                r.Image,
                r.CreatedAt
            })
            .ToListAsync();

        return Ok(records);
    }



    // ----- 删除活动 -----
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteActivity(int id)
    {
        var userId = GetCurrentUserId();

        var activity = await _context.Activities
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound("活动不存在");

        if (activity.OwnerId != userId)
            return Forbid("只有活动创建者可以删除");

        _context.Activities.Remove(activity);
        await _context.SaveChangesAsync();

        return Ok(new { message = "活动已删除" });
    }

    // ----- 加入活动 -----
    [HttpPost("{id}/join")]
    public async Task<IActionResult> JoinActivity(int id)
    {
        var userId = GetCurrentUserId();

        var activity = await _context.Activities
            .Include(a => a.Members)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound("活动不存在");

        if (activity.Members.Any(m => m.UserId == userId))
            return BadRequest("您已加入该活动");

        if (activity.Status == "已结束")
            return BadRequest("活动已结束，无法加入");

        var member = new MemberModel
        {
            ActivityId = id,
            UserId = userId,
            JoinedAt = DateTime.UtcNow
        };

        _context.Members.Add(member);
        await _context.SaveChangesAsync();

        return Ok(new JoinResponseDto
        {
            IsJoined = true,
            MembersCount = activity.Members.Count + 1,
            Message = "加入成功"
        });
    }

    // ----- 退出活动 -----
    [HttpPost("{id}/leave")]
    public async Task<IActionResult> LeaveActivity(int id)
    {
        var userId = GetCurrentUserId();

        var activity = await _context.Activities
            .Include(a => a.Members)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound("活动不存在");

        var member = activity.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            return BadRequest("您尚未加入该活动");

        if (activity.OwnerId == userId)
            return BadRequest("活动创建者不能退出，只能删除活动");

        _context.Members.Remove(member);
        await _context.SaveChangesAsync();

        return Ok(new JoinResponseDto
        {
            IsJoined = false,
            MembersCount = activity.Members.Count - 1,
            Message = "已退出活动"
        });
    }

    // ----- 获取打卡统计数据 -----
    [HttpGet("{id}/stats")]
    public async Task<IActionResult> GetStats(int id)
    {
        var userId = GetCurrentUserId();

        var activity = await _context.Activities
            .Include(a => a.Members)
                .ThenInclude(m => m.Records)
            .FirstOrDefaultAsync(a => a.Id == id);

        if (activity == null)
            return NotFound("活动不存在");

        var member = activity.Members.FirstOrDefault(m => m.UserId == userId);
        if (member == null)
            return BadRequest("您尚未加入该活动");

        var totalDays = activity.Days;
        var elapsedDays = Math.Min(
            (int)Math.Floor((DateTime.UtcNow - activity.CreatedAt).TotalDays) + 1,
            totalDays
        );

        var completedDays = member.Records.Count(r => r.IsCompleted);
        var completionRate = totalDays > 0 ? completedDays * 100 / totalDays : 0;

        var sortedDays = member.Records
            .Where(r => r.IsCompleted)
            .Select(r => r.Day)
            .OrderByDescending(d => d)
            .ToList();

        int consecutive = 0;
        if (sortedDays.Any())
        {
            consecutive = 1;
            var current = sortedDays.First();
            for (int i = 1; i < sortedDays.Count; i++)
            {
                if (sortedDays[i] == current - 1)
                {
                    consecutive++;
                    current--;
                }
                else
                {
                    break;
                }
            }
        }

        return Ok(new StatsResponseDto
        {
            TotalDays = totalDays,
            ElapsedDays = elapsedDays,
            CompletionRate = completionRate,
            ConsecutiveDays = consecutive,
            Rank = 0
        });
    }

    // ----- 获取活动类型列表 -----
    [HttpGet("types")]
    [AllowAnonymous]
    public async Task<IActionResult> GetTypes()
    {
        var types = await _context.ActivityTypes
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .Select(t => new { t.Id, t.Name })
            .ToListAsync();
        return Ok(types);
    }
}