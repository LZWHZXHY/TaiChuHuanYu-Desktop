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
        public WorldRelationsController(IWorldRelationService relationService, IWorldProjectService projectService)
        {
            _relationService = relationService;
            _projectService = projectService;  // 👈 新增
        }

        /// <summary>
        /// 获取卡片的所有关联（双向）
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetRelations(Guid cardId)
        {
            var userId = GetCurrentUserId();
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
            var result = await _relationService.DeleteRelationAsync(relationId, userId);
            if (!result)
                return NotFound(new { message = "关联不存在或无权删除" });

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

        [HttpGet("/api/world/projects/{projectId}/relations")]
        public async Task<IActionResult> GetProjectRelations(Guid projectId)
        {
            var userId = GetCurrentUserId();
            // 验证用户权限
            var project = await _projectService.GetProjectByIdAsync(projectId, userId);
            if (project == null)
                return NotFound();

            var relations = await _relationService.GetRelationsForProjectAsync(projectId);
            return Ok(relations);
        }
    }
}