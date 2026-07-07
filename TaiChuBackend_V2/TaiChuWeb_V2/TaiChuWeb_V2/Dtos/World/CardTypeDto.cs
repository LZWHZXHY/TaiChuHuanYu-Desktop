using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Dtos.World
{
    public class CardTypeDto
    {
        public string Id { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string? Icon { get; set; }
        public string? Description { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; }
        public bool IsSystem { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateCardTypeDto
    {
        [Required]
        [MaxLength(50)]
        public string Id { get; set; } = string.Empty;  // 唯一标识，如 'faction'

        [Required]
        [MaxLength(50)]
        public string Label { get; set; } = string.Empty;

        [MaxLength(10)]
        public string? Icon { get; set; }

        [MaxLength(200)]
        public string? Description { get; set; }

        public int SortOrder { get; set; } = 0;
    }

    public class UpdateCardTypeDto
    {
        [MaxLength(50)]
        public string? Label { get; set; }

        [MaxLength(10)]
        public string? Icon { get; set; }

        [MaxLength(200)]
        public string? Description { get; set; }

        public int? SortOrder { get; set; }

        public bool? IsActive { get; set; }
    }
}
