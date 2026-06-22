using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Models.Artwork;

namespace TaiChuWeb_V2.Services.Publish
{
    public class ArtPublishHandler : ILingMaiPublishHandler
    {
        private readonly AppDbContext _context;
        public string SupportType => "art";

        public ArtPublishHandler(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> ExecutePublishAsync(Guid noteId, string userId, int? categoryId, string? projectId = null)
        {
            return await _context.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
            {
                // 1. 获取灵脉草稿
                var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId);
                if (note == null) return new NotFoundObjectResult(new { message = "草稿不存在" });

                // 2. 🌟【核心修复点】：OwnerType 必须与前端 WorkspaceArt.vue 保存时抛出的 "art" 像素级一致！
                // 这样才能从 blocks 表里精确把用户上传的多张画幅和创作总览捞出来
                var artBlocks = await _context.Blocks
                    .Where(b => b.OwnerId == noteId.ToString() && b.OwnerType == note.Type)
                    .OrderBy(b => b.SortOrder)
                    .ToListAsync();

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var artwork = await _context.Artworks.FirstOrDefaultAsync(a => a.OriginalNoteId == noteId);
                    bool isNew = artwork == null;
                    if (isNew)
                    {
                        artwork = new Artwork
                        {
                            OriginalNoteId = noteId,
                            UploaderId = Guid.Parse(userId),
                            Title = note.Title,
                            UploadAt = DateTime.UtcNow
                        };
                        _context.Artworks.Add(artwork);
                    }
                    artwork.Title = note.Title;

                    // 3. 🌟【数据对齐】：处理创作总结块 (art-summary)
                    var summaryBlock = artBlocks.FirstOrDefault(b => b.Type == "art-summary");
                    if (summaryBlock != null)
                    {
                        var summaryData = JsonSerializer.Deserialize<JsonElement>(summaryBlock.Data);
                        artwork.Description = summaryData.TryGetProperty("text", out var t) ? t.GetString() : "";
                    }
                    else
                    {
                        artwork.Description = ""; // 兜底
                    }

                    // 4. 同步关联画幅图片
                    var existingImages = await _context.ArtworkImages.Where(i => i.ArtworkId == artwork.Id).ToListAsync();
                    _context.ArtworkImages.RemoveRange(existingImages);

                    foreach (var block in artBlocks.Where(b => b.Type == "image"))
                    {
                        var data = JsonSerializer.Deserialize<JsonElement>(block.Data);
                        var attrs = data.GetProperty("attrs");
                        var url = attrs.GetProperty("src").GetString();

                        // 提取 caption 并处理 null 安全
                        string caption = attrs.TryGetProperty("caption", out var cap) ? cap.GetString() ?? "" : "";

                        _context.ArtworkImages.Add(new ArtworkImage
                        {
                            Artwork = artwork,
                            ImageUrl = url ?? "",
                            Caption = caption,
                            IsCover = block.SortOrder == 0 // 第一张图自动作为封面图
                        });
                    }

                    // 5. 变更灵脉原草稿的发布状态
                    note.IsPublic = true;
                    note.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();

                    return new OkObjectResult(new { success = true });
                }
                catch (Exception ex)
                {
                    await transaction.RollbackAsync();
                    return new BadRequestObjectResult(new { message = ex.Message });
                }
            });
        }
    }
}