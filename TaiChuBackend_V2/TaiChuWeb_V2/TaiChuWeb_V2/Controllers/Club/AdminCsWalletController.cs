using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers.Club
{
    /// <summary>后台：客服钱包提现审核</summary>
    [ApiController]
    [Route("api/admin/club/cs-wallet")]
    [Authorize]
    public class AdminCsWalletController : ControllerBase
    {
        private readonly AppDbContext _db;
        public AdminCsWalletController(AppDbContext db) => _db = db;

        [HttpGet("withdraws")]
        public async Task<IActionResult> GetWithdraws(
            [FromQuery] string? status,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 100) pageSize = 20;

            var q = _db.CustomerServiceWalletTransactions
                .AsNoTracking()
                .Where(t => t.Type == "withdraw");

            if (!string.IsNullOrWhiteSpace(status))
                q = q.Where(t => t.Status == status);

            var total = await q.CountAsync();

            var raw = await q
                .OrderByDescending(t => t.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var csIds = raw.Select(t => t.CustomerServiceId).Distinct().ToList();
            var css = await _db.CustomerServices
                .AsNoTracking()
                .Where(c => csIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            var items = raw.Select(t =>
            {
                css.TryGetValue(t.CustomerServiceId, out var cs);
                return new
                {
                    id = t.Id,
                    userId = t.UserId,
                    customerServiceId = t.CustomerServiceId,
                    nickname = cs?.Name ?? "",
                    wechat = cs?.Wechat ?? "",
                    amount = Math.Abs(t.Amount),
                    status = t.Status,
                    remark = t.Remark,
                    createdAt = t.CreatedAt,
                    completedAt = t.CompletedAt
                };
            }).ToList();

            return Ok(new { total, page, pageSize, items });
        }

        [HttpPost("withdraw/{id:long}/approve")]
        public async Task<IActionResult> Approve(long id)
        {
            var tx = await _db.CustomerServiceWalletTransactions.FindAsync(id);
            if (tx == null || tx.Type != "withdraw")
                return NotFound(new { message = "提现记录不存在" });
            if (tx.Status != "pending")
                return BadRequest(new { message = $"当前状态 {tx.Status}，无法通过" });

            tx.Status = "completed";
            tx.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(new { message = "已通过" });
        }

        [HttpPost("withdraw/{id:long}/reject")]
        public async Task<IActionResult> Reject(long id, [FromBody] RejectDto dto)
        {
            var tx = await _db.CustomerServiceWalletTransactions.FindAsync(id);
            if (tx == null || tx.Type != "withdraw")
                return NotFound(new { message = "提现记录不存在" });
            if (tx.Status != "pending")
                return BadRequest(new { message = $"当前状态 {tx.Status}，无法拒绝" });

            var cs = await _db.CustomerServices
                .FirstOrDefaultAsync(c => c.Id == tx.CustomerServiceId);
            if (cs == null)
                return BadRequest(new { message = "客服档案不存在" });

            var refund = Math.Abs(tx.Amount);
            cs.Balance += refund;

            tx.Status = "rejected";
            tx.CompletedAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(dto?.Reason))
                tx.Remark = $"{tx.Remark} | 拒绝原因：{dto.Reason}";

            await _db.SaveChangesAsync();
            return Ok(new { message = "已拒绝并退款" });
        }

        public class RejectDto
        {
            public string? Reason { get; set; }
        }
    }
}