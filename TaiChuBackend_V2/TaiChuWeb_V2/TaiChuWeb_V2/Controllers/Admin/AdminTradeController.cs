using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Trade;

namespace TaiChuWeb_V2.Controllers.Admin
{
    [Authorize]
    [ApiController]
    [Route("api/admin/trade")]
    public class AdminTradeController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminTradeController(AppDbContext context)
        {
            _context = context;
        }

        // 1. 获取所有资源（包括下架的），供管理员审计
        [HttpGet("items")]
        public async Task<IActionResult> GetAllItems()
        {
            // 这里建议配合权限策略，或者手动判断
            var items = await _context.StoreItems
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();
            return Ok(items);
        }

        // 2. 上架新资源 (POST)
        [HttpPost("items")]
        public async Task<IActionResult> CreateItem([FromBody] StoreItem newItem)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            newItem.CreatedAt = DateTime.Now;
            _context.StoreItems.Add(newItem);
            await _context.SaveChangesAsync();

            return Ok(newItem);
        }

        // 3. 调试/更新资源 (PUT)
        [HttpPut("items/{id}")]
        public async Task<IActionResult> UpdateItem(int id, [FromBody] StoreItem updatedItem)
        {
            if (id != updatedItem.Id) return BadRequest("ID 不匹配");

            _context.Entry(updatedItem).State = EntityState.Modified;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!await _context.StoreItems.AnyAsync(e => e.Id == id)) return NotFound();
                throw;
            }

            return Ok(updatedItem);
        }

        // 4. 快速切换状态（上架/下架）
        [HttpPatch("items/{id}/toggle")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var item = await _context.StoreItems.FindAsync(id);
            if (item == null) return NotFound();

            item.IsActive = !item.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { id = item.Id, isActive = item.IsActive });
        }
    }
}