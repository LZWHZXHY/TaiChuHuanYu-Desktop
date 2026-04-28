namespace TaiChuWeb_V2.Services.Email
{
    
    public interface IEmailService
    {
        
        Task SendVerificationCodeAsync(string toEmail, string code);

        
        Task SendHtmlEmailAsync(string toEmail, string subject, string htmlContent);
    }
}