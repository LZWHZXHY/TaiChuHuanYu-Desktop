using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using TaiChuWeb_V2.Services.Project;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Project;

namespace TaiChuWeb_V2.Controllers.Project
{
    [Authorize, ApiController]
    [Route("api/project/{projectId}/deliveries")]
    public class ProjectDeliveryController : ControllerBase
    {
        private readonly AppDbContext _context;

        private readonly IProjectPermissionService _permission;
        private string UserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        public ProjectDeliveryController(AppDbContext context, IProjectPermissionService permission)
        {
            _context = context; _permission = permission;
        }

        // 获取该项目下所有的履约验收记录
        [HttpGet]
        public async Task<IActionResult> GetDeliveries(string projectId)
        {
            var canManage = await _permission.HasPermissionAsync(projectId, UserId, "task:manage");
            if (!canManage && !await _context.ProjectMembers.AnyAsync(m => m.ProjectId == projectId && m.UserId == UserId)) return Forbid();
            var records = await _context.ProjectDeliveryRecords
                .Include(r => r.Member)
                .Where(r => r.ProjectId == projectId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    id = r.Id,
                    memberId = r.MemberId,
                    // 🌟 永远查他现在的最新真名
                    currentName = r.Member != null ? r.Member.Username : "已注销用户",
                    // 🌟 案发时存下的历史快照名字
                    recordedName = r.RecordedName,
                    taskName = r.TaskName,
                    timingStatus = r.TimingStatus,
                    qualityStatus = r.QualityStatus,
                    commStatus = r.CommStatus,
                    reworkCount = r.ReworkCount,
                    adminNote = canManage ? r.AdminNote : null,
                    createdAt = r.CreatedAt
                })
                .ToListAsync();

            return Ok(records);
        }

        // 登记一条新的履约记录
        [HttpPost]
        public async Task<IActionResult> CreateDelivery(string projectId, [FromBody] DeliveryDto dto)
        {
            if (!await _permission.HasPermissionAsync(projectId, UserId, "task:manage")) return Forbid();
            if (!await _context.Projects.AnyAsync(p => p.Id == projectId)) return NotFound();
            var member = await _context.Users.FirstOrDefaultAsync(u => u.Id == dto.MemberId);
            if (member == null) return BadRequest(new { message = "请选择存在的协作者" });

            var record = new ProjectDeliveryRecord
            {
                ProjectId = projectId,
                MemberId = dto.MemberId,
                RecordedName = member.Username, // 🌟 存入当时的文本名字作为历史快照
                TaskName = dto.TaskName,
                TimingStatus = dto.TimingStatus,
                QualityStatus = dto.QualityStatus,
                CommStatus = dto.CommStatus,
                ReworkCount = dto.ReworkCount,
                AdminNote = dto.AdminNote,
            };

            _context.ProjectDeliveryRecords.Add(record);
            await _context.SaveChangesAsync();

            return Ok(new { message = "验收记录已登记" });
        }

        // 抹除记录
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDelivery(string projectId, Guid id)
        {
            if (!await _permission.HasPermissionAsync(projectId, UserId, "task:manage")) return Forbid();
            var record = await _context.ProjectDeliveryRecords.FirstOrDefaultAsync(r => r.Id == id && r.ProjectId == projectId);
            if (record == null) return NotFound("记录不存在");

            _context.ProjectDeliveryRecords.Remove(record);
            await _context.SaveChangesAsync();

            return Ok(new { message = "记录已抹除" });
        }
    }

    public class DeliveryDto
    {
        public Guid MemberId { get; set; }
        // 🌟 新增：接收前端传来的当时的名字
        public string RecordedName { get; set; } = string.Empty;
        [Required, MaxLength(200)] public string TaskName { get; set; } = string.Empty;
        [Required, MaxLength(50)] public string TimingStatus { get; set; } = string.Empty;
        [Required, MaxLength(50)] public string QualityStatus { get; set; } = string.Empty;
        [Required, MaxLength(50)] public string CommStatus { get; set; } = string.Empty;
        [Range(0, int.MaxValue)] public int ReworkCount { get; set; }
        [MaxLength(1000)] public string? AdminNote { get; set; }
    }
}
