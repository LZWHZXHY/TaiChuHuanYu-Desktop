using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Controllers.knowledgeBase;
using TaiChuWeb_V2.Controllers.Wiki;
using TaiChuWeb_V2.Services.Project;
using TaiChuWeb_V2.Services.Artworks;

static class ContentFlowChecks
{
    public static Task<int> Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseMySql("Server=localhost;Database=tests;User=test;", new MySqlServerVersion(new Version(8, 0, 31))).Options);
        foreach (var type in new[] { "blog", "post", "artwork", "world", "character", "wiki", "document", "all" }) {
            var sql = CatalogController.Query(db, type).Where(i => i.Title.Contains("test")).OrderByDescending(i => i.UpdatedAt).ThenBy(i => i.Type).ThenBy(i => i.Id).Skip(24).Take(24).ToQueryString();
            Check(sql.Contains("LIMIT"), "catalog database pagination " + type);
            if (type is "blog" or "post" or "all") {
                Check(sql.Contains("COLLATE utf8mb4_general_ci"), "catalog legacy collation compatibility " + type);
                Check(sql.Contains("IsPublic") && sql.Contains("Status"), "catalog retains source privacy " + type);
            }
        }
        // C# 14 将数组扩展 Contains 绑定到 ReadOnlySpan，EF 9 无法求值；使用 Enumerable 重载。
        var artworkIds = new[] { 1, 2 };
        var gallerySql = new ArtworkEngagementService(db).Stats(null).Where(a => Enumerable.Contains(artworkIds, a.Id)).ToQueryString();
        Check(gallerySql.Contains(" IN (1, 2)"), "gallery ID filter translates on net10");
        foreach (var tagIds in new[] { Array.Empty<string>(), new[] { "1", "2" } }) {
            var tagSql = db.TagAssignments.Where(t => t.EntityType == "Artwork" && Enumerable.Contains(tagIds, t.EntityId)).ToQueryString();
            Check(tagSql.Length > 0, "catalog tag IDs translate, including wiki-only empty IDs");
        }
        Check(WikiController.ValidContent("{\"type\":\"doc\",\"content\":[]}"), "valid wiki doc");
        foreach (var invalid in new[] { "", "<script></script>", "{}", "[]", "null", "{\"type\":1,\"content\":[]}" }) Check(!WikiController.ValidContent(invalid), "reject invalid wiki doc");
        Check(ProjectPermissionService.ReadPermissions("{").Count == 0, "broken role permissions fail closed");
        Check(ArtworkPublicationService.NormalizeTags(new[] { "风景", " 风景 ", "Art", "art", "" }).Length == 2, "artwork tag normalization");
        return Task.FromResult(checks);
    }
}
