using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Models.Artwork;
using TaiChuWeb_V2.Services;
using COSXML;
using COSXML.Auth;
using COSXML.Model.Object;

namespace TaiChuWeb_V2.Services.Publish
{
    public class ArtPublishHandler : ILingMaiPublishHandler
    {
        private readonly AppDbContext _context;
        private readonly WatermarkService _watermarkService;
        private readonly IConfiguration _configuration;

        public string SupportType => "art";

        public ArtPublishHandler(
            AppDbContext context,
            WatermarkService watermarkService,
            IConfiguration configuration)
        {
            _context = context;
            _watermarkService = watermarkService;
            _configuration = configuration;
        }

        public async Task<IActionResult> ExecutePublishAsync(Guid noteId, string userId, int? categoryId, string? projectId = null)
        {
            return await _context.Database.CreateExecutionStrategy().ExecuteAsync<IActionResult>(async () =>
            {
                // 1. 获取灵脉草稿
                var note = await _context.Notes.FirstOrDefaultAsync(n => n.Id == noteId);
                if (note == null) return new NotFoundObjectResult(new { message = "草稿不存在" });

                // 2. 获取所有 blocks
                var artBlocks = await _context.Blocks
                    .Where(b => b.OwnerId == noteId.ToString() && b.OwnerType == note.Type)
                    .OrderBy(b => b.SortOrder)
                    .ToListAsync();

                using var transaction = await _context.Database.BeginTransactionAsync();
                try
                {
                    // 3. 创建或获取 Artwork 实体
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

                    // ========== 4. 解析完整水印配置 ==========
                    var metaBlock = artBlocks.FirstOrDefault(b => b.Type == "art-collection-meta");
                    bool hasWatermark = false;
                    string? watermarkText = null;
                    string? watermarkImageUrl = null;
                    string watermarkType = "text";
                    string position = "bottom-right";
                    int fontSize = 14;
                    double opacity = 0.6;
                    string color = "#ffffff";
                    int rotation = 0;
                    double imageScale = 0.3;
                    double imageOpacity = 0.6;

                    if (metaBlock != null)
                    {
                        try
                        {
                            using var doc = JsonDocument.Parse(metaBlock.Data);
                            var root = doc.RootElement;
                            hasWatermark = root.TryGetProperty("watermarkEnabled", out var we) && we.ValueKind == JsonValueKind.True;
                            if (hasWatermark)
                            {
                                watermarkText = root.TryGetProperty("watermarkText", out var wt) ? wt.GetString() : note.Title;
                                watermarkImageUrl = root.TryGetProperty("watermarkImageUrl", out var wiu) ? wiu.GetString() : null;
                                watermarkType = root.TryGetProperty("watermarkType", out var wmType) ? wmType.GetString() ?? "text" : "text";
                                position = root.TryGetProperty("watermarkPosition", out var wp) ? wp.GetString() ?? "bottom-right" : "bottom-right";
                                fontSize = root.TryGetProperty("watermarkFontSize", out var fs) && fs.ValueKind == JsonValueKind.Number ? fs.GetInt32() : 14;
                                opacity = root.TryGetProperty("watermarkOpacity", out var op) && op.ValueKind == JsonValueKind.Number ? (double)op.GetDecimal() : 0.6;
                                color = root.TryGetProperty("watermarkColor", out var wc) ? wc.GetString() ?? "#ffffff" : "#ffffff";
                                rotation = root.TryGetProperty("watermarkRotation", out var wr) && wr.ValueKind == JsonValueKind.Number ? wr.GetInt32() : 0;
                                imageScale = root.TryGetProperty("watermarkImageScale", out var wis) && wis.ValueKind == JsonValueKind.Number ? (double)wis.GetDecimal() : 0.3;
                                imageOpacity = root.TryGetProperty("watermarkImageOpacity", out var wio) && wio.ValueKind == JsonValueKind.Number ? (double)wio.GetDecimal() : 0.6;
                            }
                        }
                        catch { /* 忽略解析错误 */ }
                    }

                    // ========== 5. 保存水印配置到 Artwork 表（供详情接口使用） ==========
                    artwork.WatermarkType = watermarkType;
                    artwork.WatermarkEnabled = hasWatermark;
                    artwork.WatermarkText = watermarkText ?? note.Title;
                    artwork.WatermarkPosition = position;
                    artwork.WatermarkFontSize = fontSize;
                    artwork.WatermarkOpacity = opacity;
                    artwork.WatermarkColor = color;
                    artwork.WatermarkRotation = rotation;
                    artwork.WatermarkImageUrl = watermarkImageUrl;
                    artwork.WatermarkImageWidth = 120;   // 默认值，前端会上传真实尺寸
                    artwork.WatermarkImageHeight = 120;
                    artwork.WatermarkImageScale = imageScale;
                    artwork.WatermarkImageOpacity = imageOpacity;

                    // ========== 6. 处理创作总结块 ==========
                    var summaryBlock = artBlocks.FirstOrDefault(b => b.Type == "art-summary");
                    if (summaryBlock != null)
                    {
                        var summaryData = JsonSerializer.Deserialize<JsonElement>(summaryBlock.Data);
                        artwork.Description = summaryData.TryGetProperty("text", out var t) ? t.GetString() : "";
                    }
                    else
                    {
                        artwork.Description = "";
                    }

                    // ========== 7. 删除旧的关联图片 ==========
                    var existingImages = await _context.ArtworkImages.Where(i => i.ArtworkId == artwork.Id).ToListAsync();
                    _context.ArtworkImages.RemoveRange(existingImages);

                    // ========== 8. 处理每一张图片（应用水印并上传） ==========
                    foreach (var block in artBlocks.Where(b => b.Type == "image"))
                    {
                        var data = JsonSerializer.Deserialize<JsonElement>(block.Data);
                        var attrs = data.GetProperty("attrs");
                        var url = attrs.GetProperty("src").GetString();
                        string caption = attrs.TryGetProperty("caption", out var cap) ? cap.GetString() ?? "" : "";

                        string finalImageUrl = url;

                        // 如果启用水印且 URL 有效
                        if (hasWatermark && !string.IsNullOrEmpty(url))
                        {
                            try
                            {
                                // 合成水印
                                var watermarkedBytes = await _watermarkService.ApplyWatermarkAsync(
                                    url,
                                    watermarkText,
                                    watermarkImageUrl,
                                    watermarkType,
                                    position,
                                    fontSize,
                                    opacity,
                                    color,
                                    rotation,
                                    imageScale,
                                    imageOpacity
                                );

                                // 生成文件名并上传到 COS
                                var fileName = $"watermarked_{Guid.NewGuid()}.png";
                                finalImageUrl = await UploadToCosAsync(watermarkedBytes, fileName, "artwork/watermarked");
                            }
                            catch (Exception ex)
                            {
                                // 水印合成失败，保留原图
                                Console.WriteLine($"水印合成/上传失败: {ex.Message}");
                            }
                        }

                        // 添加到数据库
                        _context.ArtworkImages.Add(new ArtworkImage
                        {
                            Artwork = artwork,
                            ImageUrl = finalImageUrl,
                            Caption = caption,
                            IsCover = block.SortOrder == 0 // 第一张图为封面
                        });
                    }

                    // ========== 9. 变更灵脉原草稿的发布状态 ==========
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

        // ========== 私有方法：复用 CosController 配置上传到 COS ==========
        private async Task<string> UploadToCosAsync(byte[] data, string fileName, string folder)
        {
            var secretId = _configuration["TencentCloud:SecretId"];
            var secretKey = _configuration["TencentCloud:SecretKey"];
            var bucket = _configuration["TencentCloud:COS:Bucket"];
            var region = _configuration["TencentCloud:COS:Region"];
            var appId = _configuration["TencentCloud:COS:AppId"];
            var baseUrl = _configuration["TencentCloud:COS:BaseUrl"] ?? $"https://{bucket}.cos.{region}.myqcloud.com";

            var cosConfig = new CosXmlConfig.Builder()
                .IsHttps(true)
                .SetRegion(region)
                .SetAppid(appId)
                .Build();

            var credentialProvider = new DefaultQCloudCredentialProvider(secretId, secretKey, 60 * 60);
            var cosXml = new CosXmlServer(cosConfig, credentialProvider);

            string cosPath = string.IsNullOrEmpty(folder) ? fileName : $"{folder.TrimEnd('/')}/{fileName}";

            int maxRetries = 3;
            int retryDelayMs = 1000;

            for (int attempt = 1; attempt <= maxRetries; attempt++)
            {
                try
                {
                    using var stream = new MemoryStream(data);
                    var request = new PutObjectRequest(bucket, cosPath, stream);
                    request.SetRequestHeader("x-cos-acl", "public-read");

                    var result = await Task.Run(() => cosXml.PutObject(request));

                    if (result.httpCode == 200)
                    {
                        return $"{baseUrl}/{cosPath}";
                    }

                    throw new Exception($"COS 上传失败: HTTP {result.httpCode}");
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    // 网络相关错误重试
                    if (ex is COSXML.CosException.CosClientException ||
                        ex is System.IO.IOException ||
                        ex.InnerException is System.IO.IOException)
                    {
                        Console.WriteLine($"COS 上传第 {attempt} 次重试: {ex.Message}");
                        await Task.Delay(retryDelayMs * attempt);
                        continue;
                    }
                    throw;
                }
            }

            throw new Exception("COS 上传失败，已重试多次");
        }
    }
}