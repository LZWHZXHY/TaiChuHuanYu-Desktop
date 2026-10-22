using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.Controllers;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Artwork;
using TaiChuWeb_V2.Models.Artwork;
using TaiChuWeb_V2.Models.Interact;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services.Artworks;

static class ArtworkChecks
{
    public static async Task<int> Run()
    {
        int checks = 0;
        void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
        var user = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var other = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var db = new TestContext();
        var author = new User { Id = user, Username = "author" };
        db.UserRows.Rows.Add(author);
        db.ArtworkRows.Rows.Add(new Artwork { Id = 1, Title = "art", UploaderId = user, Uploader = author, LikesCount = 99 });
        db.ArtworkRows.Rows.Add(new Artwork { Id = 2, Title = "private", UploaderId = user, Uploader = author, IsApproved = false });
        var service = new ArtworkEngagementService(db);
        ArtworkStatsResult Stats() => service.Stats(user).Single(a => a.Id == 1);
        async Task Error(Func<Task<object>> call, int status)
        {
            try { await call(); throw new Exception("Expected error " + status); }
            catch (ArtworkRequestException e) { Check(e.Status == status, "error status"); }
        }
        Check(Stats().LikesCount == 0, "ignore polluted legacy counter");
        await service.SetInteractionAsync(1, user, "Favorite", true);
        Check(Stats().LikesCount == 0 && Stats().FavoritesCount == 1 && Stats().IsFavorited, "favorite separate from like");
        Check(db.ArtworkRows.Rows[0].LikesCount == 0 && db.ArtworkRows.Rows[0].FavoritesCount == 1, "repair stored counters");
        await service.SetInteractionAsync(1, user, "Like", true);
        await service.SetInteractionAsync(1, user, "Like", true);
        Check(Stats().LikesCount == 1 && Stats().IsLiked, "desired state idempotent");
        await service.SetInteractionAsync(1, other, "Like", true);
        Check(Stats().LikesCount == 2, "independent users");
        db.InteractionRows.Rows.Add(new UserInteraction { UserId = user, TargetId = "1", TargetType = "Artwork", ActionType = "Like" });
        Check(Stats().LikesCount == 2, "duplicate historical records counted once");
        await service.SetInteractionAsync(1, user, "Like", false);
        await service.SetInteractionAsync(1, user, "Like", false);
        Check(Stats().LikesCount == 1 && !Stats().IsLiked && Stats().IsFavorited, "unlike removes duplicates without touching favorite");
        await service.SetInteractionAsync(1, user, "Favorite", null);
        Check(!Stats().IsFavorited && Stats().FavoritesCount == 0, "legacy toggle remains supported");
        await Error(() => service.SetInteractionAsync(1, user, "Report", true), 400);
        await Error(() => service.SetInteractionAsync(2, user, "Like", true), 404);
        await Error(() => service.SetInteractionAsync(99, user, "Like", true), 404);
        Check(!service.Stats(null).Single(a => a.Id == 1).IsLiked, "anonymous state");

        await Error(() => service.AddCommentAsync(1, user, new() { Content = "  " }), 400);
        await Error(() => service.AddCommentAsync(1, user, new() { Content = new string('a', 1001) }), 400);
        await Error(() => service.AddCommentAsync(1, user, new() { Content = "reply", ParentId = Guid.NewGuid() }), 400);
        await service.AddCommentAsync(1, user, new() { Content = "  <script>plain text</script>  " });
        var parent = db.CommentRows.Rows.Single();
        Check(parent.Content == "<script>plain text</script>" && parent.ArtworkId == 1 && parent.NoteId == null, "trim and preserve plain text");
        Check(Stats().CommentsCount == 1 && db.ArtworkRows.Rows[0].CommentsCount == 1, "comment counter");
        await Error(() => service.AddCommentAsync(2, user, new() { Content = "hidden" }), 404);
        db.ArtworkRows.Rows[1].IsApproved = true;
        await Error(() => service.AddCommentAsync(2, user, new() { Content = "cross artwork", ParentId = parent.Id }), 400);
        await service.AddCommentAsync(1, other, new() { Content = "reply", ParentId = parent.Id });
        Check(service.Comments(1, user).Single(c => c.ParentId != null).ReplyToAuthor == "author", "reply parent attribution");
        await Error(() => service.DeleteCommentAsync(1, parent.Id, other), 403);
        await Error(() => service.DeleteCommentAsync(2, parent.Id, user), 404);
        await service.DeleteCommentAsync(1, parent.Id, user);
        await service.DeleteCommentAsync(1, parent.Id, user);
        Check(db.CommentRows.Rows.Count == 2 && Stats().CommentsCount == 1, "soft delete preserves replies and is idempotent");
        Check(service.Comments(1, user).Single(c => c.Id == parent.Id).IsDeleted && !service.Comments(1, user).Single(c => c.Id == parent.Id).CanDelete, "deleted item state");
        Check(service.Comments(1, user).Single(c => c.ParentId != null).ReplyToAuthor == "已删除评论", "deleted parent attribution");
        await Error(() => service.AddCommentAsync(1, user, new() { Content = "reply", ParentId = parent.Id }), 400);
        var page = JsonSerializer.SerializeToElement(await service.GetCommentsAsync(1, user, 0, 1));
        Check(page.GetProperty("items").GetArrayLength() == 1 && page.GetProperty("hasMore").GetBoolean(), "bounded pagination includes tombstones");
        var next = JsonSerializer.SerializeToElement(await service.GetCommentsAsync(1, user, 1, 1));
        Check(!next.GetProperty("hasMore").GetBoolean(), "last page");
        Check(service.Comments(1, null).All(c => !c.CanDelete), "anonymous cannot delete");

        ControllerContext Login(string? id) => new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(id == null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.NameIdentifier, id) }, "test")) } };
        var controller = new InteractionController(service) { ControllerContext = Login(null) };
        Check(await controller.ToggleAction("1", "Artwork", "Like") is UnauthorizedResult, "login guard");
        controller.ControllerContext = Login("invalid");
        Check(await controller.ToggleAction("1", "Artwork", "Like") is UnauthorizedResult, "malformed identity");
        controller.ControllerContext = Login(user.ToString());
        Check(await controller.ToggleAction("bad", "Artwork", "Like") is BadRequestObjectResult, "invalid target");
        Check(await controller.ToggleAction("1", "Blog", "Like") is BadRequestObjectResult, "no phantom unsupported interactions");
        var artwork = new ArtworkController(db, service) { ControllerContext = Login(user.ToString()) };
        var detail = (ArtworkDetailDto)((OkObjectResult)(await artwork.GetArtworkDetail(1)).Result!).Value!;
        Check(detail.LikesCount == Stats().LikesCount && detail.CommentsCount == 1 && detail.IsFavorited == Stats().IsFavorited, "detail reads real state");
        db.ArtworkRows.Rows[1].IsApproved = false;
        var preview = (ArtworkDetailDto)((OkObjectResult)(await artwork.GetArtworkDetail(2)).Result!).Value!;
        Check(detail.CanInteract && !preview.CanInteract, "unpublished preview cannot interact");
        var list = JsonSerializer.SerializeToElement(((OkObjectResult)await artwork.GetGallery()).Value);
        Check(list.GetProperty("Data").EnumerateArray().Single(a => a.GetProperty("Id").GetInt32() == 1).GetProperty("LikesCount").GetInt32() == Stats().LikesCount, "gallery/detail counts agree");
        artwork.ControllerContext = Login(null);
        Check(await artwork.AddComment(1, new() { Content = "a" }) is UnauthorizedResult && await artwork.DeleteComment(1, parent.Id) is UnauthorizedResult, "comment write login guards");

        using var sql = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseMySql("Server=localhost;Database=tests;User=test;", new MySqlServerVersion(new Version(8, 0, 31))).Options);
        var queries = new ArtworkEngagementService(sql);
        var statsSql = queries.Stats(user).Where(a => a.Id == 1).ToQueryString();
        Check(statsSql.Contains("DISTINCT") && statsSql.Contains("COUNT("), "MySQL stats query translation");
        Check(queries.Comments(1, user).OrderByDescending(c => c.CreatedAt).Take(20).ToQueryString().Contains("LEFT JOIN"), "MySQL author/reply query translation");
        Console.WriteLine($"Artwork regression: {checks} checks passed.");
        return checks;
    }
}
