using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World
{
    [Route("api/world/cards/{cardId}/relations")]
    [ApiController]
    [Authorize]
    public class WorldRelationsController : ControllerBase
    {
        private readonly IWorldRelationService _relationService;
        private readonly IWorldProjectService _projectService;
        private readonly IWorldCardService _cardService;

        public WorldRelationsController(
            IWorldRelationService relationService,
            IWorldProjectService projectService,
            IWorldCardService cardService)
        {
            _relationService = relationService;
            _projectService = projectService;
            _cardService = cardService;
        }

        /// <summary>
        /// 获取卡片的所有关联（双向）
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetRelations(Guid cardId)
        {
            var userId = GetCurrentUserId();

            // ✅ 优化：验证卡片是否存在且用户有权访问
            if (!await _cardService.IsCardAccessibleAsync(cardId, userId))
                return NotFound(new { message = "卡片不存在或无权访问" });

            var relations = await _relationService.GetRelationsForCardAsync(cardId, userId);
            return Ok(relations);
        }

        /// <summary>
        /// 创建新关联（从当前卡片指向目标卡片）
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> CreateRelation(Guid cardId, [FromBody] CreateRelationDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            try
            {
                var userId = GetCurrentUserId();
                var relation = await _relationService.CreateRelationAsync(cardId, userId, dto);
                return CreatedAtAction(nameof(GetRelations), new { cardId }, relation);
            }
            catch (UnauthorizedAccessException ex)
            {
                return Forbid(ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>
        /// 删除关联
        /// </summary>
        [HttpDelete("{relationId}")]
        public async Task<IActionResult> DeleteRelation(Guid cardId, Guid relationId)
        {
            var userId = GetCurrentUserId();

            // ✅ 优化：验证卡片是否存在且用户有权访问
            if (!await _cardService.IsCardAccessibleAsync(cardId, userId))
                return NotFound(new { message = "卡片不存在或无权访问" });

            var result = await _relationService.DeleteRelationAsync(relationId, userId);
            if (!result)
                return NotFound(new { message = "关联不存在或无权删除" });

            return NoContent();
        }

        /// <summary>
        /// 获取项目下所有关联（用于关系图谱）
        /// </summary>
        [HttpGet("/api/world/projects/{projectId}/relations")]
        public async Task<IActionResult> GetProjectRelations(Guid projectId)
        {
            var userId = GetCurrentUserId();

            // ✅ 优化：使用轻量级权限验证
            if (!await _projectService.IsProjectAccessibleAsync(projectId, userId))
                return NotFound(new { message = "项目不存在或无权访问" });

            var relations = await _relationService.GetRelationsForProjectAsync(projectId);
            return Ok(relations);
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