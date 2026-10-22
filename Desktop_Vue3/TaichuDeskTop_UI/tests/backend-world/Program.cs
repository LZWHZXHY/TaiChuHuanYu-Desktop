using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.World;

// 引用已编译后端，使用临时本地 HTTP 服务检查契约；不启动业务网站或连接数据库。
var assembly = typeof(RelationDto).Assembly;
var mapper = assembly.GetType("TaiChuWeb_V2.Services.World.WorldRelationMapper", true)!;
var projection = (Expression<Func<WorldRelation, RelationDto>>)mapper.GetField("Projection")!.GetValue(null)!;
var map = (Func<WorldRelation, RelationDto>)mapper.GetField("FromEntity")!.GetValue(null)!;
var cacheType = assembly.GetType("TaiChuWeb_V2.Services.World.WorldCache", true)!;
var source = new WorldCard { Id = Guid.NewGuid(), Title = "源卡片", Type = "character" };
var target = new WorldCard { Id = Guid.NewGuid(), Title = "目标卡片", Type = "location" };
var relation = new WorldRelation
{
    Id = Guid.NewGuid(), SourceCardId = source.Id, TargetCardId = target.Id,
    SourceCard = source, TargetCard = target, RelationType = "出生地", CreatedAt = DateTime.UtcNow
};
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
var passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new InvalidOperationException(name);
    Console.WriteLine($"PASS {name}");
    passed++;
}

var entityDto = map(relation);
var queryDto = new[] { relation }.AsQueryable().Select(projection).Single();
Check(JsonSerializer.Serialize(entityDto, options) == JsonSerializer.Serialize(queryDto, options),
    "实体映射与查询表达式产生相同 DTO");
Check(entityDto.SourceCardTitle == source.Title && entityDto.TargetCardType == target.Type,
    "保留双向卡片的标题与类型");
using (var json = JsonDocument.Parse(JsonSerializer.Serialize(entityDto, options)))
{
    Check(json.RootElement.GetProperty("sourceCardId").GetGuid() == source.Id &&
        json.RootElement.GetProperty("relationType").GetString() == "出生地", "JSON 字段与现有接口兼容");
}
relation.SourceCard = null;
relation.TargetCard = null;
Check(map(relation).SourceCardTitle == null && map(relation).TargetCardType == null, "缺失导航实体时保留 null");

using var cache = new MemoryCache(new MemoryCacheOptions());
var otherId = Guid.NewGuid();
cache.Set($"card_{source.Id}", "source");
cache.Set($"card_{target.Id}", "target");
cache.Set($"card_{otherId}", "other");
cacheType.GetMethod("InvalidateCards")!.Invoke(null, new object[] { cache, new[] { source.Id, target.Id, source.Id } });
Check(!cache.TryGetValue($"card_{source.Id}", out _) && !cache.TryGetValue($"card_{target.Id}", out _),
    "关系变化清理两端卡片缓存");
Check(cache.TryGetValue($"card_{otherId}", out _), "保留无关卡片缓存");
var projectId = Guid.NewGuid();
var ownerId = Guid.NewGuid();
cache.Set($"cards_project_{projectId}", "cards");
cache.Set($"user_projects_{ownerId}", "owner list");
cache.Set("public_projects", "public list");
cacheType.GetMethod("InvalidateProject")!.Invoke(null, new object[] { cache, projectId, ownerId });
Check(!cache.TryGetValue($"cards_project_{projectId}", out _) &&
    !cache.TryGetValue($"user_projects_{ownerId}", out _) && !cache.TryGetValue("public_projects", out _),
    "卡片数量变化清理项目及个人/公开列表缓存");
Check(cache.TryGetValue($"card_{otherId}", out _), "项目失效不清空整个内存缓存");
QueryChecks.Run(Check);
await ContractChecks.Run(Check);
Console.WriteLine($"{passed} backend world checks passed.");
