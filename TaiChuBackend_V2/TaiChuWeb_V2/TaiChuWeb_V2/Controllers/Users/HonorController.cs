using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers.Users
{
    [ApiController]
    [Route("api/community")]
    public class HonorController : ControllerBase
    {
        private readonly AppDbContext _context;

        public HonorController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet("honor-wall")]
        public async Task<IActionResult> GetHonorWall()
        {
            // 查询所有对社区有实际贡献的用户（经验 > 0 或 贡献点数 > 0）
            var contributors = await _context.Users
                .Include(u => u.Stats)
                .Where(u => u.Stats != null && (u.Stats.Experience > 0 || u.Stats.ContributionPoints > 0))
                .Select(u => new
                {
                    id = u.Id,
                    username = u.Username,
                    title = u.Stats!.Title ?? "社区共建者",
                    totalExp = u.Stats.Experience,
                    totalPoints = u.Stats.ContributionPoints
                })
                .OrderByDescending(u => u.totalExp)
                .ToListAsync();

            // 统计宏观总览数据
            var stats = new
            {
                totalPoints = contributors.Sum(c => c.totalPoints),
                totalExp = contributors.Sum(c => c.totalExp)
            };

            // 返回完全吻合 Vue 前端解构要求的数据格式
            return Ok(new
            {
                stats = stats,
                list = contributors
            });
        }
    }
}