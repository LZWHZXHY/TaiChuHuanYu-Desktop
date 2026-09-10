using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.LingMai
{
    [Table("block_histories")]
    public class BlockHistory
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(128)]
        public string BlockId { get; set; } = string.Empty;

        [Required]
        public string AuthorId { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        public string AuthorName { get; set; } = string.Empty;

        [Column(TypeName = "json")]
        public string Data { get; set; } = string.Empty;

        public int Version { get; set; } = 1;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}