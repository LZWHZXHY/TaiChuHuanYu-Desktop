using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Models.Wiki;
using TaiChuWeb_V2.Utils;

namespace TaiChuWeb_V2.Services.Wiki
{
    public class WikiService
    {
        private readonly AppDbContext _context;

        public WikiService(AppDbContext context)
        {
            _context = context;
        }

        #region 1. 词条管理 & 提交修订提案

        /// <summary>
        /// 创建新词条并提交初始版本提案
        /// </summary>
        public async Task<string> CreateArticleAsync(string title, int categoryId, string authorId, List<BlockDto> blocks)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // 1. 创建词条元数据
                var article = new WikiArticle
                {
                    Id = Guid.NewGuid().ToString(),
                    CategoryId = categoryId,
                    Title = title,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                };
                _context.WikiArticles.Add(article);

                // 2. 创建初始版本提案
                var revision = new WikiArticleRevision
                {
                    ArticleId = article.Id,
                    ContributorId = authorId,
                    CategoryId = categoryId,
                    Title = title,
                    EditSummary = "创建初始词条",
                    Status = 0, // 待审核
                    CreatedAt = DateTime.UtcNow
                };
                _context.WikiArticleRevisions.Add(revision);
                await _context.SaveChangesAsync(); // 拿到自增的 Revision.Id

                // 3. 将内容块写入草稿表，标记 Owner 为该 Revision
                await SaveRevisionBlocksAsync(revision.Id, blocks);

                await transaction.CommitAsync();
                return article.Id;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        /// <summary>
        /// 对已有词条提交新的修订提案
        /// </summary>
        public async Task<int> SubmitRevisionAsync(string articleId, string title, int categoryId, string authorId, string summary, List<BlockDto> blocks)
        {
            var article = await _context.WikiArticles.FindAsync(articleId);
            if (article == null || article.IsDeleted) throw new Exception("词条不存在或已被删除");

            var revision = new WikiArticleRevision
            {
                ArticleId = articleId,
                ContributorId = authorId,
                CategoryId = categoryId,
                Title = title,
                EditSummary = summary,
                Status = 0, // 待审核
                CreatedAt = DateTime.UtcNow
            };

            _context.WikiArticleRevisions.Add(revision);
            await _context.SaveChangesAsync();

            // 写入多态草稿表
            await SaveRevisionBlocksAsync(revision.Id, blocks);
            return revision.Id;
        }

        #endregion

        #region 2. 审核发布流转 (灵脉多态核心逻辑)

        /// <summary>
        /// 审核修订提案
        /// </summary>
        /// <summary>
        /// 审核修订提案
        /// </summary>
        public async Task ReviewRevisionAsync(int revisionId, string reviewerId, bool approve, string? remarks = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var revision = await _context.WikiArticleRevisions.FindAsync(revisionId);
                if (revision == null || revision.Status != 0) throw new Exception("提案不存在或已被处理");

                var article = await _context.WikiArticles.FindAsync(revision.ArticleId);
                if (article == null || article.IsDeleted) throw new Exception("词条不存在");

                revision.ReviewerId = reviewerId;
                revision.ReviewRemarks = remarks;
                revision.ReviewedAt = DateTime.UtcNow;

                if (approve)
                {
                    revision.Status = 1; // 已通过

                    // 1. 从草稿区(blocks)拉取该提案的内容
                    var revBlocks = await _context.Blocks
                        .Where(b => b.OwnerId == revisionId.ToString() && b.OwnerType == LingMaiOwnerTypes.WikiRevision)
                        .ToListAsync();

                    // 2. 清理该词条在发布区(PublishedBlocks)的旧数据
                    var oldPubBlocks = _context.PublishedBlocks
                        .Where(pb => pb.OwnerId == article.Id && pb.OwnerType == LingMaiOwnerTypes.WikiArticle);
                    _context.PublishedBlocks.RemoveRange(oldPubBlocks);

                    // 3. 将提案内容克隆并写入发布区(PublishedBlocks)
                    var pubBlocks = revBlocks.Select(b => {
                        // 🌟 修复点 1：安全转换 SortOrder 为数字，失败则默认为 0
                        int.TryParse(b.SortOrder, out int parsedSortOrder);

                        return new PublishedBlock
                        {
                            // 🌟 修复点 2：如果你的 PublishedBlock.Id 是 Guid，用 Guid.Parse(b.Id)。
                            // 如果它是 string，就保持 b.Id 不变。
                            Id = Guid.TryParse(b.Id, out var guidId) ? guidId : Guid.NewGuid(),
                            OwnerId = article.Id,
                            OwnerType = LingMaiOwnerTypes.WikiArticle,
                            Type = b.Type,
                            Data = b.Data,
                            SortOrder = parsedSortOrder
                        };
                    }).ToList();

                    _context.PublishedBlocks.AddRange(pubBlocks);

                    // 4. 更新词条元数据，指向最新审核通过的版本
                    article.Title = revision.Title;
                    article.CategoryId = revision.CategoryId;
                    article.CurrentRevisionId = revision.Id;
                    article.UpdatedAt = DateTime.UtcNow;
                }
                else
                {
                    revision.Status = 2; // 已拒绝
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        #endregion

        #region 3. 辅助私有方法

        private async Task SaveRevisionBlocksAsync(int revisionId, List<BlockDto> blocks)
        {
            var newBlocks = blocks.Select(b => new Block
            {
                Id = b.Id ?? Guid.NewGuid().ToString(),
                OwnerId = revisionId.ToString(),
                OwnerType = LingMaiOwnerTypes.WikiRevision,
                Type = b.Type,
                Data = b.Data,
                SortOrder = b.SortOrder ?? "",
                UpdatedAt = DateTime.UtcNow
            }).ToList();

            _context.Blocks.AddRange(newBlocks);
            await _context.SaveChangesAsync();
        }

        #endregion
    }

    // DTO 定义，用于接收前端编辑器发送的内容
    public class BlockDto
    {
        public string? Id { get; set; }
        public string Type { get; set; } = "paragraph";
        public string Data { get; set; } = "{}";
        public string? SortOrder { get; set; }
    }
}