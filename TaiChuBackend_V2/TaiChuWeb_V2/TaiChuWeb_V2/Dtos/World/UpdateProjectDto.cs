using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Dtos.World
{
    public class UpdateProjectDto
    {
        [MaxLength(100)]
        public string? Name { get; set; }

        public string? Description { get; set; }

        [MaxLength(500)]
        public string? CoverImage { get; set; }

        /// <summary>关系图谱节点坐标 JSON</summary>
        public string? GraphPositions { get; set; }

        public bool? IsPublic { get; set; }
    }
}
