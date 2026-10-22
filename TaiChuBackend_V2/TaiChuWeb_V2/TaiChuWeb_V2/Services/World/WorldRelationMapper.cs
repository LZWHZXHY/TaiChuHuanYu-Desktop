using System.Linq.Expressions;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.World;

namespace TaiChuWeb_V2.Services.World;

internal static class WorldRelationMapper
{
    // 同一映射供 EF 查询投影和已加载的实体使用，保持 JSON 字段一致。
    public static readonly Expression<Func<WorldRelation, RelationDto>> Projection = relation => new RelationDto
    {
        Id = relation.Id,
        SourceCardId = relation.SourceCardId,
        TargetCardId = relation.TargetCardId,
        RelationType = relation.RelationType,
        CreatedAt = relation.CreatedAt,
        SourceCardTitle = relation.SourceCard != null ? relation.SourceCard.Title : null,
        TargetCardTitle = relation.TargetCard != null ? relation.TargetCard.Title : null,
        SourceCardType = relation.SourceCard != null ? relation.SourceCard.Type : null,
        TargetCardType = relation.TargetCard != null ? relation.TargetCard.Type : null
    };

    public static readonly Func<WorldRelation, RelationDto> FromEntity = Projection.Compile();
}
