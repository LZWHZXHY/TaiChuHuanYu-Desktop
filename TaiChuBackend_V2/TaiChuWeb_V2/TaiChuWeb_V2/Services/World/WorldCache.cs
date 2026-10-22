using Microsoft.Extensions.Caching.Memory;

namespace TaiChuWeb_V2.Services.World;

internal static class WorldCache
{
    public static string CardKey(Guid cardId) => $"card_{cardId}";

    public static void InvalidateCards(IMemoryCache cache, IEnumerable<Guid> cardIds)
    {
        foreach (var cardId in cardIds.Distinct()) cache.Remove(CardKey(cardId));
    }

    public static void InvalidateProject(IMemoryCache cache, Guid projectId, Guid ownerId)
    {
        cache.Remove($"cards_project_{projectId}");
        cache.Remove($"user_projects_{ownerId}");
        cache.Remove("public_projects");
    }
}
