using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Models.User
{
    public class User
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [EmailAddress]
        public string? Email { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? LegacyUserId { get; set; }

        

        public virtual UserProfile? Profile { get; set; }

 
        public virtual UserStats? Stats { get; set; }

        public virtual UserSettings? Settings { get; set; }

        public virtual ICollection<UserSignLog> SignLogs { get; set; } = new List<UserSignLog>();
    }
}