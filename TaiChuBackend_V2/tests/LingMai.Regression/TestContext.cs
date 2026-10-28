using TaiChuWeb_V2.Models.Wiki;
using TaiChuWeb_V2.Models.Project;
using TaiChuWeb_V2.Models.World;
using TaiChuWeb_V2.Models.ChaiCommunity;
using TaiChuWeb_V2.Models.Artwork;
using TaiChuWeb_V2.Models.Interact;
using System.Collections;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Models.FinalNodes;
using TaiChuWeb_V2.Models.Tag;
using TaiChuWeb_V2.Models.User;

// 只替换 EF 的存储边界；控制器、服务、查询表达式和事务分支均执行生产代码。
sealed class TestContext : AppDbContext
{
    public int Queries, Saves, Commits, Rollbacks;
    public TestSet<WorldCard> WorldCardRows = new();
    public Exception? SaveFailure, CommitFailure;
    public TestSet<Artwork> ArtworkRows = new();
    public TestSet<ArtworkImage> ImageRows = new();
    public TestSet<UserPermission> PermissionRows = new();
    public TestSet<UserInteraction> InteractionRows = new();
    public TestSet<Comment> CommentRows = new();
    public TestSet<Note> NoteRows = new();
    public TestSet<Space> SpaceRows = new();
    public TestSet<Block> BlockRows = new();
    public TestSet<NoteLink> NoteLinkRows = new();
    public TestSet<BlockLink> BlockLinkRows = new();
    public TestSet<BlockIndex> IndexRows = new();
    public TestSet<UserStats> StatsRows = new();
    public TestSet<UserExpLog> ExpRows = new();
    public TestSet<User> UserRows = new();
    public TestSet<PublishedNote> PublishedRows = new();
    public TestSet<PublishedBlock> PublishedBlockRows = new();
    public TestSet<WikiArticle> WikiRows = new();
    public TestSet<WikiArticleRevision> RevisionRows = new();
    public TestSet<WikiCategory> WikiCategoryRows = new();
    public TestSet<Project> ProjectRows = new();
    public TestSet<ProjectMember> MemberRows = new();
    public TestSet<ProjectCustomRole> RoleRows = new();
    public TestSet<ProjectCategory> CategoryRows = new();
    public TestSet<ProjectTask> TaskRows = new();
    public TestSet<ProjectDeliveryRecord> DeliveryRows = new();
    public TestContext() : base(new DbContextOptionsBuilder<AppDbContext>().UseMySql("Server=localhost;Database=tests;User=test;", new MySqlServerVersion(new Version(8, 0, 31))).Options)
    {
        WikiArticles = Track(WikiRows); WikiArticleRevisions = Track(RevisionRows); WikiCategories = Track(WikiCategoryRows); WikiCategoryRequests = Track(new TestSet<WikiCategoryRequest>());
        Projects = Track(ProjectRows); ProjectMembers = Track(MemberRows); ProjectCustomRoles = Track(RoleRows); ProjectCategories = Track(CategoryRows); ProjectTasks = Track(TaskRows); ProjectDeliveryRecords = Track(DeliveryRows);
        WorldCards = Track(WorldCardRows); WorldRelations = Track(new TestSet<WorldRelation>());
        WorldProjects = Track(new TestSet<WorldProject>()); StickmanCharacters = Track(new TestSet<StickmanCharacter>()); FinalNodes = Track(new TestSet<UniversalNode>());
        ArtworkImages = Track(ImageRows); UserPermissions = Track(PermissionRows);
        Artworks = Track(ArtworkRows); UserInteractions = Track(InteractionRows); Comments = Track(CommentRows);
        Users = Track(UserRows); PublishedNotes = Track(PublishedRows); PublishedBlocks = Track(PublishedBlockRows);
        Notes = Track(NoteRows); Spaces = Track(SpaceRows); Blocks = Track(BlockRows);
        NoteLinks = Track(NoteLinkRows); BlockLinks = Track(BlockLinkRows); BlockIndexes = Track(IndexRows);
        UserStats = Track(StatsRows); UserExpLogs = Track(ExpRows); Tags = Track(new TestSet<Tag>()); TagAssignments = Track(new TestSet<TagAssignment>());
    }
    TestSet<T> Track<T>(TestSet<T> set) where T : class { set.Query = () => Queries++; return set; }
    public override DatabaseFacade Database => new TestDatabase(this);
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        Saves++;
        foreach (var revision in RevisionRows.Rows.Where(r => r.Id == 0)) revision.Id = RevisionRows.Rows.Max(r => r.Id) + 1;
        if (SaveFailure == null) foreach (var art in ArtworkRows.Rows)
        {
            if (art.Id == 0) art.Id = ArtworkRows.Rows.Max(a => a.Id) + 1;
            foreach (var image in art.Images)
            {
                image.ArtworkId = art.Id;
                if (image.Id == 0) image.Id = ImageRows.Rows.Select(i => i.Id).DefaultIfEmpty().Max() + 1;
                if (!ImageRows.Rows.Contains(image)) ImageRows.Rows.Add(image);
            }
        }
        return SaveFailure == null ? Task.FromResult(1) : Task.FromException<int>(SaveFailure);
    }
}

sealed class TestSet<T> : DbSet<T>, IQueryable<T>, IAsyncEnumerable<T> where T : class
{
    public readonly List<T> Rows = new();
    public Action Query = () => {};
    public override IEntityType EntityType => throw new NotSupportedException();
    Type IQueryable.ElementType => typeof(T);
    Expression IQueryable.Expression => Rows.AsQueryable().Expression;
    IQueryProvider IQueryable.Provider => new TestProvider<T>(this);
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => Rows.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => Rows.GetEnumerator();
    public override IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new AsyncRows<T>(Rows.GetEnumerator());
    public override ValueTask<T?> FindAsync(params object?[]? keys)
    {
        Query();
        return ValueTask.FromResult(Rows.FirstOrDefault(row => Equals(typeof(T).GetProperty("Id")!.GetValue(row), keys![0])));
    }
    public override EntityEntry<T> Add(T entity) { Rows.Add(entity); return null!; }
    public override ValueTask<EntityEntry<T>> AddAsync(T entity, CancellationToken cancellationToken = default) => ValueTask.FromResult(Add(entity));
    public override EntityEntry<T> Update(T entity) => null!;
    public override void AddRange(IEnumerable<T> entities) => Rows.AddRange(entities);
    public override EntityEntry<T> Remove(T entity) { Rows.Remove(entity); return null!; }
    public override void RemoveRange(IEnumerable<T> entities) { foreach (var row in entities.ToArray()) Rows.Remove(row); }
}

sealed class TestProvider<T>(TestSet<T> set) : IAsyncQueryProvider where T : class
{
    IQueryProvider Inner => set.Rows.AsQueryable().Provider;
    public IQueryable CreateQuery(Expression expression) => throw new NotSupportedException();
    public IQueryable<U> CreateQuery<U>(Expression expression) => new TestQuery<U>(this, expression);
    public object? Execute(Expression expression) => Inner.Execute(new TestCollationVisitor().Visit(expression));
    public TResult Execute<TResult>(Expression expression) => Inner.Execute<TResult>(new TestCollationVisitor().Visit(expression));
    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken = default)
    {
        set.Query();
        object? result;
        if (expression is MethodCallExpression { Method.Name: "ExecuteDelete" } call)
        {
            var removed = Inner.CreateQuery<T>(call.Arguments[0]).ToArray();
            foreach (var row in removed) set.Rows.Remove(row);
            result = removed.Length;
        }
        else if (expression is MethodCallExpression { Method.Name: "ExecuteUpdate" } update)
        {
            var rows = Inner.CreateQuery<T>(update.Arguments[0]).ToArray();
            Expression Unquote(Expression e) => e is UnaryExpression { NodeType: ExpressionType.Quote } u ? u.Operand : e;
            var setter = (LambdaExpression)Unquote(update.Arguments[1]);
            for (var body = setter.Body as MethodCallExpression; body != null; body = body.Object as MethodCallExpression)
            {
                var property = (LambdaExpression)Unquote(body.Arguments[0]);
                var member = (System.Reflection.PropertyInfo)((MemberExpression)property.Body).Member;
                var value = Unquote(body.Arguments[1]);
                foreach (var row in rows) member.SetValue(row, value is LambdaExpression lambda ? lambda.Compile().DynamicInvoke(row) : Expression.Lambda(value).Compile().DynamicInvoke());
            }
            result = rows.Length;
        }
        else result = Inner.Execute(new TestCollationVisitor().Visit(expression));
        var type = typeof(TResult).GetGenericArguments()[0];
        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!.MakeGenericMethod(type).Invoke(null, new[] { result })!;
    }
}

sealed class TestQuery<T>(IQueryProvider provider, Expression expression) : IOrderedQueryable<T>, IAsyncEnumerable<T>
{
    public Type ElementType => typeof(T);
    public Expression Expression => expression;
    public IQueryProvider Provider => provider;
    public IEnumerator<T> GetEnumerator() => provider.Execute<IEnumerable<T>>(expression).GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) => new AsyncRows<T>(GetEnumerator());
}
sealed class AsyncRows<T>(IEnumerator<T> rows) : IAsyncEnumerator<T>
{
    public T Current => rows.Current;
    public ValueTask<bool> MoveNextAsync() => ValueTask.FromResult(rows.MoveNext());
    public ValueTask DisposeAsync() { rows.Dispose(); return ValueTask.CompletedTask; }
}
sealed class TestDatabase(TestContext context) : DatabaseFacade(context)
{
    public override IExecutionStrategy CreateExecutionStrategy() => new TestStrategy(context);
    public override Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IDbContextTransaction>(new TestTransaction(context));
}
sealed class TestStrategy(TestContext context) : IExecutionStrategy
{
    public bool RetriesOnFailure => false;
    public TResult Execute<TState, TResult>(TState state, Func<DbContext, TState, TResult> operation,
        Func<DbContext, TState, ExecutionResult<TResult>>? verifySucceeded) => operation(context, state);
    public Task<TResult> ExecuteAsync<TState, TResult>(TState state, Func<DbContext, TState, CancellationToken, Task<TResult>> operation,
        Func<DbContext, TState, CancellationToken, Task<ExecutionResult<TResult>>>? verifySucceeded,
        CancellationToken cancellationToken = default) => operation(context, state, cancellationToken);
}
sealed class TestTransaction(TestContext context) : IDbContextTransaction
{
    public Guid TransactionId { get; } = Guid.NewGuid();
    public void Commit() => context.Commits++;
    public void Rollback() => context.Rollbacks++;
    public Task CommitAsync(CancellationToken cancellationToken = default) { Commit(); return context.CommitFailure == null ? Task.CompletedTask : Task.FromException(context.CommitFailure); }
    public Task RollbackAsync(CancellationToken cancellationToken = default) { Rollback(); return Task.CompletedTask; }
    public void Dispose() {}
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

// 内存替身只验证权限与结果；排序规则的 SQL 生成由 ContentFlowChecks 验证。
sealed class TestCollationVisitor : ExpressionVisitor
{
    protected override Expression VisitMethodCall(MethodCallExpression node) =>
        node.Method.DeclaringType == typeof(RelationalDbFunctionsExtensions) && node.Method.Name == nameof(RelationalDbFunctionsExtensions.Collate)
            ? Visit(node.Arguments[1]) : base.VisitMethodCall(node);
}
