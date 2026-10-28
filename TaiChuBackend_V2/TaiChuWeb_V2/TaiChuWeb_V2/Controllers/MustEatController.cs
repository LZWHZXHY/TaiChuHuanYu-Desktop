using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Community;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers
{
    public class MustEatDto
    {
        public string Category { get; set; } = "其他";
        public string Title { get; set; } = string.Empty;
        public string? CoverImage { get; set; }
        public string? Description { get; set; }
        public string? Creator { get; set; }
        public string? Link { get; set; }
        public string? Source { get; set; }
        public double SortOrder { get; set; }
        public bool IsVisible { get; set; } = true;
    }

    public class MustEatRateDto
    {
        public int Score { get; set; }
    }

    /// <summary>必吃榜：公开列表 + SuperAdmin 维护。</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class MustEatController : ControllerBase
    {
        private readonly AppDbContext _context;
        public MustEatController(AppDbContext context) { _context = context; }

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
            var items = await _context.MustEatItems
                .Where(x => x.IsVisible && x.Status == "approved")
                .OrderBy(x => x.SortOrder).ThenByDescending(x => x.CreatedAt)
                .ToListAsync();

            var ids = items.Select(i => i.Id).ToList();
            var ratings = await _context.MustEatRatings.Where(r => ids.Contains(r.ItemId)).ToListAsync();
            var stats = ratings.GroupBy(r => r.ItemId)
                .ToDictionary(g => g.Key, g => new { Count = g.Count(), Avg = Math.Round(g.Average(r => r.Score), 1) });

            return Ok(items.Select(i => new
            {
                i.Id, i.Category, i.Title, i.CoverImage, i.Description, i.Link, i.Source, i.SortOrder, i.IsVisible, i.CreatedAt,
                ratingCount = stats.TryGetValue(i.Id, out var s) ? s.Count : 0,
                averageScore = stats.TryGetValue(i.Id, out var s2) ? s2.Avg : 0
            }));
        }

        // ===== 自动抓取 B 站封面（手机版页面带 og:image） =====
        private const string MobileUa = "Mozilla/5.0 (iPhone; CPU iPhone OS 16_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.0 Mobile/15E148 Safari/604.1";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(15) };

        [HttpGet("fetch-cover")]
        [AllowAnonymous]
        public async Task<IActionResult> FetchCover([FromQuery] string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return BadRequest(new { message = "缺少链接" });

            string? bvid = null, aid = null;
            var m = Regex.Match(url, @"(BV[0-9A-Za-z]{10})");
            if (m.Success) bvid = m.Groups[1].Value;
            else
            {
                var m2 = Regex.Match(url, @"av(\d+)", RegexOptions.IgnoreCase);
                if (m2.Success) aid = m2.Groups[1].Value;
            }
            if (bvid == null && aid == null) return BadRequest(new { message = "只支持 B站视频链接（BV号 / av号）" });

            try
            {
                var pageUrl = bvid != null
                    ? $"https://m.bilibili.com/video/{bvid}"
                    : $"https://m.bilibili.com/video/av{aid}";
                using var req = new HttpRequestMessage(HttpMethod.Get, pageUrl);
                req.Headers.UserAgent.ParseAdd(MobileUa);
                using var resp = await Http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return BadRequest(new { message = "抓取失败：HTTP " + (int)resp.StatusCode });
                var html = await resp.Content.ReadAsStringAsync();

                var pic = Regex.Match(html, "og:image\"\\s+content=\"([^\"]+)\"").Groups[1].Value;
                var title = Regex.Match(html, "og:title\"\\s+content=\"([^\"]+)\"").Groups[1].Value;
                if (string.IsNullOrEmpty(pic)) return BadRequest(new { message = "没抓到封面（可能不是视频页）" });
                pic = pic.Replace("http://", "https://");
                title = title.Replace("_哔哩哔哩_bilibili", "").Replace("_哔哩哔哩", "").Trim();
                return Ok(new { coverImage = pic, title = string.IsNullOrEmpty(title) ? null : title });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "抓取失败：" + ex.Message });
            }
        }

        /// <summary>封面为空且链接是 B站视频时，自动抓取 og:image 作为封面。</summary>
        private async Task<string?> ResolveCoverAsync(string? cover, string? link)
        {
            if (!string.IsNullOrWhiteSpace(cover) || string.IsNullOrWhiteSpace(link)) return cover;
            var m = Regex.Match(link, @"(BV[0-9A-Za-z]{10})");
            if (!m.Success) return cover;
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"https://m.bilibili.com/video/{m.Groups[1].Value}");
                req.Headers.UserAgent.ParseAdd(MobileUa);
                using var resp = await Http.SendAsync(req);
                if (!resp.IsSuccessStatusCode) return cover;
                var html = await resp.Content.ReadAsStringAsync();
                var pic = Regex.Match(html, "og:image\"\\s+content=\"([^\"]+)\"").Groups[1].Value;
                return string.IsNullOrWhiteSpace(pic) ? cover : pic.Replace("http://", "https://");
            }
            catch { return cover; }
        }

        // ===== 评分 =====
        [HttpGet("{id:int}/rating")]
        [AllowAnonymous]
        public async Task<IActionResult> GetRating(int id)
        {
            var ratings = await _context.MustEatRatings.Where(r => r.ItemId == id).ToListAsync();
            var avg = ratings.Count > 0 ? Math.Round(ratings.Average(r => r.Score), 1) : 0;
            int? mine = CurrentUserId.HasValue ? ratings.FirstOrDefault(r => r.UserId == CurrentUserId.Value)?.Score : null;
            return Ok(new { averageScore = avg, ratingCount = ratings.Count, myScore = mine });
        }

        [HttpPost("{id:int}/rating")]
        [Authorize]
        public async Task<IActionResult> Rate(int id, [FromBody] MustEatRateDto dto)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            if (dto.Score < 1 || dto.Score > 5) return BadRequest(new { message = "评分需在 1-5 之间" });

            var existing = await _context.MustEatRatings.FirstOrDefaultAsync(r => r.ItemId == id && r.UserId == CurrentUserId.Value);
            if (existing == null)
                _context.MustEatRatings.Add(new MustEatRating { ItemId = id, UserId = CurrentUserId.Value, Score = dto.Score, CreatedAt = DateTime.UtcNow });
            else
                existing.Score = dto.Score;
            await _context.SaveChangesAsync();

            var ratings = await _context.MustEatRatings.Where(r => r.ItemId == id).ToListAsync();
            return Ok(new { averageScore = Math.Round(ratings.Average(r => r.Score), 1), ratingCount = ratings.Count, myScore = dto.Score });
        }

        // ===== 用户投稿（待审核） =====
        [HttpPost("submit")]
        [Authorize]
        public async Task<IActionResult> Submit([FromBody] MustEatDto dto)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest(new { message = "标题不能为空" });

            var item = new MustEatItem
            {
                Category = string.IsNullOrWhiteSpace(dto.Category) ? "其他" : dto.Category,
                Title = dto.Title.Trim(),
                CoverImage = await ResolveCoverAsync(dto.CoverImage, dto.Link),
                Description = dto.Description,
                Creator = dto.Creator,
                Link = dto.Link,
                Source = dto.Source,
                Status = "pending",
                SubmittedByUserId = CurrentUserId.Value,
                IsVisible = true,
                CreatedAt = DateTime.UtcNow
            };
            _context.MustEatItems.Add(item);
            await _context.SaveChangesAsync();
            return Ok(new { id = item.Id, message = "已提交，等待审核" });
        }

        // ===== 待审核列表（管理员） =====
        [HttpGet("pending")]
        [Authorize]
        public async Task<IActionResult> Pending()
        {
            if (!await IsSuperAdmin()) return Forbid();
            var list = await _context.MustEatItems
                .Where(x => x.Status == "pending")
                .OrderByDescending(x => x.CreatedAt)
                .ToListAsync();
            return Ok(list);
        }

        [HttpPost("{id:int}/approve")]
        [Authorize]
        public async Task<IActionResult> Approve(int id)
        {
            if (!await IsSuperAdmin()) return Forbid();
            var item = await _context.MustEatItems.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound(new { message = "条目不存在" });
            item.Status = "approved";
            await _context.SaveChangesAsync();
            return Ok(new { message = "已通过" });
        }

        [HttpPost("{id:int}/reject")]
        [Authorize]
        public async Task<IActionResult> Reject(int id)
        {
            if (!await IsSuperAdmin()) return Forbid();
            var item = await _context.MustEatItems.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound(new { message = "条目不存在" });
            item.Status = "rejected";
            await _context.SaveChangesAsync();
            return Ok(new { message = "已驳回" });
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] MustEatDto dto)
        {
            if (!await IsSuperAdmin()) return Forbid();
            if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest(new { message = "标题不能为空" });

            var item = new MustEatItem
            {
                Category = string.IsNullOrWhiteSpace(dto.Category) ? "其他" : dto.Category,
                Title = dto.Title.Trim(),
                CoverImage = await ResolveCoverAsync(dto.CoverImage, dto.Link),
                Description = dto.Description,
                Creator = dto.Creator,
                Link = dto.Link,
                Source = dto.Source,
                SortOrder = dto.SortOrder,
                IsVisible = dto.IsVisible,
                CreatedAt = DateTime.UtcNow
            };
            _context.MustEatItems.Add(item);
            await _context.SaveChangesAsync();
            return Ok(new { id = item.Id });
        }

        [HttpPut("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] MustEatDto dto)
        {
            if (!await IsSuperAdmin()) return Forbid();
            var item = await _context.MustEatItems.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound(new { message = "条目不存在" });

            if (!string.IsNullOrWhiteSpace(dto.Title)) item.Title = dto.Title.Trim();
            item.Category = string.IsNullOrWhiteSpace(dto.Category) ? item.Category : dto.Category;
            item.CoverImage = await ResolveCoverAsync(dto.CoverImage, dto.Link);
            item.Description = dto.Description;
            item.Creator = dto.Creator;
            item.Link = dto.Link;
            item.Source = dto.Source;
            item.SortOrder = dto.SortOrder;
            item.IsVisible = dto.IsVisible;
            await _context.SaveChangesAsync();
            return Ok(new { id = item.Id });
        }

        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await IsSuperAdmin()) return Forbid();
            var item = await _context.MustEatItems.FirstOrDefaultAsync(x => x.Id == id);
            if (item == null) return NotFound(new { message = "条目不存在" });
            _context.MustEatItems.Remove(item);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
