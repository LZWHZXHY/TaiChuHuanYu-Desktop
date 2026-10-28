using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.World;

namespace TaiChuWeb_V2.Controllers.World;

public class NovelDto
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? CoverUrl { get; set; }
    public string? Status { get; set; }
    public Guid? ProjectId { get; set; }
    public List<string>? Tags { get; set; }
    public bool? IsPublic { get; set; }
}

public class ChapterDto
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int? Number { get; set; }
}

/// <summary>世界观配套小说：书架、目录、章节读写。</summary>
[ApiController]
[Route("api/world/novels")]
public class WorldNovelController : ControllerBase
{
    private readonly AppDbContext _context;
    public WorldNovelController(AppDbContext context) { _context = context; }

    private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    private static int CountWords(string? text)
        => string.IsNullOrEmpty(text) ? 0 : text.Count(c => !char.IsWhiteSpace(c));

    private static List<string> ParseTags(string? json)
    {
        try { return JsonSerializer.Deserialize<List<string>>(json ?? "[]") ?? new(); } catch { return new(); }
    }

    // ===== 书架：公开小说 + 我写的 =====
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> List([FromQuery] Guid? projectId = null, [FromQuery] bool mine = false)
    {
        var query = _context.WorldNovels.AsQueryable();

        if (projectId.HasValue)
            query = query.Where(n => n.ProjectId == projectId.Value);

        if (mine)
        {
            if (!CurrentUserId.HasValue) return Unauthorized();
            query = query.Where(n => n.AuthorId == CurrentUserId.Value);
        }
        else
        {
            var uid = CurrentUserId;
            query = query.Where(n => n.IsPublic || (uid.HasValue && n.AuthorId == uid.Value));
        }

        var rows = await query.OrderByDescending(n => n.UpdatedAt).Take(200).ToListAsync();
        var ids = rows.Select(n => n.Id).ToList();
        var chapterCounts = await _context.WorldNovelChapters
            .Where(c => ids.Contains(c.NovelId))
            .GroupBy(c => c.NovelId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var projectIds = rows.Where(n => n.ProjectId.HasValue).Select(n => n.ProjectId!.Value).Distinct().ToList();
        var projectNames = projectIds.Count > 0
            ? await _context.WorldProjects.Where(p => projectIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name)
            : new Dictionary<Guid, string>();

        return Ok(rows.Select(n => new
        {
            n.Id, n.Title, n.Description, n.CoverUrl, n.Status, n.AuthorName, n.AuthorId,
            n.ProjectId,
            ProjectName = n.ProjectId.HasValue ? projectNames.GetValueOrDefault(n.ProjectId.Value) : null,
            n.Views, n.WordCount, n.IsPublic, n.UpdatedAt,
            Tags = ParseTags(n.Tags),
            ChapterCount = chapterCounts.GetValueOrDefault(n.Id, 0)
        }));
    }

    // ===== 详情 + 目录（不返回正文） =====
    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> Detail(Guid id)
    {
        var novel = await _context.WorldNovels.FirstOrDefaultAsync(n => n.Id == id);
        if (novel == null) return NotFound(new { message = "小说不存在" });
        if (!novel.IsPublic && novel.AuthorId != CurrentUserId) return NotFound(new { message = "小说不存在或未公开" });

        if (novel.IsPublic)
        {
            novel.Views += 1;
            await _context.SaveChangesAsync();
        }

        var chapters = await _context.WorldNovelChapters
            .Where(c => c.NovelId == id)
            .OrderBy(c => c.Number)
            .Select(c => new { c.Id, c.Number, c.Title, c.WordCount, c.UpdatedAt })
            .ToListAsync();

        var projectName = novel.ProjectId.HasValue
            ? await _context.WorldProjects.Where(p => p.Id == novel.ProjectId.Value).Select(p => p.Name).FirstOrDefaultAsync()
            : null;

        return Ok(new
        {
            novel.Id, novel.Title, novel.Description, novel.CoverUrl, novel.Status,
            novel.AuthorName, novel.AuthorId, novel.ProjectId, projectName,
            novel.Views,
            novel.WordCount, novel.IsPublic, novel.CreatedAt, novel.UpdatedAt,
            Tags = ParseTags(novel.Tags),
            IsOwner = CurrentUserId.HasValue && novel.AuthorId == CurrentUserId.Value,
            Chapters = chapters
        });
    }

    // ===== 单章正文 =====
    [HttpGet("{id:guid}/chapters/{chapterId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetChapter(Guid id, Guid chapterId)
    {
        var novel = await _context.WorldNovels.FirstOrDefaultAsync(n => n.Id == id);
        if (novel == null) return NotFound(new { message = "小说不存在" });
        if (!novel.IsPublic && novel.AuthorId != CurrentUserId) return NotFound(new { message = "小说不存在或未公开" });

        var chapter = await _context.WorldNovelChapters
            .FirstOrDefaultAsync(c => c.Id == chapterId && c.NovelId == id);
        if (chapter == null) return NotFound(new { message = "章节不存在" });

        var list = await _context.WorldNovelChapters
            .Where(c => c.NovelId == id)
            .OrderBy(c => c.Number)
            .Select(c => c.Id)
            .ToListAsync();

        var index = list.IndexOf(chapterId);
        return Ok(new
        {
            chapter.Id, chapter.Number, chapter.Title, chapter.Content, chapter.WordCount, chapter.UpdatedAt,
            PrevId = index > 0 ? list[index - 1] : (Guid?)null,
            NextId = index >= 0 && index < list.Count - 1 ? list[index + 1] : (Guid?)null,
            Index = index + 1,
            Total = list.Count
        });
    }

    // ===== 新建小说 =====
    [HttpPost]
    [Authorize]
    public async Task<IActionResult> Create([FromBody] NovelDto dto)
    {
        if (!CurrentUserId.HasValue) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest(new { message = "请填写小说标题" });

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == CurrentUserId.Value);
        var novel = new WorldNovel
        {
            AuthorId = CurrentUserId.Value,
            AuthorName = user?.Username ?? "作者",
            Title = dto.Title.Trim(),
            Description = dto.Description,
            CoverUrl = dto.CoverUrl,
            Status = string.IsNullOrWhiteSpace(dto.Status) ? "ongoing" : dto.Status,
            ProjectId = dto.ProjectId,
            Tags = JsonSerializer.Serialize(dto.Tags ?? new List<string>()),
            IsPublic = dto.IsPublic ?? false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.WorldNovels.Add(novel);
        await _context.SaveChangesAsync();
        return Ok(new { id = novel.Id });
    }

    // ===== 编辑小说 =====
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Update(Guid id, [FromBody] NovelDto dto)
    {
        if (!CurrentUserId.HasValue) return Unauthorized();
        var novel = await _context.WorldNovels.FirstOrDefaultAsync(n => n.Id == id);
        if (novel == null) return NotFound(new { message = "小说不存在" });
        if (novel.AuthorId != CurrentUserId.Value) return Forbid();

        if (!string.IsNullOrWhiteSpace(dto.Title)) novel.Title = dto.Title.Trim();
        if (dto.Description != null) novel.Description = dto.Description;
        if (dto.CoverUrl != null) novel.CoverUrl = dto.CoverUrl;
        if (!string.IsNullOrWhiteSpace(dto.Status)) novel.Status = dto.Status;
        if (dto.Tags != null) novel.Tags = JsonSerializer.Serialize(dto.Tags);
        if (dto.IsPublic.HasValue) novel.IsPublic = dto.IsPublic.Value;
        novel.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { id = novel.Id, isPublic = novel.IsPublic });
    }

    // ===== 删除小说（含章节） =====
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> Delete(Guid id)
    {
        if (!CurrentUserId.HasValue) return Unauthorized();
        var novel = await _context.WorldNovels.FirstOrDefaultAsync(n => n.Id == id);
        if (novel == null) return NotFound(new { message = "小说不存在" });
        if (novel.AuthorId != CurrentUserId.Value) return Forbid();

        var chapters = await _context.WorldNovelChapters.Where(c => c.NovelId == id).ToListAsync();
        _context.WorldNovelChapters.RemoveRange(chapters);
        _context.WorldNovels.Remove(novel);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // ===== 新增章节 =====
    [HttpPost("{id:guid}/chapters")]
    [Authorize]
    public async Task<IActionResult> AddChapter(Guid id, [FromBody] ChapterDto dto)
    {
        if (!CurrentUserId.HasValue) return Unauthorized();
        var novel = await _context.WorldNovels.FirstOrDefaultAsync(n => n.Id == id);
        if (novel == null) return NotFound(new { message = "小说不存在" });
        if (novel.AuthorId != CurrentUserId.Value) return Forbid();
        if (string.IsNullOrWhiteSpace(dto.Title)) return BadRequest(new { message = "请填写章节标题" });

        var maxNumber = await _context.WorldNovelChapters
            .Where(c => c.NovelId == id)
            .Select(c => (int?)c.Number)
            .MaxAsync() ?? 0;

        var chapter = new WorldNovelChapter
        {
            NovelId = id,
            Number = dto.Number ?? maxNumber + 1,
            Title = dto.Title.Trim(),
            Content = dto.Content ?? string.Empty,
            WordCount = CountWords(dto.Content),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _context.WorldNovelChapters.Add(chapter);

        novel.WordCount += chapter.WordCount;
        novel.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { id = chapter.Id, number = chapter.Number });
    }

    // ===== 编辑章节 =====
    [HttpPut("{id:guid}/chapters/{chapterId:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateChapter(Guid id, Guid chapterId, [FromBody] ChapterDto dto)
    {
        if (!CurrentUserId.HasValue) return Unauthorized();
        var novel = await _context.WorldNovels.FirstOrDefaultAsync(n => n.Id == id);
        if (novel == null) return NotFound(new { message = "小说不存在" });
        if (novel.AuthorId != CurrentUserId.Value) return Forbid();

        var chapter = await _context.WorldNovelChapters
            .FirstOrDefaultAsync(c => c.Id == chapterId && c.NovelId == id);
        if (chapter == null) return NotFound(new { message = "章节不存在" });

        if (!string.IsNullOrWhiteSpace(dto.Title)) chapter.Title = dto.Title.Trim();
        if (dto.Number.HasValue) chapter.Number = dto.Number.Value;
        if (dto.Content != null)
        {
            novel.WordCount = Math.Max(0, novel.WordCount - chapter.WordCount);
            chapter.Content = dto.Content;
            chapter.WordCount = CountWords(dto.Content);
            novel.WordCount += chapter.WordCount;
        }
        chapter.UpdatedAt = DateTime.UtcNow;
        novel.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();
        return Ok(new { id = chapter.Id });
    }

    // ===== 删除章节 =====
    [HttpDelete("{id:guid}/chapters/{chapterId:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteChapter(Guid id, Guid chapterId)
    {
        if (!CurrentUserId.HasValue) return Unauthorized();
        var novel = await _context.WorldNovels.FirstOrDefaultAsync(n => n.Id == id);
        if (novel == null) return NotFound(new { message = "小说不存在" });
        if (novel.AuthorId != CurrentUserId.Value) return Forbid();

        var chapter = await _context.WorldNovelChapters
            .FirstOrDefaultAsync(c => c.Id == chapterId && c.NovelId == id);
        if (chapter == null) return NotFound(new { message = "章节不存在" });

        novel.WordCount = Math.Max(0, novel.WordCount - chapter.WordCount);
        novel.UpdatedAt = DateTime.UtcNow;
        _context.WorldNovelChapters.Remove(chapter);
        await _context.SaveChangesAsync();
        return NoContent();
    }
}
