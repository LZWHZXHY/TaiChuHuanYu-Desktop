using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.World;

/// <summary>
/// 小说的「部 / 卷 / 番外」结构（fix175）。
/// 不改原来的小说表和章节表：分组单独放在 world_novel_sections，章节属于哪个分组放在 world_novel_chapter_section。
/// · part 部：只能在最外层，里面可以放卷、番外，也可以直接放章；
/// · volume 卷：放在最外层或某一部里；
/// · extra 番外（作者可以改名成外传、后记、设定集……）：放在最外层或某一部里，里面的章不算「第几章」。
/// 没分组的章（包括以前写的所有章）排在最前面，和以前一样显示。分组删掉时里面的章不会删，回到「没分组」。
/// </summary>
public static class NovelStructure
{
    public const string Part = "part", Volume = "volume", Extra = "extra";
    public static bool IsKind(string? kind) => kind is Part or Volume or Extra;

    public sealed record Section(Guid Id, Guid NovelId, Guid? ParentId, string Kind, string Title, string? Note, int SortOrder);

    private static bool _ready;
    private static readonly SemaphoreSlim SchemaLock = new(1, 1);

    public static async Task<MySqlConnection> OpenAsync(AppDbContext context)
    {
        var cs = context.Database.GetConnectionString() ?? string.Empty;
        if (!_ready)
        {
            await SchemaLock.WaitAsync();
            try
            {
                if (!_ready)
                {
                    await using var c0 = new MySqlConnection(cs);
                    await c0.OpenAsync();
                    await using var cmd = c0.CreateCommand();
                    cmd.CommandText = @"CREATE TABLE IF NOT EXISTS world_novel_sections (
  Id CHAR(36) NOT NULL PRIMARY KEY,
  NovelId CHAR(36) NOT NULL,
  ParentId CHAR(36) NULL,
  Kind VARCHAR(10) NOT NULL,
  Title VARCHAR(60) NOT NULL,
  Note VARCHAR(500) NULL,
  SortOrder INT NOT NULL DEFAULT 0,
  CreatedAt DATETIME NOT NULL,
  KEY IX_wns_novel (NovelId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;
CREATE TABLE IF NOT EXISTS world_novel_chapter_section (
  ChapterId CHAR(36) NOT NULL PRIMARY KEY,
  NovelId CHAR(36) NOT NULL,
  SectionId CHAR(36) NOT NULL,
  KEY IX_wncs_novel (NovelId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                    await cmd.ExecuteNonQueryAsync();
                    _ready = true;
                }
            }
            finally { SchemaLock.Release(); }
        }
        var c = new MySqlConnection(cs);
        await c.OpenAsync();
        return c;
    }

    private static Guid G(object v) => v is Guid g ? g : Guid.Parse(Convert.ToString(v)!);

    /// <summary>读一部小说的分组和「章 → 分组」</summary>
    public static async Task<(List<Section> Sections, Dictionary<Guid, Guid> ChapterSection)> LoadAsync(AppDbContext context, Guid novelId)
    {
        var sections = new List<Section>();
        var map = new Dictionary<Guid, Guid>();
        await using var c = await OpenAsync(context);
        await using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT Id, NovelId, ParentId, Kind, Title, Note, SortOrder FROM world_novel_sections WHERE NovelId = @n ORDER BY SortOrder, CreatedAt";
            cmd.Parameters.AddWithValue("@n", novelId.ToString());
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                sections.Add(new Section(G(r.GetValue(0)), G(r.GetValue(1)), r.IsDBNull(2) ? null : G(r.GetValue(2)),
                    r.GetString(3), r.GetString(4), r.IsDBNull(5) ? null : r.GetString(5), r.GetInt32(6)));
        }
        await using (var cmd = c.CreateCommand())
        {
            cmd.CommandText = "SELECT ChapterId, SectionId FROM world_novel_chapter_section WHERE NovelId = @n";
            cmd.Parameters.AddWithValue("@n", novelId.ToString());
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) map[G(r.GetValue(0))] = G(r.GetValue(1));
        }
        return (sections, map);
    }

    /// <summary>章实际所在的分组（分组被删了的算没分组）</summary>
    public static Guid? SectionOf(Guid chapterId, Dictionary<Guid, Guid> map, List<Section> sections)
        => map.TryGetValue(chapterId, out var s) && sections.Any(x => x.Id == s) ? s : null;

    /// <summary>
    /// 阅读顺序：没分组的章 → 按顺序每个最外层分组（先放它自己的章，再放它里面的卷 / 番外）。
    /// 同一个分组里的章按原来的章节序号排。
    /// </summary>
    public static List<Guid> ReadingOrder(IEnumerable<(Guid Id, int Number)> chapters, List<Section> sections, Dictionary<Guid, Guid> map)
    {
        var list = chapters.ToList();
        var bySection = list.GroupBy(c => SectionOf(c.Id, map, sections))
            .ToDictionary(g => g.Key ?? Guid.Empty, g => g.OrderBy(c => c.Number).Select(c => c.Id).ToList());
        var order = new List<Guid>(list.Count);
        if (bySection.TryGetValue(Guid.Empty, out var loose)) order.AddRange(loose);
        var ids = sections.Select(s => s.Id).ToHashSet();
        var tops = sections.Where(s => s.ParentId == null || !ids.Contains(s.ParentId.Value)).OrderBy(s => s.SortOrder);
        foreach (var top in tops)
        {
            if (bySection.TryGetValue(top.Id, out var own)) order.AddRange(own);
            foreach (var child in sections.Where(s => s.ParentId == top.Id).OrderBy(s => s.SortOrder))
                if (bySection.TryGetValue(child.Id, out var inner)) order.AddRange(inner);
        }
        return order;
    }

    /// <summary>「第几章」：按阅读顺序给正文的章编号，番外里的章不编号（null）</summary>
    public static Dictionary<Guid, int?> DisplayNumbers(List<Guid> order, List<Section> sections, Dictionary<Guid, Guid> map)
    {
        var result = new Dictionary<Guid, int?>();
        var n = 0;
        foreach (var id in order)
        {
            var sid = SectionOf(id, map, sections);
            var kind = sid == null ? null : sections.First(s => s.Id == sid).Kind;
            result[id] = kind == Extra ? null : ++n;
        }
        return result;
    }

    /// <summary>章所在位置的名字，比如「第一部 · 第二卷」</summary>
    public static string? PathOf(Guid chapterId, List<Section> sections, Dictionary<Guid, Guid> map)
    {
        var sid = SectionOf(chapterId, map, sections);
        if (sid == null) return null;
        var s = sections.First(x => x.Id == sid);
        var parent = s.ParentId == null ? null : sections.FirstOrDefault(x => x.Id == s.ParentId);
        return parent == null ? s.Title : $"{parent.Title} · {s.Title}";
    }

    public static async Task SetChapterSectionAsync(AppDbContext context, Guid novelId, Guid chapterId, Guid? sectionId)
    {
        await using var c = await OpenAsync(context);
        await using var cmd = c.CreateCommand();
        if (sectionId == null)
        {
            cmd.CommandText = "DELETE FROM world_novel_chapter_section WHERE ChapterId = @c";
            cmd.Parameters.AddWithValue("@c", chapterId.ToString());
        }
        else
        {
            cmd.CommandText = "INSERT INTO world_novel_chapter_section (ChapterId, NovelId, SectionId) VALUES (@c, @n, @s) ON DUPLICATE KEY UPDATE SectionId = @s, NovelId = @n";
            cmd.Parameters.AddWithValue("@c", chapterId.ToString());
            cmd.Parameters.AddWithValue("@n", novelId.ToString());
            cmd.Parameters.AddWithValue("@s", sectionId.Value.ToString());
        }
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>删章时顺手删掉它的分组记录</summary>
    public static Task ForgetChapterAsync(AppDbContext context, Guid chapterId) => SetChapterSectionAsync(context, Guid.Empty, chapterId, null);

    /// <summary>删整部小说时删掉它的分组</summary>
    public static async Task ForgetNovelAsync(AppDbContext context, Guid novelId)
    {
        await using var c = await OpenAsync(context);
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "DELETE FROM world_novel_chapter_section WHERE NovelId = @n; DELETE FROM world_novel_sections WHERE NovelId = @n;";
        cmd.Parameters.AddWithValue("@n", novelId.ToString());
        await cmd.ExecuteNonQueryAsync();
    }
}
