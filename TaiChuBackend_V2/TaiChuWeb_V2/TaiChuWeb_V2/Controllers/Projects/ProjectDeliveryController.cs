using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext; // 替换为你的 DbContext 路径
using TaiChuWeb_V2.Models.Project;

namespace TaiChuWeb_V2.Controllers.Project
{
    [ApiController]
    [Route("api/project/{projectId}/deliveries")]
    public class ProjectDeliveryController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProjectDeliveryController(AppDbContext context)
        {
            _context = context;
        }

        // 获取该项目下所有的履约验收记录
        [HttpGet]
        public async Task<IActionResult> GetDeliveries(string projectId)
        {
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
                    adminNote = r.AdminNote,
                    createdAt = r.CreatedAt
                })
                .ToListAsync();

            return Ok(records);
        }

        // 登记一条新的履约记录
        [HttpPost]
        public async Task<IActionResult> CreateDelivery(string projectId, [FromBody] DeliveryDto dto)
        {
            // TODO: 此处应从 JWT Token 中获取当前登录用户的 ID
            // Guid currentUserId = ... 

            var record = new ProjectDeliveryRecord
            {
                ProjectId = projectId,
                MemberId = dto.MemberId,
                RecordedName = dto.RecordedName, // 🌟 存入当时的文本名字作为历史快照
                TaskName = dto.TaskName,
                TimingStatus = dto.TimingStatus,
                QualityStatus = dto.QualityStatus,
                CommStatus = dto.CommStatus,
                ReworkCount = dto.ReworkCount,
                AdminNote = dto.AdminNote,
                // CreatedById = currentUserId 
            };

            _context.ProjectDeliveryRecords.Add(record);
            await _context.SaveChangesAsync();

            return Ok(new { message = "验收记录已登记" });
        }

        // 抹除记录
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteDelivery(string projectId, Guid id)
        {
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
        public string TaskName { get; set; } = string.Empty;
        public string TimingStatus { get; set; } = string.Empty;
        public string QualityStatus { get; set; } = string.Empty;
        public string CommStatus { get; set; } = string.Empty;
        public int ReworkCount { get; set; }
        public string? AdminNote { get; set; }
    }
}