using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.System;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers
{
    // ===== 年度最佳展览 =====
    // 整个展览存成一份 JSON，放在 SystemConfigs 表（Key = "Awards:Hall"），不需要新建数据表。
    // 板块（categories）可在后台增删，目前是「艺术画廊」「柴圈动画」，以后可扩展。
    // 获奖条目可以只填作品 ID：读取时自动补全作品标题、封面和作者；手动填写的字段优先。

    public sealed class AwardsHallDto
    {
        public List<AwardCategoryDto> Categories { get; set; } = new();
        public List<AwardYearDto> Years { get; set; } = new();
    }

    public sealed class AwardCategoryDto
    {
        public string Key { get; set; } = "";          // 板块标识，例如 gallery / chai
        public string Name { get; set; } = "";         // 显示名称
        public string? Icon { get; set; }
        public string? Description { get; set; }
        /// <summary>本板块作品默认来源：artwork（画廊作品）/ chaiWork（柴圈作品）/ post（论坛帖子）/ custom（手动填写）。</summary>
        public string? Source { get; set; }
    }

    public sealed class AwardYearDto
    {
        public int Year { get; set; }
        public string? Title { get; set; }             // 例如「2026 年度最佳」
        public string? Intro { get; set; }             // 年度寄语
        public string? CeremonyDate { get; set; }      // 颁奖日期，yyyy-MM-dd
        public bool Published { get; set; }            // 未公开的年份只有管理员能看到
        public List<AwardEntryDto> Entries { get; set; } = new();
    }

    public sealed class AwardEntryDto
    {
        public string Id { get; set; } = "";
        public string Category { get; set; } = "";     // 对应 AwardCategoryDto.Key
        public string AwardName { get; set; } = "";    // 奖项名称，例如「年度最佳画作」
        public int Rank { get; set; }                  // 排序：1 为最高
        public string? WorkType { get; set; }          // artwork / chaiWork / post / custom
        public string? WorkId { get; set; }
        public string? WorkTitle { get; set; }
        public string? CoverUrl { get; set; }
        public string? WorkLink { get; set; }
        public string? AuthorId { get; set; }
        public string? AuthorName { get; set; }
        public string? Citation { get; set; }          // 颁奖词
        public string? Prize { get; set; }             // 奖品
        public string? PrizeImage { get; set; }        // 奖品图片
        public string? PrizeNote { get; set; }         // 奖品说明（发放状态等）
    }

    [ApiController]
    [Route("api/[controller]")]
    public class AwardsController : ControllerBase
    {
        private const string ConfigKey = "Awards:Hall";
        private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, PropertyNameCaseInsensitive = true };

        private readonly AppDbContext _context;
        public AwardsController(AppDbContext context) { _context = context; }

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        private async Task<bool> IsSuperAdmin()
        {
            if (!CurrentUserId.HasValue) return false;
            return await _context.UserPermissions.AnyAsync(p => p.UserId == CurrentUserId.Value && p.Permission == AdminPermission.SuperAdmin);
        }

        private static AwardsHallDto DefaultHall() => new()
        {
            Categories =
            {
                new AwardCategoryDto { Key = "gallery", Name = "艺术画廊", Icon = "🎨", Source = "artwork", Description = "年度最受认可的画作" },
                new AwardCategoryDto { Key = "chai", Name = "柴圈动画", Icon = "🎬", Source = "chaiWork", Description = "年度最佳火柴人动画作品" },
                // 服务器建筑通常发在论坛「我的世界 · 作品分享」里，所以默认按论坛帖子读取。
                new AwardCategoryDto { Key = "minecraft", Name = "我的世界建筑", Icon = "⛏️", Source = "post", Description = "社区服务器年度最佳建筑" },
            }
        };

        private async Task<AwardsHallDto> LoadAsync()
        {
            var row = await _context.SystemConfigs.AsNoTracking().FirstOrDefaultAsync(c => c.Key == ConfigKey);
            if (row == null || string.IsNullOrWhiteSpace(row.Value)) return DefaultHall();
            try { return JsonSerializer.Deserialize<AwardsHallDto>(row.Value, Json) ?? DefaultHall(); }
            catch (JsonException) { return DefaultHall(); }
        }

        /// <summary>公开读取。管理员额外能看到未公开的年份。</summary>
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> Get()
        {
            var canEdit = await IsSuperAdmin();
            var hall = await LoadAsync();
            var years = hall.Years.Where(y => canEdit || y.Published).OrderByDescending(y => y.Year).ToList();
            await EnrichAsync(years.SelectMany(y => y.Entries).ToList());
            foreach (var y in years) y.Entries = y.Entries.OrderBy(e => e.Category).ThenBy(e => e.Rank).ToList();
            return Ok(new { categories = hall.Categories, years, canEdit });
        }

        /// <summary>保存整个展览（仅超级管理员）。</summary>
        [HttpPut]
        [Authorize]
        public async Task<IActionResult> Save([FromBody] AwardsHallDto dto)
        {
            if (!await IsSuperAdmin()) return Forbid();

            // ---- 校验与清理 ----
            dto.Categories = dto.Categories.Where(c => !string.IsNullOrWhiteSpace(c.Key) && !string.IsNullOrWhiteSpace(c.Name)).ToList();
            foreach (var c in dto.Categories) { c.Key = c.Key.Trim(); c.Name = Clip(c.Name, 30)!; c.Icon = Clip(c.Icon, 8); c.Description = Clip(c.Description, 200); c.Source = c.Source is "artwork" or "chaiWork" or "post" ? c.Source : "custom"; }
            if (dto.Categories.Count == 0) return BadRequest(new { message = "至少需要一个板块" });
            if (dto.Categories.Select(c => c.Key).Distinct().Count() != dto.Categories.Count) return BadRequest(new { message = "板块标识不能重复" });
            if (dto.Years.Select(y => y.Year).Distinct().Count() != dto.Years.Count) return BadRequest(new { message = "年份不能重复" });

            var keys = dto.Categories.Select(c => c.Key).ToHashSet();
            foreach (var y in dto.Years)
            {
                if (y.Year < 2000 || y.Year > 2100) return BadRequest(new { message = $"年份 {y.Year} 无效" });
                y.Title = Clip(y.Title, 60); y.Intro = Clip(y.Intro, 2000); y.CeremonyDate = Clip(y.CeremonyDate, 20);
                foreach (var e in y.Entries)
                {
                    if (!keys.Contains(e.Category)) return BadRequest(new { message = $"{y.Year} 年有奖项属于不存在的板块「{e.Category}」" });
                    if (string.IsNullOrWhiteSpace(e.AwardName)) return BadRequest(new { message = $"{y.Year} 年有奖项没有填写奖项名称" });
                    if (string.IsNullOrWhiteSpace(e.Id)) e.Id = Guid.NewGuid().ToString("N");
                    e.AwardName = Clip(e.AwardName, 40)!;
                    e.WorkType = e.WorkType is "artwork" or "chaiWork" or "post" ? e.WorkType : "custom";
                    e.WorkId = Clip(e.WorkId, 64); e.WorkTitle = Clip(e.WorkTitle, 100); e.CoverUrl = Clip(e.CoverUrl, 500); e.WorkLink = Clip(e.WorkLink, 500);
                    e.AuthorId = Clip(e.AuthorId, 64); e.AuthorName = Clip(e.AuthorName, 50); e.Citation = Clip(e.Citation, 1000);
                    e.Prize = Clip(e.Prize, 100); e.PrizeImage = Clip(e.PrizeImage, 500); e.PrizeNote = Clip(e.PrizeNote, 300);
                }
            }

            var value = JsonSerializer.Serialize(dto, Json);
            var row = await _context.SystemConfigs.FirstOrDefaultAsync(c => c.Key == ConfigKey);
            if (row == null)
            {
                row = new SystemConfig { Key = ConfigKey, Description = "年度最佳展览（JSON）" };
                _context.SystemConfigs.Add(row);
            }
            row.Value = value;
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = CurrentUserId?.ToString();
            await _context.SaveChangesAsync();
            return Ok(new { message = "已保存" });
        }

        /// <summary>后台编辑时按作品 ID 预览作品信息。</summary>
        [HttpGet("lookup")]
        [Authorize]
        public async Task<IActionResult> Lookup([FromQuery] string type, [FromQuery] string id)
        {
            if (!await IsSuperAdmin()) return Forbid();
            var entry = new AwardEntryDto { WorkType = type, WorkId = id };
            await EnrichAsync(new List<AwardEntryDto> { entry }, overwrite: true);
            if (string.IsNullOrEmpty(entry.WorkTitle)) return NotFound(new { message = "没有找到这件作品，请检查 ID" });
            return Ok(entry);
        }

        // 按作品 ID 批量补全标题、封面、作者和链接；overwrite=false 时只填空字段，手动填写的内容优先。
        private async Task EnrichAsync(List<AwardEntryDto> entries, bool overwrite = false)
        {
            var artIds = entries.Where(e => e.WorkType == "artwork" && int.TryParse(e.WorkId, out _)).Select(e => int.Parse(e.WorkId!)).Distinct().ToList();
            var chaiIds = entries.Where(e => e.WorkType == "chaiWork" && Guid.TryParse(e.WorkId, out _)).Select(e => Guid.Parse(e.WorkId!)).Distinct().ToList();
            var postIds = entries.Where(e => e.WorkType == "post" && Guid.TryParse(e.WorkId, out _)).Select(e => Guid.Parse(e.WorkId!)).Distinct().ToList();

            var arts = await _context.Artworks.AsNoTracking()
                .Where(a => artIds.Contains(a.Id))
                .Select(a => new
                {
                    a.Id, a.Title, a.UploaderId, AuthorName = a.Uploader.Username,
                    Cover = a.Images.Where(i => i.IsCover).Select(i => i.ImageUrl).FirstOrDefault() ?? a.Images.OrderBy(i => i.Id).Select(i => i.ImageUrl).FirstOrDefault()
                }).ToListAsync();
            var works = await _context.ChaiWorks.AsNoTracking()
                .Where(w => chaiIds.Contains(w.Id))
                .Select(w => new { w.Id, w.Title, w.CoverUrl, w.AuthorId, w.AuthorName }).ToListAsync();
            // 论坛帖子：封面取帖子配图（帖子）或博客封面；作者 = 所在空间的主人（两表排序规则不同，分开查再对应）。
            var posts = await _context.PublishedNotes.AsNoTracking()
                .Where(pn => postIds.Contains(pn.Id))
                .Select(pn => new { pn.Id, pn.Title, pn.Type, pn.AuthorName, pn.SpaceId, pn.ExtraData, pn.Excerpt }).ToListAsync();
            var postSpaceIds = posts.Select(p => p.SpaceId).Distinct().ToList();
            var spaceOwners = (await _context.Spaces.AsNoTracking().Where(sp => postSpaceIds.Contains(sp.Id)).Select(sp => new { sp.Id, sp.UserId }).ToListAsync())
                .GroupBy(sp => sp.Id).ToDictionary(g => g.Key, g => g.First().UserId);

            string? Pick(string? current, string? found) => overwrite || string.IsNullOrWhiteSpace(current) ? found : current;
            foreach (var e in entries)
            {
                if (e.WorkType == "artwork" && int.TryParse(e.WorkId, out var artId) && arts.FirstOrDefault(a => a.Id == artId) is { } art)
                {
                    e.WorkTitle = Pick(e.WorkTitle, art.Title); e.CoverUrl = Pick(e.CoverUrl, art.Cover);
                    e.AuthorId = Pick(e.AuthorId, art.UploaderId.ToString()); e.AuthorName = Pick(e.AuthorName, art.AuthorName);
                    e.WorkLink = Pick(e.WorkLink, $"/gallery?workId={art.Id}");
                }
                else if (e.WorkType == "chaiWork" && Guid.TryParse(e.WorkId, out var workId) && works.FirstOrDefault(w => w.Id == workId) is { } work)
                {
                    e.WorkTitle = Pick(e.WorkTitle, work.Title); e.CoverUrl = Pick(e.CoverUrl, work.CoverUrl);
                    e.AuthorId = Pick(e.AuthorId, work.AuthorId.ToString()); e.AuthorName = Pick(e.AuthorName, work.AuthorName);
                    e.WorkLink = Pick(e.WorkLink, $"/works/{work.Id}");
                }
                else if (e.WorkType == "post" && Guid.TryParse(e.WorkId, out var postId) && posts.FirstOrDefault(p => p.Id == postId) is { } post)
                {
                    e.WorkTitle = Pick(e.WorkTitle, post.Title); e.CoverUrl = Pick(e.CoverUrl, PostCover(post.Type, post.ExtraData, post.Excerpt));
                    e.AuthorId = Pick(e.AuthorId, spaceOwners.TryGetValue(post.SpaceId, out var owner) ? owner : null);
                    e.AuthorName = Pick(e.AuthorName, post.AuthorName);
                    e.WorkLink = Pick(e.WorkLink, $"/stream?post={post.Id}");
                }
            }
        }

        // 帖子配图存在 ExtraData.cardCover；博客封面存在摘要 JSON 的 coverUrl。
        private static string? PostCover(string type, string? extraData, string? excerpt)
        {
            try
            {
                var json = type == "post" ? extraData : excerpt;
                if (string.IsNullOrWhiteSpace(json) || !json.TrimStart().StartsWith("{")) return null;
                using var doc = JsonDocument.Parse(json);
                var key = type == "post" ? "cardCover" : "coverUrl";
                return doc.RootElement.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()) ? value.GetString() : null;
            }
            catch (JsonException) { return null; }
        }

        private static string? Clip(string? value, int max)
        {
            if (value == null) return null;
            var trimmed = value.Trim();
            return trimmed.Length > max ? trimmed[..max] : trimmed;
        }
    }
}
