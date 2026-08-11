using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TaiChuWeb_V2.Services.World;
using TaiChuWeb_V2.Utils;

namespace TaiChuWeb_V2.Controllers.World
{
    [Route("api/world/quota")]
    [ApiController]
    [Authorize]
    public class WorldQuotaController : ControllerBase
    {
        private readonly IWorldQuotaService _quotaService;

        public WorldQuotaController(IWorldQuotaService quotaService)
        {
            _quotaService = quotaService;
        }

        /// <summary>
        /// 获取当前用户的配额信息
        /// </summary>
        [HttpGet("my")]
        public async Task<IActionResult> GetMyQuota()
        {
            var userId = GetCurrentUserId();
            var stats = await _quotaService.GetUserStatsAsync(userId);

            return Ok(new
            {
                usedWorldCount = stats.UsedWorldCount,
                maxWorldCount = stats.MaxWorldCount,
                remainingWorldCount = stats.MaxWorldCount - stats.UsedWorldCount,
                maxCardsPerWorld = stats.MaxCardsPerWorld,
                experience = stats.Experience,
                expCostPerWorldSlot = WorldQuotaConstants.EXP_COST_PER_WORLD_SLOT,
                expCostPer10Cards = WorldQuotaConstants.EXP_COST_PER_10_CARDS
            });
        }

        /// <summary>
        /// 检查是否可以创建新世界观
        /// </summary>
        [HttpGet("can-create-project")]
        public async Task<IActionResult> CanCreateProject()
        {
            var userId = GetCurrentUserId();
            var result = await _quotaService.CanCreateProjectAsync(userId);

            return Ok(new
            {
                result.CanCreate,
                result.Message,
                result.Used,
                result.Max
            });
        }

        /// <summary>
        /// 检查指定项目是否可以添加新卡片
        /// </summary>
        [HttpGet("can-add-card/{projectId}")]
        public async Task<IActionResult> CanAddCard(Guid projectId)
        {
            var userId = GetCurrentUserId();
            var result = await _quotaService.CanAddCardAsync(projectId, userId);

            return Ok(new
            {
                result.CanAdd,
                result.Message,
                result.CurrentCount,
                result.MaxCount
            });
        }

        /// <summary>
        /// 用经验扩容配额
        /// </summary>
        [HttpPost("upgrade")]
        public async Task<IActionResult> UpgradeQuota([FromBody] UpgradeQuotaRequest request)
        {
            var userId = GetCurrentUserId();
            var result = await _quotaService.UpgradeQuotaAsync(userId, request.UpgradeType);

            if (!result.Success)
            {
                return BadRequest(new
                {
                    success = false,
                    message = result.Message,
                    remainingExp = result.RemainingExp
                });
            }

            return Ok(new
            {
                success = true,
                message = result.Message,
                newValue = result.NewValue,
                previousValue = result.PreviousValue,
                costExp = result.CostExp,
                remainingExp = result.RemainingExp,
                upgradeType = result.UpgradeTypeName
            });
        }

        /// <summary>
        /// 获取扩容历史
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery] int limit = 20)
        {
            var userId = GetCurrentUserId();
            var history = await _quotaService.GetUpgradeHistoryAsync(userId, limit);

            return Ok(history.Select(h => new
            {
                h.UpgradeType,
                h.Amount,
                h.CostExp,
                h.PreviousValue,
                h.NewValue,
                h.CreatedAt
            }));
        }

        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("用户未认证");
            return Guid.Parse(userIdClaim);
        }
    }

    public class UpgradeQuotaRequest
    {
        public QuotaUpgradeType UpgradeType { get; set; }
    }
}