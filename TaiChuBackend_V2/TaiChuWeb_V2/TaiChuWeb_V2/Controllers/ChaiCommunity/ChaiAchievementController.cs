using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers.ChaiCommunity
{
    public class ChaiAchievementDto
    {
        public string Key { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public bool Unlocked { get; set; }
        public int Progress { get; set; }
        public int Target { get; set; }
    }

    /// <summary>柴圈成就（根据用户已有数据实时推导，无需额外存储）。</summary>
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ChaiAchievementController : ControllerBase
    {
        private readonly AppDbContext _context;
        public ChaiAchievementController(AppDbContext context) { _context = context; }

        [HttpGet("my")]
        public async Task<IActionResult> My()
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();

            var ocCount = await _context.StickmanCharacters.CountAsync(c => c.AuthorId == userId);
            var battleWins = await _context.StickmanCharacters.Where(c => c.AuthorId == userId).SumAsync(c => c.BattleWins);
            var workCount = await _context.ChaiWorks.CountAsync(w => w.AuthorId == userId);
            var jointOrganized = await _context.JointActivities.CountAsync(j => j.OrganizerId == userId);
            var jointParticipated = await _context.JointParticipants.CountAsync(p => p.UserId == userId);

            var achievements = new List<ChaiAchievementDto>
            {
                new() { Key = "first_oc", Label = "初入柴圈", Icon = "🎭", Unlocked = ocCount >= 1, Progress = ocCount, Target = 1 },
                new() { Key = "oc_master", Label = "角色大师", Icon = "🏛️", Unlocked = ocCount >= 5, Progress = ocCount, Target = 5 },
                new() { Key = "first_win", Label = "首战告捷", Icon = "⚔️", Unlocked = battleWins >= 1, Progress = battleWins, Target = 1 },
                new() { Key = "battle_master", Label = "常胜将军", Icon = "🏆", Unlocked = battleWins >= 10, Progress = battleWins, Target = 10 },
                new() { Key = "first_work", Label = "动画师", Icon = "🎬", Unlocked = workCount >= 1, Progress = workCount, Target = 1 },
                new() { Key = "work_master", Label = "高产动画师", Icon = "🎥", Unlocked = workCount >= 5, Progress = workCount, Target = 5 },
                new() { Key = "organizer", Label = "发起者", Icon = "📣", Unlocked = jointOrganized >= 1, Progress = jointOrganized, Target = 1 },
                new() { Key = "collaborator", Label = "联合作者", Icon = "🤝", Unlocked = jointParticipated >= 1, Progress = jointParticipated, Target = 1 },
            };

            return Ok(new { achievements, unlockedCount = achievements.Count(a => a.Unlocked) });
        }
    }
}
