using Microsoft.Extensions.Configuration;
using MimeKit;
// 使用别名，防止和 System.Net.Mail 冲突
using MailSmtp = MailKit.Net.Smtp;
using TaiChuWeb_V2.Services;

namespace TaiChuWeb_V2.Services.Email
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        public EmailService(IConfiguration config) => _config = config;

        public async Task SendHtmlEmailAsync(string toEmail, string subject, string htmlContent)
        {
            var emailSettings = _config.GetSection("EmailConfig");
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(
                emailSettings["SenderName"] ?? "太初灵枢",
                emailSettings["SenderEmail"] ?? "no-reply@taichu.com"
            ));
            message.To.Add(new MailboxAddress("", toEmail));
            message.Subject = subject;
            message.Body = new TextPart("html") { Text = htmlContent };

            // 使用别名确保调用的是 MailKit 的客户端
            using var client = new MailSmtp.SmtpClient();

            await client.ConnectAsync(
                emailSettings["SmtpServer"] ?? "localhost",
                int.Parse(emailSettings["Port"] ?? "465"),
                true
            );

            await client.AuthenticateAsync(
                emailSettings["SenderEmail"] ?? "",
                emailSettings["Password"] ?? ""
            );
            await client.SendAsync(message);
            await client.DisconnectAsync(true);
        }

        public async Task SendVerificationCodeAsync(string toEmail, string code)
        {
            await SendHtmlEmailAsync(toEmail, "验证码", $"您的验证码是：{code}");
        }
    }
}