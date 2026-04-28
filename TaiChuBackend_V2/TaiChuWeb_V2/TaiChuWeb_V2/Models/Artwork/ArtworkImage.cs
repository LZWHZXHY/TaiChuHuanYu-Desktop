using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Artwork   
{
    public class ArtworkImage
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string ImageUrl { get; set; } = string.Empty;

        // 是否为封面图
        public bool IsCover { get; set; } = false;

        public int ArtworkId { get; set; }

        [ForeignKey("ArtworkId")]
        public virtual Artwork Artwork { get; set; } = null!;
    }
}   