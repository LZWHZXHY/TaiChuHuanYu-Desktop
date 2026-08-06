using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Chai.Joint;
using TaiChuWeb_V2.Models.ChaiCommunity.Joint;
using TaiChuWeb_V2.Models.User; // 引入 AdminPermission

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

            // 关键词搜索
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(a => a.Title.Contains(keyword) ||
                                         a.Description.Contains(keyword));
            }

            // 状态筛选
            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                query = query.Where(a => a.Status == status);
            }

            // 类型筛选
            if (!string.IsNullOrEmpty(type) && type != "all")
            {
                query = query.Where(a => a.Type == type);
            }

            // 排序：最新优先
            query = query.OrderByDescending(a => a.CreatedAt);

            var total = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
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
                    OrganizerType = a.OrganizerType,          // 新增
                    ApprovalStatus = a.ApprovalStatus         // 新增
                })
                .ToListAsync();

            return Ok(new JointListResponse
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            });
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
                OrganizerType = activity.OrganizerType,          // 新增
                ApprovalStatus = activity.ApprovalStatus,         // 新增
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

            // 判断是否创建官方联合
            var isOfficial = request.OrganizerType == "official";
            if (isOfficial && !JointPermissionHelper.CanCreateOfficial(permissions))
                return Forbid();

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
                OrganizerType = request.OrganizerType ?? "user",  // 新增
                ApprovalStatus = isOfficial ? "approved" : "pending"  // 官方直接通过，用户需要审核
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
                ApprovalStatus = activity.ApprovalStatus
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

            // 使用权限助手检查编辑权限
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

            // 只有 SuperAdmin 可以删除
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

            // 检查活动是否可报名（状态为 open 且已审核通过或官方）
            if (activity.Status != "open")
                return BadRequest(new { message = "活动已截止或已结束" });

            if (activity.OrganizerType == "user" && activity.ApprovalStatus != "approved")
                return BadRequest(new { message = "活动尚未审核通过" });

            // 检查是否已报名
            if (activity.Participants!.Any(p => p.UserId == userId))
                return BadRequest(new { message = "你已经报名参加了此活动" });

            // 检查是否是举办者（举办者不能报名自己的活动）
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

            // 使用权限助手检查审核参与者权限
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

            // 使用权限助手检查踢出权限（与审核参与者权限一致）
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

            // 只有管理员可以审批
            if (!JointPermissionHelper.CanApproveJoint(permissions))
                return Forbid();

            // 只能审批用户自建且状态为 pending 的活动
            if (activity.OrganizerType != "user" || activity.ApprovalStatus != "pending")
                return BadRequest(new { message = "该活动无需审批或已审批" });

            activity.ApprovalStatus = request.Status; // "approved" 或 "rejected"
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

            // 检查封禁权限
            if (!JointPermissionHelper.CanBan(activity, userId, permissions))
                return Forbid();

            // 切换封禁状态
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
                    ApprovalStatus = a.ApprovalStatus
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
                    ApprovalStatus = a.ApprovalStatus
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