using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TaiChuWeb_V2.Services.Afdian
{
    public class AfdianOrderDto
    {
        public string OutTradeNo { get; set; } = "";
        public string UserId { get; set; } = "";
        public string PlanId { get; set; } = "";
        public string Title { get; set; } = "";
        public string Month { get; set; } = "";
        public string TotalAmount { get; set; } = "0";
        public string ShowAmount { get; set; } = "0";
        public int Status { get; set; }
        public string Remark { get; set; } = "";
        public long CreateTime { get; set; }
        public int ProductType { get; set; }
    }

    public class AfdianSponsorDto
    {
        public string UserId { get; set; } = "";
        public string Name { get; set; } = "";
        public string Avatar { get; set; } = "";
        public string TierName { get; set; } = "";
        public string AllSumAmount { get; set; } = "0";
        public long CreateTime { get; set; }
    }

    /// <summary>爱发电 Open API 客户端（按官方签名规则调用 query-order / query-sponsor）。</summary>
    public class AfdianService
    {
        private readonly IHttpClientFactory _httpFactory;
        private readonly IConfiguration _config;

        public AfdianService(IHttpClientFactory httpFactory, IConfiguration config)
        {
            _httpFactory = httpFactory;
            _config = config;
        }

        private string BaseUrl => (_config["Afdian:BaseUrl"] ?? "https://ifdian.net/api/open").TrimEnd('/');

        // 签名：md5(token + "params" + paramsJson + "ts" + ts + "user_id" + userId)
        private static string GenerateSign(string userId, string token, string paramsJson, long ts)
        {
            var signStr = token + "params" + paramsJson + "ts" + ts + "user_id" + userId;
            var hash = MD5.HashData(Encoding.UTF8.GetBytes(signStr));
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        private async Task<JsonDocument> QueryRawAsync(string path, string userId, string token, object paramsObj, CancellationToken ct)
        {
            var paramsJson = JsonSerializer.Serialize(paramsObj);
            var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["user_id"] = userId,
                ["params"] = paramsJson,
                ["ts"] = ts.ToString(),
                ["sign"] = GenerateSign(userId, token, paramsJson, ts)
            });

            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}{path}") { Content = form };
            var client = _httpFactory.CreateClient();
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));
            var resp = await client.SendAsync(req, cts.Token);
            var body = await resp.Content.ReadAsStringAsync(cts.Token);
            return JsonDocument.Parse(body);
        }

        public Task<List<AfdianOrderDto>> QueryOrdersAsync(string userId, string token, int page, CancellationToken ct = default)
            => QueryOrdersCoreAsync(userId, token, new { page }, ct);

        /// <summary>按订单号查询单笔订单（用户绑定爱发电账号时用来核实订单归属）</summary>
        public async Task<AfdianOrderDto?> QueryOrderAsync(string userId, string token, string outTradeNo, CancellationToken ct = default)
        {
            var list = await QueryOrdersCoreAsync(userId, token, new { out_trade_no = outTradeNo }, ct);
            return list.FirstOrDefault(o => o.OutTradeNo == outTradeNo);
        }

        private async Task<List<AfdianOrderDto>> QueryOrdersCoreAsync(string userId, string token, object paramsObj, CancellationToken ct)
        {
            using var doc = await QueryRawAsync("/query-order", userId, token, paramsObj, ct);
            var root = doc.RootElement;
            if (!root.TryGetProperty("ec", out var ec) || ec.GetInt32() != 200)
                throw new InvalidOperationException("爱发电返回错误：" + (root.TryGetProperty("em", out var em) ? em.GetString() : "未知"));

            if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("list", out var list))
                return new List<AfdianOrderDto>();

            var orders = new List<AfdianOrderDto>();
            foreach (var o in list.EnumerateArray())
            {
                orders.Add(new AfdianOrderDto
                {
                    OutTradeNo = Str(o, "out_trade_no"),
                    UserId = Str(o, "user_id"),
                    PlanId = Str(o, "plan_id"),
                    Title = Str(o, "title"),
                    Month = Str(o, "month"),
                    TotalAmount = Str(o, "total_amount"),
                    ShowAmount = Str(o, "show_amount"),
                    Status = o.TryGetProperty("status", out var st) && st.TryGetInt32(out var v) ? v : 0,
                    Remark = Str(o, "remark"),
                    CreateTime = o.TryGetProperty("create_time", out var ctEl) && ctEl.TryGetInt64(out var t) ? t : 0,
                    ProductType = o.TryGetProperty("product_type", out var pt) && pt.TryGetInt32(out var p) ? p : 0
                });
            }
            return orders;
        }

        public async Task<List<AfdianSponsorDto>> QuerySponsorsAsync(string userId, string token, int page, CancellationToken ct = default)
        {
            using var doc = await QueryRawAsync("/query-sponsor", userId, token, new { page }, ct);
            var root = doc.RootElement;
            if (!root.TryGetProperty("ec", out var ec) || ec.GetInt32() != 200)
                throw new InvalidOperationException("爱发电返回错误：" + (root.TryGetProperty("em", out var em) ? em.GetString() : "未知"));

            if (!root.TryGetProperty("data", out var data) || !data.TryGetProperty("list", out var list))
                return new List<AfdianSponsorDto>();

            var sponsors = new List<AfdianSponsorDto>();
            foreach (var s in list.EnumerateArray())
            {
                var user = s.TryGetProperty("user", out var u) ? u : default;
                var plan = s.TryGetProperty("current_plan", out var cp) ? cp : default;
                sponsors.Add(new AfdianSponsorDto
                {
                    UserId = user.ValueKind == JsonValueKind.Object ? Str(user, "user_id") : "",
                    Name = user.ValueKind == JsonValueKind.Object ? Str(user, "name") : "",
                    Avatar = user.ValueKind == JsonValueKind.Object ? Str(user, "avatar") : "",
                    TierName = plan.ValueKind == JsonValueKind.Object ? Str(plan, "name") : "",
                    AllSumAmount = Str(s, "all_sum_amount"),
                    CreateTime = s.TryGetProperty("create_time", out var ctEl) && ctEl.TryGetInt64(out var t) ? t : 0
                });
            }
            return sponsors;
        }

        private static string Str(JsonElement el, string name)
        {
            if (el.ValueKind != JsonValueKind.Object) return "";
            if (!el.TryGetProperty(name, out var v)) return "";
            return v.ValueKind switch
            {
                JsonValueKind.String => v.GetString() ?? "",
                JsonValueKind.Number => v.GetRawText(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => ""
            };
        }
    }
}
