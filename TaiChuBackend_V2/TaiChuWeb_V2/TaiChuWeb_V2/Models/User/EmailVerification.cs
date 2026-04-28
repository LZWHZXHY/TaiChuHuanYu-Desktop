using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Models.User
{
    // Models/User/EmailVerification.cs
    public class EmailVerification
    {
        [Key]
        public string Email { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
    }
}
