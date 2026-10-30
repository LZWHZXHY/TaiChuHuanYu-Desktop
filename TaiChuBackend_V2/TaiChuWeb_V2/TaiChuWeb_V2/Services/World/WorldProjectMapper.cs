using System.Linq.Expressions;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.World;

namespace TaiChuWeb_V2.Services.World;

internal static class WorldProjectMapper
{
    public static readonly Expression<Func<WorldProject, ProjectResponseDto>> Projection = project => new ProjectResponseDto
    {
        Id = project.Id, Name = project.Name, Description = project.Description,
        CoverImage = project.CoverImage,
        GraphPositions = project.GraphPositions,
        IsPublic = project.IsPublic, CreatedAt = project.CreatedAt, UpdatedAt = project.UpdatedAt,
        CardCount = project.Cards.Count, OwnerName = project.Owner != null ? project.Owner.Username : null,
        OwnerId = project.OwnerId,
        CollaboratorCount = project.Collaborators.Count
    };
    public static readonly Func<WorldProject, ProjectResponseDto> FromEntity = Projection.Compile();
}
