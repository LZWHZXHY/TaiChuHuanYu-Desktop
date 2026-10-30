using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Analytics;
using TaiChuWeb_V2.Services.Games;

namespace TaiChuWeb_V2.Controllers
{
    /// <summary>土豆（游戏区娱乐积分）：钱包、每日领取、破产补助、收支记录、土豆榜。</summary>
    [ApiController]
    [Route("api/[controller]")]
    public class PotatoController(PotatoService potatoes, AppDbContext db) : ControllerBase
    {
        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        private object WalletView(PotatoService.Wallet w) => new
        {
            balance = w.Balance,
            dailyClaimed = w.DailyClaimed,
            signBonusClaimed = w.SignBonusClaimed,
            signedToday = w.SignedToday,
            reliefLeft = w.ReliefLeft,
            canRelief = w.CanRelief,
            rules = new
            {
                welcome = PotatoService.WelcomeGift, daily = PotatoService.DailyAmount, signBonus = PotatoService.SignBonus,
                relief = PotatoService.ReliefAmount, reliefThreshold = PotatoService.ReliefThreshold, reliefPerDay = PotatoService.ReliefPerDay,
                botWinDailyCap = PotatoService.BotWinDailyCap,
                expRate = potatoes.ExpRate,
                expDailyCap = potatoes.ExpDailyCap,
            },
        };

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            return Ok(WalletView(await potatoes.GetAsync(uid)));
        }

        [Authorize]
        [HttpPost("daily")]
        public async Task<IActionResult> Daily()
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var (gained, message) = await potatoes.ClaimDailyAsync(uid);
            return Ok(new { gained, message, wallet = WalletView(await potatoes.GetAsync(uid)) });
        }

        [Authorize]
        [HttpPost("relief")]
        public async Task<IActionResult> Relief()
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var (gained, message) = await potatoes.ClaimReliefAsync(uid);
            if (gained == 0) return BadRequest(new { message });
            return Ok(new { gained, message, wallet = WalletView(await potatoes.GetAsync(uid)) });
        }

        public sealed class ExchangeDto { public int Exp { get; set; } }

        /// <summary>经验兑换土豆：汇率、今日剩余额度、当前经验</summary>
        [Authorize]
        [HttpGet("exchange")]
        public async Task<IActionResult> ExchangeInfo()
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var info = await potatoes.ExpExchangeInfoAsync(uid);
            return Ok(new
            {
                rate = info.Rate,
                dailyCap = info.DailyCap,
                usedToday = info.UsedToday,
                leftToday = info.LeftToday,
                experience = info.Experience,
                level = TaiChuWeb_V2.Models.User.UserStats.CalculateLevel(info.Experience),
            });
        }

        /// <summary>用经验换土豆（单向，土豆不能换回经验）</summary>
        [Authorize]
        [HttpPost("exchange")]
        public async Task<IActionResult> Exchange([FromBody] ExchangeDto dto)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var (ok, message, gained) = await potatoes.ExchangeExpAsync(uid, dto.Exp);
            if (!ok) return BadRequest(new { message });
            return Ok(new { message, gained, wallet = WalletView(await potatoes.GetAsync(uid)) });
        }

        [Authorize]
        [HttpGet("ledger")]
        public async Task<IActionResult> Ledger([FromQuery] int limit = 50)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            return Ok(await potatoes.LedgerAsync(uid, limit));
        }

        /// <summary>土豆榜：按对局净赢的土豆排名（不含每日领取、补助和经验兑换，防止靠兑换刷榜）。total 全部 / week 本周 / month 本月</summary>
        [HttpGet("leaderboard")]
        public async Task<IActionResult> Leaderboard([FromQuery] string range = "total", [FromQuery] int limit = 50)
        {
            var today = ChinaClock.Today;
            var since = range switch
            {
                "week" => ChinaClock.DayStartUtc(today.AddDays(-(((int)today.DayOfWeek + 6) % 7))),
                "month" => ChinaClock.DayStartUtc(new DateTime(today.Year, today.Month, 1)),
                _ => new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            };
            List<(Guid UserId, long Value)> rows = (await potatoes.NetSinceAsync(since, limit)).Where(r => r.Net > 0).Select(r => (r.UserId, r.Net)).ToList();

            var ids = rows.Select(r => r.UserId).ToList();
            var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.Username, Avatar = u.Profile != null ? u.Profile.Avatar : null }).ToListAsync();
            var map = users.ToDictionary(u => u.Id);
            return Ok(rows.Where(r => map.ContainsKey(r.UserId)).Select((r, i) => new
            {
                rank = i + 1,
                userId = r.UserId,
                name = map[r.UserId].Username,
                avatar = map[r.UserId].Avatar,
                value = r.Value,
            }));
        }
    }
}
