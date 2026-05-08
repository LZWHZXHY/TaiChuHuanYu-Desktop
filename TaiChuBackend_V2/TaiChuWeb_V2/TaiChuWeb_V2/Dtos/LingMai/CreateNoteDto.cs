// Dtos/LingMai/CreateNoteDto.cs
using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Dtos.LingMai
{
    public class CreateNoteDto
    {
        [Required(ErrorMessage = "标题不能为空")]
        [MaxLength(255)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "必须指定所属空间")]
        public Guid SpaceId { get; set; }

        public Guid? FolderId { get; set; }
        public string Type { get; set; } = "note";

        public string? SortOrder { get; set; }
    }
}