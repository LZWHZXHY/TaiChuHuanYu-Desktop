namespace TaiChuWeb_V2.Dtos.LoginRegister
{
    public class RegisterDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string VerificationCode { get; set; } = string.Empty;
    }
}
