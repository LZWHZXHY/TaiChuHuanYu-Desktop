namespace TaiChuWeb_V2.Dtos.LoginRegister
{
    public class LoginDto
    {
        // 改名为 Identifier（标识符），代表它可以是任何能证明身份的东西
        public string Identifier { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
