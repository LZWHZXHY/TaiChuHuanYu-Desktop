using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Chai.Joint;
using TaiChuWeb_V2.Models.ChaiCommunity.Joint;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers.ChaiCommunity
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class JointController : ControllerBase
    {
        private readonly AppDbContext _context;

        public JointController(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 辅助方法：获取当前用户权限列表
        // ============================================================
        private async Task<List<AdminPermission>> GetUserPermissions(Guid userId)
        {
            return await _context.UserPermissions
                .Where(p => p.UserId == userId)
                .Select(p => p.Permission)
                .ToListAsync();
        }





        // ============================================================
        // 14. 首页专用：获取 open 状态的前 N 个活动（轻量）
        // ============================================================
        [HttpGet("home")]
        [AllowAnonymous]
        public async Task<ActionResult<List<JointActivityDto>>> GetHomeList(
            [FromQuery] int count = 3)
        {
            var now = DateTime.UtcNow;

            var items = await _context.JointActivities
                .Where(a => a.Status == "open")
                .Where(a => !a.EndDate.HasValue || a.EndDate.Value >= now)
                .OrderByDescending(a => a.CreatedAt)
                .Take(count)
                .Select(a => new JointActivityDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    Requirements = a.Requirements,
                    Contact = a.Contact,
                    Type = a.Type,
                    Status = a.Status,
                    AuditRequired = a.AuditRequired,
                    CoverUrl = a.CoverUrl,
                    OrganizerId = a.OrganizerId,
                    OrganizerName = a.OrganizerName,
                    ParticipantCount = a.ParticipantCount,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt,
                    OrganizerType = a.OrganizerType,
                    ApprovalStatus = a.ApprovalStatus,
                    StartDate = a.StartDate,
                    EndDate = a.EndDate,
                    // Participants 不需要，首页列表不展示参与者列表
                    Participants = null
                })
                .ToListAsync();

            return Ok(items);
        }






        // ============================================================
        // 1. 获取列表（公开）
        // ============================================================
        // ============================================================
        // 1. 获取列表（公开）
        // ============================================================
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<JointListResponse>> GetList(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            [FromQuery] string? keyword = null,
            [FromQuery] string? status = null,
            [FromQuery] string? type = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 12;

            var query = _context.JointActivities.AsQueryable();

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(a => a.Title.Contains(keyword) ||
                                         a.Description.Contains(keyword));
            }

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                query = query.Where(a => a.Status == status);
            }

            if (!string.IsNullOrEmpty(type) && type != "all")
            {
                query = query.Where(a => a.Type == type);
            }

            var total = await query.CountAsync();

            // ⭐ 先获取所有数据到内存（用于修正状态和排序）
            var allItems = await query.ToListAsync();

            var now = DateTime.UtcNow;

            // ⭐ 修正：过期的 open 状态自动变为 ended
            foreach (var item in allItems)
            {
                if (item.Status == "open" && item.EndDate.HasValue && item.EndDate.Value < now)
                {
                    item.Status = "ended";
                }
            }

            // ⭐ 按状态权重排序（open 最优先，abandoned 最后）
            var sortedItems = allItems
                .OrderBy(a => GetStatusWeight(a.Status))
                .ThenByDescending(a => a.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            var items = sortedItems.Select(a => new JointActivityDto
            {
                Id = a.Id,
                Title = a.Title,
                Description = a.Description,
                Requirements = a.Requirements,
                Contact = a.Contact,
                Type = a.Type,
                Status = a.Status,
                AuditRequired = a.AuditRequired,
                CoverUrl = a.CoverUrl,
                OrganizerId = a.OrganizerId,
                OrganizerName = a.OrganizerName,
                ParticipantCount = a.ParticipantCount,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                OrganizerType = a.OrganizerType,
                ApprovalStatus = a.ApprovalStatus,
                StartDate = a.StartDate,
                EndDate = a.EndDate
            }).ToList();

            return Ok(new JointListResponse
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            });
        }

        // ⭐ 辅助方法：状态权重
        private int GetStatusWeight(string status)
        {
            return status switch
            {
                "open" => 1,
                "closed" => 2,
                "ended" => 3,
                "banned" => 4,
                "abandoned" => 5,
                _ => 99
            };
        }

        // ============================================================
        // 2. 获取详情
        // ============================================================
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<JointActivityDto>> GetDetail(Guid id)
        {
            var activity = await _context.JointActivities
                .Include(a => a.Participants)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (activity == null)
                return NotFound(new { message = "联合活动不存在" });

            var dto = new JointActivityDto
            {
                Id = activity.Id,
                Title = activity.Title,
                Description = activity.Description,
                Requirements = activity.Requirements,
                Contact = activity.Contact,
                Type = activity.Type,
                Status = activity.Status,
                AuditRequired = activity.AuditRequired,
                CoverUrl = activity.CoverUrl,
                OrganizerId = activity.OrganizerId,
                OrganizerName = activity.OrganizerName,
                ParticipantCount = activity.ParticipantCount,
                CreatedAt = activity.CreatedAt,
                UpdatedAt = activity.UpdatedAt,
                OrganizerType = activity.OrganizerType,
                ApprovalStatus = activity.ApprovalStatus,
                // ===== 新增 =====
                StartDate = activity.StartDate,
                EndDate = activity.EndDate,
                Participants = activity.Participants?.Select(p => new JointParticipantDto
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    UserName = p.UserName,
                    Status = p.Status,
                    Remark = p.Remark,
                    CreatedAt = p.CreatedAt
                }).ToList()
            };

            return Ok(dto);
        }

        // ============================================================
        // 3. 创建活动
        // ============================================================
        [HttpPost]
        public async Task<ActionResult<JointActivityDto>> Create([FromBody] CreateJointRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var username = User.FindFirstValue(ClaimTypes.Name) ?? "未知用户";
            var permissions = await GetUserPermissions(userId);

            var isOfficial = request.OrganizerType == "official";
            if (isOfficial && !JointPermissionHelper.CanCreateOfficial(permissions))
                return Forbid();

            // ===== 校验：结束时间必须晚于开始时间 =====
            if (request.EndDate.HasValue && request.EndDate.Value <= request.StartDate)
            {
                return BadRequest(new { message = "结束时间必须晚于开始时间" });
            }

            var activity = new JointActivity
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Description = request.Description,
                Requirements = request.Requirements,
                Contact = request.Contact,
                Type = request.Type,
                Status = request.Status,
                AuditRequired = request.AuditRequired,
                CoverUrl = request.CoverUrl,
                OrganizerId = userId,
                OrganizerName = username,
                ParticipantCount = 0,
                CreatedAt = DateTime.UtcNow,
                OrganizerType = request.OrganizerType ?? "user",
                ApprovalStatus = isOfficial ? "approved" : "pending",
                // ===== 新增 =====
                StartDate = request.StartDate,
                EndDate = request.EndDate
            };

            _context.JointActivities.Add(activity);
            await _context.SaveChangesAsync();

            var dto = new JointActivityDto
            {
                Id = activity.Id,
                Title = activity.Title,
                Description = activity.Description,
                Requirements = activity.Requirements,
                Contact = activity.Contact,
                Type = activity.Type,
                Status = activity.Status,
                AuditRequired = activity.AuditRequired,
                CoverUrl = activity.CoverUrl,
                OrganizerId = activity.OrganizerId,
                OrganizerName = activity.OrganizerName,
                ParticipantCount = activity.ParticipantCount,
                CreatedAt = activity.CreatedAt,
                UpdatedAt = activity.UpdatedAt,
                OrganizerType = activity.OrganizerType,
                ApprovalStatus = activity.ApprovalStatus,
                // ===== 新增 =====
                StartDate = activity.StartDate,
                EndDate = activity.EndDate
            };

            return CreatedAtAction(nameof(GetDetail), new { id = activity.Id }, dto);
        }

        // ============================================================
        // 4. 更新活动
        // ============================================================
        [HttpPut("{id}")]
        public async Task<ActionResult<JointActivityDto>> Update(Guid id, [FromBody] UpdateJointRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var activity = await _context.JointActivities
                .FirstOrDefaultAsync(a => a.Id == id);

            if (activity == null)
                return NotFound(new { message = "联合活动不存在" });

            var permissions = await GetUserPermissions(userId);

            if (!JointPermissionHelper.CanEdit(activity, userId, permissions))
                return Forbid();

            if (!string.IsNullOrEmpty(request.Title))
                activity.Title = request.Title;
            if (!string.IsNullOrEmpty(request.Description))
                activity.Description = request.Description;
            if (request.Requirements != null)
                activity.Requirements = request.Requirements;
            if (request.Contact != null)
                activity.Contact = request.Contact;
            if (!string.IsNullOrEmpty(request.Type))
                activity.Type = request.Type;
            if (!string.IsNullOrEmpty(request.Status))
                activity.Status = request.Status;
            if (request.AuditRequired.HasValue)
                activity.AuditRequired = request.AuditRequired.Value;
            if (request.CoverUrl != null)
                activity.CoverUrl = request.CoverUrl;

            // ===== 新增：更新日期 =====
            if (request.StartDate.HasValue)
            {
                // 如果同时更新了 EndDate，校验结束时间 > 开始时间
                var endDate = request.EndDate ?? activity.EndDate;
                if (endDate.HasValue && endDate.Value <= request.StartDate.Value)
                {
                    return BadRequest(new { message = "结束时间必须晚于开始时间" });
                }
                activity.StartDate = request.StartDate.Value;
            }
            if (request.EndDate.HasValue)
            {
                // 校验结束时间 > 当前开始时间
                var startDate = request.StartDate ?? activity.StartDate;
                if (request.EndDate.Value <= startDate)
                {
                    return BadRequest(new { message = "结束时间必须晚于开始时间" });
                }
                activity.EndDate = request.EndDate.Value;
            }

            activity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var updated = await _context.JointActivities
                .Include(a => a.Participants)
                .FirstOrDefaultAsync(a => a.Id == id);

            return Ok(MapToDto(updated!));
        }

        // ============================================================
        // 5. 删除活动（仅 SuperAdmin）
        // ============================================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var activity = await _context.JointActivities
                .FirstOrDefaultAsync(a => a.Id == id);

            if (activity == null)
                return NotFound(new { message = "联合活动不存在" });

            var permissions = await GetUserPermissions(userId);

            if (!JointPermissionHelper.CanDelete(permissions))
                return Forbid();

            _context.JointActivities.Remove(activity);
            await _context.SaveChangesAsync();

            return Ok(new { message = "删除成功" });
        }

        // ============================================================
        // 6. 报名参加
        // ============================================================
        [HttpPost("{id}/join")]
        public async Task<ActionResult<JointActivityDto>> Join(Guid id, [FromBody] JoinRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var username = User.FindFirstValue(ClaimTypes.Name) ?? "未知用户";

            var activity = await _context.JointActivities
                .Include(a => a.Participants)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (activity == null)
                return NotFound(new { message = "联合活动不存在" });

            if (activity.Status != "open")
                return BadRequest(new { message = "活动已截止或已结束" });

            if (activity.OrganizerType == "user" && activity.ApprovalStatus != "approved")
                return BadRequest(new { message = "活动尚未审核通过" });

            // ===== 新增：检查活动是否已开始（不能报名已开始的活动） =====
            if (activity.StartDate <= DateTime.UtcNow)
            {
                return BadRequest(new { message = "活动已开始，无法报名" });
            }

            if (activity.Participants!.Any(p => p.UserId == userId))
                return BadRequest(new { message = "你已经报名参加了此活动" });

            if (activity.OrganizerId == userId)
                return BadRequest(new { message = "举办者不能报名自己的活动" });

            var participant = new JointParticipant
            {
                Id = Guid.NewGuid(),
                ActivityId = activity.Id,
                UserId = userId,
                UserName = username,
                Status = activity.AuditRequired ? "pending" : "approved",
                Remark = request.Remark,
                CreatedAt = DateTime.UtcNow
            };

            _context.JointParticipants.Add(participant);
            activity.ParticipantCount += 1;

            await _context.SaveChangesAsync();

            var updated = await _context.JointActivities
                .Include(a => a.Participants)
                .FirstOrDefaultAsync(a => a.Id == id);

            return Ok(MapToDto(updated!));
        }

        // ============================================================
        // 7. 取消报名
        // ============================================================
        [HttpPost("{id}/cancel")]
        public async Task<ActionResult<JointActivityDto>> CancelJoin(Guid id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var activity = await _context.JointActivities
                .Include(a => a.Participants)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (activity == null)
                return NotFound(new { message = "联合活动不存在" });

            var participant = activity.Participants!.FirstOrDefault(p => p.UserId == userId);
            if (participant == null)
                return BadRequest(new { message = "你尚未报名此活动" });

            _context.JointParticipants.Remove(participant);
            activity.ParticipantCount -= 1;

            await _context.SaveChangesAsync();

            var updated = await _context.JointActivities
                .Include(a => a.Participants)
                .FirstOrDefaultAsync(a => a.Id == id);

            return Ok(MapToDto(updated!));
        }

        // ============================================================
        // 8. 审核参与者
        // ============================================================
        [HttpPost("{id}/audit")]
        public async Task<ActionResult<JointActivityDto>> AuditParticipant(Guid id, [FromBody] AuditRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var activity = await _context.JointActivities
                .Include(a => a.Participants)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (activity == null)
                return NotFound(new { message = "联合活动不存在" });

            var permissions = await GetUserPermissions(userId);

            if (!JointPermissionHelper.CanAuditParticipants(activity, userId, permissions))
                return Forbid();

            var participant = activity.Participants!.FirstOrDefault(p => p.UserId == request.UserId);
            if (participant == null)
                return BadRequest(new { message = "参与者不存在" });

            if (participant.Status != "pending")
                return BadRequest(new { message = "该参与者已审核" });

            participant.Status = request.Status;

            await _context.SaveChangesAsync();

            var updated = await _context.JointActivities
                .Include(a => a.Participants)
                .FirstOrDefaultAsync(a => a.Id == id);

            return Ok(MapToDto(updated!));
        }

        // ============================================================
        // 9. 踢出参与者
        // ============================================================
        [HttpPost("{id}/kick")]
        public async Task<ActionResult<JointActivityDto>> KickParticipant(Guid id, [FromBody] KickRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var activity = await _context.JointActivities
                .Include(a => a.Participants)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (activity == null)
                return NotFound(new { message = "联合活动不存在" });

            var permissions = await GetUserPermissions(userId);

            if (!JointPermissionHelper.CanAuditParticipants(activity, userId, permissions))
                return Forbid();

            var participant = activity.Participants!.FirstOrDefault(p => p.UserId == request.UserId);
            if (participant == null)
                return BadRequest(new { message = "参与者不存在" });

            _context.JointParticipants.Remove(participant);
            activity.ParticipantCount -= 1;

            await _context.SaveChangesAsync();

            var updated = await _context.JointActivities
                .Include(a => a.Participants)
                .FirstOrDefaultAsync(a => a.Id == id);

            return Ok(MapToDto(updated!));
        }

        // ============================================================
        // 10. 审批用户自建联合（管理员）
        // ============================================================
        [HttpPost("{id}/approve")]
        public async Task<ActionResult<JointActivityDto>> ApproveJoint(Guid id, [FromBody] ApproveRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var activity = await _context.JointActivities
                .FirstOrDefaultAsync(a => a.Id == id);

            if (activity == null)
                return NotFound(new { message = "联合活动不存在" });

            var permissions = await GetUserPermissions(userId);

            if (!JointPermissionHelper.CanApproveJoint(permissions))
                return Forbid();

            if (activity.OrganizerType != "user" || activity.ApprovalStatus != "pending")
                return BadRequest(new { message = "该活动无需审批或已审批" });

            activity.ApprovalStatus = request.Status;
            activity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var updated = await _context.JointActivities
                .Include(a => a.Participants)
                .FirstOrDefaultAsync(a => a.Id == id);

            return Ok(MapToDto(updated!));
        }

        // ============================================================
        // 11. 封禁/解封活动（管理员）
        // ============================================================
        [HttpPost("{id}/ban")]
        public async Task<ActionResult<JointActivityDto>> BanJoint(Guid id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var activity = await _context.JointActivities
                .FirstOrDefaultAsync(a => a.Id == id);

            if (activity == null)
                return NotFound(new { message = "联合活动不存在" });

            var permissions = await GetUserPermissions(userId);

            if (!JointPermissionHelper.CanBan(activity, userId, permissions))
                return Forbid();

            activity.Status = activity.Status == "banned" ? "open" : "banned";
            activity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var updated = await _context.JointActivities
                .Include(a => a.Participants)
                .FirstOrDefaultAsync(a => a.Id == id);

            return Ok(MapToDto(updated!));
        }

        // ============================================================
        // 12. 我举办的活动
        // ============================================================
        [HttpGet("my/organized")]
        public async Task<ActionResult<List<JointActivityDto>>> GetMyOrganized([FromQuery] string? status = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var query = _context.JointActivities
                .Where(a => a.OrganizerId == userId);

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                query = query.Where(a => a.Status == status);
            }

            var items = await query
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new JointActivityDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    Requirements = a.Requirements,
                    Contact = a.Contact,
                    Type = a.Type,
                    Status = a.Status,
                    AuditRequired = a.AuditRequired,
                    CoverUrl = a.CoverUrl,
                    OrganizerId = a.OrganizerId,
                    OrganizerName = a.OrganizerName,
                    ParticipantCount = a.ParticipantCount,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt,
                    OrganizerType = a.OrganizerType,
                    ApprovalStatus = a.ApprovalStatus,
                    // ===== 新增 =====
                    StartDate = a.StartDate,
                    EndDate = a.EndDate
                })
                .ToListAsync();

            return Ok(items);
        }

        // ============================================================
        // 13. 我参与的活动
        // ============================================================
        [HttpGet("my/participated")]
        public async Task<ActionResult<List<JointActivityDto>>> GetMyParticipated([FromQuery] string? status = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var participantActivityIds = await _context.JointParticipants
                .Where(p => p.UserId == userId)
                .Select(p => p.ActivityId)
                .ToListAsync();

            var query = _context.JointActivities
                .Where(a => participantActivityIds.Contains(a.Id));

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                query = query.Where(a => a.Status == status);
            }

            var items = await query
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new JointActivityDto
                {
                    Id = a.Id,
                    Title = a.Title,
                    Description = a.Description,
                    Requirements = a.Requirements,
                    Contact = a.Contact,
                    Type = a.Type,
                    Status = a.Status,
                    AuditRequired = a.AuditRequired,
                    CoverUrl = a.CoverUrl,
                    OrganizerId = a.OrganizerId,
                    OrganizerName = a.OrganizerName,
                    ParticipantCount = a.ParticipantCount,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt,
                    OrganizerType = a.OrganizerType,
                    ApprovalStatus = a.ApprovalStatus,
                    // ===== 新增 =====
                    StartDate = a.StartDate,
                    EndDate = a.EndDate
                })
                .ToListAsync();

            return Ok(items);
        }

        // ============================================================
        // 辅助方法
        // ============================================================
        private JointActivityDto MapToDto(JointActivity activity)
        {
            return new JointActivityDto
            {
                Id = activity.Id,
                Title = activity.Title,
                Description = activity.Description,
                Requirements = activity.Requirements,
                Contact = activity.Contact,
                Type = activity.Type,
                Status = activity.Status,
                AuditRequired = activity.AuditRequired,
                CoverUrl = activity.CoverUrl,
                OrganizerId = activity.OrganizerId,
                OrganizerName = activity.OrganizerName,
                ParticipantCount = activity.ParticipantCount,
                CreatedAt = activity.CreatedAt,
                UpdatedAt = activity.UpdatedAt,
                OrganizerType = activity.OrganizerType,
                ApprovalStatus = activity.ApprovalStatus,
                // ===== 新增 =====
                StartDate = activity.StartDate,
                EndDate = activity.EndDate,
                Participants = activity.Participants?.Select(p => new JointParticipantDto
                {
                    Id = p.Id,
                    UserId = p.UserId,
                    UserName = p.UserName,
                    Status = p.Status,
                    Remark = p.Remark,
                    CreatedAt = p.CreatedAt
                }).ToList()
            };
        }
    }
}