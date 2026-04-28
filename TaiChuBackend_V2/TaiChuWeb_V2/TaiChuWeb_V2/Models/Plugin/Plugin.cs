using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Models.Plugin
{
    public class Plugin
    {
        [Key] // 主键
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Url { get; set; } = string.Empty;

        public string Icon { get; set; } = "#";

        public bool RequiresAuth { get; set; }

        public int Order { get; set; }

        public int PlatformScope { get; set; } = 0;
    }
}
