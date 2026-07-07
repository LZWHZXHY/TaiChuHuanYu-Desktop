using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Dtos.World
{
    public class CreateProjectDto
    {
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsPublic { get; set; } = false;
    }
}
