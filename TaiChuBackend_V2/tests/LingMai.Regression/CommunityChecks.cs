using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Controllers.Wiki;
using TaiChuWeb_V2.Controllers.knowledgeBase;
using TaiChuWeb_V2.Controllers.Projects;
using TaiChuWeb_V2.Dtos.Wiki;
using TaiChuWeb_V2.Dtos.Project;
using TaiChuWeb_V2.Models.Wiki;
using TaiChuWeb_V2.Models.Project;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services.Project;

static class CommunityChecks
{
    public static async Task<int> Run()
    {
        int checks = 0; void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
        var user = Guid.NewGuid();
        ControllerContext Login() => new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.ToString()) }, "test")) } };
        var db = new TestContext(); db.UserRows.Rows.Add(new() { Id = user, Username = "贡献者" });
        db.WikiCategoryRows.Rows.Add(new() { Id = 1, Name = "资料" });
        db.WikiRows.Rows.Add(new() { Id = "wiki", Title = "资料", CurrentRevisionId = 2, CategoryId = 1 });
        const string content = "{\"type\":\"doc\",\"content\":[]}";
        db.RevisionRows.Rows.AddRange(new[] {
            new WikiArticleRevision { Id = 1, ArticleId = "wiki", Content = content, Status = 1, ContributorId = user.ToString() },
            new WikiArticleRevision { Id = 2, ArticleId = "wiki", Content = content, Status = 1, PreviousRevisionId = 1, ContributorId = user.ToString() },
            new WikiArticleRevision { Id = 3, ArticleId = "wiki", Content = "private pending", Status = 0, PreviousRevisionId = 2, ContributorId = user.ToString() },
            new WikiArticleRevision { Id = 4, ArticleId = "another", Content = "other article", Status = 1 }
        });
        var wiki = new WikiController(db) { ControllerContext = Login() };
        var history = JsonSerializer.Serialize(((OkObjectResult)await wiki.History("wiki")).Value);
        Check(history.Contains("贡献者", StringComparison.Ordinal) || history.Contains("\\u8D21"), "history resolves contributor names");
        Check(!history.Contains("private pending") && !history.Contains("other article"), "public history excludes pending/other articles");
        Check(await wiki.Revision("wiki", 3) is NotFoundResult, "pending revision is not public");
        Check(await wiki.Revision("wiki", 4) is NotFoundResult, "revision must belong to article");
        var diff = JsonSerializer.Serialize(((OkObjectResult)await wiki.Revision("wiki", 2)).Value);
        Check(diff.Contains("previousContent"), "comparison includes original snapshot");
        var stale = await wiki.UpdateFromNote(new WikiUpdateDto { ArticleId = "wiki", BaseRevisionId = 1, Content = content });
        Check(stale is ConflictObjectResult && db.Saves == 0, "stale edit does not insert revision");
        var update = await wiki.UpdateFromNote(new WikiUpdateDto { ArticleId = "wiki", BaseRevisionId = 2, Content = content, Summary = "修正段落" });
        Check(update is OkObjectResult && db.RevisionRows.Rows.Last().PreviousRevisionId == 2 && db.RevisionRows.Rows.Last().Status == 0, "editing records base and waits for review");
        var review = new WikiReviewController(db) { ControllerContext = Login() };
        Check(await review.HandleRevision(3, new ReviewRequestDto { Approved = true }) is ForbidResult, "ordinary user cannot approve");
        Check(await review.CleanupDeletedArticles() is ForbidResult, "ordinary user cannot clean history");
        Check(await review.ToggleArchive("wiki") is ForbidResult, "ordinary user cannot archive");
        db.PermissionRows.Rows.Add(new() { UserId = user, Permission = AdminPermission.Wiki_Editor });
        Check(await review.Rollback("wiki", 4) is NotFoundResult, "cannot roll another article into this one");
        Check(await review.HandleRevision(3, new ReviewRequestDto { Approved = true }) is OkObjectResult, "moderator can approve current proposal");
        Check(db.WikiRows.Rows[0].CurrentRevisionId == 3, "approval advances current version");
        Check(await review.HandleRevision(3, new ReviewRequestDto { Approved = true }) is ConflictObjectResult, "cannot approve twice");
        Check(await review.HandleRevision(db.RevisionRows.Rows.Last().Id, new ReviewRequestDto { Approved = true }) is ConflictObjectResult, "another proposal on the previous base conflicts");
        db.WikiRows.Rows[0].IsDeleted = true;
        Check(await wiki.GetArticleDetail("wiki") is NotFoundResult && await wiki.History("wiki") is NotFoundResult, "archived wiki unavailable through detail/history");

        var visible = new TestContext();
        var source = Guid.NewGuid(); visible.NoteRows.Rows.Add(new Note { Id = source, IsPublic = false });
        visible.PublishedRows.Rows.Add(new PublishedNote { Id = Guid.NewGuid(), OriginalNoteId = source, Type = "blog", Title = "private" });
        visible.PublishedRows.Rows.Add(new PublishedNote { Id = Guid.NewGuid(), Type = "blog", Title = "legacy public" });
        visible.WikiRows.Rows.Add(new WikiArticle { Id = "hidden", CurrentRevisionId = 1, IsDeleted = true });
        visible.WikiRows.Rows.Add(new WikiArticle { Id = "pending" });
        Check(CatalogController.Query(visible, "all").Count() == 1, "catalog hides private sources, archived and pending wiki");
        visible.NoteRows.Rows[0].IsPublic = true; Check(CatalogController.Query(visible, "all").Count() == 2, "published source enters catalog");
        visible.NoteRows.Rows[0].Status = 2; Check(CatalogController.Query(visible, "all").Count() == 1, "removed source leaves catalog");
        visible.PublishedRows.Rows[1].Excerpt = JsonSerializer.Serialize(new { text = "风景练习", coverUrl = "https://example.invalid/cover.png" });
        var catalog = new CatalogController(visible);
        var found = JsonSerializer.Serialize(((OkObjectResult)await catalog.Get("blog", "风景")).Value);
        Check(found.Contains("\"total\":1") && found.Contains("cover.png"), "catalog searches escaped legacy summary and restores cover");
        visible.WikiCategoryRows.Rows.Add(new() { Id = 2, ParentId = 1, Name = "子分类" });
        visible.WikiRows.Rows.Add(new() { Id = "child", Title = "子分类词条", CategoryId = 2, CurrentRevisionId = 1 });
        var children = JsonSerializer.Serialize(((OkObjectResult)await catalog.Get("wiki", categoryId: 1)).Value);
        Check(children.Contains("\"total\":1") && children.Contains("child"), "parent category includes existing child articles");

        var projects = new TestContext();
        projects.ProjectRows.Rows.Add(new Project { Id = "p", OwnerId = Guid.NewGuid(), Name = "项目" });
        projects.MemberRows.Rows.Add(new ProjectMember { ProjectId = "p", UserId = user.ToString(), RoleIds = new() { "foreign", "system" } });
        projects.RoleRows.Rows.Add(new ProjectCustomRole { Id = "foreign", ProjectId = "q", Name = "外部角色", PermissionsJson = "[\"task:manage\"]" });
        projects.RoleRows.Rows.Add(new ProjectCustomRole { Id = "system", ProjectId = null, IsSystemDefault = true, Name = "系统模板", PermissionsJson = "[\"task:move_self\"]" });
        var permission = new ProjectPermissionService(projects);
        Check(!await permission.HasPermissionAsync("p", user.ToString(), "task:manage"), "foreign project role cannot grant access");
        Check(await permission.HasPermissionAsync("p", user.ToString(), "task:move_self"), "system role templates remain supported");
        projects.RoleRows.Rows[1].PermissionsJson = "{";
        Check((await permission.GetUserPermissionsAsync("p", user.ToString())).Count == 0, "malformed role does not crash permissions");
        projects.ProjectRows.Rows[0].OwnerId = user;
        projects.CategoryRows.Rows.Add(new ProjectCategory { Id = "foreign-category", ProjectId = "q", Name = "其他项目" });
        var kanban = new ProjectKanbanController(projects, permission) { ControllerContext = Login() };
        Check(await kanban.CreateTask("p", new CreateTaskDto { Title = "测试", CategoryId = "foreign-category" }) is BadRequestObjectResult, "cannot create task in foreign project category");
        Check(projects.TaskRows.Rows.Count == 0, "invalid category does not write task");
        var delivery = new TaiChuWeb_V2.Controllers.Project.ProjectDeliveryController(projects, permission) { ControllerContext = Login() };
        var outsider = Guid.NewGuid();
        projects.UserRows.Rows.Add(new User { Id = outsider, Username = "外部协作者" });
        var dto = new TaiChuWeb_V2.Controllers.Project.DeliveryDto { MemberId = outsider, TaskName = "交付", RecordedName = "伪造名字", TimingStatus = "按时", QualityStatus = "达标", CommStatus = "正常" };
        Check(await delivery.CreateDelivery("p", dto) is OkObjectResult, "manager can record external contributor");
        Check(projects.DeliveryRows.Rows.Single().RecordedName == "外部协作者", "delivery records actual user name");
        projects.DeliveryRows.Rows[0].AdminNote = "manager-only-note";
        Check(JsonSerializer.Serialize(((OkObjectResult)await delivery.GetDeliveries("p")).Value).Contains("manager-only-note"), "manager sees internal delivery note");
        projects.ProjectRows.Rows[0].OwnerId = Guid.NewGuid();
        Check(!JsonSerializer.Serialize(((OkObjectResult)await delivery.GetDeliveries("p")).Value).Contains("manager-only-note"), "ordinary member cannot read management note");
        Check(await delivery.CreateDelivery("p", dto) is ForbidResult, "ordinary member cannot create delivery");
        Check(await delivery.DeleteDelivery("p", projects.DeliveryRows.Rows[0].Id) is ForbidResult, "ordinary member cannot delete delivery");
        projects.MemberRows.Rows.Clear();
        Check(await delivery.GetDeliveries("p") is ForbidResult, "outside user cannot read internal delivery records");
        var topics = new TestContext();
        topics.PublishedRows.Rows.AddRange(new[] {
            new PublishedNote { Id = Guid.NewGuid(), Type = "post", Title = "literal", Tags = "[\"我的世界\"]", PublishedAt = DateTime.UtcNow.AddMinutes(-1) },
            new PublishedNote { Id = Guid.NewGuid(), Type = "post", Title = "escaped", Tags = JsonSerializer.Serialize(new[] { "我的世界" }), PublishedAt = DateTime.UtcNow.AddMinutes(-2) },
            new PublishedNote { Id = Guid.NewGuid(), Type = "post", Title = "different", Tags = "[\"我的世界观\"]", PublishedAt = DateTime.UtcNow },
            new PublishedNote { Id = Guid.NewGuid(), Type = "blog", Title = "blog", Tags = "我的世界" }
        });
        var stream = new TaiChuWeb_V2.Controllers.LingMai.LingMaiPublishController(topics, Array.Empty<TaiChuWeb_V2.Services.Publish.ILingMaiPublishHandler>(), new TaiChuWeb_V2.Services.LingMai.NoteCommentService(topics));
        JsonElement Rows(IActionResult result) => JsonSerializer.SerializeToElement(((OkObjectResult)result).Value);
        var tagged = Rows(await stream.GetPublicStream("post", tag: " 我的世界 "));
        Check(tagged.GetArrayLength() == 2, "topic matches escaped and literal JSON but not longer tags");
        var secondPage = Rows(await stream.GetPublicStream("post", page: 2, pageSize: 1, tag: "我的世界"));
        Check(secondPage[0].GetProperty("Title").GetString() == "escaped", "topic filter applies before pagination");
        Check(Rows(await stream.GetPublicStream(null, tag: "我的世界")).GetArrayLength() == 3, "topic includes existing blogs and legacy single tags");
        Check(Rows(await stream.GetPublicStream(null, page: 1)).GetArrayLength() == 4, "omitting topic retains original stream");
        Check(await stream.GetPublicStream(null, tag: new string('a', 51)) is BadRequestObjectResult, "overlong topic is rejected");
        var privateSource = Guid.NewGuid();
        topics.NoteRows.Rows.Add(new Note { Id = privateSource, IsPublic = false });
        topics.PublishedRows.Rows[0].OriginalNoteId = privateSource;
        Check(Rows(await stream.GetPublicStream("post", tag: "我的世界")).GetArrayLength() == 1, "topic cannot expose withdrawn content");
        return checks;
    }
}
