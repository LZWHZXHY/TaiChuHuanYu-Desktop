using System.Data.Common;
using System.Globalization;
using MySqlConnector;
using TaiChuWeb_V2.Services.Analytics;

namespace TaiChuWeb_V2.Services.Resources;

/// <summary>一条社区资源（community_resources 表的一行）</summary>
public sealed class ResourceRow
{
    public Guid Id { get; set; }
    public Guid UploaderId { get; set; }
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string Category { get; set; } = "其他";
    /// <summary>位置（展示 / 整理用的虚拟文件夹，比如「PS笔刷/分类A」）；没填是 null，显示时用分类名</summary>
    public string? Folder { get; set; }
    /// <summary>文件存在哪：null / "nas" = NAS 的 MinIO，"cos" = 腾讯云（ResourceStorage.Nas / Cos）</summary>
    public string? Storage { get; set; }
    /// <summary>逗号分隔</summary>
    public string Tags { get; set; } = "";
    public string ObjectKey { get; set; } = "";
    public string FileName { get; set; } = "";
    public long Size { get; set; }
    public string ContentType { get; set; } = "application/octet-stream";
    public int Price { get; set; }
    /// <summary>uploading 上传中 / pending 待审核 / approved 已公开 / rejected 被打回 / removed 被下架</summary>
    public string Status { get; set; } = ResourceStatus.Uploading;
    public string? ReviewNote { get; set; }
    public Guid? ReviewerId { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public int DownloadCount { get; set; }
    public int UniqueDownloadCount { get; set; }
    /// <summary>这个资源一共给上传者带来多少经验（付费下载收入 + 免费资源下载奖励；审核经验不算在内）</summary>
    public long ExpEarned { get; set; }
    public string? UploadId { get; set; }
    public long PartSize { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime? DeletedAt { get; set; }

    public List<string> TagList => Tags.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
    public int PartCount => ResourceRules.PartCount(Size, PartSize > 0 ? PartSize : ResourceRules.PartSize);
}

public static class ResourceStatus
{
    public const string Uploading = "uploading";
    public const string Pending = "pending";
    public const string Approved = "approved";
    public const string Rejected = "rejected";
    public const string Removed = "removed";
}

/// <summary>买到了 / 已经买过 / 经验不够 / 某一方没有经验记录（账号数据不完整）</summary>
public enum PurchaseOutcome { Bought, AlreadyOwned, NotEnough, MissingStats }

/// <summary>
/// 社区资源的数据读写（原生 SQL，表由 CommunityResourceSchema 启动时建好）。
/// 价格单位是经验：付费下载时「扣下载者经验、加上传者经验、记购买、双方各记一条经验流水」在同一个数据库事务里完成
/// （直接写 UserStats / UserExpLog 两张表，表名列名见 ExpTableNames；等级是按经验实时算的，没有要同步的缓存列）。
/// 资源区完全不碰土豆。
/// </summary>
public sealed class CommunityResourceStore(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;

    private async Task<MySqlConnection> OpenAsync(CancellationToken ct)
    {
        var c = new MySqlConnection(_connectionString);
        await c.OpenAsync(ct);
        return c;
    }

    private const string FullColumns = "Id, UploaderId, Title, Description, Category, Tags, ObjectKey, FileName, Size, ContentType, Price, Status, ReviewNote, ReviewerId, ReviewedAt, DownloadCount, UniqueDownloadCount, ExpEarned, UploadId, PartSize, CreatedAt, UpdatedAt, PublishedAt, DeletedAt, Folder, Storage";
    /// <summary>列表用：介绍只取前 300 字（卡片只显示摘要，完整介绍在详情里）</summary>
    private const string ListColumns = "Id, UploaderId, Title, LEFT(Description, 300) AS Description, Category, Tags, ObjectKey, FileName, Size, ContentType, Price, Status, ReviewNote, ReviewerId, ReviewedAt, DownloadCount, UniqueDownloadCount, ExpEarned, UploadId, PartSize, CreatedAt, UpdatedAt, PublishedAt, DeletedAt, Folder, Storage";

    // ---------- 读取助手 ----------
    /// <summary>CHAR(36) 列：MySqlConnector 默认读成 Guid，也兼容读成字符串的情况</summary>
    internal static Guid ReadGuid(DbDataReader r, int i)
    {
        var v = r.GetValue(i);
        return v is Guid g ? g : Guid.Parse(Convert.ToString(v, CultureInfo.InvariantCulture) ?? "");
    }
    private static Guid? ReadGuidOrNull(DbDataReader r, int i) => r.IsDBNull(i) ? null : ReadGuid(r, i);
    private static string? ReadStringOrNull(DbDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
    private static DateTime ReadTime(DbDataReader r, int i) => DateTime.SpecifyKind(r.GetDateTime(i), DateTimeKind.Utc);
    private static DateTime? ReadTimeOrNull(DbDataReader r, int i) => r.IsDBNull(i) ? null : ReadTime(r, i);

    private static ResourceRow ReadRow(DbDataReader r) => new()
    {
        Id = ReadGuid(r, 0),
        UploaderId = ReadGuid(r, 1),
        Title = r.GetString(2),
        Description = ReadStringOrNull(r, 3),
        Category = r.GetString(4),
        Tags = r.GetString(5),
        ObjectKey = r.GetString(6),
        FileName = r.GetString(7),
        Size = r.GetInt64(8),
        ContentType = r.GetString(9),
        Price = r.GetInt32(10),
        Status = r.GetString(11),
        ReviewNote = ReadStringOrNull(r, 12),
        ReviewerId = ReadGuidOrNull(r, 13),
        ReviewedAt = ReadTimeOrNull(r, 14),
        DownloadCount = r.GetInt32(15),
        UniqueDownloadCount = r.GetInt32(16),
        ExpEarned = r.GetInt64(17),
        UploadId = ReadStringOrNull(r, 18),
        PartSize = r.GetInt64(19),
        CreatedAt = ReadTime(r, 20),
        UpdatedAt = ReadTime(r, 21),
        PublishedAt = ReadTimeOrNull(r, 22),
        DeletedAt = ReadTimeOrNull(r, 23),
        // Folder 是后来加的列，放在 SELECT 列表的最后，前面的序号都不变
        Folder = ReadStringOrNull(r, 24),
        // Storage 也是后来加的，接在 Folder 后面
        Storage = ReadStringOrNull(r, 25),
    };

    private static async Task<List<ResourceRow>> ReadRowsAsync(MySqlCommand cmd, CancellationToken ct)
    {
        var list = new List<ResourceRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(ReadRow(r));
        return list;
    }

    private static MySqlCommand Command(MySqlConnection c, string sql, MySqlTransaction? tx = null, params (string Name, object? Value)[] ps)
    {
        var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        cmd.Transaction = tx;
        foreach (var (name, value) in ps) cmd.Parameters.AddWithValue(name, value ?? DBNull.Value);
        return cmd;
    }

    private async Task<int> ExecAsync(string sql, CancellationToken ct, params (string, object?)[] ps)
    {
        await using var c = await OpenAsync(ct);
        await using var cmd = Command(c, sql, null, ps);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    // ---------- 资源 ----------

    public async Task InsertAsync(ResourceRow x, CancellationToken ct)
    {
        await ExecAsync(@"INSERT INTO community_resources (Id, UploaderId, Title, Description, Category, Tags, ObjectKey, FileName, Size, ContentType, Price, Status, UploadId, PartSize, CreatedAt, UpdatedAt, Folder, Storage)
VALUES (@id, @u, @t, @d, @c, @tags, @k, @f, @s, @ct, @p, @st, @up, @ps, @now, @now, @folder, @storage)", ct, ("@folder", x.Folder), ("@storage", x.Storage),
            ("@id", x.Id.ToString()), ("@u", x.UploaderId.ToString()), ("@t", x.Title), ("@d", x.Description), ("@c", x.Category), ("@tags", x.Tags),
            ("@k", x.ObjectKey), ("@f", x.FileName), ("@s", x.Size), ("@ct", x.ContentType), ("@p", x.Price), ("@st", x.Status),
            ("@up", x.UploadId), ("@ps", x.PartSize), ("@now", x.CreatedAt));
    }

    public async Task<ResourceRow?> GetAsync(Guid id, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        await using var cmd = Command(c, $"SELECT {FullColumns} FROM community_resources WHERE Id=@id", null, ("@id", id.ToString()));
        return (await ReadRowsAsync(cmd, ct)).FirstOrDefault();
    }

    /// <summary>所有已公开的资源（列表页在内存里再搜索 / 排序 / 分页；社区规模几千条以内足够快）</summary>
    public async Task<List<ResourceRow>> ApprovedAsync(CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        await using var cmd = Command(c, $"SELECT {ListColumns} FROM community_resources WHERE Status='approved' AND DeletedAt IS NULL ORDER BY PublishedAt DESC LIMIT 5000");
        return await ReadRowsAsync(cmd, ct);
    }

    /// <summary>某人上传的全部资源（没删除的）</summary>
    public async Task<List<ResourceRow>> ByUploaderAsync(Guid uploaderId, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        await using var cmd = Command(c, $"SELECT {ListColumns} FROM community_resources WHERE UploaderId=@u AND DeletedAt IS NULL ORDER BY CreatedAt DESC LIMIT 500", null, ("@u", uploaderId.ToString()));
        return await ReadRowsAsync(cmd, ct);
    }

    /// <summary>某人各状态的资源数量（限制同时上传中 / 待审核的个数）</summary>
    public async Task<Dictionary<string, int>> CountMineAsync(Guid uploaderId, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        await using var cmd = Command(c, "SELECT Status, COUNT(*) FROM community_resources WHERE UploaderId=@u AND DeletedAt IS NULL GROUP BY Status", null, ("@u", uploaderId.ToString()));
        var map = new Dictionary<string, int>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) map[r.GetString(0)] = Convert.ToInt32(r.GetValue(1));
        return map;
    }

    /// <summary>管理面板：按状态分页（待审核按提交顺序，先来先审）</summary>
    public async Task<(List<ResourceRow> Items, int Total)> ByStatusAsync(string status, int page, int pageSize, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        int total;
        await using (var count = Command(c, "SELECT COUNT(*) FROM community_resources WHERE Status=@s AND DeletedAt IS NULL", null, ("@s", status)))
            total = Convert.ToInt32(await count.ExecuteScalarAsync(ct));
        var order = status == ResourceStatus.Pending ? "UpdatedAt ASC" : "UpdatedAt DESC";
        await using var cmd = Command(c, $"SELECT {ListColumns} FROM community_resources WHERE Status=@s AND DeletedAt IS NULL ORDER BY {order} LIMIT @o, @l", null,
            ("@s", status), ("@o", (page - 1) * pageSize), ("@l", pageSize));
        return (await ReadRowsAsync(cmd, ct), total);
    }

    /// <summary>各状态的数量（管理面板的标签页和待审核角标）</summary>
    public async Task<Dictionary<string, int>> CountsAsync(CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        await using var cmd = Command(c, "SELECT Status, COUNT(*) FROM community_resources WHERE DeletedAt IS NULL AND Status<>'uploading' GROUP BY Status");
        var map = new Dictionary<string, int>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) map[r.GetString(0)] = Convert.ToInt32(r.GetValue(1));
        return map;
    }

    /// <summary>传完了：上传中 → 待审核（只有还在「上传中」的才会变）</summary>
    public async Task<bool> MarkUploadedAsync(Guid id, CancellationToken ct) =>
        await ExecAsync("UPDATE community_resources SET Status='pending', UploadId=NULL, UpdatedAt=UTC_TIMESTAMP(6) WHERE Id=@id AND Status='uploading' AND DeletedAt IS NULL", ct, ("@id", id.ToString())) > 0;

    /// <summary>换一个上传编号（旧的失效时重新开始上传）</summary>
    public Task SetUploadIdAsync(Guid id, string? uploadId, CancellationToken ct) =>
        ExecAsync("UPDATE community_resources SET UploadId=@up, UpdatedAt=UTC_TIMESTAMP(6) WHERE Id=@id", ct, ("@id", id.ToString()), ("@up", uploadId));

    /// <summary>软删除：数据和 NAS 上的文件都保留，只是谁都看不到了。keepUploadId=true 表示分片还没清理掉，留给后台任务再试。</summary>
    public Task SoftDeleteAsync(Guid id, bool keepUploadId, CancellationToken ct) =>
        ExecAsync($"UPDATE community_resources SET DeletedAt=UTC_TIMESTAMP(6), UpdatedAt=UTC_TIMESTAMP(6){(keepUploadId ? "" : ", UploadId=NULL")} WHERE Id=@id AND DeletedAt IS NULL", ct, ("@id", id.ToString()));

    /// <summary>
    /// 审核类操作（通过 / 打回 / 下架 / 恢复）：只有当前状态在 fromStatuses 里才会改，防止两个管理员同时操作改乱。
    /// publish=true 时第一次公开记下 PublishedAt（社区动态按它排序，之后再通过也不会重复刷动态）。
    /// </summary>
    public async Task<bool> ReviewAsync(Guid id, string toStatus, IReadOnlyCollection<string> fromStatuses, Guid reviewerId, string? note, bool publish, CancellationToken ct)
    {
        var from = fromStatuses.ToList();
        var names = string.Join(", ", from.Select((_, i) => "@f" + i));
        var ps = new List<(string, object?)> { ("@id", id.ToString()), ("@s", toStatus), ("@r", reviewerId.ToString()), ("@n", note), ("@pub", publish ? 1 : 0) };
        ps.AddRange(from.Select((s, i) => ("@f" + i, (object?)s)));
        return await ExecAsync($@"UPDATE community_resources SET Status=@s, ReviewerId=@r, ReviewNote=@n, ReviewedAt=UTC_TIMESTAMP(6), UpdatedAt=UTC_TIMESTAMP(6),
  PublishedAt = CASE WHEN @pub=1 AND PublishedAt IS NULL THEN UTC_TIMESTAMP(6) ELSE PublishedAt END
WHERE Id=@id AND DeletedAt IS NULL AND Status IN ({names})", ct, ps.ToArray()) > 0;
    }

    /// <summary>上传者修改资料。toStatus 不为空时同时改状态（改了标题 / 介绍要重新审核）。</summary>
    public Task UpdateMetaAsync(Guid id, string title, string? description, string category, string tags, int price, string? folder, string? toStatus, CancellationToken ct) =>
        ExecAsync($@"UPDATE community_resources SET Title=@t, Description=@d, Category=@c, Tags=@tags, Price=@p, Folder=@folder, UpdatedAt=UTC_TIMESTAMP(6){(toStatus != null ? ", Status=@s, ReviewNote=NULL" : "")}
WHERE Id=@id AND DeletedAt IS NULL", ct,
            ("@id", id.ToString()), ("@t", title), ("@d", description), ("@c", category), ("@tags", tags), ("@p", price), ("@folder", folder), ("@s", toStatus));

    /// <summary>只改位置（管理员审核时整理用；位置只是展示用的，不需要重新审核）</summary>
    public async Task<bool> SetFolderAsync(Guid id, string? folder, CancellationToken ct) =>
        await ExecAsync("UPDATE community_resources SET Folder=@folder, UpdatedAt=UTC_TIMESTAMP(6) WHERE Id=@id AND DeletedAt IS NULL", ct, ("@id", id.ToString()), ("@folder", folder)) > 0;

    /// <summary>社区资源用过的位置（没删除的；没填位置的用分类名），给「选位置」的下拉框用</summary>
    public async Task<List<string>> UsedFoldersAsync(CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        await using var cmd = Command(c, "SELECT DISTINCT COALESCE(NULLIF(Folder, ''), Category) FROM community_resources WHERE DeletedAt IS NULL AND Status<>'uploading' LIMIT 2000");
        var list = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) if (!r.IsDBNull(0)) list.Add(r.GetString(0));
        return list;
    }

    // ---------- 分片进度 ----------

    public Task AddPartAsync(Guid id, int partNumber, long size, CancellationToken ct) =>
        ExecAsync(@"INSERT INTO community_resource_parts (ResourceId, PartNumber, Size, CompletedAt) VALUES (@id, @n, @s, UTC_TIMESTAMP(6))
ON DUPLICATE KEY UPDATE Size=VALUES(Size), CompletedAt=VALUES(CompletedAt)", ct, ("@id", id.ToString()), ("@n", partNumber), ("@s", size));

    public async Task<Dictionary<int, long>> PartsAsync(Guid id, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        await using var cmd = Command(c, "SELECT PartNumber, Size FROM community_resource_parts WHERE ResourceId=@id", null, ("@id", id.ToString()));
        var map = new Dictionary<int, long>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) map[r.GetInt32(0)] = r.GetInt64(1);
        return map;
    }

    public Task ClearPartsAsync(Guid id, CancellationToken ct) =>
        ExecAsync("DELETE FROM community_resource_parts WHERE ResourceId=@id", ct, ("@id", id.ToString()));

    /// <summary>需要后台清理的上传：开始超过 cutoff 还没传完的，或者已经删除 / 放弃但分片还没清掉的</summary>
    public async Task<List<ResourceRow>> StaleUploadsAsync(DateTime cutoffUtc, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        await using var cmd = Command(c, $"SELECT {FullColumns} FROM community_resources WHERE Status='uploading' AND UploadId IS NOT NULL AND (CreatedAt < @cut OR DeletedAt IS NOT NULL) LIMIT 200", null, ("@cut", cutoffUtc));
        return await ReadRowsAsync(cmd, ct);
    }

    /// <summary>清理完一条放弃的上传：去掉上传编号，标记删除（数据保留）</summary>
    public Task MarkAbandonedAsync(Guid id, CancellationToken ct) =>
        ExecAsync("UPDATE community_resources SET UploadId=NULL, DeletedAt=COALESCE(DeletedAt, UTC_TIMESTAMP(6)), UpdatedAt=UTC_TIMESTAMP(6) WHERE Id=@id", ct, ("@id", id.ToString()));

    // ---------- 购买 ----------

    /// <summary>这个人买过哪些资源</summary>
    public async Task<HashSet<Guid>> PurchasedIdsAsync(Guid userId, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        await using var cmd = Command(c, "SELECT ResourceId FROM community_resource_purchases WHERE UserId=@u", null, ("@u", userId.ToString()));
        var set = new HashSet<Guid>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) set.Add(ReadGuid(r, 0));
        return set;
    }

    public async Task<bool> HasPurchasedAsync(Guid resourceId, Guid userId, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        await using var cmd = Command(c, "SELECT 1 FROM community_resource_purchases WHERE ResourceId=@r AND UserId=@u LIMIT 1", null, ("@r", resourceId.ToString()), ("@u", userId.ToString()));
        return await cmd.ExecuteScalarAsync(ct) != null;
    }

    /// <summary>
    /// 买付费资源（一个数据库事务）：锁住下载者和上传者的经验记录 → 经验不够就什么都不做 → 记购买（资源 + 用户唯一，并发点两次也只扣一次）
    /// → 下载者扣经验、上传者加同样多 → 双方经验流水各一条（和现有扣经验 / 加经验的地方一样写 UserExpLog）→ 资源累计收入。
    /// 返回扣完后下载者剩下的经验（经验不够时返回现有经验）。
    /// </summary>
    public async Task<(PurchaseOutcome Outcome, long Balance)> PurchaseAsync(ResourceRow res, Guid buyerId, ExpTableNames t, CancellationToken ct)
    {
        var price = res.Price;
        await using var c = await OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);

        await using (var exists = Command(c, "SELECT 1 FROM community_resource_purchases WHERE ResourceId=@r AND UserId=@u LIMIT 1", tx, ("@r", res.Id.ToString()), ("@u", buyerId.ToString())))
        {
            if (await exists.ExecuteScalarAsync(ct) != null) { await tx.RollbackAsync(ct); return (PurchaseOutcome.AlreadyOwned, 0); }
        }

        // 一条语句锁住双方的经验记录（按主键顺序加锁，两个人互相买也不会死锁）
        var exp = new Dictionary<Guid, long>();
        await using (var lockCmd = Command(c, $"SELECT `{t.StatsUser}`, `{t.StatsExp}` FROM `{t.Stats}` WHERE `{t.StatsUser}` IN (@a, @b) ORDER BY `{t.StatsUser}` FOR UPDATE", tx,
                         ("@a", buyerId.ToString()), ("@b", res.UploaderId.ToString())))
        await using (var r = await lockCmd.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct)) exp[ReadGuid(r, 0)] = Convert.ToInt64(r.GetValue(1));
        }
        if (!exp.ContainsKey(buyerId) || !exp.ContainsKey(res.UploaderId)) { await tx.RollbackAsync(ct); return (PurchaseOutcome.MissingStats, 0); }
        if (exp[buyerId] < price) { await tx.RollbackAsync(ct); return (PurchaseOutcome.NotEnough, exp[buyerId]); }

        try
        {
            await using var ins = Command(c, "INSERT INTO community_resource_purchases (ResourceId, UserId, Price, CreatedAt) VALUES (@r, @u, @p, UTC_TIMESTAMP(6))", tx,
                ("@r", res.Id.ToString()), ("@u", buyerId.ToString()), ("@p", price));
            await ins.ExecuteNonQueryAsync(ct);
        }
        catch (MySqlException ex) when (ex.Number == 1062) // 同一个人同时点了两次：另一边已经买好了
        {
            await tx.RollbackAsync(ct);
            return (PurchaseOutcome.AlreadyOwned, exp[buyerId]);
        }

        var title = res.Title.Length > 40 ? res.Title[..40] + "…" : res.Title;
        await AddExpAsync(c, tx, t, buyerId, -price, $"购买资源《{title}》", ct);
        await AddExpAsync(c, tx, t, res.UploaderId, price, $"资源《{title}》被购买", ct);
        await using (var earn = Command(c, "UPDATE community_resources SET ExpEarned=ExpEarned+@p WHERE Id=@r", tx, ("@p", price), ("@r", res.Id.ToString())))
            await earn.ExecuteNonQueryAsync(ct);

        await tx.CommitAsync(ct);
        return (PurchaseOutcome.Bought, exp[buyerId] - price);
    }

    /// <summary>改经验（delta 可正可负）并记一条经验流水。返回是否真的改到了（没有经验记录的账号返回 false）。</summary>
    private static async Task<bool> AddExpAsync(MySqlConnection c, MySqlTransaction tx, ExpTableNames t, Guid userId, int delta, string reason, CancellationToken ct)
    {
        await using (var upd = Command(c, $"UPDATE `{t.Stats}` SET `{t.StatsExp}` = `{t.StatsExp}` + @d WHERE `{t.StatsUser}`=@u", tx, ("@d", delta), ("@u", userId.ToString())))
        {
            if (await upd.ExecuteNonQueryAsync(ct) == 0) return false;
        }
        await using var log = Command(c, $"INSERT INTO `{t.Log}` (`{t.LogUser}`, `{t.LogChange}`, `{t.LogReason}`, `{t.LogCreated}`) VALUES (@u, @d, @r, UTC_TIMESTAMP())", tx,
            ("@u", userId.ToString()), ("@d", delta), ("@r", reason.Length > 200 ? reason[..200] : reason));
        await log.ExecuteNonQueryAsync(ct);
        return true;
    }

    // ---------- 下载记录 / 奖励 ----------

    /// <summary>记一次下载（上传者自己下载、管理员审核预览不调用）。返回这是不是这个人第一次下载这个资源。</summary>
    public async Task<bool> RecordDownloadAsync(ResourceRow res, Guid userId, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        await using (var log = Command(c, "INSERT INTO community_resource_downloads (ResourceId, UserId, CreatedAt) VALUES (@r, @u, UTC_TIMESTAMP(6))", tx, ("@r", res.Id.ToString()), ("@u", userId.ToString())))
            await log.ExecuteNonQueryAsync(ct);
        int inserted;
        await using (var first = Command(c, "INSERT IGNORE INTO community_resource_first_downloads (ResourceId, UserId, UploaderId, RewardExp, CreatedAt) VALUES (@r, @u, @up, 0, UTC_TIMESTAMP(6))", tx,
                         ("@r", res.Id.ToString()), ("@u", userId.ToString()), ("@up", res.UploaderId.ToString())))
            inserted = await first.ExecuteNonQueryAsync(ct);
        await using (var count = Command(c, $"UPDATE community_resources SET DownloadCount=DownloadCount+1{(inserted > 0 ? ", UniqueDownloadCount=UniqueDownloadCount+1" : "")} WHERE Id=@r", tx, ("@r", res.Id.ToString())))
            await count.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return inserted > 0;
    }

    /// <summary>这个上传者今天已经从「免费资源被下载」拿了多少经验</summary>
    public async Task<int> FreeRewardTodayAsync(Guid uploaderId, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        await using var cmd = Command(c, "SELECT COALESCE(SUM(RewardExp),0) FROM community_resource_first_downloads WHERE UploaderId=@u AND CreatedAt >= @since", null,
            ("@u", uploaderId.ToString()), ("@since", ChinaClock.DayStartUtc(ChinaClock.Today)));
        return Convert.ToInt32(await cmd.ExecuteScalarAsync(ct));
    }

    /// <summary>
    /// 免费资源的下载奖励（一个事务）：上传者加经验 + 记经验流水 + 记下这次「首次下载」给了多少（算每日上限用）+ 累计到资源收入。
    /// 上传者没有经验记录时什么都不做。
    /// </summary>
    public async Task<bool> GrantFreeDownloadExpAsync(ResourceRow res, Guid downloaderId, int amount, ExpTableNames t, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        await using var tx = await c.BeginTransactionAsync(ct);
        var title = res.Title.Length > 30 ? res.Title[..30] + "…" : res.Title;
        if (!await AddExpAsync(c, tx, t, res.UploaderId, amount, $"资源《{title}》有新的人下载", ct)) { await tx.RollbackAsync(ct); return false; }
        await using (var a = Command(c, "UPDATE community_resource_first_downloads SET RewardExp=@a WHERE ResourceId=@r AND UserId=@u", tx, ("@a", amount), ("@r", res.Id.ToString()), ("@u", downloaderId.ToString())))
            await a.ExecuteNonQueryAsync(ct);
        await using (var b = Command(c, "UPDATE community_resources SET ExpEarned=ExpEarned+@a WHERE Id=@r", tx, ("@a", amount), ("@r", res.Id.ToString())))
            await b.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
        return true;
    }

    /// <summary>
    /// 审核通过的经验：每个资源只算一次（ExpRewardedAt 记下来，下架后再恢复也不会重复给），
    /// 每人每天最多 dailyCap 个资源有经验。返回：-1 = 这个资源已经算过；0 = 今天的名额用完了；>0 = 应该给的经验。
    /// </summary>
    public async Task<int> ClaimApproveExpAsync(ResourceRow res, int exp, int dailyCap, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        int already;
        await using (var count = Command(c, "SELECT COUNT(*) FROM community_resources WHERE UploaderId=@u AND ApproveExp>0 AND ExpRewardedAt >= @since", null,
                         ("@u", res.UploaderId.ToString()), ("@since", ChinaClock.DayStartUtc(ChinaClock.Today))))
            already = Convert.ToInt32(await count.ExecuteScalarAsync(ct));
        var grant = already < dailyCap ? exp : 0;
        await using var mark = Command(c, "UPDATE community_resources SET ExpRewardedAt=UTC_TIMESTAMP(6), ApproveExp=@e WHERE Id=@id AND ExpRewardedAt IS NULL", null,
            ("@e", grant), ("@id", res.Id.ToString()));
        return await mark.ExecuteNonQueryAsync(ct) > 0 ? grant : -1;
    }

    // ---------- 荣誉墙 / 社区动态 ----------

    public sealed record ContributorRow(Guid UploaderId, int Approved, long UniqueDownloads, long Downloads);

    /// <summary>资源贡献榜的原始数据：每个上传者公开了几个资源、一共被多少人 / 多少次下载</summary>
    public async Task<List<ContributorRow>> ContributorsAsync(CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        await using var cmd = Command(c, @"SELECT UploaderId, COUNT(*), COALESCE(SUM(UniqueDownloadCount),0), COALESCE(SUM(DownloadCount),0)
FROM community_resources WHERE Status='approved' AND DeletedAt IS NULL GROUP BY UploaderId");
        var list = new List<ContributorRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new ContributorRow(ReadGuid(r, 0), Convert.ToInt32(r.GetValue(1)), Convert.ToInt64(r.GetValue(2)), Convert.ToInt64(r.GetValue(3))));
        return list;
    }

    public sealed record FeedRow(Guid Id, string Title, Guid UploaderId, string Category, DateTime PublishedAt);

    /// <summary>社区动态：最近公开的资源</summary>
    public async Task<List<FeedRow>> RecentPublishedAsync(int take, CancellationToken ct)
    {
        await using var c = await OpenAsync(ct);
        await using var cmd = Command(c, "SELECT Id, Title, UploaderId, Category, PublishedAt FROM community_resources WHERE Status='approved' AND DeletedAt IS NULL AND PublishedAt IS NOT NULL ORDER BY PublishedAt DESC LIMIT @t", null,
            ("@t", Math.Clamp(take, 1, 200)));
        var list = new List<FeedRow>();
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct)) list.Add(new FeedRow(ReadGuid(r, 0), r.GetString(1), ReadGuid(r, 2), r.GetString(3), ReadTime(r, 4)));
        return list;
    }
}
