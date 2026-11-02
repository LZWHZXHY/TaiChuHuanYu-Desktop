using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.World;
using TaiChuWeb_V2.Services.Cos;

namespace TaiChuWeb_V2.Services.World;

public class WorldRestoreService(AppDbContext db, CosService cos, IWorldQuotaService quota,
    IMemoryCache cache, ILogger<WorldRestoreService> logger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static WorldOperationException Invalid(string message) => new(WorldErrorKind.Invalid, message);
    private static WorldRestorePlan Plan(WorldRestore job) => JsonSerializer.Deserialize<WorldRestorePlan>(job.Plan, Json)!;
    private static HashSet<string> Uploaded(WorldRestore job) => JsonSerializer.Deserialize<HashSet<string>>(job.Uploaded, Json)!;
    private static string Key(Guid id, string name) => $"world/restores/{id:N}/{name}";
    private WorldRestoreStatus View(WorldRestore job) => new(job.Id, job.State, job.TargetProjectId, Plan(job),
        Uploaded(job).ToDictionary(n => "./images/" + n, n => cos.ObjectUrl(Key(job.Id, n))), job.UpdatedAt);

    private async Task RequireOwner(Guid user, Guid project) => _ = await db.WorldProjects.AsNoTracking()
        .OwnedBy(user).FirstOrDefaultAsync(p => p.Id == project)
        ?? throw new WorldOperationException(WorldErrorKind.Forbidden, "无权恢复到此世界");

    private async Task<WorldRestore> Get(Guid user, Guid id, bool locked = false)
    {
        // 行锁保护上传、取消和提交，多个浏览器或服务实例不能交叉清理文件。
        var rows = locked ? await db.WorldRestores.FromSqlInterpolated(
            $"SELECT * FROM WorldRestores WHERE Id = {id} AND OwnerId = {user} FOR UPDATE").ToListAsync()
            : await db.WorldRestores.Where(j => j.Id == id && j.OwnerId == user).ToListAsync();
        return rows.SingleOrDefault() ?? throw new WorldOperationException(WorldErrorKind.NotFound, "恢复记录不存在");
    }

    private Task<T> Transaction<T>(Func<Task<T>> action) => db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var result = await action();
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
        return result;
    });

    private async Task CheckCapacity(Guid user, StartWorldRestoreDto plan)
    {
        await RequireOwner(user, plan.SourceProjectId);
        var stats = await quota.GetUserStatsAsync(user);
        if (plan.NewProject && stats.UsedWorldCount >= stats.MaxWorldCount)
            throw Invalid("世界数量已达账号上限，请选择有剩余容量的现有世界。");
        var used = plan.NewProject ? 0 : await db.WorldCards.CountAsync(c => c.ProjectId == plan.SourceProjectId);
        if (plan.CardCount > stats.MaxCardsPerWorld - used)
            throw Invalid($"需要 {plan.CardCount} 个条目位置，目标世界仅剩 {Math.Max(0, stats.MaxCardsPerWorld - used)} 个。");
    }

    public async Task<List<WorldRestoreStatus>> ListAsync(Guid user, Guid project)
    {
        await RequireOwner(user, project);
        var jobs = await db.WorldRestores.AsNoTracking().Where(j => j.OwnerId == user && j.SourceProjectId == project
            && (j.State == "pending" || j.State == "cleaning")).OrderByDescending(j => j.UpdatedAt).ToListAsync();
        return jobs.Select(View).ToList();
    }

    public async Task<WorldRestoreStatus> StatusAsync(Guid user, Guid id) => View(await Get(user, id));

    public async Task<WorldRestoreStatus> StartAsync(Guid user, StartWorldRestoreDto source)
    {
        WorldValidation.Validate(source);
        foreach (var image in source.Images) WorldValidation.Validate(image);
        if (source.Images.Select(i => i.Name).Distinct().Count() != source.Images.Count
            || source.Images.Sum(i => (long)i.Size) > 256L * 1024 * 1024) throw Invalid("图片清单无效或过大");
        var identity = $"{user:N}/{source.SourceProjectId:N}/{source.Fingerprint}/{source.NewProject}/{source.DefaultType}/{source.PlainText}";
        var id = new Guid(SHA256.HashData(Encoding.UTF8.GetBytes(identity)).AsSpan(0, 16));
        var existing = await db.WorldRestores.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id && j.OwnerId == user);
        if (existing != null && existing.State != "cancelled") return View(existing);
        await CheckCapacity(user, source);
        if (existing != null)
            return await Transaction(async () =>
            {
                var job = await Get(user, id, true);
                if (job.State == "cancelled") { job.State = "pending"; job.UpdatedAt = DateTime.UtcNow; }
                return View(job);
            });
        var created = new WorldRestore
        {
            Id = id, OwnerId = user, SourceProjectId = source.SourceProjectId,
            TargetProjectId = source.NewProject ? Guid.NewGuid() : source.SourceProjectId,
            Plan = JsonSerializer.Serialize(new WorldRestorePlan(source, Enumerable.Range(0, source.CardCount).Select(_ => Guid.NewGuid()).ToArray()), Json)
        };
        db.WorldRestores.Add(created);
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            existing = await db.WorldRestores.AsNoTracking().FirstOrDefaultAsync(j => j.Id == id && j.OwnerId == user);
            if (existing == null) throw;
            return View(existing);
        }
        return View(created);
    }

    public async Task<WorldRestoreStatus> UploadAsync(Guid user, Guid id, string name, IFormFile file)
    {
        if (file.Length is < 1 or > 32 * 1024 * 1024) throw Invalid("原图大小必须在 32MB 以内");
        using var buffer = new MemoryStream();
        await file.CopyToAsync(buffer);
        var bytes = buffer.ToArray();
        if (!name.StartsWith(Convert.ToHexStringLower(SHA256.HashData(bytes)) + ".", StringComparison.Ordinal))
            throw Invalid("图片校验失败，请重新选择原备份文件");
        return await Transaction(async () =>
        {
            var job = await Get(user, id, true);
            var plan = Plan(job);
            await RequireOwner(user, job.SourceProjectId);
            var image = plan.Source.Images.SingleOrDefault(i => i.Name == name);
            if (image == null || image.Size != bytes.Length) throw Invalid("图片不属于此恢复记录");
            if (job.State == "committed") return View(job);
            if (job.State != "pending") throw Invalid("恢复已取消或正在清理");
            var uploaded = Uploaded(job);
            if (!uploaded.Contains(name))
            {
                // key 上传前已持久化于计划；上传成功后进程退出，也可重试覆盖同一 key 或删除它。
                await cos.UploadAsync(bytes, name, $"world/restores/{id:N}");
                uploaded.Add(name);
                job.Uploaded = JsonSerializer.Serialize(uploaded, Json);
            }
            job.UpdatedAt = DateTime.UtcNow;
            return View(job);
        });
    }

    public async Task<WorldRestoreStatus> CommitAsync(Guid user, Guid id, CommitWorldRestoreDto dto)
    {
        try
        {
            var result = await Transaction(async () =>
            {
                var job = await Get(user, id, true);
                if (job.State == "committed") return View(job);
                if (job.State != "pending") throw Invalid("恢复已取消或正在清理");
                var plan = Plan(job);
                // 所有条目、关系、世界计数和完成标记在同一事务内提交。
                await db.UserStats.FromSqlInterpolated($"SELECT * FROM UserStats WHERE UserId = {user} FOR UPDATE").ToListAsync();
                await CheckCapacity(user, plan.Source);
                if (dto.Cards.Count != plan.CardIds.Length || Uploaded(job).Count != plan.Source.Images.Count)
                    throw Invalid("条目或图片尚未齐全，恢复未提交");
                ValidateCards(dto.Cards, plan.CardIds);
                if (plan.Source.NewProject)
                {
                    db.WorldProjects.Add(new WorldProject { Id = job.TargetProjectId, OwnerId = user,
                        Name = plan.Source.Name, Description = dto.Description, IsPublic = false });
                    (await quota.GetUserStatsAsync(user)).UsedWorldCount++;
                }
                var cards = dto.Cards.Select((card, i) => WorldCardService.CreateEntity(job.TargetProjectId, plan.CardIds[i], card)).ToArray();
                db.WorldCards.AddRange(cards);
                // 先落卡片再落外键，仍未提交事务；后续失败会一起回滚。
                await db.SaveChangesAsync();
                db.WorldRelations.AddRange(dto.Cards.SelectMany((card, i) => (card.Relations ?? [])
                    .Select(r => new WorldRelation
                    { SourceCardId = plan.CardIds[i], TargetCardId = r.TargetCardId, RelationType = r.RelationType })));
                // 编年史（时间线、事件、历法）和条目在同一个事务里写入；老备份 / 老页面没带编年史就跳过
                if ((dto as CommitWorldRestoreWithChronicleDto)?.Chronicle is { } chronicle)
                    await AddChronicleAsync(job.TargetProjectId, plan, chronicle, user);
                job.State = "committed";
                job.UpdatedAt = DateTime.UtcNow;
                return View(job);
            });
            WorldCache.InvalidateProject(cache, result.TargetProjectId, user);
            return result;
        }
        catch (Exception ex) when (ex is WorldOperationException or WorldValidationException or DbUpdateException)
        {
            // 网络断线不代表提交失败。确定的业务/写入失败才回滚并安排清理。
            db.ChangeTracker.Clear();
            await CancelAsync(user, id);
            throw;
        }
    }

    /// <summary>
    /// 备份里的编年史写进目标世界：时间线、事件换成新 Id，只关联这次恢复的条目。
    /// 历法：恢复成新世界时直接用备份里的；恢复到现有世界时，只有它还没设置过历法才用（不覆盖作者现在的历法）。
    /// </summary>
    private async Task AddChronicleAsync(Guid projectId, WorldRestorePlan plan, WorldChronicleTransferDto chronicle, Guid user)
    {
        var fresh = plan.Source.NewProject;
        var lineCount = fresh ? 0 : await db.WorldTimelines.CountAsync(t => t.ProjectId == projectId);
        var eventCount = fresh ? 0 : await db.WorldTimelineEvents.CountAsync(e => e.ProjectId == projectId);
        var sortBase = fresh ? 0 : (await db.WorldTimelines.Where(t => t.ProjectId == projectId).MaxAsync(t => (int?)t.SortOrder) ?? 0);
        var built = WorldChronicleTransfer.Build(projectId, chronicle, plan.CardIds.ToHashSet(), user, sortBase);
        // 和平时编辑的上限一样：每个世界最多 200 条时间线、5000 个事件
        if (lineCount + built.Timelines.Count > 200)
            throw Invalid($"编年史放不下：目标世界已有 {lineCount} 条时间线，备份里有 {built.Timelines.Count} 条，合计不能超过 200 条。可以改为恢复成新的世界。");
        if (eventCount + built.Events.Count > 5000)
            throw Invalid($"编年史放不下：目标世界已有 {eventCount} 个事件，备份里有 {built.Events.Count} 个，合计不能超过 5000 个。可以改为恢复成新的世界。");
        db.WorldTimelines.AddRange(built.Timelines);
        db.WorldTimelineEvents.AddRange(built.Events);
        if (built.Calendar != null && (fresh || !await db.WorldCalendars.AnyAsync(c => c.ProjectId == projectId)))
            db.WorldCalendars.Add(new WorldCalendar { ProjectId = projectId, Json = built.Calendar, UpdatedAt = DateTime.UtcNow });
    }

    internal static void ValidateCards(List<CreateCardDto> cards, Guid[] ids)
    {
        var allowed = ids.ToHashSet();
        for (var index = 0; index < cards.Count; index++)
        {
            var card = cards[index];
            WorldValidation.Validate(card);
            if (WorldCardService.ValidateRelations(ids[index], card.Relations ?? []).Any(id => !allowed.Contains(id)))
                throw Invalid("关系目标不在此次恢复的条目中");
            if ((card.EmbeddedCards ?? []).Concat((card.ContentBlocks ?? []).Select(b => b.CardId)).Any(id => !allowed.Contains(id)))
                throw Invalid("引用目标不在此次恢复的条目中");
        }
    }

    public async Task<WorldRestoreStatus> CancelAsync(Guid user, Guid id)
    {
        var marked = await Transaction(async () =>
        {
            var job = await Get(user, id, true);
            if (job.State == "pending") { job.State = "cleaning"; job.UpdatedAt = DateTime.UtcNow; }
            return View(job);
        });
        if (marked.State != "cleaning") return marked; // 迟到的取消不删除已提交内容。
        try
        {
            return await Transaction(async () =>
            {
                var job = await Get(user, id, true);
                if (job.State != "cleaning") return View(job);
                // 包括结果未知的上传，不能只删除 Uploaded 清单。
                foreach (var image in Plan(job).Source.Images) await cos.DeleteAsync(Key(job.Id, image.Name));
                job.Uploaded = "[]"; job.State = "cancelled"; job.UpdatedAt = DateTime.UtcNow;
                return View(job);
            });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "世界观恢复 {RestoreId} 图片清理待重试", id);
            db.ChangeTracker.Clear();
            return await StatusAsync(user, id);
        }
    }

    public async Task CleanExpiredAsync()
    {
        var expiry = DateTime.UtcNow.AddDays(-7);
        var jobs = await db.WorldRestores.AsNoTracking().Where(j => j.State == "cleaning" || j.State == "pending" && j.UpdatedAt < expiry)
            .OrderBy(j => j.UpdatedAt).Take(20).Select(j => new { j.Id, j.OwnerId }).ToListAsync();
        foreach (var job in jobs)
        {
            var cleaning = await Transaction(async () =>
            {
                var current = await Get(job.OwnerId, job.Id, true);
                if (current.State == "pending" && current.UpdatedAt < expiry) current.State = "cleaning";
                return current.State == "cleaning";
            });
            if (cleaning) await CancelAsync(job.OwnerId, job.Id);
        }
    }
}

public class WorldRestoreCleanup(IServiceScopeFactory scopes, ILogger<WorldRestoreCleanup> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5));
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<WorldRestoreService>().CleanExpiredAsync();
            }
            catch (Exception ex) { logger.LogWarning(ex, "世界观恢复清理稍后重试"); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
