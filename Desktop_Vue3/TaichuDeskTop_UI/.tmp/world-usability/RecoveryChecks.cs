using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using TaiChuWeb_V2.Controllers;
using TaiChuWeb_V2.Controllers.Admin;
using TaiChuWeb_V2.Controllers.knowledgeBase;
using TaiChuWeb_V2.Filters;
using TaiChuWeb_V2.Models.Artwork;
using TaiChuWeb_V2.Models.Interact;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services.Artworks;

static class RecoveryChecks
{
    public static async Task<int> Run()
    {
        int checks = 0;
        void Check(bool ok, string message) { checks++; if (!ok) throw new Exception(message); }
        var id = Guid.NewGuid(); var other = Guid.NewGuid(); var now = DateTime.UtcNow;
        var identity = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()) }, "test"));
        var db = new TestContext(); var author = new User { Id = id, Username = "author" };
        db.UserRows.Rows.Add(author);
        db.ArtworkRows.Rows.AddRange(new[] {
            new Artwork { Id = 1, Title = "legacy", IsApproved = true, Status = "", Uploader = author, UploadAt = now.AddDays(-2) },
            new Artwork { Id = 2, Title = "recent", IsApproved = true, Status = "published", Uploader = author, UploadAt = now },
            new Artwork { Id = 3, Title = "private", IsApproved = true, Status = "draft", Uploader = author },
            new Artwork { Id = 4, Title = "unapproved", IsApproved = false, Status = "", Uploader = author }
        });
        foreach (var user in new[] { id, id, other }) db.InteractionRows.Rows.Add(new UserInteraction { UserId = user, TargetId = "1", TargetType = "Artwork", ActionType = "Like", CreatedAt = now.AddDays(-2) });
        db.InteractionRows.Rows.Add(new UserInteraction { UserId = id, TargetId = "2", TargetType = "Artwork", ActionType = "Favorite", CreatedAt = now });
        var gallery = new ArtworkController(db, new ArtworkEngagementService(db)) { ControllerContext = new() { HttpContext = new DefaultHttpContext() } };
        JsonElement Data(IActionResult result) => JsonSerializer.SerializeToElement(((OkObjectResult)result).Value);
        var all = Data(await gallery.GetGallery(rankPeriod: "all"));
        Check(all.GetProperty("Total").GetInt32() == 2, "approved legacy artworks remain public; drafts and unapproved hidden");
        Check(all.GetProperty("Data")[0].GetProperty("Id").GetInt32() == 1 && all.GetProperty("Data")[0].GetProperty("RankingLikes").GetInt32() == 2, "ranking sorts distinct real likes instead of upload time");
        Check(Data(await gallery.GetGallery(rankPeriod: "day")).GetProperty("Total").GetInt32() == 0, "daily ranking excludes old likes and favorites");
        Check(Data(await gallery.GetGallery(rankPeriod: "week")).GetProperty("Total").GetInt32() == 1, "weekly ranking includes eligible old work");
        Check(await gallery.GetGallery(rankPeriod: "invalid") is BadRequestObjectResult, "reject invalid ranking periods");
        var source = Guid.NewGuid();
        db.NoteRows.Rows.Add(new Note { Id = source, IsPublic = false });
        db.PublishedRows.Rows.AddRange(new[] {
            new PublishedNote { Id = Guid.NewGuid(), OriginalNoteId = Guid.NewGuid(), Type = "blog", Title = "legacy snapshot" },
            new PublishedNote { Id = Guid.NewGuid(), OriginalNoteId = source, Type = "blog", Title = "private source" }
        });
        Check(CatalogController.Query(db, "blog").Count() == 1, "orphan publication restored without exposing private sources");
        db.NoteRows.Rows[0].IsPublic = true; db.NoteRows.Rows[0].Status = 2;
        Check(CatalogController.Query(db, "blog").Count() == 1, "banned sources remain hidden");

        async Task<IActionResult?> Authorize(string controller, string action, ClaimsPrincipal user)
        {
            var type = controller switch { "Migration" => typeof(MigrationController), "News" => typeof(NewsController), "Event" => typeof(EventController), "Feedback" => typeof(FeedbackController), _ => typeof(AdminUserController) };
            var descriptor = new ControllerActionDescriptor { ControllerName = controller, ActionName = action, ControllerTypeInfo = type.GetTypeInfo() };
            var context = new AuthorizationFilterContext(new ActionContext(new DefaultHttpContext { User = user }, new RouteData(), descriptor), new List<IFilterMetadata>());
            await new AdminAccessFilter(db).OnAuthorizationAsync(context); return context.Result;
        }
        Check(await Authorize("AdminUser", "GetUsers", new ClaimsPrincipal()) is UnauthorizedResult, "anonymous admin API denied");
        Check(await Authorize("AdminUser", "GetUsers", identity) is ForbidResult, "ordinary user admin API denied");
        db.PermissionRows.Rows.Add(new() { UserId = other, Permission = AdminPermission.SuperAdmin });
        Check(await Authorize("AdminUser", "GetUsers", identity) is ForbidResult, "another user's permission cannot grant access");
        db.PermissionRows.Rows.Add(new() { UserId = id, Permission = AdminPermission.User_Audit });
        Check(await Authorize("AdminUser", "GetUsers", identity) == null, "user auditor keeps scoped access");
        Check(await Authorize("AdminUser", "UpdatePermissions", identity) is ForbidResult, "auditor cannot grant admin rights");
        Check(await Authorize("AdminTrade", "GetOrders", identity) is ForbidResult, "auditor cannot access trade admin");
        foreach (var (controller, action) in new[] { ("Migration", "ClearMigrationData"), ("Migration", "MigrateWorldCards"), ("News", "CreateNews"), ("News", "DeleteNews"), ("Event", "GetAllEvents"), ("Event", "UpdateEvent"), ("Feedback", "GetAllFeedbacks"), ("Feedback", "DeleteFeedback") })
        {
            Check(await Authorize(controller, action, new ClaimsPrincipal()) is UnauthorizedResult, "anonymous shared admin endpoint denied: " + controller + action);
            Check(await Authorize(controller, action, identity) is ForbidResult, "auditor cannot manage shared admin endpoint: " + controller + action);
        }
        foreach (var (controller, action) in new[] { ("News", "GetAllNews"), ("Event", "GetMonthEvents"), ("Feedback", "GetPublicFeedbacks"), ("Feedback", "SubmitFeedback") })
            Check(await Authorize(controller, action, new ClaimsPrincipal()) == null, "public/user action retains its own authorization: " + controller + action);
        db.PermissionRows.Rows.Add(new() { UserId = id, Permission = AdminPermission.SuperAdmin });
        Check(await Authorize("AdminUser", "UpdatePermissions", identity) == null, "superadmin permission management retained");

        Check(await Authorize("Migration", "ClearMigrationData", identity) == null, "superadmin migration access retained without executing action");

        db.ExpRows.Rows.AddRange(new[] {
            new UserExpLog { Id = 1, UserId = id, Change = 10, Reason = "sign", CreatedAt = now.AddDays(-1) },
            new UserExpLog { Id = 2, UserId = id, Change = -5, Reason = "exchange", CreatedAt = now },
            new UserExpLog { Id = 3, UserId = other, Change = 100, Reason = "other", CreatedAt = now }
        });
        var account = new UserController(db) { ControllerContext = new() { HttpContext = new DefaultHttpContext { User = identity } } };
        var first = Data(await account.ExperienceHistory(limit: 1));
        Check(first.GetProperty("items")[0].GetProperty("Id").GetInt64() == 2 && first.GetProperty("hasMore").GetBoolean(), "experience history newest own record and pagination");
        var next = Data(await account.ExperienceHistory(offset: 1, limit: 1));
        Check(next.GetProperty("items")[0].GetProperty("Id").GetInt64() == 1 && !next.GetProperty("hasMore").GetBoolean(), "experience history excludes other accounts");
        account.ControllerContext.HttpContext.User = new ClaimsPrincipal();
        Check(await account.ExperienceHistory() is UnauthorizedResult, "anonymous experience history denied");
        return checks;
    }
}
