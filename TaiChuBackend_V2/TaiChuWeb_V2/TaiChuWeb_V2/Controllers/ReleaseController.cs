using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Release;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers
{
    public class ReleaseDto
    {
        public string Version { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public DateTime? ReleasedAt { get; set; }
        public bool IsPublished { get; set; } = true;
    }

    /// <summary>版本更新（Changelog）：公开列表 + SuperAdmin 维护。</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ReleaseController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ReleaseController(AppDbContext context) { _context = context; }

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        private async Task<bool> IsSuperAdmin()
        {
            if (!CurrentUserId.HasValue) return false;
            return await _context.UserPermissions.AnyAsync(p => p.UserId == CurrentUserId.Value && p.Permission == AdminPermission.SuperAdmin);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> List()
        {
            var isAdmin = await IsSuperAdmin();
            var list = await _context.Releases
                .Where(r => isAdmin || r.IsPublished)
                .ToListAsync();
            // 按版本号语义化倒序（7.0.0 > 6.0.0 > 1.0.0，10.0.0 也排对），同日再按时间倒序
            list = list.OrderByDescending(r => ParseVersion(r.Version))
                .ThenByDescending(r => r.ReleasedAt)
                .ThenByDescending(r => r.Id)
                .ToList();
            return Ok(list);
        }

        [HttpGet("latest")]
        [AllowAnonymous]
        public async Task<IActionResult> Latest()
        {
            var list = await _context.Releases.Where(r => r.IsPublished).ToListAsync();
            var latest = list.OrderByDescending(r => ParseVersion(r.Version))
                .ThenByDescending(r => r.ReleasedAt)
                .FirstOrDefault();
            return Ok(latest);
        }

        private static (int Major, int Minor, int Patch) ParseVersion(string? version)
        {
            var m = System.Text.RegularExpressions.Regex.Match(version ?? "", @"(\d+)\.(\d+)\.(\d+)");
            return m.Success
                ? (int.Parse(m.Groups[1].Value), int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value))
                : (0, 0, 0);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] ReleaseDto dto)
        {
            if (!await IsSuperAdmin()) return Forbid();
            if (string.IsNullOrWhiteSpace(dto.Version)) return BadRequest(new { message = "请填写版本号" });

            var release = new Release
            {
                Version = dto.Version.Trim(),
                Title = dto.Title?.Trim() ?? "",
                Content = dto.Content,
                ReleasedAt = dto.ReleasedAt ?? DateTime.UtcNow,
                IsPublished = dto.IsPublished,
                CreatedAt = DateTime.UtcNow
            };
            _context.Releases.Add(release);
            await _context.SaveChangesAsync();
            return Ok(new { id = release.Id });
        }

        [HttpPut("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] ReleaseDto dto)
        {
            if (!await IsSuperAdmin()) return Forbid();
            var release = await _context.Releases.FirstOrDefaultAsync(x => x.Id == id);
            if (release == null) return NotFound(new { message = "版本不存在" });

            if (!string.IsNullOrWhiteSpace(dto.Version)) release.Version = dto.Version.Trim();
            release.Title = dto.Title?.Trim() ?? release.Title;
            release.Content = dto.Content;
            if (dto.ReleasedAt.HasValue) release.ReleasedAt = dto.ReleasedAt.Value;
            release.IsPublished = dto.IsPublished;
            await _context.SaveChangesAsync();
            return Ok(new { id = release.Id });
        }

        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await IsSuperAdmin()) return Forbid();
            var release = await _context.Releases.FirstOrDefaultAsync(x => x.Id == id);
            if (release == null) return NotFound(new { message = "版本不存在" });
            _context.Releases.Remove(release);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
