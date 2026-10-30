using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TaiChuWeb_V2.Controllers.World;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.World;
using TaiChuWeb_V2.Services.World;

static class WorldDraftChecks
{
    public static async Task<int> Run()
    {
        int checks = 0; void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
        var owner = Guid.NewGuid(); var pid = Guid.NewGuid(); var id = Guid.NewGuid();
        var db = new TestContext();
        var card = new WorldCard { Id = id, ProjectId = pid, Title = "server", Type = "character",
            UpdatedAt = new DateTime(2026, 10, 25, 12, 0, 0, DateTimeKind.Utc),
            Project = new WorldProject { Id = pid, OwnerId = owner } };
        db.WorldCardRows.Rows.Add(card);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new WorldCardService(db, null!, cache, null!);
        Check(db.Model.FindEntityType(typeof(WorldCard))!.FindProperty(nameof(WorldCard.UpdatedAt))!.IsConcurrencyToken,
            "EF update must include old UpdatedAt in its WHERE clause");
        var oldVersion = card.UpdatedAt;
        await service.UpdateCardAsync(id, owner, new UpdateCardDto { Title = "newer", ExpectedUpdatedAt = oldVersion }, pid);
        Check(db.Saves == 1 && card.Title == "newer", "matching revision saves");
        Check(card.UpdatedAt > oldVersion && card.UpdatedAt.Ticks % 10 == 0, "revision is monotonic and matches MySQL microseconds");
        try {
            await service.UpdateCardAsync(id, owner, new UpdateCardDto { Title = "stale", ExpectedUpdatedAt = oldVersion }, pid);
            throw new Exception("stale update was accepted");
        } catch (WorldOperationException error) {
            Check(error.Kind == WorldErrorKind.Conflict && Equals(error.Details["code"], "WORLD_CARD_CONFLICT"), "stale revision returns conflict code");
        }
        Check(db.Saves == 1 && card.Title == "newer", "conflict does not mutate content or save relations");
        Check(await service.UpdateCardAsync(id, Guid.NewGuid(), new UpdateCardDto { Title = "foreign", ExpectedUpdatedAt = oldVersion }, pid) == null,
            "ownership check happens before revealing revision conflicts");
        var context = new ExceptionContext(new ActionContext(new DefaultHttpContext(), new RouteData(), new ActionDescriptor()), new List<IFilterMetadata>())
            { Exception = new DbUpdateConcurrencyException() };
        new WorldExceptionFilterAttribute().OnException(context);
        Check(context.ExceptionHandled && context.Result is ConflictObjectResult, "database race is HTTP 409 instead of 500");
        var types = new TestSet<CardType>(); db.CardTypes = types;
        var typeService = new CardTypeService(db);
        var created = await typeService.CreateTypeAsync(new CreateCardTypeDto { Id = "regression", Label = "测试类型" });
        Check(created.IsActive && !created.IsSystem && types.Rows.Count == 1, "custom type creation retains active/non-system flags");
        async Task Reject(Func<Task> action, WorldErrorKind kind) {
            try { await action(); throw new Exception("invalid type operation accepted"); }
            catch (WorldOperationException error) { Check(error.Kind == kind, "type validation returns expected error"); }
        }
        await Reject(() => typeService.CreateTypeAsync(new CreateCardTypeDto { Id = "regression", Label = "重复" }), WorldErrorKind.Conflict);
        try { await typeService.CreateTypeAsync(new CreateCardTypeDto { Id = "", Label = "" }); throw new Exception("empty type accepted"); }
        catch (WorldValidationException) { Check(types.Rows.Count == 1, "invalid type does not write a row"); }
        await typeService.UpdateTypeAsync("regression", new UpdateCardTypeDto { IsActive = false });
        Check(!(await typeService.GetAllActiveTypesAsync()).Any(), "inactive types are excluded from selectors");
        types.Rows[0].IsSystem = true;
        await Reject(() => typeService.DeleteTypeAsync("regression"), WorldErrorKind.Invalid);
        await Reject(() => typeService.UpdateTypeAsync("regression", new UpdateCardTypeDto { Label = "改系统名称" }), WorldErrorKind.Invalid);
        types.Rows[0].IsSystem = false; card.Type = "regression";
        await Reject(() => typeService.DeleteTypeAsync("regression"), WorldErrorKind.Conflict);
        var controller = typeof(WorldCardTypesController);
        Check(controller.GetCustomAttribute<AuthorizeAttribute>()?.Roles == "Admin", "type mutations require admin role");
        foreach (var method in new[] { "CreateCardType", "UpdateCardType", "DeleteCardType" })
            Check(controller.GetMethod(method)!.GetCustomAttribute<AllowAnonymousAttribute>() == null, "type writes cannot bypass admin guard");
        var projects = new TestSet<WorldProject>(); db.WorldProjects = projects;
        projects.Rows.Add(card.Project!);
        var target = new WorldCard { Id = Guid.NewGuid(), ProjectId = pid, Project = card.Project, Title = "target", Type = "location" };
        var foreign = new WorldCard { Id = Guid.NewGuid(), ProjectId = Guid.NewGuid(), Title = "private elsewhere", Type = "location" };
        db.WorldCardRows.Rows.Add(target); db.WorldCardRows.Rows.Add(foreign);
        card.Description = $"{{CARD:{target.Id}}} {{CARD:{foreign.Id}}} {{CARD:{id}}}";
        card.ContentBlocks = $"[{{\"CardId\":\"{target.Id}\"}},{{\"cardId\":\"{target.Id}\"}}]";
        var references = await service.GetReferencesAsync(pid, owner);
        Check(references[id].SequenceEqual(new[] { target.Id }), "bulk references deduplicate, handle old JSON casing, omit self and foreign project");
        await Reject(() => service.GetReferencesAsync(pid, Guid.Empty), WorldErrorKind.NotFound);
        await Reject(() => service.GetCardsByIdsAsync(pid, new[] { id }, Guid.NewGuid()), WorldErrorKind.NotFound);
        card.Project!.IsPublic = true;
        Check((await service.GetReferencesAsync(pid, Guid.Empty))[id].Contains(target.Id), "anonymous readers can load public references");
        Check((await service.GetCardsByIdsAsync(pid, new[] { id, foreign.Id }, Guid.Empty)).Select(c => c.Id).SequenceEqual(new[] { id }), "batch export stays within the readable project");
        card.ContentBlocks = "broken legacy JSON";
        Check((await service.GetReferencesAsync(pid, owner))[id].Contains(target.Id), "malformed legacy blocks do not suppress valid inline references");
        // 图谱旧客户端与权限失败必须在数据库写入之前被拦截。
        var graphController = new WorldProjectsController(null!, db) {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
        graphController.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, owner.ToString()) }, "test"));
        Check(await graphController.SaveGraphPositions(pid, new GraphPositionsDto { Positions = "{}" }) is ObjectResult { StatusCode: 428 }, "legacy graph client must refresh before writing");
        Check(await graphController.SaveGraphPositions(pid, new GraphPositionsDto { ExpectedPositions = "{}" }) is BadRequestObjectResult, "null graph payload cannot clear saved layout");
        Check(await graphController.SaveGraphPositions(Guid.NewGuid(), new GraphPositionsDto { Positions = "{}", ExpectedPositions = "{}" }) is NotFoundObjectResult, "missing graph cannot be saved");
        graphController.HttpContext.User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
            new[] { new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()) }, "test"));
        Check(await graphController.SaveGraphPositions(pid, new GraphPositionsDto { Positions = "{}", ExpectedPositions = "{}" }) is ForbidResult, "non-owner cannot save graph or receive conflicting positions");
        return checks;
    }
}
