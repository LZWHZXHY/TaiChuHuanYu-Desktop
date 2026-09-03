using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using MailKit.Net.Smtp;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Admin;

namespace TaiChuWeb_V2.Controllers.Admin
{
    [Authorize]
    [ApiController]
    [Route("api/Admin/Email")]
    public class AdminEmailController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _config;

        public AdminEmailController(AppDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        // POST: api/Admin/Email/Push
        [HttpPost("Push")]
        public async Task<IActionResult> PushEmail([FromBody] EmailPushDto dto)
        {
            if (dto.Type != "update" && dto.Type != "activity")
                return BadRequest(new { message = "当前接口仅支持 'update' 和 'activity'。" });

            var targetUsersQuery = _context.Users
                .Include(u => u.Settings)
                .Where(u => !string.IsNullOrEmpty(u.Email));

            if (dto.Type == "update")
                targetUsersQuery = targetUsersQuery.Where(u => u.Settings == null || u.Settings.ReceiveUpdateEmail);
            else if (dto.Type == "activity")
                targetUsersQuery = targetUsersQuery.Where(u => u.Settings != null && u.Settings.ReceiveActivityEmail);

            var emailList = await targetUsersQuery.Select(u => u.Email!).ToListAsync();

            // 过滤合法邮箱
            var validEmails = emailList.Where(e => e.Contains("@") && e.Contains(".")).ToList();

            if (!validEmails.Any())
                return BadRequest(new { message = "当前没有任何道友满足该类型的邮件接收条件，或邮箱格式全部非法。" });

            var emailConfig = _config.GetSection("EmailConfig");
            string smtpHost = emailConfig["SmtpServer"] ?? "smtp.qq.com";
            int smtpPort = int.TryParse(emailConfig["Port"], out int port) ? port : 465;
            string senderEmail = emailConfig["SenderEmail"]!;
            string senderName = emailConfig["SenderName"] ?? "太初寰宇社区";
            string authCode = emailConfig["Password"]!;

            int successCount = 0;
            // 🚀 提速：每批发 40 人
            int batchSize = 40;
            string lastError = string.Empty;


            string finalHtmlBody = BuildTaiChuEmailTemplate(dto.Subject, dto.Content);
            using var client = new SmtpClient();

            try
            {
                await client.ConnectAsync(smtpHost, smtpPort, true);
                await client.AuthenticateAsync(senderEmail, authCode);

                for (int i = 0; i < validEmails.Count; i += batchSize)
                {
                    var batchEmails = validEmails.Skip(i).Take(batchSize).ToList();
                    var message = new MimeMessage();

                    message.From.Add(new MailboxAddress(senderName, senderEmail));
                    message.To.Add(new MailboxAddress(senderName, senderEmail));
                    message.Subject = dto.Subject;
                    message.Body = new TextPart("html") { Text = finalHtmlBody };

                    foreach (var email in batchEmails)
                    {
                        try
                        {
                            message.Bcc.Add(new MailboxAddress("", email.Trim()));
                        }
                        catch { /* 忽略无法解析的脏邮箱 */ }
                    }

                    if (message.Bcc.Count > 0)
                    {
                        try
                        {
                            await client.SendAsync(message);
                            successCount += message.Bcc.Count;
                        }
                        catch (Exception ex)
                        {
                            // 🌟 核心修改：抓取腾讯网关的真实拦截日志，并直接中断后续发送
                            lastError = ex.Message;
                            break;
                        }
                    }

                    if (i + batchSize < validEmails.Count)
                    {
                        await Task.Delay(1000);
                    }
                }

                await client.DisconnectAsync(true);

                // 🌟 核心修改：如果是 0 触达，直接给前端返回 500 并在弹窗里显示腾讯的真实报错！
                if (successCount == 0 && !string.IsNullOrEmpty(lastError))
                {
                    return StatusCode(500, new { message = $"发送被拦截！腾讯网关返回: {lastError}" });
                }

                var log = new EmailLog
                {
                    Type = dto.Type,
                    Subject = dto.Subject,
                    TargetCount = successCount,
                    Status = "success",
                    SentAt = DateTime.UtcNow
                };
                _context.EmailLogs.Add(log);
                await _context.SaveChangesAsync();

                return Ok(new { message = $"邮件推送完成！已成功触达 {successCount} 位道友。", count = successCount });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = $"服务器连接失败，详细信息: {ex.Message}" });
            }
        }

        // POST: api/Admin/Email/TestPush
        [HttpPost("TestPush")]
        public async Task<IActionResult> TestPushEmail([FromBody] EmailPushDto dto)
        {
            var emailConfig = _config.GetSection("EmailConfig2");
            string smtpHost = emailConfig["SmtpServer"] ?? "smtp.qq.com";
            int smtpPort = int.TryParse(emailConfig["Port"], out int port) ? port : 465;
            string senderEmail = emailConfig["SenderEmail"]!;
            string senderName = emailConfig["SenderName"] ?? "太初寰宇社区";
            string authCode = emailConfig["Password"]!;

            if (string.IsNullOrEmpty(senderEmail) || string.IsNullOrEmpty(authCode))
            {
                return StatusCode(500, new { message = "服务器未配置完整的邮件发送凭据 (SMTP)。" });
            }

            string testReceiver = senderEmail;
            string testNotice = "<div style='padding:12px; background:#fffbeb; color:#b45309; border: 1px solid #fef3c7; margin-bottom:24px; border-radius:6px; font-size:14px;'>" +
                                "<strong>⚠️ [内部测试]</strong> 这是一封从太初后台发出的测试预览邮件。</div>";

            string combinedContent = testNotice + dto.Content;
            string finalHtmlBody = BuildTaiChuEmailTemplate(dto.Subject, combinedContent);

            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(senderName, senderEmail));
            message.To.Add(new MailboxAddress("", testReceiver));
            message.Subject = "[测试预览] " + dto.Subject;
            message.Body = new TextPart("html") { Text = finalHtmlBody };

            using var client = new SmtpClient();

            try
            {
                await client.ConnectAsync(smtpHost, smtpPort, true);
                await client.AuthenticateAsync(senderEmail, authCode);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                return Ok(new { message = "测试邮件已成功发送至您的发件邮箱，请查收！" });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Email Test Error] SMTP 发送异常: {ex.Message}");
                return StatusCode(500, new { message = "测试发送失败，请检查 SMTP 授权码或网络配置。" });
            }
        }

        // GET: api/Admin/Email/History
        [HttpGet("History")]
        public async Task<IActionResult> GetEmailHistory()
        {
            var logs = await _context.EmailLogs
                .OrderByDescending(l => l.SentAt)
                .Take(50)
                .Select(l => new
                {
                    id = l.Id,
                    type = l.Type,
                    typeLabel = l.Type == "update" ? "手动群发 (更新)" :
                                l.Type == "activity" ? "手动群发 (活动)" :
                                l.Type == "recall" ? "自动触发 (召回)" : "自动触发 (生辰)",
                    subject = l.Subject,
                    count = l.TargetCount,
                    status = l.Status,
                    time = l.SentAt.ToString("yyyy-MM-dd HH:mm")
                })
                .ToListAsync();

            return Ok(logs);
        }

        private string BuildTaiChuEmailTemplate(string subject, string content)
        {
            string formattedContent = content.Replace("\n", "<br>");

            return $@"
            <div style=""background-color: #f6f8fa; padding: 40px 15px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Helvetica, Arial, sans-serif;"">
                <div style=""max-width: 600px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; overflow: hidden; box-shadow: 0 4px 20px rgba(0,0,0,0.05);"">
                    
                    <!-- 邮件头部 -->
                    <div style=""background-color: #111111; padding: 32px 40px; text-align: center;"">
                        <h1 style=""color: #ffffff; margin: 0; font-size: 24px; font-weight: 600; letter-spacing: 2px;"">太初寰宇</h1>
                        <p style=""color: #888888; margin: 8px 0 0 0; font-size: 13px; letter-spacing: 1px;"">TAICHU UNIVERSE</p>
                    </div>
                    
                    <!-- 邮件主体 -->
                    <div style=""padding: 40px; color: #333333; font-size: 16px; line-height: 1.8;"">
                        <h2 style=""font-size: 20px; color: #111111; margin-top: 0; margin-bottom: 24px; border-bottom: 1px solid #eeeeee; padding-bottom: 12px;"">
                            {subject}
                        </h2>
                        
                        <div style=""color: #444444;"">
                            {formattedContent}
                        </div>
                    </div>
                    
                    <!-- 邮件底部 -->
                    <div style=""background-color: #fafbfc; padding: 24px 40px; text-align: center; border-top: 1px solid #eaeef2;"">
                        <p style=""color: #888888; font-size: 12px; margin: 0 0 8px 0; line-height: 1.5;"">
                            此信件由太初社区中枢系统发出，请勿直接回复本邮件。
                        </p>
                        <p style=""color: #bbbbbb; font-size: 12px; margin: 0;"">
                            &copy; 2026 太初寰宇. 探索边界，重塑法则.
                        </p>
                    </div>
                    
                </div>
            </div>";
        }
    }

    public class EmailPushDto
    {
        public string Type { get; set; } = string.Empty;
        public string Subject { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public string RecallDays { get; set; } = string.Empty;
        public string FestivalType { get; set; } = string.Empty;
        public string HolidayDate { get; set; } = string.Empty;
    }
}