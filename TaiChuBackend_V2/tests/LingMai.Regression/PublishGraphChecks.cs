using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using TaiChuWeb_V2.Controllers.LingMai;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Services.LingMai;
using TaiChuWeb_V2.Services.Publish;

static class PublishGraphChecks
{
    public static async Task<int> Run(SortedDictionary<string, object?> trace)
    {
        int checks = 0;
        void Check(bool ok, string name) { checks++; if (!ok) throw new Exception(name); }
        var user = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var noteId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var spaceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
        var otherId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        var pubId = Guid.Parse("55555555-5555-5555-5555-555555555555");
        var date = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        ControllerContext Login(bool login) => new() { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(login ? new[] { new Claim(ClaimTypes.NameIdentifier, user.ToString()) } : Array.Empty<Claim>(), "test")) } };
        object Response(IActionResult r) => r switch
        {
            ObjectResult o => new { status = o.StatusCode ?? 200, body = o.Value },
            StatusCodeResult s => new { status = s.StatusCode, body = (object?)null },
            ForbidResult => new { status = 403, body = (object?)null },
            _ => throw new Exception(r.GetType().Name)
        };
        TestContext Create(string type)
        {
            var db = new TestContext();
            db.NoteRows.Rows.Add(new Note { Id = noteId, SpaceId = spaceId, Type = type, Title = "默认标题", Tags = "tag", ExtraData = "{}", UpdatedAt = date });
            db.SpaceRows.Rows.Add(new Space { Id = spaceId, UserId = user.ToString(), Name = "space" });
            db.UserRows.Rows.Add(new() { Id = user, Username = "author" });
            return db;
        }
        foreach (var type in new[] { "blog", "post" })
        foreach (var scenario in new[] { "new", "update", "empty", "malformed", "long", "missing", "failure", "anonymous" })
        {
            var db = Create(type);
            if (scenario == "missing") db.NoteRows.Rows.Clear();
            if (scenario == "anonymous") db.UserRows.Rows.Clear();
            if (scenario == "failure") db.SaveFailure = new InvalidOperationException("write failed");
            if (scenario == "update") db.PublishedRows.Rows.Add(new PublishedNote { Id = pubId, OriginalNoteId = noteId, SpaceId = spaceId, Type = type, AuthorName = "original", Resonance = 8 });
            db.PublishedBlockRows.Rows.Add(new PublishedBlock { Id = otherId, OwnerId = pubId.ToString(), OwnerType = "note", Data = "old" });
            if (scenario != "empty")
            {
                db.BlockRows.Rows.Add(new Block { Id = otherId.ToString(), OwnerId = noteId.ToString(), OwnerType = type, SortOrder = 2, Type = "paragraph", Data = scenario == "malformed" ? "{" : JsonSerializer.Serialize(new { content = new[] { new { text = scenario == "long" ? new string('x', 350) : "正文" } } }) });
                db.BlockRows.Rows.Add(new Block { Id = "non-guid", OwnerId = noteId.ToString(), OwnerType = type, SortOrder = 3, Type = "container", Data = """{"content":[{"type":"image","attrs":{"src":"cover.png"}}]}""" });
                db.BlockRows.Rows.Add(new Block { Id = "cover", OwnerId = noteId.ToString(), OwnerType = "note", SortOrder = 0, Type = "blog_fixed_cover", Data = """{"url":"blog.png"}""" });
                db.BlockRows.Rows.Add(new Block { Id = "unrelated", OwnerId = otherId.ToString(), OwnerType = type, Type = "paragraph", Data = "{}" });
            }
            ILingMaiPublishHandler handler = type == "blog" ? new BlogPublishHandler(db) : new PostPublishHandler(db);
            var result = await handler.ExecutePublishAsync(noteId, user.ToString(), null);
            Check(scenario switch { "missing" => result is NotFoundObjectResult, "failure" => result is BadRequestObjectResult, _ => result is OkObjectResult }, type + scenario + " response");
            Check(db.Commits == (scenario is "missing" or "failure" ? 0 : 1) && db.Rollbacks == (scenario == "failure" ? 1 : 0), type + scenario + " transaction");
            if (result is OkObjectResult)
            {
                var published = db.PublishedRows.Rows.Single();
                Check(db.NoteRows.Rows[0].IsPublic && db.Saves == 1, "draft flag and save");
                Check(published.AuthorName == (scenario == "update" ? "original" : scenario == "anonymous" ? "匿名隐士" : "author"), "author snapshot");
                var blocks = db.PublishedBlockRows.Rows.Where(b => b.OwnerId == published.Id.ToString()).ToArray();
                Check(blocks.Length == (scenario == "empty" ? 0 : type == "blog" ? 3 : 2), "block owner filters");
                Check(blocks.All(b => b.OwnerType == type && b.Id != Guid.Empty), "published block type/ids");
                if (scenario == "update") Check(published.Id == pubId && published.Resonance == 8 && !blocks.Any(b => b.Data == "old"), "stable identity and replace");
                trace[$"publish-{type}-{scenario}"] = new { published.Title, published.Type, published.AuthorName, published.Tags, published.Excerpt, published.ExtraData, published.Resonance,
                    blocks = blocks.Select(b => new { b.OwnerType, b.Type, b.Data, b.SortOrder }), db.Saves, db.Commits, db.Rollbacks };
            }
            else trace[$"publish-{type}-{scenario}"] = Response(result);
        }
        foreach (var scenario in new[] { "normal", "no-spaces", "foreign", "missing", "logout", "archived" })
        {
            var db = Create("note");
            db.NoteRows.Rows.Add(new Note { Id = otherId, SpaceId = spaceId, Title = "linked", UpdatedAt = date });
            db.NoteLinkRows.Rows.Add(new NoteLink { SourceNoteId = noteId, TargetNoteId = otherId, SourceNote = db.NoteRows.Rows[0], TargetNote = db.NoteRows.Rows[1], Excerpt = "link" });
            if (scenario == "no-spaces") db.SpaceRows.Rows.Clear();
            if (scenario == "foreign") db.SpaceRows.Rows[0].UserId = "other";
            if (scenario == "missing") db.NoteRows.Rows.RemoveAt(0);
            if (scenario == "archived") db.NoteRows.Rows[1].Status = 3;
            var ctor = typeof(LingMaiNodeGraphController).GetConstructors().Single();
            var controller = (LingMaiNodeGraphController)ctor.Invoke(ctor.GetParameters().Select(p => p.ParameterType == typeof(LingMaiService) ? (object)new LingMaiService(db) : db).ToArray());
            controller.ControllerContext = Login(scenario != "logout");
            var outgoing = await controller.GetOutlinks(noteId);
            var incoming = await controller.GetBacklinks(noteId);
            Check(scenario switch { "logout" => outgoing is UnauthorizedResult, "missing" => outgoing is NotFoundResult, "foreign" or "no-spaces" => outgoing is ForbidResult, _ => outgoing is OkObjectResult }, "graph link guard " + scenario);
            Check(outgoing.GetType() == incoming.GetType(), "same direction guards");
            trace["outlinks-" + scenario] = Response(outgoing);
            trace["backlinks-" + scenario] = Response(incoming);
            var reverse = await controller.GetBacklinks(otherId);
            trace["backlinks-other-" + scenario] = Response(reverse);
            if (scenario is "normal" or "archived")
            {
                var body = JsonSerializer.SerializeToElement(((OkObjectResult)reverse).Value);
                Check(body.GetArrayLength() == 1 && body[0].GetProperty("id").GetGuid() == noteId, "incoming source id");
                Check(body[0].GetProperty("title").GetString() == "默认标题" && body[0].GetProperty("excerpt").GetString() == "link", "incoming fields");
            }
            trace["graph-" + scenario] = Response(await controller.GetGraphData(spaceId, null));
            trace["all-graph-" + scenario] = Response(await controller.GetGraphData(otherId, "all"));
        }
        {
            var db = Create("blog");
            db.PublishedRows.Rows.AddRange(new[] { "post", "blog", "wiki", "note" }.Select((type, i) => new PublishedNote { Id = new Guid(i + 1, 0, 0, new byte[8]), Type = type, PublishedAt = date.AddDays(i) }));
            var c = new LingMaiPublishController(db, Array.Empty<ILingMaiPublishHandler>()) { ControllerContext = Login(false) };
            trace["stream-default"] = Response(await c.GetPublicStream());
            trace["stream-all"] = Response(await c.GetPublicStream(null, (string?)null));
            trace["stream-public"] = Response(await c.GetPublicStream(null, 1, 20));
            trace["stream-page"] = Response(await c.GetPublicStream(null, 2, 1));
            Check(await c.PublishNote(noteId, new() { type = "post" }) is UnauthorizedResult, "publish login");
            c.ControllerContext = Login(true);
            Check(await c.PublishNote(noteId, new() { type = "note" }) is BadRequestObjectResult, "private note");
            Check(await c.PublishNote(noteId, new() { type = "unsupported" }) is BadRequestObjectResult, "unknown type");
        }
        {
            var db = Create("note");
            db.NoteLinkRows.Rows.Add(new NoteLink { SourceNoteId = noteId, TargetNoteId = otherId });
            var ctor = typeof(LingMaiNodeGraphController).GetConstructors().Single();
            var c = (LingMaiNodeGraphController)ctor.Invoke(ctor.GetParameters().Select(p => p.ParameterType == typeof(LingMaiService) ? (object)new LingMaiService(db) : db).ToArray());
            c.ControllerContext = Login(true);
            var body = JsonSerializer.SerializeToElement(((OkObjectResult)await c.GetOutlinks(noteId)).Value)[0];
            Check(body.GetProperty("title").GetString() == "无标题碎片" && body.GetProperty("excerpt").ValueKind == JsonValueKind.Null, "missing navigation fallback");
            Check(Math.Abs((body.GetProperty("updatedAt").GetDateTime() - DateTime.UtcNow).TotalSeconds) < 10, "missing navigation timestamp");
        }
        foreach (var type in new[] { typeof(LingMaiPublishController), typeof(LingMaiNodeGraphController) })
            trace[type.Name + "-routes"] = type.GetMethods().SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>().Select(a => new { m.Name, a.Template, verbs = a.HttpMethods, parameters = m.GetParameters().Select(p => p.ParameterType.FullName) })).ToArray();
        Console.WriteLine($"Publish/graph regression: {checks} checks passed.");
        return checks;
    }
}
