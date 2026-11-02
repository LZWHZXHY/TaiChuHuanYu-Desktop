using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World;

/// <summary>小说的部 / 卷 / 番外：新建、改名、排序、删除，把章放进某个分组、在分组里上下移（fix175，只有作者能改）</summary>
[ApiController]
[Route("api/world/novels/{novelId:guid}")]
[Authorize]
public class WorldNovelStructureController : ControllerBase
{
    private readonly AppDbContext _context;
    public WorldNovelStructureController(AppDbContext context) { _context = context; }

    public sealed class SectionDto
    {
        public string? Kind { get; set; }
        public string? Title { get; set; }
        public string? Note { get; set; }
        public Guid? ParentId { get; set; }
    }
    public sealed class MoveDto { public int Direction { get; set; } }
    public sealed class ChapterSectionDto { public Guid? SectionId { get; set; } }

    private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    private async Task<IActionResult?> CheckOwnerAsync(Guid novelId)
    {
        var authorId = await _context.WorldNovels.AsNoTracking().Where(n => n.Id == novelId).Select(n => (Guid?)n.AuthorId).FirstOrDefaultAsync();
        if (authorId == null) return NotFound(new { message = "小说不存在" });
        if (authorId != Me) return StatusCode(403, new { message = "只有作者能改目录" });
        return null;
    }

    private static string? CleanTitle(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim().Length > 60 ? t.Trim()[..60] : t.Trim();
    private static string? CleanNote(string? t) => string.IsNullOrWhiteSpace(t) ? null : t.Trim().Length > 500 ? t.Trim()[..500] : t.Trim();

    /// <summary>新建部 / 卷 / 番外</summary>
    [HttpPost("sections")]
    public async Task<IActionResult> Create(Guid novelId, [FromBody] SectionDto dto)
    {
        if (await CheckOwnerAsync(novelId) is { } denied) return denied;
        if (!NovelStructure.IsKind(dto.Kind)) return BadRequest(new { message = "分组类型不对" });
        var title = CleanTitle(dto.Title);
        if (title == null) return BadRequest(new { message = "请填写名字" });

        var (sections, _) = await NovelStructure.LoadAsync(_context, novelId);
        if (sections.Count >= 300) return BadRequest(new { message = "分组太多了" });
        if (dto.ParentId != null)
        {
            if (dto.Kind == NovelStructure.Part) return BadRequest(new { message = "「部」只能放在最外层" });
            var parent = sections.FirstOrDefault(s => s.Id == dto.ParentId);
            if (parent == null || parent.Kind != NovelStructure.Part) return BadRequest(new { message = "卷和番外只能放进「部」里" });
        }
        var order = sections.Where(s => s.ParentId == dto.ParentId).Select(s => s.SortOrder).DefaultIfEmpty(0).Max() + 1;
        var id = Guid.NewGuid();
        await using var c = await NovelStructure.OpenAsync(_context);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "INSERT INTO world_novel_sections (Id, NovelId, ParentId, Kind, Title, Note, SortOrder, CreatedAt) VALUES (@id, @n, @p, @k, @t, @note, @o, @at)";
        cmd.Parameters.AddWithValue("@id", id.ToString());
        cmd.Parameters.AddWithValue("@n", novelId.ToString());
        cmd.Parameters.AddWithValue("@p", dto.ParentId.HasValue ? (object)dto.ParentId.Value.ToString() : DBNull.Value);
        cmd.Parameters.AddWithValue("@k", dto.Kind);
        cmd.Parameters.AddWithValue("@t", title);
        cmd.Parameters.AddWithValue("@note", (object?)CleanNote(dto.Note) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@o", order);
        cmd.Parameters.AddWithValue("@at", DateTime.UtcNow);
        await cmd.ExecuteNonQueryAsync();
        await TouchAsync(novelId);
        return Ok(new { id });
    }

    /// <summary>改名字、简介</summary>
    [HttpPut("sections/{sectionId:guid}")]
    public async Task<IActionResult> Update(Guid novelId, Guid sectionId, [FromBody] SectionDto dto)
    {
        if (await CheckOwnerAsync(novelId) is { } denied) return denied;
        var title = CleanTitle(dto.Title);
        if (title == null) return BadRequest(new { message = "请填写名字" });
        await using var c = await NovelStructure.OpenAsync(_context);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "UPDATE world_novel_sections SET Title = @t, Note = @note WHERE Id = @id AND NovelId = @n";
        cmd.Parameters.AddWithValue("@t", title);
        cmd.Parameters.AddWithValue("@note", (object?)CleanNote(dto.Note) ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@id", sectionId.ToString());
        cmd.Parameters.AddWithValue("@n", novelId.ToString());
        if (await cmd.ExecuteNonQueryAsync() == 0) return NotFound(new { message = "分组不存在" });
        return Ok(new { success = true });
    }

    /// <summary>和前一个 / 后一个同级分组换位置（direction：-1 上移，1 下移）</summary>
    [HttpPost("sections/{sectionId:guid}/move")]
    public async Task<IActionResult> Move(Guid novelId, Guid sectionId, [FromBody] MoveDto dto)
    {
        if (await CheckOwnerAsync(novelId) is { } denied) return denied;
        var (sections, _) = await NovelStructure.LoadAsync(_context, novelId);
        var me = sections.FirstOrDefault(s => s.Id == sectionId);
        if (me == null) return NotFound(new { message = "分组不存在" });
        var siblings = sections.Where(s => s.ParentId == me.ParentId).OrderBy(s => s.SortOrder).ToList();
        var i = siblings.IndexOf(me);
        var j = i + Math.Sign(dto.Direction);
        if (j < 0 || j >= siblings.Count) return Ok(new { success = true });
        // 重新按 1、2、3… 编号再交换，避免以前编号相同换不动
        for (var k = 0; k < siblings.Count; k++) siblings[k] = siblings[k] with { SortOrder = k + 1 };
        (siblings[i], siblings[j]) = (siblings[j], siblings[i]);
        await using var c = await NovelStructure.OpenAsync(_context);
        for (var k = 0; k < siblings.Count; k++)
        {
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "UPDATE world_novel_sections SET SortOrder = @o WHERE Id = @id";
            cmd.Parameters.AddWithValue("@o", k + 1);
            cmd.Parameters.AddWithValue("@id", siblings[k].Id.ToString());
            await cmd.ExecuteNonQueryAsync();
        }
        return Ok(new { success = true });
    }

    /// <summary>删分组：里面的章不删，回到「没分组」；部里面还有卷 / 番外时不让删</summary>
    [HttpDelete("sections/{sectionId:guid}")]
    public async Task<IActionResult> Delete(Guid novelId, Guid sectionId)
    {
        if (await CheckOwnerAsync(novelId) is { } denied) return denied;
        var (sections, _) = await NovelStructure.LoadAsync(_context, novelId);
        if (sections.All(s => s.Id != sectionId)) return NotFound(new { message = "分组不存在" });
        if (sections.Any(s => s.ParentId == sectionId)) return BadRequest(new { message = "这一部里面还有卷或番外，先把它们删掉或移走" });
        await using var c = await NovelStructure.OpenAsync(_context);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM world_novel_chapter_section WHERE SectionId = @id; DELETE FROM world_novel_sections WHERE Id = @id AND NovelId = @n;";
        cmd.Parameters.AddWithValue("@id", sectionId.ToString());
        cmd.Parameters.AddWithValue("@n", novelId.ToString());
        await cmd.ExecuteNonQueryAsync();
        await TouchAsync(novelId);
        return NoContent();
    }

    /// <summary>把一章放进某个分组（sectionId 为空 = 拿出来，不分组）</summary>
    [HttpPut("chapters/{chapterId:guid}/section")]
    public async Task<IActionResult> SetSection(Guid novelId, Guid chapterId, [FromBody] ChapterSectionDto dto)
    {
        if (await CheckOwnerAsync(novelId) is { } denied) return denied;
        if (!await _context.WorldNovelChapters.AnyAsync(c => c.Id == chapterId && c.NovelId == novelId)) return NotFound(new { message = "章节不存在" });
        if (dto.SectionId != null)
        {
            var (sections, _) = await NovelStructure.LoadAsync(_context, novelId);
            if (sections.All(s => s.Id != dto.SectionId)) return BadRequest(new { message = "分组不存在" });
        }
        await NovelStructure.SetChapterSectionAsync(_context, novelId, chapterId, dto.SectionId);
        await TouchAsync(novelId);
        return Ok(new { success = true });
    }

    /// <summary>在同一个分组里上移 / 下移一章（和相邻那章交换序号）</summary>
    [HttpPost("chapters/{chapterId:guid}/move")]
    public async Task<IActionResult> MoveChapter(Guid novelId, Guid chapterId, [FromBody] MoveDto dto)
    {
        if (await CheckOwnerAsync(novelId) is { } denied) return denied;
        var (sections, map) = await NovelStructure.LoadAsync(_context, novelId);
        var chapters = await _context.WorldNovelChapters.Where(c => c.NovelId == novelId).ToListAsync();
        var me = chapters.FirstOrDefault(c => c.Id == chapterId);
        if (me == null) return NotFound(new { message = "章节不存在" });
        var group = NovelStructure.SectionOf(chapterId, map, sections);
        var same = chapters.Where(c => NovelStructure.SectionOf(c.Id, map, sections) == group).OrderBy(c => c.Number).ThenBy(c => c.CreatedAt).ToList();
        var i = same.IndexOf(me);
        var j = i + Math.Sign(dto.Direction);
        if (j < 0 || j >= same.Count) return Ok(new { success = true });
        var other = same[j];
        if (other.Number == me.Number)
        {
            // 以前序号重复的：整本先重新编号（按现在的顺序），再交换
            var ordered = chapters.OrderBy(c => c.Number).ThenBy(c => c.CreatedAt).ToList();
            for (var k = 0; k < ordered.Count; k++) ordered[k].Number = k + 1;
        }
        (me.Number, other.Number) = (other.Number, me.Number);
        await _context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    private async Task TouchAsync(Guid novelId)
        => await _context.WorldNovels.Where(n => n.Id == novelId).ExecuteUpdateAsync(s => s.SetProperty(n => n.UpdatedAt, DateTime.UtcNow));
}
