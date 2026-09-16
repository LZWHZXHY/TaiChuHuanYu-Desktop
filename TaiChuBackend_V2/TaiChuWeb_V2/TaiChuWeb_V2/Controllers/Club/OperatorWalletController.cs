using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Club;

namespace TaiChuWeb_V2.Controllers.Club
{
    /// <summary>打手钱包（打手端）</summary>
    [ApiController]
    [Route("api/club/wallet")]
    [Authorize]
    public class OperatorWalletController : ControllerBase
    {
        private readonly AppDbContext _db;
        public OperatorWalletController(AppDbContext db) => _db = db;

        // ==================================================
        //  钱包总览 + 流水
        // ==================================================
        [HttpGet]
        public async Task<IActionResult> GetWallet(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30)
        {
            var userId = GetUserId();
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 30;

            await ProcessUnfreezes(userId);

            var profile = await _db.OperatorProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
                return BadRequest(new { message = "打手档案不存在" });

            var q = _db.OperatorWalletTransactions
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedAt);

            var total = await q.CountAsync();

            var items = await q
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(t => new
                {
                    id = t.Id,
                    type = t.Type,
                    amount = t.Amount,
                    orderNo = t.OrderNo,
                    status = t.Status,
                    remark = t.Remark,
                    isUnfrozen = t.IsUnfrozen,
                    unfreezeAt = t.UnfreezeAt,
                    createdAt = t.CreatedAt,
                    completedAt = t.CompletedAt
                })
                .ToListAsync();

            var totalIncome = await _db.OperatorWalletTransactions
                .AsNoTracking()
                .Where(t => t.UserId == userId
                         && t.Type == "income"
                         && t.Status == "completed")
                .SumAsync(t => (decimal?)t.Amount) ?? 0;

            var totalWithdrawn = await _db.OperatorWalletTransactions
                .AsNoTracking()
                .Where(t => t.UserId == userId
                         && t.Type == "withdraw"
                         && t.Status == "completed")
                .SumAsync(t => (decimal?)t.Amount) ?? 0;
            totalWithdrawn = Math.Abs(totalWithdrawn);

            // ⭐ 本周提现状态
            var weekStart = GetWeekStartUtc(DateTime.UtcNow);
            var nextWeekStart = weekStart.AddDays(7);

            var thisWeekWithdraw = await _db.OperatorWalletTransactions
                .AsNoTracking()
                .Where(t => t.UserId == userId
                         && t.Type == "withdraw"
                         && t.CreatedAt >= weekStart
                         && t.Status != "rejected")
                .OrderByDescending(t => t.CreatedAt)
                .FirstOrDefaultAsync();

            var canWithdraw = thisWeekWithdraw == null;

            return Ok(new
            {
                balance = profile.Balance,
                frozenBalance = profile.FrozenBalance,
                totalIncome,
                totalWithdrawn,
                // ⭐ 提现规则
                canWithdraw,
                minWithdrawAmount = 1m,
                weekStart,
                nextWeekStart,
                thisWeekWithdrawAt = thisWeekWithdraw?.CreatedAt,
                total,
                page,
                pageSize,
                items
            });
        }

        // ==================================================
        //  提现申请
        // ==================================================
        [HttpPost("withdraw")]
        public async Task<IActionResult> Withdraw([FromBody] WithdrawDto dto)
        {
            var userId = GetUserId();

            if (dto.Amount <= 0)
                return BadRequest(new { message = "提现金额必须大于 0" });
            if (dto.Amount < 1)
                return BadRequest(new { message = "单次提现最低 ¥1" });
            if (string.IsNullOrWhiteSpace(dto.ContactType) ||
                string.IsNullOrWhiteSpace(dto.ContactValue))
                return BadRequest(new { message = "请填写收款方式" });

            var profile = await _db.OperatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
                return BadRequest(new { message = "打手档案不存在" });

            if (profile.Balance < dto.Amount)
                return BadRequest(new
                {
                    message = $"可用余额不足，当前 ¥{profile.Balance:0.00}"
                });

            // ⭐ 每周只能提现一次
            var weekStart = GetWeekStartUtc(DateTime.UtcNow);
            var hasWithdrawnThisWeek = await _db.OperatorWalletTransactions
                .AnyAsync(t => t.UserId == userId
                            && t.Type == "withdraw"
                            && t.CreatedAt >= weekStart
                            && t.Status != "rejected");

            if (hasWithdrawnThisWeek)
                return BadRequest(new
                {
                    message = "每周只能申请一次提现，请下周再来",
                    nextWeekStart = weekStart.AddDays(7)
                });

            profile.Balance -= dto.Amount;

            _db.OperatorWalletTransactions.Add(new OperatorWalletTransaction
            {
                UserId = userId,
                Type = "withdraw",
                Amount = -dto.Amount,
                Status = "pending",
                Remark = $"提现 · {dto.ContactType}:{dto.ContactValue}" +
                          (string.IsNullOrWhiteSpace(dto.Remark) ? "" : $" · {dto.Remark}"),
                CreatedAt = DateTime.UtcNow
            });

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "提现申请已提交，客服将在 1-2 个工作日内处理",
                balance = profile.Balance,
                nextWeekStart = weekStart.AddDays(7)
            });
        }

        // ==================================================
        //  内部：惰性解冻
        // ==================================================
        private async Task ProcessUnfreezes(Guid userId)
        {
            var now = DateTime.UtcNow;

            var pending = await _db.OperatorWalletTransactions
                .Where(t => t.UserId == userId
                         && t.Type == "income"
                         && t.Status == "completed"
                         && !t.IsUnfrozen
                         && t.UnfreezeAt != null
                         && t.UnfreezeAt <= now)
                .ToListAsync();

            if (pending.Count == 0) return;

            var profile = await _db.OperatorProfiles
                .FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null) return;

            foreach (var t in pending)
            {
                profile.FrozenBalance -= t.Amount;
                profile.Balance += t.Amount;
                t.IsUnfrozen = true;
            }

            await _db.SaveChangesAsync();
        }

        // ==================================================
        //  内部：本周起点（UTC 周一 00:00）
        // ==================================================
        private static DateTime GetWeekStartUtc(DateTime now)
        {
            // DayOfWeek: Sunday = 0, Monday = 1, ..., Saturday = 6
            var dow = (int)now.DayOfWeek;
            var diff = (dow == 0) ? 6 : (dow - 1); // 距离周一的天数
            return now.Date.AddDays(-diff);
        }

        private Guid GetUserId()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? throw new UnauthorizedAccessException();
            return Guid.Parse(sub);
        }

        public class WithdrawDto
        {
            public decimal Amount { get; set; }
            public string ContactType { get; set; } = "";
            public string ContactValue { get; set; } = "";
            public string? Remark { get; set; }
        }
    }
}