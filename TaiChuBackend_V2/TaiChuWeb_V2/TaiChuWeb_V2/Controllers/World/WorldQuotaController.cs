using TaiChuWeb_V2.Dtos.World;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using TaiChuWeb_V2.Services.World;
using TaiChuWeb_V2.Utils;

namespace TaiChuWeb_V2.Controllers.World
{
    [Route("api/world/quota")]
    [Authorize]
    public class WorldQuotaController : WorldControllerBase
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
            var userId = CurrentUserId;
            var stats = await _quotaService.GetUserStatsAsync(userId);

            return Ok(new
            {
                usedWorldCount = stats.UsedWorldCount,
                maxWorldCount = stats.MaxWorldCount,
                remainingWorldCount = Math.Max(0, stats.MaxWorldCount - stats.UsedWorldCount),
                maxCardsPerWorld = stats.MaxCardsPerWorld,
                experience = stats.Experience
            });
        }

        /// <summary>
        /// 检查是否可以创建新世界观
        /// </summary>
        [HttpGet("can-create-project")]
        public async Task<IActionResult> CanCreateProject()
        {
            var userId = CurrentUserId;
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
            var userId = CurrentUserId;
            var result = await _quotaService.CanAddCardAsync(projectId, userId);

            return Ok(new
            {
                result.CanAdd,
                result.Message,
                result.CurrentCount,
                result.MaxCount
            });
        }

        // 扩容统一走交易行（额度类商品：MaxWorldCount / MaxCardsPerWorld），这里不再单独扣经验。
        [HttpPost("upgrade")]
        public IActionResult UpgradeQuota() =>
            StatusCode(StatusCodes.Status410Gone, new { success = false, message = "世界观扩容已移到交易行，请到交易行兑换" });

        /// <summary>
        /// 获取扩容历史
        /// </summary>
        [HttpGet("history")]
        public async Task<IActionResult> GetHistory([FromQuery, Range(0, int.MaxValue)] int limit = 20)
        {
            var userId = CurrentUserId;
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

    }
}
