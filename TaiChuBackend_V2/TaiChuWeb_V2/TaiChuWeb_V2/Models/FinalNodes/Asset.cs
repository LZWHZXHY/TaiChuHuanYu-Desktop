// TaiChuWeb_V2/Models/FinalNodes/Asset.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace TaiChuWeb_V2.Models.FinalNodes
{
    /// <summary>
    /// 附件表：图片 / PDF / 手写 / 音频 / 视频
    /// </summary>
    [Table("assets")]
    [Index(nameof(OwnerId), nameof(Kind), Name = "IX_Assets_Owner_Kind")]
    [Index(nameof(SpaceId), Name = "IX_Assets_Space")]
    public class Asset
    {
        [Key]
        [MaxLength(36)]
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        [MaxLength(36)]
        public string OwnerId { get; set; } = string.Empty;   // 上传者用户 ID

        public Guid? SpaceId { get; set; }

        // image / pdf / handwriting / audio / video
        [Required]
        [MaxLength(20)]
        public string Kind { get; set; } = "image";

        [MaxLength(500)]
        public string? FileName { get; set; }

        [MaxLength(100)]
        public string? MimeType { get; set; }

        public long Size { get; set; }

        // 本地路径 / S3 key / OSS key
        [Required]
        [MaxLength(500)]
        public string StorageKey { get; set; } = string.Empty;

        // 对外的可访问 URL（COS 上传后可直存这里）
        [MaxLength(1000)]
        public string? Url { get; set; }

        public int? Width { get; set; }
        public int? Height { get; set; }

        [MaxLength(500)]
        public string? ThumbKey { get; set; }

        // OCR / 手写识别结果
        [Column(TypeName = "longtext")]
        public string? ExtractedText { get; set; }

        [Column(TypeName = "longtext")]
        public string? ExtractedLatex { get; set; }

        [MaxLength(50)]
        public string? OcrProvider { get; set; }   // mathpix / pix2tex / gpt4v

        public double? OcrConfidence { get; set; }

        // 渲染缓存：LaTeX -> SVG
        [Column(TypeName = "longtext")]
        public string? RenderedSvg { get; set; }

        // 用于全文检索（OCR 文本 + 口语化公式）
        [Column(TypeName = "longtext")]
        public string? SearchText { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}