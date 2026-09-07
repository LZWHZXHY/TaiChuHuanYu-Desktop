using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.DTOs.Artwork
{
    public class GalleryDirectUploadDto
    {
        // 允许可选关联灵脉笔记（独立发布时不传即可，为 null）
        public Guid? OriginalNoteId { get; set; }

        [Required(ErrorMessage = "标题不能为空")]
        [MaxLength(100, ErrorMessage = "标题长度不能超过100个字符")]
        public string Title { get; set; } = string.Empty;

        [MaxLength(1000, ErrorMessage = "描述长度不能超过1000个字符")]
        public string? Description { get; set; }

        // 水印设置
        public bool WatermarkEnabled { get; set; } = true;
        [MaxLength(100)]
        public string? WatermarkText { get; set; }
        [MaxLength(30)]
        public string? WatermarkPosition { get; set; } = "bottom-right";
        [MaxLength(20)]
        public string? WatermarkColor { get; set; } = "#ffffff";

        // 图片与元数据
        [Required(ErrorMessage = "请至少上传一张图片")]
        public List<IFormFile> Images { get; set; } = new();

        public List<string?>? Captions { get; set; }

        // 指定封面图片索引（默认为第 0 张）
        public int CoverIndex { get; set; } = 0;
    }
}