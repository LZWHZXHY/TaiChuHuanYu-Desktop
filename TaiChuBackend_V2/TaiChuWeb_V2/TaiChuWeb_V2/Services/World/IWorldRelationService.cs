using TaiChuWeb_V2.Dtos.World;

namespace TaiChuWeb_V2.Services.World
{
    public interface IWorldRelationService
    {
        Task<RelationDto> CreateRelationAsync(Guid sourceCardId, Guid userId, CreateRelationDto dto);
        Task<bool> DeleteRelationAsync(Guid relationId, Guid userId);
        Task<IEnumerable<RelationDto>> GetRelationsForCardAsync(Guid cardId, Guid userId);
    }
}
