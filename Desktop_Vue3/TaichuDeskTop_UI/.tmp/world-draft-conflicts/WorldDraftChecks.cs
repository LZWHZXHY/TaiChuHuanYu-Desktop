using Microsoft.AspNetCore.Http;
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
        return checks;
    }
}
