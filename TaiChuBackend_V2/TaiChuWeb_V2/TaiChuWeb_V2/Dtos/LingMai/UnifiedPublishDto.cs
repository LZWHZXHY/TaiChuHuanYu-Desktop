// TaiChuWeb_V2/Dtos/LingMai/UnifiedPublishDto.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Dtos.LingMai
{
    public class UnifiedPublishDto
    {
        [Required(ErrorMessage = "必须指定所属空间")]
        public Guid SpaceId { get; set; }

        public Guid? FolderId { get; set; }

        [Required(ErrorMessage = "内容类型不能为空")]
        [MaxLength(20)]
        public string Type { get; set; } = "note"; // note (随笔), thought (简语)

        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [Required]
        public bool IsPublic { get; set; } = false;

        /// <summary>
        /// 伴随发布时携带的内容块
        /// </summary>
        public List<PublishBlockDto> Blocks { get; set; } = new();
    }

    public class PublishBlockDto
    {
        /// <summary>
        /// 前端 NanoID，为空时后端自动生成
        /// </summary>
        public string? Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Type { get; set; } = "paragraph";

        [Required]
        public string Data { get; set; } = "{}"; // JSON 格式

        [Required]
        public string SortOrder { get; set; } = "0";
    }
}
