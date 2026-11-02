using System.Data;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Services.Resources;

/// <summary>
/// 启动时创建「社区资源」的几张表（不需要 EF 迁移，表已存在就跳过）：
/// · community_resources：资源本身（标题、介绍、价格、审核状态、下载数、收入、分片上传编号……）
/// · community_resource_parts：分片上传进度（哪些片传完了），页面刷新后可以接着传
/// · community_resource_purchases：谁买过哪个付费资源（资源 + 用户唯一，防止重复扣经验）
/// · community_resource_downloads：每次下载的记录
/// · community_resource_first_downloads：每个用户第一次下载某资源（算「多少人下载过」、免费资源给上传者的经验奖励只给一次）
/// </summary>
public class CommunityResourceSchema(IServiceScopeFactory scopes, ILogger<CommunityResourceSchema> logger) : IHostedService
{
    private const string ResourcesSql = @"CREATE TABLE IF NOT EXISTS community_resources (
  Id CHAR(36) NOT NULL,
  UploaderId CHAR(36) NOT NULL,
  Title VARCHAR(60) NOT NULL DEFAULT '',
  Description TEXT NULL,
  Category VARCHAR(16) NOT NULL DEFAULT '其他',
  Folder VARCHAR(200) NULL,
  Storage VARCHAR(16) NULL,
  Tags VARCHAR(300) NOT NULL DEFAULT '',
  ObjectKey VARCHAR(400) NOT NULL,
  FileName VARCHAR(200) NOT NULL,
  Size BIGINT NOT NULL DEFAULT 0,
  ContentType VARCHAR(120) NOT NULL DEFAULT 'application/octet-stream',
  Price INT NOT NULL DEFAULT 0,
  Status VARCHAR(16) NOT NULL DEFAULT 'uploading',
  ReviewNote VARCHAR(300) NULL,
  ReviewerId CHAR(36) NULL,
  ReviewedAt DATETIME(6) NULL,
  DownloadCount INT NOT NULL DEFAULT 0,
  UniqueDownloadCount INT NOT NULL DEFAULT 0,
  ExpEarned BIGINT NOT NULL DEFAULT 0,
  UploadId VARCHAR(255) NULL,
  PartSize BIGINT NOT NULL DEFAULT 0,
  ApproveExp INT NOT NULL DEFAULT 0,
  ExpRewardedAt DATETIME(6) NULL,
  CreatedAt DATETIME(6) NOT NULL,
  UpdatedAt DATETIME(6) NOT NULL,
  PublishedAt DATETIME(6) NULL,
  DeletedAt DATETIME(6) NULL,
  PRIMARY KEY (Id),
  KEY IX_cres_status (Status, DeletedAt, PublishedAt),
  KEY IX_cres_uploader (UploaderId, DeletedAt),
  KEY IX_cres_created (Status, CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    private const string PartsSql = @"CREATE TABLE IF NOT EXISTS community_resource_parts (
  ResourceId CHAR(36) NOT NULL,
  PartNumber INT NOT NULL,
  Size BIGINT NOT NULL DEFAULT 0,
  CompletedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (ResourceId, PartNumber)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    private const string PurchasesSql = @"CREATE TABLE IF NOT EXISTS community_resource_purchases (
  Id BIGINT NOT NULL AUTO_INCREMENT,
  ResourceId CHAR(36) NOT NULL,
  UserId CHAR(36) NOT NULL,
  Price INT NOT NULL DEFAULT 0,
  CreatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY UX_cres_purchase (ResourceId, UserId),
  KEY IX_cres_purchase_user (UserId)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    private const string DownloadsSql = @"CREATE TABLE IF NOT EXISTS community_resource_downloads (
  Id BIGINT NOT NULL AUTO_INCREMENT,
  ResourceId CHAR(36) NOT NULL,
  UserId CHAR(36) NOT NULL,
  CreatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (Id),
  KEY IX_cres_dl_resource (ResourceId, CreatedAt),
  KEY IX_cres_dl_user (UserId, CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    private const string FirstDownloadsSql = @"CREATE TABLE IF NOT EXISTS community_resource_first_downloads (
  ResourceId CHAR(36) NOT NULL,
  UserId CHAR(36) NOT NULL,
  UploaderId CHAR(36) NOT NULL,
  RewardExp INT NOT NULL DEFAULT 0,
  CreatedAt DATETIME(6) NOT NULL,
  PRIMARY KEY (ResourceId, UserId),
  KEY IX_cres_first_uploader (UploaderId, CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4";

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var connection = db.Database.GetDbConnection();
            var opened = connection.State != ConnectionState.Open;
            if (opened) await connection.OpenAsync(cancellationToken);
            try
            {
                foreach (var sql in new[] { ResourcesSql, PartsSql, PurchasesSql, DownloadsSql, FirstDownloadsSql })
                {
                    await using var command = connection.CreateCommand();
                    command.CommandText = sql;
                    await command.ExecuteNonQueryAsync(cancellationToken);
                }
                // 后来加的列：表已经在库里（已有用户上传的数据）时，CREATE TABLE IF NOT EXISTS 不会改表，要单独补列，已有数据不动
                foreach (var (table, column, definition) in new[]
                {
                    ("community_resources", "Folder", "VARCHAR(200) NULL AFTER Category"), // 位置（展示用的虚拟文件夹）
                    ("community_resources", "Storage", "VARCHAR(16) NULL"), // 文件存在哪：NULL / nas = NAS，cos = 腾讯云
                })
                {
                    await using var check = connection.CreateCommand();
                    check.CommandText = $"SELECT COUNT(*) FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{table}' AND COLUMN_NAME = '{column}'";
                    if (Convert.ToInt64(await check.ExecuteScalarAsync(cancellationToken)) > 0) continue;
                    await using var alter = connection.CreateCommand();
                    alter.CommandText = $"ALTER TABLE {table} ADD COLUMN {column} {definition}";
                    await alter.ExecuteNonQueryAsync(cancellationToken);
                }
            }
            finally { if (opened) await connection.CloseAsync(); }
        }
        catch (Exception ex) { logger.LogError(ex, "社区资源表创建失败，知识库的上传 / 下载资源会出错"); }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
