using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services.Games;
using TaiChuWeb_V2.Services.Shop;

namespace TaiChuWeb_V2.Controllers.Users
{
    /// <summary>
    /// 改名卡：改一次用户名。三种付法：
    /// · 用一张改名卡（在交易行用经验或土豆买的）；
    /// · 直接付经验（默认 1000，Rename:ExpCost）；
    /// · 直接付土豆（默认 5000，Rename:PotatoCost；按 1 经验 = 5 土豆折算）。
    /// 用户名同时是登录名（登录接口按「用户名或邮箱」匹配），改名后旧用户名不能再登录，邮箱不受影响。
    /// 改名后接口会返回新的登录凭证（JWT 里带着用户名），前端直接替换即可，不用重新登录。
    /// 旧用户名保留 30 天，期间别人不能注册或改成这个名字，防止冒充。
    /// 改名记录表 username_history 在第一次使用时自动创建（不需要 EF 迁移）。
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/Users/Rename")]
    public class RenameController(AppDbContext db, IConfiguration config, JwtService jwt, ShopService shop, PotatoService potatoes) : ControllerBase
    {
        public const int MinLength = 2;
        public const int MaxLength = 20;
        public const int ReserveDays = 30;

        private long Cost => Math.Max(0, config.GetValue<long?>("Rename:ExpCost") ?? 1000);
        private long PotatoCost => Math.Max(0, config.GetValue<long?>("Rename:PotatoCost") ?? 5000);
        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        public sealed class RenameDto
        {
            public string NewName { get; set; } = string.Empty;
            /// <summary>card 用改名卡 / exp 付经验（默认）/ potato 付土豆</summary>
            public string? Pay { get; set; }
        }

        private static bool _tableReady;

        private Task EnsureTableAsync() => EnsureTableAsync(db);

        private static async Task EnsureTableAsync(AppDbContext db)
        {
            if (_tableReady) return;
            await db.Database.ExecuteSqlRawAsync(@"
CREATE TABLE IF NOT EXISTS username_history (
  Id BIGINT NOT NULL AUTO_INCREMENT PRIMARY KEY,
  UserId CHAR(36) NOT NULL,
  OldName VARCHAR(50) NOT NULL,
  NewName VARCHAR(50) NOT NULL,
  Cost BIGINT NOT NULL,
  CreatedAt DATETIME NOT NULL,
  KEY IX_username_history_user (UserId, Id),
  KEY IX_username_history_old (OldName, CreatedAt)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;");
            // 后来加的列：用什么付的（card / exp / potato）
            try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE username_history ADD COLUMN Pay VARCHAR(10) NOT NULL DEFAULT 'exp'"); }
            catch (Exception ex) when (ex.Message.Contains("Duplicate column", StringComparison.OrdinalIgnoreCase)) { }
            _tableReady = true;
        }

        /// <summary>检查新用户名的格式，返回错误提示（null 表示通过）</summary>
        public static string? ValidateName(string name)
        {
            if (name.Length < MinLength || name.Length > MaxLength) return $"用户名需要 {MinLength}–{MaxLength} 个字";
            foreach (var ch in name)
            {
                var cat = char.GetUnicodeCategory(ch);
                var ok = char.IsLetterOrDigit(ch) || ch is '_' or '-' or '·' || cat == UnicodeCategory.OtherLetter;
                if (!ok) return "用户名只能包含中文、字母、数字和 _ - · 这几个符号";
            }
            return null;
        }

        private sealed class HistoryRow
        {
            public string OldName { get; set; } = string.Empty;
            public string NewName { get; set; } = string.Empty;
            public long Cost { get; set; }
            public string Pay { get; set; } = "exp";
            public DateTime CreatedAt { get; set; }
        }

        /// <summary>这个用户名是不是别人刚改掉、还在保留期内（注册时也会检查）</summary>
        public static async Task<bool> IsReservedAsync(AppDbContext db, string name, Guid? exceptUserId)
        {
            await EnsureTableAsync(db);
            var since = DateTime.UtcNow.AddDays(-ReserveDays);
            var except = exceptUserId?.ToString() ?? string.Empty;
            var count = await db.Database
                .SqlQuery<int>($"SELECT COUNT(*) AS Value FROM username_history WHERE OldName = {name} AND UserId <> {except} AND CreatedAt > {since}")
                .FirstAsync();
            return count > 0;
        }

        private async Task<List<HistoryRow>> HistoryAsync(Guid uid)
        {
            await EnsureTableAsync();
            var id = uid.ToString();
            return await db.Database
                .SqlQuery<HistoryRow>($"SELECT OldName, NewName, Cost, Pay, CreatedAt FROM username_history WHERE UserId = {id} ORDER BY Id DESC LIMIT 20")
                .ToListAsync();
        }

        /// <summary>改名卡信息：价格、当前经验、改名后等级、历史记录</summary>
        [HttpGet]
        public async Task<IActionResult> Info()
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var user = await db.Users.AsNoTracking().Include(u => u.Stats).FirstOrDefaultAsync(u => u.Id == uid);
            if (user == null) return NotFound();
            var exp = user.Stats?.Experience ?? 0;
            var history = await HistoryAsync(uid);
            long potatoBalance = 0;
            try { potatoBalance = (await potatoes.GetAsync(uid)).Balance; } catch { /* 土豆读不到就只显示经验 */ }
            var cards = await shop.CountAsync(uid, ShopService.RenameCardKey);
            return Ok(new
            {
                cost = Cost,
                potatoCost = PotatoCost,
                potatoes = potatoBalance,
                cards,
                username = user.Username,
                experience = exp,
                canAfford = exp >= Cost,
                level = UserStats.CalculateLevel(exp),
                levelAfter = UserStats.CalculateLevel(Math.Max(0, exp - Cost)),
                rules = new { minLength = MinLength, maxLength = MaxLength, reserveDays = ReserveDays },
                history = history.Select(h => new { oldName = h.OldName, newName = h.NewName, cost = h.Cost, pay = h.Pay, at = DateTime.SpecifyKind(h.CreatedAt, DateTimeKind.Utc) }),
            });
        }

        /// <summary>检查用户名能不能用（输入时实时提示）</summary>
        [HttpGet("check")]
        public async Task<IActionResult> Check([FromQuery] string name)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var reason = await UnavailableReasonAsync(uid, (name ?? string.Empty).Trim());
            return Ok(new { available = reason == null, message = reason ?? "可以使用" });
        }

        private async Task<string?> UnavailableReasonAsync(Guid uid, string name)
        {
            if (ValidateName(name) is string bad) return bad;
            var me = await db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.Username).FirstOrDefaultAsync();
            if (me == name) return "和现在的用户名一样";
            if (await db.Users.AnyAsync(u => u.Username == name && u.Id != uid)) return "这个用户名已经有人用了";
            if (await IsReservedAsync(db, name, uid)) return $"这个用户名刚被别人改掉，{ReserveDays} 天内为原主人保留";
            return null;
        }

        /// <summary>改名：按选择的方式付费（改名卡 / 经验 / 土豆）、改用户名、记流水，返回新的登录凭证</summary>
        [HttpPost]
        public async Task<IActionResult> Rename([FromBody] RenameDto dto)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            var newName = (dto.NewName ?? string.Empty).Trim();
            if (await UnavailableReasonAsync(uid, newName) is string reason) return BadRequest(new { message = reason });
            var pay = dto.Pay is "card" or "potato" ? dto.Pay : "exp";

            var oldNameForLog = await db.Users.AsNoTracking().Where(u => u.Id == uid).Select(u => u.Username).FirstOrDefaultAsync();
            if (oldNameForLog == null) return NotFound(new { message = "账号不存在" });

            // 改名卡、土豆不在 EF 的表里：先扣，后面失败了再还回去
            long potatoPaid = 0;
            var cardUsed = false;
            if (pay == "card")
            {
                if (!await shop.ConsumeAsync(uid, ShopService.RenameCardKey)) return BadRequest(new { message = "你没有改名卡了，可以在交易行买，或者直接付经验 / 土豆" });
                cardUsed = true;
            }
            else if (pay == "potato" && PotatoCost > 0)
            {
                var (ok, _, err) = await potatoes.SpendAsync(uid, PotatoCost, $"改名卡：{oldNameForLog} → {newName}", "shop", "rename");
                if (!ok) return BadRequest(new { message = err ?? "土豆不够" });
                potatoPaid = PotatoCost;
            }
            async Task Undo()
            {
                if (cardUsed) await shop.AddCountAsync(uid, ShopService.RenameCardKey, 1);
                if (potatoPaid > 0) await potatoes.RefundAsync(uid, potatoPaid, "改名失败退还", "shop-refund", "rename");
            }

            try
            {
                var cost = pay == "exp" ? Cost : 0;
                await using var tx = await db.Database.BeginTransactionAsync();
                var user = await db.Users.Include(u => u.Stats).FirstOrDefaultAsync(u => u.Id == uid);
                if (user == null) { await Undo(); return NotFound(new { message = "账号不存在" }); }

                // 原子扣经验：余额不够时一行都不会更新
                if (cost > 0)
                {
                    var updated = await db.UserStats
                        .Where(s => s.UserId == uid && s.Experience >= cost)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.Experience, x => x.Experience - cost));
                    if (updated == 0) return BadRequest(new { message = $"经验不够，改名需要 {cost} 经验" });
                    db.UserExpLogs.Add(new UserExpLog { UserId = uid, Change = (int)-cost, Reason = $"改名卡：{user.Username} → {newName}", CreatedAt = DateTime.UtcNow });
                }

                var oldName = user.Username;
                user.Username = newName;
                await db.SaveChangesAsync();
                var id = uid.ToString();
                var now = DateTime.UtcNow;
                var paid = pay == "potato" ? potatoPaid : cost;
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO username_history (UserId, OldName, NewName, Cost, Pay, CreatedAt) VALUES ({id}, {oldName}, {newName}, {paid}, {pay}, {now})");
                await tx.CommitAsync();

                var exp = await db.UserStats.AsNoTracking().Where(s => s.UserId == uid).Select(s => s.Experience).FirstOrDefaultAsync();
                return Ok(new
                {
                    message = $"改名成功！以后请用「{newName}」或邮箱登录。",
                    username = newName,
                    experience = exp,
                    level = UserStats.CalculateLevel(exp),
                    token = jwt.GenerateToken(user),
                });
            }
            catch
            {
                await Undo();
                throw;
            }
        }
    }
}
