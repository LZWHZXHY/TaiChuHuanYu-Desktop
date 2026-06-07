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

        public async Task<IActionResult> ExecutePublishAsync(Guid noteId, string userId, int? categoryId)
        {
            return await _context.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
            {
                var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId);
                if (note == null) return new NotFoundObjectResult(new { message = "草稿不存在" });

                // 🌟【重点修复】：OwnerType 必须与前端保存时一致！通常是 "art" 或 "note"
                // 请去数据库查一下 blocks 表，看看你的艺术品 block 的 OwnerType 到底写的是什么
                var artBlocks = await _context.Blocks
                    .Where(b => b.OwnerId == noteId.ToString() && b.OwnerType == "note") // 🌟 必须改为 "note"
                    .OrderBy(b => b.SortOrder)
                    .ToListAsync();

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    var artwork = await _context.Artworks.FirstOrDefaultAsync(a => a.OriginalNoteId == noteId);
                    bool isNew = artwork == null;
                    if (isNew)
                    {
                        artwork = new Artwork { OriginalNoteId = noteId, UploaderId = Guid.Parse(userId), Title = note.Title, UploadAt = DateTime.UtcNow };
                        _context.Artworks.Add(artwork);
                    }
                    artwork.Title = note.Title;

                    // 🌟【重点修复】：处理总结块 (art-summary)
                    var summaryBlock = artBlocks.FirstOrDefault(b => b.Type == "art-summary");
                    if (summaryBlock != null)
                    {
                        var summaryData = JsonSerializer.Deserialize<JsonElement>(summaryBlock.Data);
                        artwork.Description = summaryData.TryGetProperty("text", out var t) ? t.GetString() : "";
                    }

                    // 同步图片
                    var existingImages = await _context.ArtworkImages.Where(i => i.ArtworkId == artwork.Id).ToListAsync();
                    _context.ArtworkImages.RemoveRange(existingImages);

                    foreach (var block in artBlocks.Where(b => b.Type == "image"))
                    {
                        var data = JsonSerializer.Deserialize<JsonElement>(block.Data);
                        var attrs = data.GetProperty("attrs");
                        var url = attrs.GetProperty("src").GetString();

                        // 🌟 提取 caption 并处理 null 安全
                        string caption = attrs.TryGetProperty("caption", out var cap) ? cap.GetString() ?? "" : "";

                        _context.ArtworkImages.Add(new ArtworkImage
                        {
                            Artwork = artwork,
                            ImageUrl = url ?? "",
                            Caption = caption, // 🌟 存入刚才新增的数据库字段
                            IsCover = block.SortOrder == 0
                        });
                    }

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