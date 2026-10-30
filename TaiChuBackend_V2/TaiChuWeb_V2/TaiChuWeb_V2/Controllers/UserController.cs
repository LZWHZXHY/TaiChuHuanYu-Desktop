using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.User;
using TaiChuWeb_V2.Models.User;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.Controllers.LingMai;
using TaiChuWeb_V2.Services.Cos;
using TaiChuWeb_V2.Services.Shop;

[Authorize]
[Route("api/[controller]")]
[ApiController]
public class UserController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly ShopService _shop;
    private readonly CosService _cos;

    public UserController(AppDbContext context, ShopService shop, CosService cos)
    {
        _context = context;
        _shop = shop;
        _cos = cos;
    }

    /// <summary>用户自己上传的头像放在这个目录下（只由服务器写入）</summary>
    private string OwnAvatarPrefix(Guid userId) => _cos.ObjectUrl($"avatars/u/{userId:N}/");

    /// <summary>
    /// 上传自己的头像：只收静态图（JPG / PNG / WebP）。GIF、动态 WebP、APNG 会被拒绝——动图头像只能用官方的（交易行出售）。
    /// 服务器检查文件内容后存到 avatars/u/{用户}/ 并直接设成头像。
    /// </summary>
    [HttpPost("avatar")]
    [RequestSizeLimit(AvatarImage.MaxUserBytes + 64 * 1024)]
    public async Task<IActionResult> UploadAvatar(IFormFile? file)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        if (file == null || file.Length == 0) return BadRequest(new { message = "没有收到图片" });
        if (file.Length > AvatarImage.MaxUserBytes) return BadRequest(new { message = "头像不能超过 5MB" });
        await using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        var bytes = ms.ToArray();
        var info = AvatarImage.Sniff(bytes);
        if (info == null) return BadRequest(new { message = "只支持 JPG、PNG、WebP 图片" });
        if (info.Animated) return BadRequest(new { message = "不能上传 GIF 或动图头像。动图头像可以在交易行兑换官方的。" });
        var url = await _cos.UploadAsync(bytes, $"{Guid.NewGuid():N}.{info.Ext}", $"avatars/u/{userId:N}");

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
        if (profile == null)
        {
            profile = new UserProfile { UserId = userId };
            _context.UserProfiles.Add(profile);
        }
        profile.Avatar = url;
        await _context.SaveChangesAsync();
        return Ok(new { avatar = url });
    }


    [HttpGet("experience-history")]
    public async Task<IActionResult> ExperienceHistory(int offset = 0, int limit = 20)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        offset = Math.Max(0, offset); limit = Math.Clamp(limit, 1, 50);
        var rows = await _context.UserExpLogs.AsNoTracking().Where(log => log.UserId == id)
            .OrderByDescending(log => log.CreatedAt).ThenByDescending(log => log.Id).Skip(offset).Take(limit + 1)
            .Select(log => new { log.Id, log.Change, log.Reason, log.CreatedAt }).ToListAsync();
        return Ok(new { items = rows.Take(limit), hasMore = rows.Count > limit });
    }

    // 把它加到你现有的 UserController 里
    [HttpGet("search")]
    public async Task<IActionResult> SearchGlobalUsers([FromQuery] string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
            return Ok(new List<object>());

        var users = await _context.Users
            .Where(u => u.Username.Contains(keyword))
            .Take(10)
            .Select(u => new
            {
                id = u.Id,
                username = u.Username,
                email = u.Email
            })
            .ToListAsync();

        return Ok(users);
    }







    // 公开个人主页：任何人（包括未登录访客）都能看。只返回公开资料和已公开的内容，
    // 不含邮箱、手机号、住址、生日等私人信息。
    [AllowAnonymous]
    [HttpGet("{id:guid}/public")]
    public async Task<IActionResult> GetPublicProfile(Guid id)
    {
        var user = await _context.Users.AsNoTracking()
            .Include(u => u.Profile)
            .Include(u => u.Stats)
            .FirstOrDefaultAsync(u => u.Id == id);
        if (user == null) return NotFound(new { message = "没有找到这位用户" });

        // spaces 与 PublishedNotes 两表排序规则不同，先取出空间 ID 再筛选，不在 SQL 里直接比较两列。
        var spaceIds = await LingMaiPublishController.AuthorSpaceIds(_context, id);
        var notes = LingMaiPublishController.VisiblePublishedNotes(_context)
            .Where(pn => (pn.Type == "post" || pn.Type == "blog" || pn.Type == "note") && spaceIds.Contains(pn.SpaceId));
        var artworks = _context.Artworks.AsNoTracking()
            .Where(a => a.UploaderId == id && a.IsApproved && (a.Status == "published" || a.Status == "" || a.Status == null));
        var worlds = _context.WorldProjects.AsNoTracking().Where(w => w.OwnerId == id && w.IsPublic);
        var characters = _context.StickmanCharacters.AsNoTracking().Where(c => c.AuthorId == id && c.Status == "published");

        var counts = new
        {
            Posts = await notes.CountAsync(),
            Artworks = await artworks.CountAsync(),
            Worlds = await worlds.CountAsync(),
            Characters = await characters.CountAsync()
        };

        var recentArtworks = await artworks.OrderByDescending(a => a.UploadAt).Take(12)
            .Select(a => new { a.Id, a.Title, CoverImageUrl = a.Images.Where(i => i.IsCover).Select(i => i.ImageUrl).FirstOrDefault() })
            .ToListAsync();
        var publicWorlds = await worlds.OrderByDescending(w => w.UpdatedAt).Take(6)
            .Select(w => new { w.Id, w.Name, w.Description, w.CoverImage })
            .ToListAsync();
        var publishedCharacters = await characters.OrderByDescending(c => c.UpdatedAt ?? c.CreatedAt).Take(8)
            .Select(c => new { c.Id, c.Title, c.CoverUrl, c.Description })
            .ToListAsync();

        return Ok(new
        {
            user.Id,
            user.Username,
            JoinedAt = user.CreatedAt,
            Avatar = user.Profile?.Avatar,
            Bio = user.Profile?.Bio,
            Mood = user.Profile?.Mood,
            Level = user.Stats?.Level ?? 0,
            Title = user.Stats?.Title,
            MaxSignStreak = user.Stats?.MaxSignStreak ?? 0,
            LikesReceived = user.Stats?.LikesReceived ?? 0,
            Counts = counts,
            Artworks = recentArtworks,
            Worlds = publicWorlds,
            Characters = publishedCharacters
        });
    }

    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

        // 🌟 联查 User、Profile 和 Stats
        var user = await _context.Users
            .Include(u => u.Profile)
            .Include(u => u.Stats)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null) return NotFound("用户不存在");

        // 🌟 核心修复：直接通过 Projects 表的 OwnerId 统计当前用户作为所有者且未封存（Status != 3）的活跃项目数
        // 彻底解决 CS1061 报错，不再依赖 ProjectMember.RoleId
        var activeProjectCount = await _context.Projects
            .CountAsync(p => p.OwnerId == userId && p.Status != 3);

        return Ok(new
        {
            user.Username,
            user.Email,
            user.CreatedAt,

            // --- Profile 数据 ---
            Avatar = user.Profile?.Avatar,
            Gender = user.Profile?.Gender ?? "未知",
            Address = user.Profile?.Address,
            PhoneNumber = user.Profile?.PhoneNumber,
            Bio = user.Profile?.Bio,
            Mood = user.Profile?.Mood,
            SocialLinks = user.Profile?.SocialLinks,
            ExtraConfig = user.Profile?.ExtraConfig,

            Zodiac = user.Profile?.Zodiac,
            ChineseZodiac = user.Profile?.ChineseZodiac,
            Birthday = user.Profile?.Birthday,
            Age = user.Profile?.Age ?? 0,

            // --- Stats 数据 ---
            Level = user.Stats?.Level ?? 0,
            Experience = user.Stats?.Experience ?? 0,
            MaxSignStreak = user.Stats?.MaxSignStreak ?? 0,
            Title = user.Stats?.Title,

            // 🌟 灵脉编织额度载荷看板（无缝输送给前端）
            ProjectQuota = new
            {
                ActiveCount = activeProjectCount,                           // 已使用的活跃项目数
                MaxCount = user.Stats?.MaxProjectCount ?? 10,               // 基于 UserStats 表的上限额度（保底 10 个）
                AvailableCount = (user.Stats?.MaxProjectCount ?? 10) - activeProjectCount, // 剩余可用名额
                IsFull = activeProjectCount >= (user.Stats?.MaxProjectCount ?? 10)         // 额度是否已满
            }
        });
    }

    // 私人内容工作区只使用登录身份，不接受可被篡改的作者 ID。
    public sealed class ContentItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime UpdatedAt { get; set; }
    }

    [HttpGet("content")]
    public async Task<IActionResult> GetContent(string type = "artwork", int page = 1)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var authorId = userId.ToString();
        IQueryable<ContentItem> query;
        switch (type)
        {
            case "artwork":
                query = _context.Artworks.Where(a => a.UploaderId == userId).Select(a => new ContentItem {
                    Id = a.Id.ToString(), Title = a.Title, // 状态和作品详情页一致：自己下架的显示「已下架」，可以点进去「重新上架」
                    Status = (a.Status == "published" || a.Status == "" || a.Status == null)
                        ? (a.IsApproved ? "已发布" : "已下架")
                        : a.Status == "reviewing" ? "审核中" : a.Status == "rejected" ? "未通过" : a.Status == "hidden" ? "被管理员隐藏" : "未公开",
                    UpdatedAt = a.UploadAt });
                break;
            case "blog":
            case "post":
                query = _context.Notes.Where(n => n.AuthorId == authorId && n.Type == type).Select(n => new ContentItem {
                    Id = n.Id.ToString(), Title = n.Title, Status = n.Status == 2 ? "已下架" : n.IsPublic ? "已发布" : "草稿", UpdatedAt = n.UpdatedAt });
                break;
            case "character":
                query = _context.StickmanCharacters.Where(c => c.AuthorId == userId).Select(c => new ContentItem {
                    Id = c.Id.ToString(), Title = c.Title, Status = c.Status == "published" ? "已发布" : c.Status == "draft" ? "草稿" : "已归档", UpdatedAt = c.UpdatedAt ?? c.CreatedAt });
                break;
            case "world":
                query = _context.WorldProjects.Where(w => w.OwnerId == userId).Select(w => new ContentItem {
                    Id = w.Id.ToString(), Title = w.Name, Status = w.IsPublic ? "公开" : "私有", UpdatedAt = w.UpdatedAt });
                break;
            default:
                return BadRequest(new { message = "不支持的内容类型" });
        }
        page = Math.Clamp(page, 1, 100000);
        const int pageSize = 12;
        var total = await query.CountAsync();
        var items = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        return Ok(new { items, total, page, pageSize });
    }

    [HttpPatch("update-profile")]
    public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileDto dto)
    {
        var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

        var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);

        if (profile == null)
        {
            profile = new UserProfile { UserId = userId };
            _context.UserProfiles.Add(profile);
        }

        // 头像只能是：自己通过 /api/User/avatar 上传的静态图，或者能用的官方头像（不改就不检查）
        if (!string.IsNullOrEmpty(dto.Avatar) && dto.Avatar != profile.Avatar)
        {
            var ok = dto.Avatar.StartsWith(OwnAvatarPrefix(userId), StringComparison.Ordinal);
            if (!ok && await _shop.FindAvatarDefByUrlAsync(dto.Avatar) is { } def) ok = await _shop.CanUseAvatarAsync(userId, def);
            if (!ok) return BadRequest(new { message = "头像请在个人中心上传静态图片，或者选用官方头像" });
        }

        // --- 覆盖式更新字段 ---
        profile.Avatar = string.IsNullOrEmpty(dto.Avatar) ? profile.Avatar : dto.Avatar;
        profile.Address = dto.Address ?? profile.Address;
        profile.PhoneNumber = dto.PhoneNumber ?? profile.PhoneNumber;
        profile.Gender = dto.Gender ?? profile.Gender;
        profile.Bio = dto.Bio ?? profile.Bio;
        profile.Mood = dto.Mood ?? profile.Mood;
        profile.SocialLinks = dto.SocialLinks ?? profile.SocialLinks;
        profile.Birthday = dto.Birthday ?? profile.Birthday;
        profile.ExtraConfig = dto.ExtraConfig ?? profile.ExtraConfig;

        try
        {
            await _context.SaveChangesAsync();

            return Ok(new
            {
                message = "寰宇档案已重塑",
                data = new
                {
                    profile.Zodiac,
                    profile.ChineseZodiac,
                    profile.Age
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "数据库同步失败", detail = ex.Message });
        }
    }
}