using System.Data.Common;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.World;
using TaiChuWeb_V2.Services.World;

internal static class QueryChecks
{
    public static void Run(Action<bool, string> check)
    {
        var owner = Guid.NewGuid();
        var other = Guid.NewGuid();
        var projects = new[]
        {
            new WorldProject { Id = Guid.NewGuid(), OwnerId = owner, IsPublic = false },
            new WorldProject { Id = Guid.NewGuid(), OwnerId = owner, IsPublic = true },
            new WorldProject { Id = Guid.NewGuid(), OwnerId = other, IsPublic = false }
        };
        foreach (var user in new[] { owner, other, Guid.Empty })
        {
            var readable = projects.Where(p => WorldAccess.CanRead(p.IsPublic, p.OwnerId, user)).Select(p => p.Id);
            check(readable.SequenceEqual(projects.AsQueryable().ReadableBy(user).Select(p => p.Id)),
                $"公开/私有读取的实体与查询规则一致 ({user})");
        }
        check(!WorldAccess.IsOwner(owner, other) && !WorldAccess.IsOwner(Guid.Empty, Guid.Empty) &&
              WorldAccess.IsOwner(owner, owner), "只有有效的所有者身份可修改");
        check(projects.AsQueryable().ReadableBy(Guid.Empty).Select(p => p.Id).SequenceEqual(new[] { projects[1].Id }),
            "访客只看公开世界，不能读作者或其他人的私密世界");
        check(!WorldAccess.CanRead(false, Guid.Empty, Guid.Empty), "空作者 ID 不会让匿名用户成为所有者");
        try { WorldAccess.RequireReadable(projects[0], other); check(false, "私有项目应拒绝访问"); }
        catch (WorldOperationException error) { check(error.Kind == WorldErrorKind.NotFound, "私有项目拒绝读取且不暴露存在性"); }

        WorldValidation.Validate(new UpdateCardDto { Title = "", Type = "" });
        WorldValidation.Validate(new CreateRelationDto { RelationType = "" });
        check(true, "保留空更新字段和未命名关系的原有兼容行为");
        foreach (var dto in new object[] {
            new CreateProjectDto { Name = " " }, new CreateCardDto { Title = "角色", Type = "" },
            new UpdateCardDto { Title = new string('x', 201) },
            new UpdateCardDto { Type = new string('x', 51) },
            new CreateRelationDto { RelationType = new string('x', 101) },
            new UpgradeQuotaRequest { UpgradeType = (QuotaUpgradeType)99 }
        })
        {
            try { WorldValidation.Validate(dto); check(false, "应拒绝无效 DTO"); }
            catch (WorldValidationException error) { check(error.Errors.Count > 0, $"服务复用 DTO 约束：{dto.GetType().Name}"); }
        }

        // 使用项目已有 MySQL provider 生成 SQL，不打开连接，不使用配置文件或真实凭证。
        using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql("Server=127.0.0.1;Database=world_contract_test;User=unused;Password=unused;",
                new MySqlServerVersion(new Version(8, 0, 36)))
            .AddInterceptors(new NoDatabaseConnections()).Options);
        var assembly = typeof(WorldAccess).Assembly;
        var projection = (Expression<Func<WorldProject, ProjectResponseDto>>)assembly
            .GetType("TaiChuWeb_V2.Services.World.WorldProjectMapper", true)!.GetField("Projection")!.GetValue(null)!;
        var sql = context.WorldProjects.ReadableBy(owner).Select(projection).ToQueryString();
        check(sql.Contains("WHERE") && sql.Contains("IsPublic") && sql.Contains("OwnerId") && sql.Contains("COUNT"),
            "项目访问规则与摘要投影可翻译为 MySQL SQL");
        var relationProjection = (Expression<Func<WorldRelation, RelationDto>>)assembly
            .GetType("TaiChuWeb_V2.Services.World.WorldRelationMapper", true)!.GetField("Projection")!.GetValue(null)!;
        var relationSql = context.WorldRelations.Select(relationProjection).ToQueryString();
        check(relationSql.Contains("JOIN") && relationSql.Contains("RelationType"), "复用关系投影可翻译为 MySQL SQL");

        var referencedId = Guid.NewGuid().ToString();
        var backlinksSql = context.WorldCards.Where(card => EF.Functions.JsonSearchAny(card.ContentBlocks, referencedId)).ToQueryString();
        check(backlinksSql.Contains("JSON_SEARCH") && !backlinksSql.Contains(" AS json", StringComparison.OrdinalIgnoreCase),
            "反向引用的普通 ID 使用 JSON 字符串搜索，不转换成 JSON 文档");

        var project = projects[0];
        project.Cards.Add(new WorldCard());
        var projected = new[] { project }.AsQueryable().Select(projection).Single();
        check(projected.CardCount == 1 && projected.OwnerName == null && projected.OwnerId == owner,
            "项目摘要计数和 nullable 作者字段保持兼容");

        using var cache = new MemoryCache(new MemoryCacheOptions());
        var quota = new WorldQuotaService(context, NullLogger<WorldQuotaService>.Instance);
        try { quota.UpgradeQuotaAsync(owner, (QuotaUpgradeType)99).GetAwaiter().GetResult(); check(false, "无效扩容不得访问数据库"); }
        catch (WorldValidationException) { check(true, "无效扩容在读取经验或扣费前被拒绝"); }
        try { quota.GetUpgradeHistoryAsync(owner, -1).GetAwaiter().GetResult(); check(false, "负数 limit 应被拒绝"); }
        catch (WorldValidationException) { check(true, "负数历史条数在查询前被拒绝"); }
        var projectService = new WorldProjectService(context, cache, quota);
        try { projectService.CreateProjectAsync(owner, new CreateProjectDto()).GetAwaiter().GetResult(); check(false, "无效项目不得持久化"); }
        catch (WorldValidationException) { check(true, "直接调用项目服务也执行参数校验"); }
        var cardService = new WorldCardService(context, projectService, cache, quota);
        var applyRelations = typeof(WorldCardService).GetMethod("ApplyRelationsAsync",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!;
        var editingCard = new WorldCard { Id = Guid.NewGuid(), ProjectId = project.Id };
        void Apply(List<CreateRelationDto>? inputs) =>
            ((Task)applyRelations.Invoke(cardService, new object?[] { editingCard, inputs })!).GetAwaiter().GetResult();
        Apply(null); Apply(new());
        check(context.ChangeTracker.Entries().Count() == 0, "独立创建和未提交关系无需查询或改变关系数据");
        foreach (var inputs in new[] {
            new List<CreateRelationDto> { new() { TargetCardId = editingCard.Id } },
            new List<CreateRelationDto> { new() { TargetCardId = other }, new() { TargetCardId = other } },
        })
        {
            try { Apply(inputs); check(false, "无效关系应拒绝"); }
            catch (WorldOperationException error) { check(error.Kind == WorldErrorKind.Invalid, "自关联或重复关系在数据库写入前拒绝"); }
        }
        var savedRelation = new WorldRelation { Id = Guid.NewGuid(), SourceCardId = editingCard.Id,
            TargetCardId = other, RelationType = "旧关系" };
        editingCard.OutRelations.Add(savedRelation); context.WorldRelations.Attach(savedRelation);
        Apply(null);
        check(context.Entry(savedRelation).State == EntityState.Unchanged, "旧客户端省略关系时不清空已有关系");
        Apply(new());
        check(editingCard.OutRelations.Count == 0 && context.Entry(savedRelation).State == EntityState.Deleted,
            "显式空集合删除出向关系，等待正文一起提交");
        try { cardService.UpdateCardAsync(Guid.NewGuid(), owner, new UpdateCardDto { Type = new string('x', 51) }).GetAwaiter().GetResult(); check(false, "无效卡片不得持久化"); }
        catch (WorldValidationException) { check(true, "直接调用卡片服务也执行参数校验"); }
    }

    private sealed class NoDatabaseConnections : DbConnectionInterceptor
    {
        public override InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData,
            InterceptionResult result) => throw new InvalidOperationException("Tests must not connect to a database");
        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection,
            ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Tests must not connect to a database");
    }
}
