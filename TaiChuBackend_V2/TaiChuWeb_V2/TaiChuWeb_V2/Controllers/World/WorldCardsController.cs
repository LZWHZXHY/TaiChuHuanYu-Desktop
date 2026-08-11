using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World
{
    [Route("api/world/projects/{projectId}/cards")]
    [ApiController]
    [Authorize]
    public class WorldCardsController : ControllerBase
    {
        private readonly IWorldCardService _cardService;
        private readonly IWorldQuotaService _quotaService;  // 🆕 新增

        public WorldCardsController(IWorldCardService cardService, IWorldQuotaService quotaService)  // 🆕 新增参数
        {
            _cardService = cardService;
            _quotaService = quotaService;  // 🆕
        }


        [HttpGet]
        public async Task<IActionResult> GetCards(Guid projectId, [FromQuery] string? type = null)
        {
            var userId = GetCurrentUserId();
            var cards = await _cardService.GetCardSummariesByProjectAsync(projectId, userId, type);
            return Ok(cards);
        }

        /// <summary>
        /// 获取单个卡片详情
        /// </summary>
        [HttpGet("{cardId}")]
        public async Task<IActionResult> GetCard(Guid projectId, Guid cardId)
        {
            var userId = GetCurrentUserId();
            var card = await _cardService.GetCardByIdAsync(cardId, userId);
            if (card == null)
                return NotFound(new { message = "卡片不存在或无权访问" });
            if (card.ProjectId != projectId)
                return NotFound(new { message = "卡片不属于该项目" });
            return Ok(card);
        }

        /// <summary>
        /// 创建新卡片
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateCard(Guid projectId, [FromBody] CreateCardDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var userId = GetCurrentUserId();

                // ✅ 检查词条配额
                var quotaCheck = await _quotaService.CanAddCardAsync(projectId, userId);
                if (!quotaCheck.CanAdd)
                {
                    return BadRequest(new
                    {
                        success = false,
                        code = "CARD_LIMIT_EXCEEDED",
                        message = quotaCheck.Message,
                        currentCount = quotaCheck.CurrentCount,
                        maxCount = quotaCheck.MaxCount
                    });
                }

                var card = await _cardService.CreateCardAsync(projectId, userId, dto);
                return CreatedAtAction(nameof(GetCard), new { projectId, cardId = card.Id }, card);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
        }

        /// <summary>
        /// 更新卡片
        /// </summary>
        [HttpPut("{cardId}")]
        public async Task<IActionResult> UpdateCard(Guid projectId, Guid cardId, [FromBody] UpdateCardDto dto)
        {
            var userId = GetCurrentUserId();

            var card = await _cardService.UpdateCardAsync(cardId, userId, dto);
            if (card == null)
                return NotFound(new { message = "卡片不存在或无权修改" });

            return Ok(card);
        }

        /// <summary>
        /// 删除卡片
        /// </summary>
        [HttpDelete("{cardId}")]
        public async Task<IActionResult> DeleteCard(Guid projectId, Guid cardId)
        {
            var userId = GetCurrentUserId();

            // ✅ 直接调用 Service，内部已做完整验证
            var result = await _cardService.DeleteCardAsync(cardId, userId);
            if (!result)
                return NotFound(new { message = "卡片不存在或无权删除" });

            return NoContent();
        }

        /// <summary>
        /// 从 JWT 中获取当前用户 ID
        /// </summary>
        private Guid GetCurrentUserId()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                throw new UnauthorizedAccessException("用户未认证");

            return Guid.Parse(userIdClaim);
        }
    }
}