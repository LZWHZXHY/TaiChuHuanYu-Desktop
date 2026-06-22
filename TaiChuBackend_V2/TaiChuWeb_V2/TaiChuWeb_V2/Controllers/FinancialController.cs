using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Financial;

namespace TaiChuWeb_V2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class FinancialController : ControllerBase
    {
        private readonly AppDbContext _context;

        public FinancialController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// 获取所有财政公示账目明细（按入账时间倒序排序）
        /// GET: api/financial/report
        /// </summary>
        [HttpGet("report")]
        public async Task<IActionResult> GetFinancialReport()
        {
            try
            {
                var report = await _context.Financials
                    .OrderByDescending(f => f.date)
                    .ToListAsync();

                return Ok(report);
            }
            catch (Exception ex)
            {
                // 生产环境建议引入 ILogger 记录日志
                return StatusCode(500, new { message = "审计国库流水失败，请联系管理员", details = ex.Message });
            }
        }

        /// <summary>
        /// 新增一条财务收支流水（供后台更新数据使用）
        /// POST: api/financial/add
        /// </summary>
        [HttpPost("add")]
        public async Task<IActionResult> AddFinancialRecord([FromBody] Financial model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { message = "账目契约校验失败，请补全必填字段" });
            }

            try
            {
                // 如果传入的时间是 UTC 格式，且你的 MySQL 没有特别配置，可以视情况统一转为本地时间
                if (model.date == default)
                {
                    model.date = DateTime.Now;
                }

                _context.Financials.Add(model);
                await _context.SaveChangesAsync();

                return Ok(new { message = "🎉 财务流水更新成功，国库账目已刷新！", index = model.index });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "账目入库失败，请检查数据库连接", details = ex.Message });
            }
        }
    }
}