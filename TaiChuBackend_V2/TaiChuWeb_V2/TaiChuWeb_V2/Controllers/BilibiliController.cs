using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace TaiChuWeb_V2.Controllers
{
    /// <summary>太初寰宇 B 站官号信息（粉丝数，带 60 秒内存缓存）。</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class BilibiliController : ControllerBase
    {
        private const string Mid = "3546736970696727";
        private const string SpaceUrl = "https://space.bilibili.com/zh-Hans/3546736970696727";
        private const string Ua = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
        private static readonly HttpClient Http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        private static (int follower, DateTime at) _cache = (0, DateTime.MinValue);

        [HttpGet("stats")]
        [AllowAnonymous]
        public async Task<IActionResult> Stats()
        {
            if (DateTime.UtcNow - _cache.at < TimeSpan.FromSeconds(60) && _cache.follower > 0)
                return Ok(new { follower = _cache.follower, mid = Mid, url = SpaceUrl });

            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.bilibili.com/x/relation/stat?vmid={Mid}");
                req.Headers.UserAgent.ParseAdd(Ua);
                req.Headers.Referrer = new Uri("https://space.bilibili.com/");
                using var resp = await Http.SendAsync(req);
                var json = await resp.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;
                if (root.TryGetProperty("code", out var code) && code.GetInt32() == 0
                    && root.TryGetProperty("data", out var data)
                    && data.TryGetProperty("follower", out var f))
                {
                    var n = f.GetInt32();
                    _cache = (n, DateTime.UtcNow);
                    return Ok(new { follower = n, mid = Mid, url = SpaceUrl });
                }
            }
            catch { /* 用缓存兜底 */ }

            return Ok(new { follower = _cache.follower, mid = Mid, url = SpaceUrl });
        }
    }
}
