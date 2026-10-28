using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Community;
using TaiChuWeb_V2.Models.Financial;

namespace TaiChuWeb_V2.Services.Afdian
{
    /// <summary>把爱发电的订单/赞助者同步进本站：订单→财政流水，赞助者→赞助墙。</summary>
    public class AfdianSyncService
    {
        private readonly AppDbContext _context;
        private readonly AfdianService _afdian;
        private readonly IConfiguration _config;

        public AfdianSyncService(AppDbContext context, AfdianService afdian, IConfiguration config)
        {
            _context = context;
            _afdian = afdian;
            _config = config;
        }

        private string EncryptionSecret => _config["Ai:EncryptionKey"] ?? _config["Jwt:Key"] ?? "TaichuAiDefaultSecret";

        /// <summary>爱发电平台服务费（默认 6%），用于把「赞助者支付金额」换算成「实际到账」。</summary>
        private double FeeRate => Math.Clamp(double.TryParse(_config["Afdian:FeeRate"], out var r) ? r : 0.06, 0, 0.5);

        /// <summary>执行一次同步，返回 (新增财政收入数, 同步赞助者数)。</summary>
        public async Task<(int ordersCreated, int sponsorsSynced)> SyncAsync(CancellationToken ct = default)
        {
            var cfg = await _context.AfdianConfigs.FirstOrDefaultAsync(ct);
            if (cfg == null || !cfg.Enabled || string.IsNullOrEmpty(cfg.UserId)) return (0, 0);

            var token = AiKeyCrypto.Decrypt(cfg.EncryptedToken, EncryptionSecret);
            if (string.IsNullOrEmpty(token)) return (0, 0);

            // 1) 先取赞助者：既用于赞助墙，也用于把订单的 user_id 映射成赞助者姓名
            var sponsors = await _afdian.QuerySponsorsAsync(cfg.UserId, token, 1, ct);
            var nameByUser = sponsors
                .Where(s => !string.IsNullOrWhiteSpace(s.UserId))
                .GroupBy(s => s.UserId!)
                .ToDictionary(g => g.Key, g => g.First().Name);

            // 2) 订单 → 财政收入流水（按 out_trade_no 去重；已存在的按净额/姓名校正）
            var orders = await _afdian.QueryOrdersAsync(cfg.UserId, token, 1, ct);
            var created = 0;
            var corrected = 0;
            foreach (var o in orders.Where(o => o.Status == 2 && !string.IsNullOrWhiteSpace(o.OutTradeNo)))
            {
                var netAmount = Math.Round(ParseAmount(o.TotalAmount) * (1 - FeeRate), 2);
                var payerName = !string.IsNullOrWhiteSpace(o.UserId)
                    && nameByUser.TryGetValue(o.UserId, out var n)
                    && !string.IsNullOrWhiteSpace(n) ? n : null;

                var existing = await _context.Financials.FirstOrDefaultAsync(f => f.ExternalOrderId == o.OutTradeNo, ct);
                if (existing != null)
                {
                    var changed = false;
                    // 校正金额：老记录可能按毛额记的，这里统一按扣除服务费后的净额
                    if (Math.Abs(existing.Amount - netAmount) > 0.001) { existing.Amount = netAmount; changed = true; }
                    // 校正来源：把原来写死的「爱发电用户」替换成赞助者姓名
                    if (payerName != null && existing.ShouKuan != payerName) { existing.ShouKuan = payerName; changed = true; }
                    if (changed) corrected++;
                    continue;
                }

                var title = string.IsNullOrWhiteSpace(o.Title) ? "赞助" : o.Title;
                _context.Financials.Add(new Financial
                {
                    ZhiChu = "赞助收入",
                    ZhiChuXiangMu = "爱发电-" + title,
                    date = o.CreateTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(o.CreateTime).LocalDateTime : DateTime.Now,
                    ShouKuan = payerName ?? "爱发电用户", // 优先用赞助者姓名
                    Amount = netAmount, // 已扣除平台服务费
                    PayReceive = 0, // 收入
                    ExternalOrderId = o.OutTradeNo
                });
                created++;
            }
            if (created > 0 || corrected > 0) await _context.SaveChangesAsync(ct);

            // 订单留言（remark）→ 每个用户取最新一条，回填到赞助墙
            var remarkByUser = orders
                .Where(o => !string.IsNullOrWhiteSpace(o.Remark) && !string.IsNullOrWhiteSpace(o.UserId))
                .OrderByDescending(o => o.CreateTime)
                .GroupBy(o => o.UserId!)
                .ToDictionary(g => g.Key, g => g.First().Remark);

            // 3) 赞助者 → 赞助墙（按爱发电 user_id 去重/更新）
            var synced = 0;
            foreach (var s in sponsors.Where(s => !string.IsNullOrWhiteSpace(s.UserId)))
            {
                remarkByUser.TryGetValue(s.UserId, out var remark);
                // 赞助墙也按实际到账（扣除服务费）显示，与财政口径一致
                var netAmount = Math.Round(ParseAmount(s.AllSumAmount) * (1 - FeeRate), 2);
                var existing = await _context.Sponsors.FirstOrDefaultAsync(x => x.AfdianUserId == s.UserId, ct);
                if (existing == null)
                {
                    _context.Sponsors.Add(new Sponsor
                    {
                        Name = string.IsNullOrWhiteSpace(s.Name) ? "匿名支持者" : s.Name,
                        AfdianUserId = s.UserId,
                        Avatar = s.Avatar,
                        Tier = string.IsNullOrWhiteSpace(s.TierName) ? "觉醒者" : s.TierName,
                        Amount = netAmount,
                        Message = string.IsNullOrWhiteSpace(remark) ? null : remark,
                        SponsoredAt = s.CreateTime > 0 ? DateTimeOffset.FromUnixTimeSeconds(s.CreateTime).LocalDateTime : DateTime.UtcNow
                    });
                }
                else
                {
                    if (!string.IsNullOrWhiteSpace(s.Name)) existing.Name = s.Name;
                    if (!string.IsNullOrWhiteSpace(s.Avatar)) existing.Avatar = s.Avatar;
                    if (!string.IsNullOrWhiteSpace(s.TierName)) existing.Tier = s.TierName;
                    existing.Amount = netAmount;
                    if (!string.IsNullOrWhiteSpace(remark)) existing.Message = remark;
                }
                synced++;
            }

            cfg.LastSyncAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(ct);
            return (created, synced);
        }

        private static double ParseAmount(string? raw)
        {
            return double.TryParse(raw, out var v) ? Math.Round(v, 2) : 0;
        }
    }
}
