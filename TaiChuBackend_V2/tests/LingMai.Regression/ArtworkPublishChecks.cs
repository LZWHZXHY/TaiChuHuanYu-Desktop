using System.Security.Claims;
using TaiChuWeb_V2.Models.Interact;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Configuration;
using TaiChuWeb_V2.Controllers;
using TaiChuWeb_V2.Controllers.Admin;
using TaiChuWeb_V2.Controllers.LingMai;
using TaiChuWeb_V2.DTOs.Artwork;
using TaiChuWeb_V2.Models.Artwork;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services;
using TaiChuWeb_V2.Services.Artworks;
using TaiChuWeb_V2.Services.Cos;
using TaiChuWeb_V2.Services.LingMai;
using TaiChuWeb_V2.Services.Publish;

static class ArtworkPublishChecks
{
    public static async Task<int> Run()
    {
        int checks = 0;
        void Check(bool value, string name) { checks++; if (!value) throw new Exception(name); }
        async Task Error(Func<Task<object>> action, int status)
        {
            try { await action(); throw new Exception("Expected artwork error " + status); }
            catch (ArtworkRequestException e) { Check(e.Status == status, e.Message); }
        }
        var user = Guid.NewGuid(); var other = Guid.NewGuid(); var noteId = Guid.NewGuid(); var spaceId = Guid.NewGuid();
        var db = new TestContext(); var cos = new RecordingCos(); var watermark = new RecordingWatermark();
        var service = new ArtworkPublicationService(db, cos, watermark);
        var note = new Note { Id = noteId, SpaceId = spaceId, Type = "art", Title = "原稿", UpdatedAt = DateTime.UtcNow };
        db.NoteRows.Rows.Add(note); db.SpaceRows.Rows.Add(new Space { Id = spaceId, UserId = user.ToString() });
        db.UserRows.Rows.Add(new User { Id = user, Username = "owner" });
        FormFile File(byte[]? bytes = null) => new(new MemoryStream(bytes ?? new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }), 0, (bytes ?? new byte[8]).Length, "images", "same.png") { Headers = new HeaderDictionary(), ContentType = "image/png" };
        GalleryDirectUploadDto Upload() => new() { Title = "  新作  ", Images = new() { File(), File() }, Captions = new() { "", "第二张题注" }, CoverIndex = 1, WatermarkEnabled = false };
        var empty = Upload(); empty.Title = " "; await Error(() => service.UploadAsync(user, empty), 400);
        var fake = Upload(); fake.Images = new() { File(new byte[] { 60, 115, 99, 114, 105, 112, 116, 62 }) }; await Error(() => service.UploadAsync(user, fake), 400);
        var hijack = Upload(); hijack.OriginalNoteId = noteId; await Error(() => service.UploadAsync(other, hijack), 403);
        Check(cos.Uploads.Count == 0 && db.ArtworkRows.Rows.Count == 0, "validate before uploading or writing");
        var result = JsonSerializer.SerializeToElement(await service.UploadAsync(user, Upload()));
        var art = db.ArtworkRows.Rows.Single(); art.Uploader = db.UserRows.Rows[0];
        Check(result.GetProperty("artworkId").GetInt32() == art.Id && art.Title == "新作", "publish result and trimmed title");
        Check(art.Images.ElementAt(0).Caption == "" && art.Images.ElementAt(1).Caption == "第二张题注" && art.Images.Last().IsCover, "sparse captions and selected cover");
        Check(cos.Uploads.Distinct().Count() == 2 && !art.WatermarkEnabled, "unique media names for identical file names");
        var update = new ArtworkEditDto { Title = "更新", Description = "简介", Captions = new() { "首张", "" }, CoverIndex = 0 };
        await Error(() => service.EditAsync(art.Id, other, update), 403);
        art.LikesCount = 7; art.Status = "rejected"; art.IsApproved = false;
        await service.EditAsync(art.Id, user, update);
        Check(art.Title == "更新" && art.Images.First().IsCover && !art.Images.Last().IsCover && art.Images.Last().Caption == "", "metadata edit clears captions and moves cover");
        Check(art.LikesCount == 7 && !art.IsApproved && art.Status == "rejected" && cos.Uploads.Count == 2, "edit retains interactions, moderation and media");
        update.Captions = new(); await Error(() => service.EditAsync(art.Id, user, update), 409);
        var watermarked = Upload(); watermarked.WatermarkEnabled = true;
        await service.UploadAsync(user, watermarked);
        // 发布端目前明确禁用水印；旧客户端传 true 也不能伪报已加水印。
        Check(watermark.ByteCalls == 0 && !db.ArtworkRows.Rows.Last().WatermarkEnabled,
            "disabled watermark is reported honestly for legacy uploads");

        await Error(() => service.PublishNoteAsync(noteId, other), 403);
        await Error(() => service.PublishNoteAsync(noteId, user), 400);
        db.BlockRows.Rows.Add(new Block { Id = "meta", OwnerId = noteId.ToString(), OwnerType = "art", Type = "art-collection-meta", SortOrder = 0, Data = "{\"watermarkEnabled\":false}" });
        db.BlockRows.Rows.Add(new Block { Id = "image", OwnerId = noteId.ToString(), OwnerType = "art", Type = "image", SortOrder = 2, Data = "{\"attrs\":{\"src\":\"https://media.test/original.png\",\"caption\":\"画幅\"}}" });
        await service.PublishNoteAsync(noteId, user);
        var linked = db.ArtworkRows.Rows.Single(a => a.OriginalNoteId == noteId); linked.Uploader = db.UserRows.Rows[0];
        Check(linked.Images.Single().IsCover && note.IsPublic, "first image is cover even after a metadata block");
        var linkedId = linked.Id; linked.LikesCount = 9; linked.Status = "hidden"; linked.IsApproved = false; note.Title = "修订稿";
        await service.PublishNoteAsync(noteId, user);
        Check(linked.Id == linkedId && linked.Title == "修订稿" && linked.LikesCount == 9 && !linked.IsApproved && linked.Status == "hidden", "republish preserves identity, counters and moderation");
        await Error(() => service.EditAsync(linked.Id, user, update), 409);
        await Error(() => service.UploadAsync(user, hijack), 409);
        var controller = new LingMaiPublishController(db, Array.Empty<ILingMaiPublishHandler>(), new TaiChuWeb_V2.Services.LingMai.NoteCommentService(db));
        ControllerContext Login(Guid? id) => new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(id == null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.NameIdentifier, id.ToString()!) }, "test")) } };
        controller.ControllerContext = Login(other);
        Check(await controller.UnpublishNote(noteId) is ForbidResult, "non-owner cannot withdraw");
        Check(await controller.PublishNote(noteId, new() { type = "art" }) is ForbidResult, "non-owner cannot dispatch publication");
        controller.ControllerContext = Login(user);
        linked.Status = "published"; linked.IsApproved = true;
        Check(await controller.UnpublishNote(noteId) is OkObjectResult && !linked.IsApproved && !note.IsPublic, "withdraw both draft and gallery");
        Check(db.ArtworkRows.Rows.Any(a => a.Id == linkedId) && linked.LikesCount == 9 && linked.Images.Count == 1, "withdraw keeps media and interactions");
        await service.PublishNoteAsync(noteId, user);
        Check(linked.IsApproved && note.IsPublic && linked.Id == linkedId, "owner can republish a withdrawn public artwork");
        controller.ControllerContext = Login(null);
        Check(await controller.UnpublishNote(noteId) is UnauthorizedResult, "withdraw login guard");

        var detail = new ArtworkController(db, new ArtworkEngagementService(db)) { ControllerContext = Login(null) };
        Check((await detail.GetArtworkDetail(art.Id)).Result is NotFoundObjectResult, "anonymous cannot read rejected work");
        detail.ControllerContext = Login(other);
        Check((await detail.GetArtworkDetail(art.Id)).Result is NotFoundObjectResult, "other user cannot read rejected work");
        detail.ControllerContext = Login(user);
        Check((await detail.GetArtworkDetail(art.Id)).Result is OkObjectResult, "owner can preview rejected work");
        var admin = new AdminProductController(db) { ControllerContext = Login(other) };
        async Task<bool> Allowed()
        {
            admin.ControllerContext.RouteData = new Microsoft.AspNetCore.Routing.RouteData();
            admin.ControllerContext.ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor();
            var context = new ActionExecutingContext(admin.ControllerContext, new List<IFilterMetadata>(), new Dictionary<string, object?>(), admin);
            bool ran = false;
            await admin.OnActionExecutionAsync(context, () => { ran = true; return Task.FromResult(new ActionExecutedContext(admin.ControllerContext, new List<IFilterMetadata>(), admin)); });
            return ran;
        }
        Check(!await Allowed(), "admin endpoint rejects ordinary users");
        db.PermissionRows.Rows.Add(new UserPermission { UserId = other, Permission = AdminPermission.Trade_Manage });
        Check(await Allowed(), "existing product management permission remains valid");
        detail.ControllerContext = Login(other);
        Check((await detail.GetArtworkDetail(art.Id)).Result is OkObjectResult, "moderator may inspect non-public work");
        Check(await admin.UpdateGalleryGovernance("GAL-000001", new() { Status = "invalid" }) is BadRequestObjectResult, "moderation status validation");
        await controllerWithOwner().UnpublishNote(noteId);
        await admin.UpdateGalleryGovernance("GAL-" + linkedId, new() { Status = "published" });
        Check(!linked.IsApproved, "admin restore cannot override owner withdrawal");
        LingMaiPublishController controllerWithOwner() => new(db, Array.Empty<ILingMaiPublishHandler>(), new TaiChuWeb_V2.Services.LingMai.NoteCommentService(db)) { ControllerContext = Login(user) };
        var metadata = new LingMaiController(new LingMaiService(db), db) { ControllerContext = Login(other) };
        var change = JsonSerializer.Deserialize<JsonElement>("{\"type\":\"blog\"}");
        Check(await metadata.UpdateNoteMeta(noteId, change) is ForbidResult, "metadata cannot hijack foreign source");
        metadata.ControllerContext = Login(user); note.IsPublic = true;
        Check(await metadata.UpdateNoteMeta(noteId, change) is BadRequestObjectResult, "published source cannot change type silently");
        // 管理端使用真实、按用户去重的互动，不受旧缓存或旧客户端人工计数影响。
        foreach (var action in new[] { "Like", "Like", "Favorite" })
            db.InteractionRows.Rows.Add(new UserInteraction { UserId = user, TargetId = art.Id.ToString(), TargetType = "Artwork", ActionType = action });
        art.LikesCount = 999; art.FavoritesCount = 888;
        var listing = JsonSerializer.SerializeToElement(((OkObjectResult)await admin.GetGalleryWorks()).Value);
        var listed = listing.GetProperty("items").EnumerateArray().Single(a => a.GetProperty("id").GetString() == $"GAL-{art.Id:D6}");
        Check(listed.GetProperty("likes").GetInt32() == 1 && listed.GetProperty("favorites").GetInt32() == 1, "admin counts distinct real likes and favorites");
        Check(await admin.UpdateGalleryGovernance("GAL-" + art.Id, new() { Status = "published", Likes = -500, Views = 12, IsFeatured = true }) is OkObjectResult,
            "legacy likes payload accepted without changing real engagement");
        Check(art.LikesCount == 999 && art.ViewCount == 12 && art.IsFeatured && db.InteractionRows.Rows.Count == 3, "governance never invents or rewrites likes");
        Check(await admin.UpdateGalleryGovernance("GAL-" + art.Id, new() { Status = "published", Views = -1 }) is BadRequestObjectResult, "views validation preserved");
        Check(cos.Deletes.Count == 0, "successful publication and edits do not delete media");

        async Task Failed(Func<Task<object>> action, string message)
        {
            try { await action(); throw new Exception("expected failure: " + message); }
            catch (IOException e) { Check(e.Message == message, "cleanup preserves original exception: " + message); }
        }
        var failedDb = new TestContext(); var failedCos = new RecordingCos { FailUploadAt = 2 };
        var failedService = new ArtworkPublicationService(failedDb, failedCos, new RecordingWatermark());
        await Failed(() => failedService.UploadAsync(user, Upload()), "upload failed");
        Check(failedCos.Deletes.SequenceEqual(failedCos.Uploads) && failedCos.Deletes.Count == 2 && failedDb.Saves == 0,
            "partial upload cleans successful and ambiguous attempted keys before database save");
        failedCos = new RecordingCos(); failedService = new(failedDb, failedCos, new RecordingWatermark());
        var invalidSecond = Upload(); invalidSecond.Images[1] = File(new byte[] { 1, 2, 3 });
        await Error(() => failedService.UploadAsync(user, invalidSecond), 400);
        Check(failedCos.Deletes.SequenceEqual(failedCos.Uploads) && failedCos.Deletes.Count == 1, "invalid later image cleans prior upload");
        failedDb = new TestContext { SaveFailure = new IOException("save failed") }; failedCos = new RecordingCos { FailDelete = true };
        failedService = new(failedDb, failedCos, new RecordingWatermark());
        await Failed(() => failedService.UploadAsync(user, Upload()), "save failed");
        Check(failedCos.Deletes.Count == 2, "one cleanup failure does not prevent attempts for other keys");
        failedCos = new RecordingCos(); failedService = new(failedDb, failedCos, new RecordingWatermark());
        await Failed(() => failedService.UploadAsync(user, Upload()), "save failed");
        Check(failedCos.Deletes.SequenceEqual(failedCos.Uploads), "database save failure cleans all new images");
        failedDb = new TestContext { CommitFailure = new IOException("commit response lost") }; failedCos = new RecordingCos();
        failedService = new(failedDb, failedCos, new RecordingWatermark());
        await Failed(() => failedService.UploadAsync(user, Upload()), "commit response lost");
        Check(failedCos.Deletes.Count == 0 && failedDb.ImageRows.Rows.Count == 2, "committed image references survive ambiguous commit failure");
        failedDb = new TestContext { SaveFailure = new IOException("database unavailable") }; failedCos = new RecordingCos();
        failedDb.ImageRows.Query = () => throw new IOException("cannot verify references");
        failedService = new(failedDb, failedCos, new RecordingWatermark());
        await Failed(() => failedService.UploadAsync(user, Upload()), "database unavailable");
        Check(failedCos.Deletes.Count == 0, "unverifiable database state never deletes potentially published files");
        db.SaveFailure = new IOException("republish failed");
        var beforeCleanup = cos.Deletes.Count;
        await Failed(() => service.PublishNoteAsync(noteId, user), "republish failed");
        Check(cos.Deletes.Count == beforeCleanup, "unwatermarked original URLs are never cleanup targets");
        db.BlockRows.Rows.Single(b => b.Id == "meta").Data = "{\"watermarkEnabled\":true}";
        var beforeUploads = cos.Uploads.Count;
        await Failed(() => service.PublishNoteAsync(noteId, user), "republish failed");
        Check(cos.Uploads.Count == beforeUploads && cos.Deletes.Count == beforeCleanup,
            "disabled watermark republish neither creates derivatives nor deletes originals");
        db.SaveFailure = null;
        if (OperatingSystem.IsWindowsVersionAtLeast(6, 1))
        {
            using var original = new System.Drawing.Bitmap(240, 120);
            using (var graphics = System.Drawing.Graphics.FromImage(original)) graphics.Clear(System.Drawing.Color.White);
            using var stream = new MemoryStream(); original.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
            var bytes = await new WatermarkService(new LocalHttpFactory()).ApplyWatermarkAsync(stream.ToArray(), "TEST", null,
                "text", "center", 24, 1, "#000000", 0, .3, .6);
            using var rendered = new System.Drawing.Bitmap(new MemoryStream(bytes));
            Check(rendered.Width == 240 && rendered.Height == 120, "real watermark preserves image dimensions");
            var changed = false;
            for (var x = 0; x < rendered.Width; x++) for (var y = 0; y < rendered.Height; y++) changed |= rendered.GetPixel(x, y).ToArgb() != System.Drawing.Color.White.ToArgb();
            Check(changed, "real watermark changes image pixels");
        }
        Console.WriteLine($"Artwork publishing regression: {checks} checks passed.");
        return checks;
    }
}

sealed class RecordingCos() : CosService(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> {
    ["TencentCloud:SecretId"] = "test", ["TencentCloud:SecretKey"] = "test", ["TencentCloud:COS:Bucket"] = "test-1250000000", ["TencentCloud:COS:Region"] = "ap-shanghai", ["TencentCloud:COS:AppId"] = "1250000000"
}).Build())
{
    public readonly List<string> Uploads = new();
    public readonly List<string> Deletes = new();
    public int FailUploadAt;
    public bool FailDelete;
    public override Task<string> UploadAsync(byte[] data, string fileName, string folder)
    {
        var key = folder + "/" + fileName; Uploads.Add(key);
        return Uploads.Count == FailUploadAt ? Task.FromException<string>(new IOException("upload failed")) : Task.FromResult(ObjectUrl(key));
    }
    public override Task DeleteAsync(string key)
    { Deletes.Add(key); return FailDelete ? Task.FromException(new IOException("delete failed")) : Task.CompletedTask; }
}
sealed class RecordingWatermark() : WatermarkService(null!)
{
    public override Task<byte[]> ApplyWatermarkAsync(string url, string? text, string? image, string type, string position, int size, double opacity, string color, int rotation, double scale, double imageOpacity)
        => ApplyWatermarkAsync(new byte[8], text, image, type, position, size, opacity, color, rotation, scale, imageOpacity);
    public int ByteCalls;
    public override Task<byte[]> ApplyWatermarkAsync(byte[] bytes, string? text, string? image, string type, string position, int size, double opacity, string color, int rotation, double scale, double imageOpacity)
    { ByteCalls++; return Task.FromResult(bytes); }
}

sealed class LocalHttpFactory : IHttpClientFactory { public HttpClient CreateClient(string name) => new(); }
