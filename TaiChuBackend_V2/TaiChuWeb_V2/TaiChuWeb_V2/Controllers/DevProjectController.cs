using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Community;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers
{
    public class MilestoneDto
    {
        public string Title { get; set; } = string.Empty;
        public bool Done { get; set; }
    }

    /// <summary>参考文献（论文 / 书籍 / 资料）。</summary>
    public class ReferenceDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Authors { get; set; }
        public string? Venue { get; set; }
        public string? Year { get; set; }
        public string? Url { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>实验 / 调研数据点。</summary>
    public class DataPointDto
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Value { get; set; }
        public string? Unit { get; set; }
        public string? Note { get; set; }
    }

    /// <summary>研究日志（过程记录）。</summary>
    public class ResearchLogDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Date { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public List<string> Images { get; set; } = new();
        /// <summary>写这条日志时的项目进度（0-100），可为空</summary>
        public int? Progress { get; set; }
    }

    public class DevProjectDto
    {
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Content { get; set; }
        public string Category { get; set; } = "other";
        public string Status { get; set; } = "ongoing";
        public int Progress { get; set; }
        public List<MilestoneDto> Milestones { get; set; } = new();
        public string Type { get; set; } = "dev";
        public List<ReferenceDto> References { get; set; } = new();
        public List<DataPointDto> DataPoints { get; set; } = new();
        public List<ResearchLogDto> Logs { get; set; } = new();
        public string Icon { get; set; } = "🚀";
        public int SortOrder { get; set; }
        public Guid? ParentId { get; set; }
    }

    /// <summary>官方开发进度（公开查看，管理员维护）。</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class DevProjectController : ControllerBase
    {
        private readonly AppDbContext _context;
        public DevProjectController(AppDbContext context) { _context = context; }

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        private async Task<bool> IsSuperAdmin()
        {
            if (!CurrentUserId.HasValue) return false;
            return await _context.UserPermissions.AnyAsync(p => p.UserId == CurrentUserId.Value && p.Permission == AdminPermission.SuperAdmin);
        }

        // ===== 公开查询 =====
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> List()
        {
            var list = await _context.DevProjects
                .OrderBy(p => p.SortOrder).ThenByDescending(p => p.CreatedAt)
                .ToListAsync();
            return Ok(list.Select(p => new
            {
                p.Id, p.Name, p.Description, p.Content, p.Category, p.Status, p.Progress, p.Icon, p.SortOrder, p.ParentId, p.UpdatedAt,
                Milestones = Deserialize<List<MilestoneDto>>(p.Milestones),
                Type = p.Type,
                References = Deserialize<List<ReferenceDto>>(p.ReferencesJson),
                DataPoints = Deserialize<List<DataPointDto>>(p.DataJson),
                Logs = Deserialize<List<ResearchLogDto>>(p.LogsJson)
            }));
        }

        // ===== 管理员维护 =====
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] DevProjectDto dto)
        {
            if (!await IsSuperAdmin()) return Forbid();
            if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { message = "项目名称不能为空" });
            var project = new DevProject
            {
                Name = dto.Name.Trim(),
                Description = dto.Description,
                Content = dto.Content,
                Category = dto.Category,
                Status = dto.Status,
                Progress = Math.Clamp(dto.Progress, 0, 100),
                Milestones = JsonSerializer.Serialize(dto.Milestones ?? new List<MilestoneDto>()),
                Type = string.IsNullOrWhiteSpace(dto.Type) ? "dev" : dto.Type,
                ReferencesJson = JsonSerializer.Serialize(dto.References ?? new List<ReferenceDto>()),
                DataJson = JsonSerializer.Serialize(dto.DataPoints ?? new List<DataPointDto>()),
                LogsJson = JsonSerializer.Serialize(dto.Logs ?? new List<ResearchLogDto>()),
                Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "🚀" : dto.Icon,
                SortOrder = dto.SortOrder,
                ParentId = dto.ParentId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.DevProjects.Add(project);
            await _context.SaveChangesAsync();
            return Ok(new { id = project.Id });
        }

        [HttpPut("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> Update(Guid id, [FromBody] DevProjectDto dto)
        {
            if (!await IsSuperAdmin()) return Forbid();
            var project = await _context.DevProjects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null) return NotFound(new { message = "项目不存在" });
            if (!string.IsNullOrWhiteSpace(dto.Name)) project.Name = dto.Name.Trim();
            project.Description = dto.Description;
            project.Content = dto.Content;
            project.Category = dto.Category;
            project.Status = dto.Status;
            project.Progress = Math.Clamp(dto.Progress, 0, 100);
            project.Milestones = JsonSerializer.Serialize(dto.Milestones ?? new List<MilestoneDto>());
            project.Type = string.IsNullOrWhiteSpace(dto.Type) ? project.Type : dto.Type;
            project.ReferencesJson = JsonSerializer.Serialize(dto.References ?? new List<ReferenceDto>());
            project.DataJson = JsonSerializer.Serialize(dto.DataPoints ?? new List<DataPointDto>());
            project.LogsJson = JsonSerializer.Serialize(dto.Logs ?? new List<ResearchLogDto>());
            if (!string.IsNullOrWhiteSpace(dto.Icon)) project.Icon = dto.Icon;
            project.SortOrder = dto.SortOrder;
            project.ParentId = dto.ParentId;
            project.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { id = project.Id });
        }

        [HttpDelete("{id:guid}")]
        [Authorize]
        public async Task<IActionResult> Delete(Guid id)
        {
            if (!await IsSuperAdmin()) return Forbid();
            var project = await _context.DevProjects.FirstOrDefaultAsync(p => p.Id == id);
            if (project == null) return NotFound(new { message = "项目不存在" });
            // 解除子项目归属，避免悬空引用
            await _context.DevProjects.Where(p => p.ParentId == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.ParentId, (Guid?)null));
            _context.DevProjects.Remove(project);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static T Deserialize<T>(string json) where T : new()
        {
            try { return JsonSerializer.Deserialize<T>(json) ?? new T(); }
            catch { return new T(); }
        }
    }
}
