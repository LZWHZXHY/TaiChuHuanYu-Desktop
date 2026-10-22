using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.DTOs.Artwork;
using TaiChuWeb_V2.Models.Artwork;
using TaiChuWeb_V2.Models.LingMai;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Models.Tag;
using TaiChuWeb_V2.Services.Cos;

namespace TaiChuWeb_V2.Services.Artworks;

public class ArtworkPublicationService(AppDbContext context, CosService cos, WatermarkService watermark,
    ILogger<ArtworkPublicationService>? logger = null)
{
    public static Task<bool> CanManageAsync(AppDbContext db, Guid? userId) => db.UserPermissions.AnyAsync(p =>
        p.UserId == userId && (p.Permission == AdminPermission.SuperAdmin || p.Permission == AdminPermission.Trade_Manage));

    private async Task<Note> OwnedNoteAsync(Guid id, Guid userId)
    {
        var note = await context.Notes.AsNoTracking().FirstOrDefaultAsync(n => n.Id == id)
            ?? throw new ArtworkRequestException(404, "草稿不存在");
        if (!await context.Spaces.AnyAsync(s => s.Id == note.SpaceId && s.UserId == userId.ToString()))
            throw new ArtworkRequestException(403, "只能发布自己空间中的作品");
        if (note.Type != "art") throw new ArtworkRequestException(400, "草稿类型不是艺术作品");
        if (note.Status == 2) throw new ArtworkRequestException(403, "该草稿已被下架");
        return note;
    }

    private static void Validate(Artwork artwork, IReadOnlyList<string?> captions, int count, int cover)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(artwork, new ValidationContext(artwork), errors, true))
            throw new ArtworkRequestException(400, errors[0].ErrorMessage ?? "作品信息不完整");
        if (count < 1 || cover < 0 || cover >= count || captions.Any(c => c?.Length > 500))
            throw new ArtworkRequestException(400, "需要至少一张图片及有效封面，单图题注不能超过 500 字");
        if (!artwork.WatermarkEnabled) return;
        if (artwork.WatermarkType is not ("text" or "image" or "both") ||
            !Regex.IsMatch(artwork.WatermarkColor ?? "", "^#[0-9a-fA-F]{6}$") ||
            artwork.WatermarkFontSize is < 1 or > 200 || artwork.WatermarkOpacity is < 0 or > 1 ||
            artwork.WatermarkImageScale is <= 0 or > 5 || artwork.WatermarkImageOpacity is < 0 or > 1 ||
            artwork.WatermarkRotation is < -360 or > 360)
            throw new ArtworkRequestException(400, "水印参数无效");
        if (artwork.WatermarkType is "image" or "both" && !IsImageUrl(artwork.WatermarkImageUrl))
            throw new ArtworkRequestException(400, "请提供有效的图片水印地址");
    }

    private static bool IsImageUrl(string? url) => Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https";

    public async Task<object> UploadAsync(Guid userId, GalleryDirectUploadDto dto)
    {
        var tags = NormalizeTags(dto.Tags);
        var source = dto.OriginalNoteId is Guid noteId ? await OwnedNoteAsync(noteId, userId) : null;
        if (source != null && await context.Artworks.AnyAsync(a => a.OriginalNoteId == source.Id))
            throw new ArtworkRequestException(409, "该原稿已有作品，请从灵脉更新发布");
        var art = new Artwork { Title = dto.Title?.Trim() ?? "", Description = dto.Description?.Trim(), UploaderId = userId,
            OriginalNoteId = dto.OriginalNoteId, WatermarkEnabled = dto.WatermarkEnabled,
            WatermarkText = string.IsNullOrWhiteSpace(dto.WatermarkText) ? "太初寰宇" : dto.WatermarkText.Trim(),
            WatermarkColor = dto.WatermarkColor ?? "#ffffff", WatermarkPosition = dto.WatermarkPosition ?? "bottom-right" };
        var files = dto.Images ?? new();
        var cover = dto.CoverIndex >= 0 && dto.CoverIndex < files.Count ? dto.CoverIndex : 0;
        Validate(art, dto.Captions ?? new(), files.Count, cover);
        if (files.Count > 20 || files.Any(f => f.Length <= 0 || f.Length > 10 * 1024 * 1024 || !f.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)) ||
            files.Sum(f => f.Length) > 25 * 1024 * 1024)
            throw new ArtworkRequestException(400, "请选择有效图片：最多 20 张，单张不超过 10 MB，总计不超过 25 MB");
        return await WithUploadsAsync(art, source, true, async upload =>
        {
            for (var i = 0; i < files.Count; i++)
            {
                using var stream = new MemoryStream();
                await files[i].CopyToAsync(stream);
                var bytes = stream.ToArray();
                var extension = ImageExtension(bytes);
                if (art.WatermarkEnabled) { bytes = await ApplyWatermarkAsync(art, bytes); extension = ".png"; }
                var url = await upload(bytes, $"{Guid.NewGuid():N}{extension}", "artwork/images");
                art.Images.Add(new ArtworkImage { ImageUrl = url, Caption = dto.Captions?.ElementAtOrDefault(i)?.Trim(), IsCover = i == cover });
            }
        }, tags);
    }

    public async Task<object> PublishNoteAsync(Guid noteId, Guid userId)
    {
        var note = await OwnedNoteAsync(noteId, userId);
        var blocks = await context.Blocks.AsNoTracking().Where(b => b.OwnerId == noteId.ToString() && b.OwnerType == note.Type)
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Id).ToListAsync();
        Artwork art;
        try
        {
            var meta = blocks.FirstOrDefault(b => b.Type == "art-collection-meta");
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            // Read only watermark fields from the metadata, never identifiers, ownership or counters.
            var config = meta == null ? new Artwork { WatermarkEnabled = false } : JsonSerializer.Deserialize<Artwork>(meta.Data, options)!;
            art = new Artwork { Title = note.Title.Trim(), UploaderId = userId, OriginalNoteId = noteId };
            config.WatermarkEnabled = meta != null && JsonSerializer.Deserialize<JsonElement>(meta.Data).TryGetProperty("watermarkEnabled", out var enabled) && enabled.ValueKind == JsonValueKind.True;
            CopyWatermark(config, art);
            art.WatermarkText = string.IsNullOrWhiteSpace(config.WatermarkText) ? note.Title : config.WatermarkText;
            var summary = blocks.FirstOrDefault(b => b.Type == "art-summary");
            art.Description = summary == null ? "" : JsonSerializer.Deserialize<JsonElement>(summary.Data).GetProperty("text").GetString();
            foreach (var block in blocks.Where(b => b.Type == "image"))
            {
                var attrs = JsonSerializer.Deserialize<JsonElement>(block.Data).GetProperty("attrs");
                var url = attrs.GetProperty("src").GetString();
                if (!IsImageUrl(url)) throw new ArtworkRequestException(400, "画幅地址无效，请先完成图片上传");
                art.Images.Add(new ArtworkImage { ImageUrl = url!, Caption = attrs.TryGetProperty("caption", out var caption) ? caption.GetString() : null,
                    IsCover = art.Images.Count == 0 });
            }
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or NullReferenceException)
        { throw new ArtworkRequestException(400, "画幅或水印数据无效，请检查原稿"); }
        Validate(art, art.Images.Select(i => i.Caption).ToArray(), art.Images.Count, 0);
        return await WithUploadsAsync(art, note, false, async upload =>
        {
            if (art.WatermarkEnabled)
                foreach (var image in art.Images)
                {
                    var bytes = await watermark.ApplyWatermarkAsync(image.ImageUrl, art.WatermarkText, art.WatermarkImageUrl, art.WatermarkType,
                        art.WatermarkPosition!, art.WatermarkFontSize, art.WatermarkOpacity, art.WatermarkColor!, art.WatermarkRotation,
                        art.WatermarkImageScale, art.WatermarkImageOpacity);
                    image.ImageUrl = await upload(bytes, $"{Guid.NewGuid():N}.png", "artwork/watermarked");
                }
        });
    }

    private async Task<object> WithUploadsAsync(Artwork art, Note? source, bool createOnly,
        Func<Func<byte[], string, string, Task<string>>, Task> prepare, string[]? tags = null)
    {
        var uploads = new Dictionary<string, string>();
        var saving = false;
        async Task<string> Upload(byte[] bytes, string name, string folder)
        {
            var key = $"{folder}/{name}";
            // 上传响应丢失也可能已经写入 COS，因此在请求前登记本次唯一 key。
            uploads[key] = cos.ObjectUrl(key);
            return uploads[key] = await cos.UploadAsync(bytes, name, folder);
        }
        try
        {
            await prepare(Upload);
            saving = true;
            return await SaveAsync(art, source, createOnly, tags);
        }
        catch
        {
            foreach (var (key, url) in uploads)
            {
                try
                {
                    // 提交响应丢失时不能误删已经落库的图片；数据库不可用则保留并记录 key。
                    if (saving && await context.ArtworkImages.AsNoTracking().AnyAsync(i => i.ImageUrl == url)) continue;
                    await cos.DeleteAsync(key);
                }
                catch (Exception cleanupError)
                { logger?.LogError(cleanupError, "作品上传补偿未完成，保留对象待核查：{ObjectKey}", key); }
            }
            throw;
        }
    }

    private static string ImageExtension(byte[] bytes)
    {
        var data = bytes.AsSpan();
        if (data.StartsWith(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return ".png";
        if (data.StartsWith(new byte[] { 255, 216, 255 })) return ".jpg";
        if (data.StartsWith("GIF87a"u8) || data.StartsWith("GIF89a"u8)) return ".gif";
        if (data.StartsWith("RIFF"u8) && data.Length >= 12 && data.Slice(8, 4).SequenceEqual("WEBP"u8)) return ".webp";
        if (data.StartsWith("BM"u8)) return ".bmp";
        throw new ArtworkRequestException(400, "仅支持 JPG、PNG、GIF、WEBP 或 BMP 图片，请勿仅修改文件后缀");
    }

    private Task<byte[]> ApplyWatermarkAsync(Artwork art, byte[] bytes) => watermark.ApplyWatermarkAsync(bytes,
        art.WatermarkText, art.WatermarkImageUrl, art.WatermarkType, art.WatermarkPosition!, art.WatermarkFontSize,
        art.WatermarkOpacity, art.WatermarkColor!, art.WatermarkRotation, art.WatermarkImageScale, art.WatermarkImageOpacity);

    private Task<object> SaveAsync(Artwork prepared, Note? source, bool createOnly, string[]? tags = null) => context.Database.CreateExecutionStrategy().ExecuteAsync<object>(async () =>
    {
        context.ChangeTracker.Clear();
        using var transaction = await context.Database.BeginTransactionAsync();
        Note? note = null;
        if (source != null)
        {
            var locked = await context.Notes.Where(n => n.Id == source.Id && n.UpdatedAt == source.UpdatedAt && n.Status != 2)
                .ExecuteUpdateAsync(s => s.SetProperty(n => n.UpdatedAt, n => n.UpdatedAt));
            if (locked == 0) throw new ArtworkRequestException(409, "原稿已变化，请重新保存后发布");
            note = await OwnedNoteAsync(source.Id, prepared.UploaderId);
            // The ownership query is no-tracking; load the entity whose publication flag will be saved.
            note = await context.Notes.FirstAsync(n => n.Id == source.Id);
        }
        var existing = source == null ? null : await context.Artworks.FirstOrDefaultAsync(a => a.OriginalNoteId == source.Id);
        if (existing != null && (existing.UploaderId != prepared.UploaderId || createOnly))
            throw new ArtworkRequestException(409, "原稿已关联其他发布，请刷新后重试");
        var art = existing ?? prepared;
        if (existing == null) context.Artworks.Add(art);
        else
        {
            art.Title = prepared.Title; art.Description = prepared.Description;
            CopyWatermark(prepared, art);
            var oldImages = await context.ArtworkImages.Where(i => i.ArtworkId == art.Id).ToListAsync();
            context.ArtworkImages.RemoveRange(oldImages);
            art.Images = prepared.Images;
        }
        // Re-publishing cannot undo an administrator's hidden/reviewing/rejected decision.
        art.IsApproved = art.Status == "published";
        if (note != null) { note.IsPublic = true; note.UpdatedAt = DateTime.UtcNow; }
        await context.SaveChangesAsync();
        if (tags != null) { await SaveTagsAsync(art.Id, tags); await context.SaveChangesAsync(); }
        await transaction.CommitAsync();
        return new { success = true, artworkId = art.Id, imagesCount = art.Images.Count,
            message = art.IsApproved ? "作品发布成功" : "作品已更新，当前仍处于审核或下架状态" };
    });

    public async Task<object> EditAsync(int id, Guid userId, ArtworkEditDto dto)
    {
        var art = await context.Artworks.Include(a => a.Images).FirstOrDefaultAsync(a => a.Id == id)
            ?? throw new ArtworkRequestException(404, "作品不存在");
        if (art.UploaderId != userId) throw new ArtworkRequestException(403, "只能编辑自己的作品");
        if (art.OriginalNoteId != null) throw new ArtworkRequestException(409, "请从灵脉原稿编辑并更新发布");
        var images = art.Images.OrderBy(i => i.Id).ToArray();
        var updated = new Artwork { Title = dto.Title?.Trim() ?? "", Description = dto.Description?.Trim(), WatermarkEnabled = false };
        Validate(updated, dto.Captions ?? new(), images.Length, dto.CoverIndex);
        if (dto.Captions == null || dto.Captions.Count != images.Length) throw new ArtworkRequestException(409, "画幅已变化，请刷新后编辑");
        if (dto.Tags != null) await SaveTagsAsync(id, NormalizeTags(dto.Tags));
        art.Title = updated.Title; art.Description = updated.Description;
        for (var i = 0; i < images.Length; i++) { images[i].Caption = dto.Captions[i]?.Trim(); images[i].IsCover = i == dto.CoverIndex; }
        await context.SaveChangesAsync();
        return new { success = true, artworkId = art.Id, imagesCount = images.Length };
    }

    public static string[] NormalizeTags(IEnumerable<string>? source)
    {
        var tags = (source ?? []).Select(t => (t ?? "").Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (tags.Length > 10 || tags.Any(t => t.Length > 50)) throw new ArtworkRequestException(400, "最多 10 个标签，每个不超过 50 字");
        return tags;
    }

    private async Task SaveTagsAsync(int artworkId, string[] names)
    {
        var id = artworkId.ToString();
        var old = await context.TagAssignments.Where(t => t.EntityType == "Artwork" && t.EntityId == id).ToListAsync();
        var desired = new List<Guid>();
        foreach (var name in names) {
            var normalized = name.ToLowerInvariant();
            var tag = await context.Tags.FirstOrDefaultAsync(t => t.NormalizedName == normalized);
            if (tag == null) { tag = new Tag { Name = name, NormalizedName = normalized, SpaceId = Guid.Empty }; context.Tags.Add(tag); }
            desired.Add(tag.Id);
            if (!old.Any(t => t.TagId == tag.Id)) context.TagAssignments.Add(new TagAssignment { EntityType = "Artwork", EntityId = id, TagId = tag.Id, Tag = tag });
        }
        context.TagAssignments.RemoveRange(old.Where(t => !desired.Contains(t.TagId)));
    }

    private static void CopyWatermark(Artwork from, Artwork to)
    {
        to.WatermarkEnabled = from.WatermarkEnabled; to.WatermarkType = from.WatermarkType ?? "text";
        to.WatermarkText = from.WatermarkText; to.WatermarkPosition = from.WatermarkPosition ?? "bottom-right";
        to.WatermarkFontSize = from.WatermarkFontSize; to.WatermarkOpacity = from.WatermarkOpacity;
        to.WatermarkColor = from.WatermarkColor ?? "#ffffff"; to.WatermarkRotation = from.WatermarkRotation;
        to.WatermarkImageUrl = from.WatermarkImageUrl; to.WatermarkImageWidth = from.WatermarkImageWidth;
        to.WatermarkImageHeight = from.WatermarkImageHeight; to.WatermarkImageScale = from.WatermarkImageScale;
        to.WatermarkImageOpacity = from.WatermarkImageOpacity;
    }
}
