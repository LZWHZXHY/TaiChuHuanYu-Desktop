using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Community;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers
{
    public class CommunityRuleDto
    {
        public int? ParentId { get; set; }
        public string? Code { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? Content { get; set; }
        public string? Penalty { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public string Version { get; set; } = "1.0";
    }

    /// <summary>社区规则：公开列表 + SuperAdmin 维护。</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class CommunityRuleController : ControllerBase
    {
        private readonly AppDbContext _context;
        public CommunityRuleController(AppDbContext context) { _context = context; }

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        private async Task<bool> IsSuperAdmin()
        {
            if (!CurrentUserId.HasValue) return false;
            return await _context.UserPermissions.AnyAsync(p => p.UserId == CurrentUserId.Value && p.Permission == AdminPermission.SuperAdmin);
        }

        /// <summary>当前站点版本（取最新发布的 Release 版本号），用于自动记录规则所属版本。</summary>
        private async Task<string> CurrentSiteVersionAsync()
        {
            var version = await _context.Releases
                .Where(r => r.IsPublished)
                .OrderByDescending(r => r.ReleasedAt)
                .ThenByDescending(r => r.Id)
                .Select(r => r.Version)
                .FirstOrDefaultAsync();
            return string.IsNullOrWhiteSpace(version) ? "1.0.0" : version.Trim();
        }

        /// <summary>规则文档版本（单例，Id=1）。</summary>
        private async Task<RulesMeta> GetOrCreateMetaAsync()
        {
            var meta = await _context.RulesMeta.FirstOrDefaultAsync(m => m.Id == 1);
            if (meta == null)
            {
                meta = new RulesMeta { Id = 1, Version = "1.0.0", UpdatedAt = DateTime.UtcNow };
                _context.RulesMeta.Add(meta);
                await _context.SaveChangesAsync();
            }
            return meta;
        }

        /// <summary>版本升级：minor=true 升次版本（新增条款），否则升补丁（修改/删除）。</summary>
        private static string BumpVersion(string? version, bool minor)
        {
            var parts = (version ?? "1.0.0").Split('.');
            var major = parts.Length > 0 && int.TryParse(parts[0], out var a) ? a : 1;
            var min = parts.Length > 1 && int.TryParse(parts[1], out var b) ? b : 0;
            var patch = parts.Length > 2 && int.TryParse(parts[2], out var c) ? c : 0;
            if (minor) { min++; patch = 0; } else { patch++; }
            return $"{major}.{min}.{patch}";
        }

        /// <summary>规则文档版本（公开）。</summary>
        [HttpGet("meta")]
        [AllowAnonymous]
        public async Task<IActionResult> Meta()
        {
            var meta = await GetOrCreateMetaAsync();
            return Ok(new { version = meta.Version, updatedAt = meta.UpdatedAt });
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> List()
        {
            var isAdmin = await IsSuperAdmin();
            var list = await _context.CommunityRules
                .Where(r => isAdmin || r.IsActive)
                .OrderBy(r => r.SortOrder).ThenBy(r => r.Id)
                .ToListAsync();
            return Ok(list);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] CommunityRuleDto dto)
        {
            if (!await IsSuperAdmin()) return Forbid();
            var rule = new CommunityRule
            {
                ParentId = dto.ParentId,
                Code = dto.Code,
                Title = dto.Title?.Trim() ?? "",
                Content = dto.Content,
                Penalty = dto.Penalty,
                SortOrder = dto.SortOrder,
                IsActive = dto.IsActive,
                Version = await CurrentSiteVersionAsync(),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _context.CommunityRules.Add(rule);
            var meta = await GetOrCreateMetaAsync();
            meta.Version = BumpVersion(meta.Version, minor: true);
            meta.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(new { id = rule.Id, version = meta.Version });
        }

        [HttpPut("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] CommunityRuleDto dto)
        {
            if (!await IsSuperAdmin()) return Forbid();
            var rule = await _context.CommunityRules.FirstOrDefaultAsync(x => x.Id == id);
            if (rule == null) return NotFound(new { message = "规则不存在" });

            rule.ParentId = dto.ParentId;
            rule.Code = dto.Code;
            rule.Title = dto.Title?.Trim() ?? rule.Title;
            rule.Content = dto.Content;
            rule.Penalty = dto.Penalty;
            rule.SortOrder = dto.SortOrder;
            rule.IsActive = dto.IsActive;
            rule.Version = await CurrentSiteVersionAsync();
            rule.UpdatedAt = DateTime.UtcNow;

            var meta = await GetOrCreateMetaAsync();
            meta.Version = BumpVersion(meta.Version, minor: false);
            meta.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(new { id = rule.Id, version = meta.Version });
        }

        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await IsSuperAdmin()) return Forbid();
            var rule = await _context.CommunityRules.FirstOrDefaultAsync(x => x.Id == id);
            if (rule == null) return NotFound(new { message = "规则不存在" });
            _context.CommunityRules.Remove(rule);
            var meta = await GetOrCreateMetaAsync();
            meta.Version = BumpVersion(meta.Version, minor: false);
            meta.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
