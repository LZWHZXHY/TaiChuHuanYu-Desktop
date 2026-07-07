using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Dtos.World
{
    public class UpdateProjectDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        public string? Description { get; set; }

        public bool? IsPublic { get; set; }
    }
}
