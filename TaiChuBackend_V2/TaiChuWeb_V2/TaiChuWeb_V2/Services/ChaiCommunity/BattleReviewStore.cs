using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.ChaiCommunity.Battle;

namespace TaiChuWeb_V2.Services.ChaiCommunity
{
    /// <summary>
    /// 约战作品审核：每件提交的作品（链接）要管理员审核通过后，路人才看得到链接。
    /// 审核状态单独存一张表 battle_submission_reviews（不改原来的 BattleSubmissions 表，不用迁移）：
    /// pending 待审核 / approved 通过 / rejected 打回（Note 写原因）。
    /// 表第一次创建时，把已经存在的作品都记为「通过」，老约战的作品照常显示。
    /// </summary>
    public static class BattleReviewStore
    {
        public const string Pending = "pending", Approved = "approved", Rejected = "rejected";

        public sealed record Review(string Status, string? Note, Guid? ReviewerId, DateTime? ReviewedAt);

        private static async Task<DbConnection> OpenAsync(AppDbContext db)
        {
            var conn = db.Database.GetDbConnection();
            if (conn.State != ConnectionState.Open) await db.Database.OpenConnectionAsync();
            return conn;
        }

        /// <summary>启动时建表；如果是第一次建，把现有作品都记为通过</summary>
        public static async Task EnsureAsync(AppDbContext db, CancellationToken ct = default)
        {
            var conn = await OpenAsync(db);
            await using (var check = conn.CreateCommand())
            {
                check.CommandText = "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'battle_submission_reviews'";
                if (Convert.ToInt64(await check.ExecuteScalarAsync(ct)) > 0) return;
            }
            await using (var create = conn.CreateCommand())
            {
                create.CommandText = @"CREATE TABLE IF NOT EXISTS battle_submission_reviews (
  SubmissionId VARCHAR(64) NOT NULL PRIMARY KEY,
  Status VARCHAR(16) NOT NULL,
  Note VARCHAR(300) NULL,
  ReviewerId CHAR(36) NULL,
  ReviewedAt DATETIME NULL,
  UpdatedAt DATETIME NOT NULL,
  KEY IX_battle_submission_reviews_status (Status, UpdatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";
                await create.ExecuteNonQueryAsync(ct);
            }
            var table = db.Model.FindEntityType(typeof(BattleSubmission))?.GetTableName() ?? "BattleSubmissions";
            await using var seed = conn.CreateCommand();
            seed.CommandText = $"INSERT IGNORE INTO battle_submission_reviews (SubmissionId, Status, Note, ReviewedAt, UpdatedAt) SELECT Id, 'approved', '审核功能上线前提交的作品', UTC_TIMESTAMP(), UTC_TIMESTAMP() FROM `{table}`";
            await seed.ExecuteNonQueryAsync(ct);
        }

        /// <summary>按作品编号取审核状态（没有记录的不在结果里，调用方按「待审核」处理）</summary>
        public static async Task<Dictionary<string, Review>> GetAsync(AppDbContext db, IReadOnlyCollection<string> ids)
        {
            var result = new Dictionary<string, Review>(StringComparer.OrdinalIgnoreCase);
            if (ids.Count == 0) return result;
            var conn = await OpenAsync(db);
            await using var cmd = conn.CreateCommand();
            var names = new List<string>();
            var i = 0;
            foreach (var id in ids.Distinct())
            {
                var p = cmd.CreateParameter(); p.ParameterName = "@p" + i; p.Value = id; cmd.Parameters.Add(p);
                names.Add("@p" + i); i++;
            }
            cmd.CommandText = $"SELECT SubmissionId, Status, Note, ReviewerId, ReviewedAt FROM battle_submission_reviews WHERE SubmissionId IN ({string.Join(",", names)})";
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                result[r.GetString(0)] = new Review(r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2),
                    r.IsDBNull(3) ? null : Guid.TryParse(Convert.ToString(r.GetValue(3)), out var g) ? g : null,
                    r.IsDBNull(4) ? null : DateTime.SpecifyKind(r.GetDateTime(4), DateTimeKind.Utc));
            return result;
        }

        public static async Task SetAsync(AppDbContext db, string submissionId, string status, string? note, Guid? reviewerId)
        {
            var conn = await OpenAsync(db);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO battle_submission_reviews (SubmissionId, Status, Note, ReviewerId, ReviewedAt, UpdatedAt)
VALUES (@id, @s, @n, @r, @at, UTC_TIMESTAMP())
ON DUPLICATE KEY UPDATE Status = @s, Note = @n, ReviewerId = @r, ReviewedAt = @at, UpdatedAt = UTC_TIMESTAMP()";
            void Add(string name, object? value) { var p = cmd.CreateParameter(); p.ParameterName = name; p.Value = value ?? DBNull.Value; cmd.Parameters.Add(p); }
            Add("@id", submissionId);
            Add("@s", status);
            Add("@n", string.IsNullOrWhiteSpace(note) ? null : note.Trim().Length > 300 ? note.Trim()[..300] : note.Trim());
            Add("@r", reviewerId?.ToString());
            Add("@at", status == Pending ? null : DateTime.UtcNow);
            await cmd.ExecuteNonQueryAsync();
        }

        /// <summary>待审核的作品编号（最早的在前）</summary>
        public static async Task<List<string>> PendingIdsAsync(AppDbContext db, int limit = 200)
        {
            var conn = await OpenAsync(db);
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT SubmissionId FROM battle_submission_reviews WHERE Status = 'pending' ORDER BY UpdatedAt LIMIT " + Math.Clamp(limit, 1, 500);
            var list = new List<string>();
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) list.Add(r.GetString(0));
            return list;
        }
    }

    /// <summary>启动时建审核表</summary>
    public sealed class BattleReviewSchema(IServiceScopeFactory scopes, ILogger<BattleReviewSchema> logger) : IHostedService
    {
        public async Task StartAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await BattleReviewStore.EnsureAsync(db, cancellationToken);
            }
            catch (Exception ex) { logger.LogError(ex, "约战作品审核表创建失败"); }
        }

        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
