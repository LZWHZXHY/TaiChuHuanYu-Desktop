using Microsoft.AspNetCore.Mvc;
using TaiChuWeb_V2.Models.Tag;
using TaiChuWeb_V2.Services.Tags; // 确保引入刚才修改后的 ITagService 命名空间

namespace TaiChuWeb_V2.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class TagController : ControllerBase
    {
        private readonly ITagService _tagService;

        public TagController(ITagService tagService)
        {
            _tagService = tagService;
        }

        /// <summary>
        /// 1. 获取某个实体的所有标签
        /// GET: api/tag/{entityType}/{entityId}
        /// </summary>
        [HttpGet("{entityType}/{entityId}")]
        public async Task<ActionResult<List<Tag>>> GetTags(string entityType, string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId))
            {
                return BadRequest("EntityType and EntityId are required.");
            }

            var tags = await _tagService.GetTagsAsync(entityType, entityId);
            return Ok(tags);
        }

        /// <summary>
        /// 2. 批量获取多个实体的标签（列表页防 N+1 查询）
        /// POST: api/tag/{entityType}/batch
        /// </summary>
        [HttpPost("{entityType}/batch")]
        public async Task<ActionResult<Dictionary<string, List<Tag>>>> GetTagsForEntities(string entityType, [FromBody] List<string> entityIds)
        {
            if (string.IsNullOrWhiteSpace(entityType) || entityIds == null || !entityIds.Any())
            {
                return BadRequest("EntityType and EntityIds cannot be empty.");
            }

            var tagsMap = await _tagService.GetTagsForEntitiesAsync(entityType, entityIds);
            return Ok(tagsMap);
        }

        /// <summary>
        /// 3. 更新某个实体的标签
        /// PUT: api/tag/{entityType}/{entityId}
        /// </summary>
        [HttpPut("{entityType}/{entityId}")]
        public async Task<IActionResult> UpdateTags(string entityType, string entityId, [FromBody] List<string> tagNames)
        {
            if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId))
            {
                return BadRequest("EntityType and EntityId are required.");
            }

            // 更新标签（会自动处理新增 Tag、建立关联、删除废弃关联）
            await _tagService.UpdateTagsAsync(entityType, entityId, tagNames ?? new List<string>());

            return NoContent();
        }

        /// <summary>
        /// 4. 清空某个实体的所有标签关联
        /// DELETE: api/tag/{entityType}/{entityId}
        /// </summary>
        [HttpDelete("{entityType}/{entityId}")]
        public async Task<IActionResult> DeleteTags(string entityType, string entityId)
        {
            if (string.IsNullOrWhiteSpace(entityType) || string.IsNullOrWhiteSpace(entityId))
            {
                return BadRequest("EntityType and EntityId are required.");
            }

            await _tagService.RemoveTagsFromEntityAsync(entityType, entityId);
            return NoContent();
        }
    }
}