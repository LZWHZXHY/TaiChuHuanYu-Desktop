using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using TaiChuWeb_V2.Controllers.LingMai;
using TaiChuWeb_V2.Dtos.LingMai;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services.LingMai;

try
{
    if (args.Contains("--verify-failure-exit")) throw new InvalidOperationException("Expected regression failure: verifying console-only reporting.");
var user = Guid.Parse("11111111-1111-1111-1111-111111111111");
var noteId = Guid.Parse("22222222-2222-2222-2222-222222222222");
var spaceId = Guid.Parse("33333333-3333-3333-3333-333333333333");
var targetId = Guid.Parse("44444444-4444-4444-4444-444444444444");
int checks = 0;
var trace = new SortedDictionary<string, object?>();
void Check(bool condition, string message) { checks++; if (!condition) throw new Exception(message); }
(TestContext db, LingMaiController controller) Create(string type = "note", bool login = true)
{
    var db = new TestContext();
    db.NoteRows.Rows.AddRange(new[] { new Note { Id = noteId, SpaceId = spaceId, Type = type, Title = "原始标题" }, new Note { Id = targetId, SpaceId = spaceId, Type = "note" } });
    db.SpaceRows.Rows.Add(new Space { Id = spaceId, UserId = user.ToString(), Name = "空间" });
    db.StatsRows.Rows.Add(new UserStats { UserId = user, MaxSpaces = 2, MaxNotes = 10 });
    var controller = new LingMaiController(new LingMaiService(db), db) { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
    if (login) controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.ToString()) }, "test"));
    return (db, controller);
}
object Response(IActionResult result) => result switch
{
    ObjectResult r => new { status = r.StatusCode ?? 200, body = r.Value },
    StatusCodeResult r => new { status = r.StatusCode, body = (object?)null },
    ForbidResult => new { status = 403, body = (object?)null },
    _ => throw new Exception("Unexpected result: " + result.GetType().Name)
};
async Task<IActionResult> Save(LingMaiController controller, NoteSyncDto dto) => await (Task<IActionResult>)typeof(LingMaiController).GetMethod("SaveNoteAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(controller, new object[] { dto })!;
object State(TestContext db, IActionResult response) => new
{
    response = Response(response), db.Saves, db.Commits, db.Rollbacks,
    notes = db.NoteRows.Rows.Select(n => new { n.Id, n.Title, n.Tags, n.ExtraData, n.BlocksData }),
    blocks = db.BlockRows.Rows.Select(b => new { b.Id, b.OwnerId, b.OwnerType, b.Type, b.Data, b.SortOrder }),
    links = db.NoteLinkRows.Rows.Select(l => new { l.SourceNoteId, l.TargetNoteId, l.Excerpt }),
    references = db.BlockLinkRows.Rows.Select(l => new { l.SourceBlockId, l.TargetBlockId, l.RelationType }),
    indexes = db.IndexRows.Rows.Select(b => new { b.Id, b.NodeId, b.BlockType, b.TextContent, b.Latex, b.AssetId, b.SortOrder })
};
NoteSyncDto Payload() => new() { NoteId = noteId, Title = "新标题", ExtraData = "[]", Blocks = new()
{
    new() { Id = "p", Type = "paragraph", Data = JsonSerializer.Serialize(new { type = "paragraph", content = new object[] { new { text = "正文" }, new { type = "spirit-link", attrs = new { id = targetId, blockId = "target-block" } } } }) },
    new() { Id = "asset", Type = "pdfEmbed", SortOrder = 7, Data = JsonSerializer.Serialize(new { attrs = new { assetId = "asset", latex = "x+y" }, title = "预览标题" }) }
} };
foreach (var type in new[] { "note", "blog", "wiki", "schedule", "canvas", "map" })
{
    var (db, c) = Create(type); var dto = Payload();
    var result = await Save(c, dto);
    Check(result is OkObjectResult, type + " save response");
    Check(db.Commits == 1 && db.Rollbacks == 0, type + " commit");
    Check(db.NoteLinkRows.Rows.Count == 1 && db.BlockLinkRows.Rows.Count == 1 && db.IndexRows.Rows.Count == 2, type + " projections");
    if (type is "canvas" or "map") Check(db.BlockRows.Rows.Count == 0 && db.NoteRows.Rows[0].BlocksData!.Contains("source") == false, type + " JSON storage");
    else Check(db.BlockRows.Rows.Select(b => b.SortOrder).SequenceEqual(new[] { 0, 7 }) && db.BlockRows.Rows.All(b => b.OwnerType == type), type + " order/type");
    trace[type] = State(db, result);
}
foreach (var scenario in new[] { "empty", "null", "duplicate", "missing", "forbidden" })
{
    var (db, c) = Create(); var dto = Payload();
    if (scenario == "empty") dto.Blocks.Clear();
    if (scenario == "null") dto.Blocks = null!;
    if (scenario == "duplicate") dto.Blocks.InsertRange(0, new[] { new BlockSyncDto { Id = "", Type = "paragraph", Data = "{}" }, dto.Blocks[0] });
    if (scenario == "missing") db.NoteRows.Rows.Clear();
    if (scenario == "forbidden") db.SpaceRows.Rows.Clear();
    var response = await Save(c, dto);
    Check(scenario switch { "missing" => response is NotFoundObjectResult, "forbidden" => response is ForbidResult, _ => response is OkObjectResult }, scenario + " response");
    if (scenario is "empty" or "null") Check(db.BlockRows.Rows.Count == 0, scenario + " clear");
    if (scenario == "duplicate") Check(db.BlockRows.Rows.Select(b => b.Id).SequenceEqual(new[] { "p", "asset" }), "duplicate/empty IDs skipped");
    if (scenario is "missing" or "forbidden") Check(db.Saves == 0 && db.Commits == 0, "no unauthorized write");
    trace[scenario] = State(db, response);
}
MySqlException Mysql(int number)
{
    var ctor = typeof(MySqlException).GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).First(c => c.GetParameters().Any(p => p.ParameterType == typeof(MySqlErrorCode)));
    return (MySqlException)ctor.Invoke(ctor.GetParameters().Select(p => p.ParameterType == typeof(MySqlErrorCode) ? (object)(MySqlErrorCode)number : p.ParameterType == typeof(string) ? "simulated" : p.ParameterType == typeof(int) ? number : null).ToArray());
}
foreach (var failure in new Exception[] { new DbUpdateConcurrencyException("concurrent"), new DbUpdateException("deadlock", Mysql(1213)), new DbUpdateException("duplicate", Mysql(1062)), new InvalidOperationException("failure", new Exception("inner")) })
{
    var (db, c) = Create(); db.SaveFailure = failure;
    var result = await Save(c, Payload());
    Check(db.Rollbacks == 1 && db.Commits == 0, failure.Message + " rollback");
    Check(failure is InvalidOperationException ? result is ObjectResult { StatusCode: 500 } : result is OkObjectResult, failure.Message + " HTTP contract");
    trace[failure.Message] = Response(result);
}
foreach (var scenario in new[] { "default", "at-limit", "over-limit", "below-limit" })
{
    var (db, c) = Create();
    if (scenario == "default") db.StatsRows.Rows.Clear();
    else { db.StatsRows.Rows[0].MaxSpaces = scenario == "below-limit" ? 2 : 1; db.StatsRows.Rows[0].MaxNotes = scenario == "over-limit" ? 1 : 2; }
    trace["quota-" + scenario] = Response(await c.GetQuotaUsage());
    db.Queries = 0;
    var space = await c.CreateSpace(new CreateSpaceDto { Name = "" });
    Check(scenario == "below-limit" ? space is BadRequestObjectResult : space is ObjectResult { StatusCode: 403 }, "space cap checked before name");
    Check(db.Queries == 3, "quota query count");
    trace["space-" + scenario] = Response(space);
    Console.WriteLine($"CreateSpace/{scenario}: {db.Queries} queries");
    var sync = await c.SyncNote(Payload());
    Check(scenario == "over-limit" ? sync is ObjectResult { StatusCode: 423 } : sync is OkObjectResult, "editing allows equality");
    trace["sync-" + scenario] = Response(sync);
}
{
    var (_, c) = Create(login: false);
    Check(await c.GetQuotaUsage() is UnauthorizedResult && await c.CreateSpace(new CreateSpaceDto()) is UnauthorizedResult && await c.SyncNote(Payload()) is UnauthorizedResult, "login guard");
}
object? Helper(string name, object? arg)
{
    var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
    var method = typeof(LingMaiController).GetMethod(name, flags) ?? typeof(LingMaiService).GetMethod(name, flags)!;
    return method.Invoke(null, new[] { arg });
}
for (var i = 0; i < 100; i++)
{
    var json = JsonSerializer.Serialize(new { title = "标题" + i, text = "正文" + i, content = new object[] { new { text = "子块", title = "子标题" }, new { type = "spirit-link", attrs = new { blockId = "b" + i } } } });
    using var doc = JsonDocument.Parse(json);
    Check((string?)Helper("ExtractTextFromBlockData", json) == $"正文{i} 标题{i} 子块 子标题", "preview title");
    Check((string?)Helper("ExtractBlockText", doc.RootElement) == $"正文{i} 子块", "index excludes title");
    Check(((List<string>)Helper("ExtractBlockRefTargets", json)!).SequenceEqual(new[] { "b" + i }), "reference target");
    trace["text-" + i] = new[] { Helper("ExtractTextFromBlockData", json), Helper("ExtractBlockText", doc.RootElement), Helper("ExtractBlockRefTargets", json) };
}
foreach (var json in new string?[] { null, "", "{", "null", "[]", "{}" })
{
    Check((string?)Helper("ExtractTextFromBlockData", json) == "", "preview invalid/empty");
    Check(((List<string>)Helper("ExtractBlockRefTargets", json)!).Count == 0, "references invalid/empty");
}
var legacy = LingMaiService.ReadSnapshot(noteId, """{"content":[{"type":"paragraph","attrs":{"id":"p"},"content":[{"text":"正文"}]}]}""");
Check(legacy.Blocks[0].Id == "p" && legacy.Blocks[0].SortOrder == 0, "legacy snapshot");
var modern = LingMaiService.ReadSnapshot(noteId, """{"blocks":[{"id":"item","type":"schedule-item","sortOrder":"2","data":"{\"title\":\"事件\"}"}]}""");
Check(modern.Blocks[0].SortOrder == 2 && modern.Blocks[0].Data.Contains("事件"), "raw snapshot");
trace["routes"] = typeof(LingMaiController).GetMethods().SelectMany(m => m.GetCustomAttributes<HttpMethodAttribute>().Select(a => new { m.Name, a.Template, verbs = a.HttpMethods, parameters = m.GetParameters().Select(p => p.ParameterType.FullName) })).ToArray();
checks += await PublishGraphChecks.Run(trace);
checks += await ContentFlowChecks.Run();
checks += await CommunityChecks.Run();
checks += await RecoveryChecks.Run();
checks += await JointBattleChecks.Run();
checks += await ArtworkChecks.Run();
checks += await ArtworkPublishChecks.Run();
checks += await WorldDraftChecks.Run();
if (args.Length > 0) File.WriteAllText(args[0], JsonSerializer.Serialize(trace));
Console.WriteLine($"LingMai regression: {checks} checks passed.");

}
catch (Exception error)
{
    Console.Error.WriteLine(error);
    Environment.ExitCode = 1;
}
