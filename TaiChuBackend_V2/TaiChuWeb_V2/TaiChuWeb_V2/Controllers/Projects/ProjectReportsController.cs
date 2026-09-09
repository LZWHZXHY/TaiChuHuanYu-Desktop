using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Project;

namespace TaiChuWeb_V2.Controllers.Projects
{
    [Authorize]
    [ApiController]
    [Route("api/project/{projectId}/reports")]
    public class ProjectReportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProjectReportsController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetReports(string projectId)
        {
            var reports = await _context.ProjectReports
                .Where(r => r.ProjectId == projectId)
                .OrderByDescending(r => r.CreatedAt)
                .Select(r => new
                {
                    id = r.Id,
                    authorName = r.AuthorName,
                    type = r.Type,
                    module = r.Module,
                    hours = r.Hours,
                    summary = r.Summary,
                    blockers = r.Blockers,
                    nextPlan = r.NextPlan,
                    images = string.IsNullOrEmpty(r.ImagesJson) || r.ImagesJson == "[]"
                        ? new List<string>()
                        : JsonSerializer.Deserialize<List<string>>(r.ImagesJson, (JsonSerializerOptions?)null),
                    date = r.CreatedAt.ToString("yyyy-MM-dd")
                })
                .ToListAsync();

            return Ok(reports);
        }

        [HttpPost]
        public async Task<IActionResult> CreateReport(string projectId, [FromBody] ReportDto dto)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "未能识别身份" });

            // 获取项目配置，检查是否超时
            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);
            if (project == null) return NotFound("项目不存在");

            var isOwner = project.OwnerId == userId; // 判断是否是主理人

            // 普通成员受时间限制，主理人/管理员免疫
            if (!isOwner)
            {
                var now = DateTime.UtcNow.AddHours(8); // 以北京时间为准进行校验
                if (dto.Type == "周报" && project.WeeklyReportDeadline > 0)
                {
                    int currentDayOfWeek = now.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)now.DayOfWeek;
                    if (currentDayOfWeek > project.WeeklyReportDeadline)
                        return BadRequest(new { message = "本周汇报提交通道已关闭，已超时。" });
                }

                if (dto.Type == "月报" && project.MonthlyReportDeadline > 0)
                {
                    if (now.Day > project.MonthlyReportDeadline)
                        return BadRequest(new { message = "本月汇报提交通道已关闭，已超时。" });
                }
            }

            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            var authorName = currentUser?.Username ?? "未知成员";

            var report = new ProjectReport
            {
                ProjectId = projectId,
                UserId = userIdStr, // 模型中 UserId 是 string
                AuthorName = authorName,
                Type = string.IsNullOrWhiteSpace(dto.Type) ? "周报" : dto.Type,
                Module = string.IsNullOrWhiteSpace(dto.Module) ? "常规推进" : dto.Module,
                Hours = dto.Hours,
                Summary = dto.Summary,
                Blockers = dto.Blockers,
                NextPlan = dto.NextPlan,
                ImagesJson = dto.Images != null && dto.Images.Any() ? JsonSerializer.Serialize(dto.Images) : "[]"
            };

            _context.ProjectReports.Add(report);
            await _context.SaveChangesAsync();
            return Ok(new { message = "汇报已成功归档" });
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteReport(string projectId, string id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "未能识别身份" });

            var project = await _context.Projects.FirstOrDefaultAsync(p => p.Id == projectId);

            // 仅项目所有者可删除
            if (project == null || project.OwnerId != userId)
            {
                return StatusCode(403, new { message = "仅项目掌控者/管理员有权抹除已归档的汇报" });
            }

            var report = await _context.ProjectReports.FirstOrDefaultAsync(r => r.Id == id && r.ProjectId == projectId);
            if (report == null) return NotFound("汇报不存在");

            _context.ProjectReports.Remove(report);
            await _context.SaveChangesAsync();
            return Ok(new { message = "汇报已抹除" });
        }
    }

    public class ReportDto
    {
        public string Type { get; set; } = string.Empty;
        public string? Module { get; set; }
        public decimal Hours { get; set; }
        public string Summary { get; set; } = string.Empty;
        public string? Blockers { get; set; }
        public string? NextPlan { get; set; }
        public List<string>? Images { get; set; }
    }
}