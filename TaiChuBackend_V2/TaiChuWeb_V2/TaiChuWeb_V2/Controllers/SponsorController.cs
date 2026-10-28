using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Community;
using TaiChuWeb_V2.Models.Financial;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services;
using TaiChuWeb_V2.Services.Afdian;

namespace TaiChuWeb_V2.Controllers
{
    public class SponsorDto
    {
        public string Name { get; set; } = string.Empty;
        public string Tier { get; set; } = "觉醒者";
        public double Amount { get; set; }
        public string? Message { get; set; }
        public bool IsAnonymous { get; set; }
        public bool IsVisible { get; set; } = true;
    }

    public class AfdianConfigDto
    {
        public string UserId { get; set; } = string.Empty;
        public string? Token { get; set; }
        public bool Enabled { get; set; }
    }

    /// <summary>赞助墙：公开列表 + 管理员维护（新增赞助自动写入财政流水）。</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class SponsorController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly AfdianSyncService _afdianSync;
        private readonly IConfiguration _config;

        public SponsorController(AppDbContext context, AfdianSyncService afdianSync, IConfiguration config)
        {
            _context = context;
            _afdianSync = afdianSync;
            _config = config;
        }

        private string EncryptionSecret => _config["Ai:EncryptionKey"] ?? _config["Jwt:Key"] ?? "TaichuAiDefaultSecret";

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        private async Task<bool> IsSuperAdmin()
        {
            if (!CurrentUserId.HasValue) return false;
            return await _context.UserPermissions.AnyAsync(p => p.UserId == CurrentUserId.Value && p.Permission == AdminPermission.SuperAdmin);
        }

        // ===== 公开：赞助墙 =====
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> List()
        {
            var list = await _context.Sponsors
                .Where(s => s.IsVisible)
                .OrderByDescending(s => s.SponsoredAt)
                .ToListAsync();

            var totalAmount = list.Sum(s => s.Amount);
            var totalCount = list.Count;

            return Ok(new
            {
                totalAmount,
                totalCount,
                sponsors = list.Select(s => new
                {
                    s.Id,
                    Name = s.IsAnonymous ? "匿名支持者" : s.Name,
                    s.Tier,
                    s.Amount,
                    s.Message,
                    s.IsAnonymous,
                    s.SponsoredAt
                })
            });
        }

        // ===== 管理员：新增赞助者（自动写入财政流水） =====
        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] SponsorDto dto)
        {
            if (!await IsSuperAdmin()) return Forbid();
            if (string.IsNullOrWhiteSpace(dto.Name)) return BadRequest(new { message = "名字不能为空" });

            var sponsor = new Sponsor
            {
                Name = dto.Name.Trim(),
                Tier = string.IsNullOrWhiteSpace(dto.Tier) ? "觉醒者" : dto.Tier,
                Amount = dto.Amount,
                Message = dto.Message,
                IsAnonymous = dto.IsAnonymous,
                IsVisible = dto.IsVisible,
                SponsoredAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow
            };

            var fin = new Financial
            {
                ZhiChu = "赞助收入",
                ZhiChuXiangMu = $"赞助-{sponsor.Tier}",
                date = DateTime.Now,
                ShouKuan = string.IsNullOrWhiteSpace(sponsor.Name) ? "匿名支持者" : sponsor.Name,
                Amount = sponsor.Amount,
                PayReceive = 0 // 0 = 收入
            };

            _context.Sponsors.Add(sponsor);
            _context.Financials.Add(fin);
            await _context.SaveChangesAsync();
            sponsor.FinancialIndex = fin.index;
            await _context.SaveChangesAsync();

            return Ok(new { id = sponsor.Id });
        }

        // ===== 管理员：编辑 =====
        [HttpPut("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] SponsorDto dto)
        {
            if (!await IsSuperAdmin()) return Forbid();
            var sponsor = await _context.Sponsors.FirstOrDefaultAsync(s => s.Id == id);
            if (sponsor == null) return NotFound(new { message = "赞助记录不存在" });

            sponsor.Name = string.IsNullOrWhiteSpace(dto.Name) ? sponsor.Name : dto.Name.Trim();
            sponsor.Tier = string.IsNullOrWhiteSpace(dto.Tier) ? sponsor.Tier : dto.Tier;
            sponsor.Amount = dto.Amount;
            sponsor.Message = dto.Message;
            sponsor.IsAnonymous = dto.IsAnonymous;
            sponsor.IsVisible = dto.IsVisible;

            // 同步财政流水（若有关联）
            if (sponsor.FinancialIndex.HasValue)
            {
                var fin = await _context.Financials.FirstOrDefaultAsync(f => f.index == sponsor.FinancialIndex.Value);
                if (fin != null)
                {
                    fin.ZhiChuXiangMu = $"赞助-{sponsor.Tier}";
                    fin.ShouKuan = sponsor.Name;
                    fin.Amount = sponsor.Amount;
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { id = sponsor.Id });
        }

        // ===== 管理员：删除（连带清理财政流水） =====
        [HttpDelete("{id:int}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            if (!await IsSuperAdmin()) return Forbid();
            var sponsor = await _context.Sponsors.FirstOrDefaultAsync(s => s.Id == id);
            if (sponsor == null) return NotFound(new { message = "赞助记录不存在" });

            if (sponsor.FinancialIndex.HasValue)
            {
                var fin = await _context.Financials.FirstOrDefaultAsync(f => f.index == sponsor.FinancialIndex.Value);
                if (fin != null) _context.Financials.Remove(fin);
            }

            _context.Sponsors.Remove(sponsor);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ===== 爱发电同步：配置 =====
        [HttpGet("config")]
        [Authorize]
        public async Task<IActionResult> GetConfig()
        {
            if (!await IsSuperAdmin()) return Forbid();
            var cfg = await _context.AfdianConfigs.FirstOrDefaultAsync();
            if (cfg == null) return Ok(new { userId = "", maskedToken = "", hasToken = false, enabled = false, lastSyncAt = (DateTime?)null });
            var token = AiKeyCrypto.Decrypt(cfg.EncryptedToken, EncryptionSecret);
            return Ok(new { userId = cfg.UserId, maskedToken = AiKeyCrypto.Mask(token), hasToken = !string.IsNullOrEmpty(token), enabled = cfg.Enabled, lastSyncAt = cfg.LastSyncAt });
        }

        [HttpPut("config")]
        [Authorize]
        public async Task<IActionResult> SaveConfig([FromBody] AfdianConfigDto dto)
        {
            if (!await IsSuperAdmin()) return Forbid();
            var cfg = await _context.AfdianConfigs.FirstOrDefaultAsync();
            if (cfg == null) { cfg = new AfdianConfig { Id = 1 }; _context.AfdianConfigs.Add(cfg); }
            cfg.UserId = dto.UserId?.Trim() ?? "";
            cfg.Enabled = dto.Enabled;
            cfg.UpdatedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(dto.Token) && !dto.Token.Contains("****"))
                cfg.EncryptedToken = AiKeyCrypto.Encrypt(dto.Token.Trim(), EncryptionSecret);
            await _context.SaveChangesAsync();
            return Ok(new { message = "已保存" });
        }

        [HttpPost("sync")]
        [Authorize]
        public async Task<IActionResult> Sync()
        {
            if (!await IsSuperAdmin()) return Forbid();
            try
            {
                var (orders, sponsors) = await _afdianSync.SyncAsync();
                return Ok(new { ordersCreated = orders, sponsorsSynced = sponsors });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = "同步失败：" + ex.Message });
            }
        }

        // ===== 爱发电 Webhook 回调（公开，验签后触发同步） =====
        [HttpPost("webhook")]
        [AllowAnonymous]
        public async Task<IActionResult> Webhook()
        {
            try
            {
                using var doc = await JsonDocument.ParseAsync(Request.Body);
                var root = doc.RootElement;
                if (!root.TryGetProperty("data", out var data))
                    return Ok(new { ec = 400, em = "invalid payload" });

                var dataType = data.TryGetProperty("type", out var tEl) ? (tEl.GetString() ?? "") : "";
                if (dataType != "order" || !data.TryGetProperty("order", out var order) || !data.TryGetProperty("sign", out var signEl))
                    return Ok(new { ec = 200, em = "" }); // 非订单事件直接确认

                var outTradeNo = GetStr(order, "out_trade_no");
                var userId = GetStr(order, "user_id");
                var planId = GetStr(order, "plan_id");
                var totalAmount = GetStr(order, "total_amount");
                var sign = signEl.GetString() ?? "";

                // 签名：order 的 out_trade_no + user_id + plan_id + total_amount
                if (!VerifyWebhookSign(outTradeNo + userId + planId + totalAmount, sign))
                    return Ok(new { ec = 400005, em = "sign validation failed" });

                try { await _afdianSync.SyncAsync(); } catch { /* 忽略，返回成功避免平台重试 */ }
                return Ok(new { ec = 200, em = "" });
            }
            catch
            {
                return Ok(new { ec = 200, em = "" });
            }
        }

        private static string GetStr(JsonElement el, string name)
        {
            if (el.ValueKind != JsonValueKind.Object || !el.TryGetProperty(name, out var v)) return "";
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString() ?? "",
                JsonValueKind.Number => v.GetRawText(),
                _ => ""
            };
        }

        private static bool VerifyWebhookSign(string signStr, string sign)
        {
            const string publicKey = @"-----BEGIN PUBLIC KEY-----
MIIBIjANBgkqhkiG9w0BAQEFAAOCAQ8AMIIBCgKCAQEAwwdaCg1Bt+UKZKs0R54y
lYnuANma49IpgoOwNmk3a0rhg/PQuhUJ0EOZSowIC44l0K3+fqGns3Ygi4AfmEfS
4EKbdk1ahSxu7Zkp2rHMt+R9GarQFQkwSS/5x1dYiHNVMiR8oIXDgjmvxuNes2Cr
8fw9dEF0xNBKdkKgG2qAawcN1nZrdyaKWtPVT9m2Hl0ddOO9thZmVLFOb9NVzgYf
jEgI+KWX6aY19Ka/ghv/L4t1IXmz9pctablN5S0CRWpJW3Cn0k6zSXgjVdKm4uN7
jRlgSRaf/Ind46vMCm3N2sgwxu/g3bnooW+db0iLo13zzuvyn727Q3UDQ0MmZcEW
MQIDAQAB
-----END PUBLIC KEY-----";
            try
            {
                using var rsa = RSA.Create();
                rsa.ImportFromPem(publicKey);
                return rsa.VerifyData(Encoding.UTF8.GetBytes(signStr), Convert.FromBase64String(sign), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            }
            catch { return false; }
        }
    }
}
