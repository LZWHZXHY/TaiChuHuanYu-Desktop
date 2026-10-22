using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Dtos.World
{
    public class CreateRelationDto
    {
        public Guid TargetCardId { get; set; }
        [MaxLength(100)]
        public string RelationType { get; set; } = string.Empty;
    }
}
