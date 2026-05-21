using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext; // 替换为你的真实 DbContext 命名空间
using TaiChuWeb_V2.Models.Project;
using TaiChuWeb_V2.Dtos.Project; // 如果你有自己单独拆分 DTO 文件，可以用这个，这里为了闭环我们写在下面

namespace TaiChuWeb_V2.Controllers.Projects
{
    [Authorize]
    [ApiController]
    [Route("api/project/{projectId}/kanban")]
    public class ProjectKanbanController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ProjectKanbanController(AppDbContext context)
        {
            _context = context;
        }

        #region --- 1. 动态分栏（Category）管理 ---

        /// <summary>
        /// 获取当前项目的所有自定义分栏
        /// </summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories(string projectId)
        {
            var categories = await _context.ProjectCategories
                .Where(c => c.ProjectId == projectId)
                .OrderBy(c => c.CreatedAt) // 默认按创建时间作为栏目顺序
                .ToListAsync();
            return Ok(categories);
        }

        /// <summary>
        /// 创建新的画布分栏
        /// </summary>
        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory(string projectId, [FromBody] CreateCategoryDto dto)
        {
            var category = new ProjectCategory
            {
                ProjectId = projectId,
                Name = dto.Name,
                ColorCode = dto.ColorCode ?? "#1a1a1a" // 默认纯黑禅意色
            };

            _context.ProjectCategories.Add(category);
            await _context.SaveChangesAsync();
            return Ok(category);
        }

        /// <summary>
        /// 局部修改分栏（改名、改色）
        /// </summary>
        [HttpPut("categories/{categoryId}")]
        public async Task<IActionResult> UpdateCategory(string projectId, string categoryId, [FromBody] UpdateCategoryDto dto)
        {
            var category = await _context.ProjectCategories
                .FirstOrDefaultAsync(c => c.Id == categoryId && c.ProjectId == projectId);
            if (category == null) return NotFound("未寻得该分栏");

            // 核心逻辑：只更新前端传过来的值，没传的保持原样
            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                category.Name = dto.Name;
            }

            if (!string.IsNullOrWhiteSpace(dto.ColorCode))
            {
                category.ColorCode = dto.ColorCode;
            }

            await _context.SaveChangesAsync();
            return Ok(category);
        }

        /// <summary>
        /// 解构（删除）分栏
        /// </summary>
        [HttpDelete("categories/{categoryId}")]
        public async Task<IActionResult> DeleteCategory(string projectId, string categoryId)
        {
            var category = await _context.ProjectCategories
                .FirstOrDefaultAsync(c => c.Id == categoryId && c.ProjectId == projectId);
            if (category == null) return NotFound("未寻得该分栏");

            // 🌟 核心逻辑：将原本属于这个栏目的任务全部退回“游离意图（未分类）”
            var tasks = await _context.ProjectTasks
                .Where(t => t.ProjectId == projectId && t.CategoryId == categoryId)
                .ToListAsync();

            foreach (var task in tasks)
            {
                task.CategoryId = null;
            }

            _context.ProjectCategories.Remove(category);
            await _context.SaveChangesAsync();
            return Ok("分栏已解构，意图已归入游离池");
        }

        #endregion

        #region --- 2. 看板核心数据流与意图管理 ---

        /// <summary>
        /// 核心：获取完整的动态画布结构（包含分类及各自旗下的任务）
        /// </summary>
        [HttpGet("board")]
        public async Task<IActionResult> GetKanbanBoard(string projectId)
        {
            var categories = await _context.ProjectCategories
                .Where(c => c.ProjectId == projectId)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();

            var allTasks = await _context.ProjectTasks
                .Where(t => t.ProjectId == projectId)
                .OrderBy(t => t.SortOrder)
                .ToListAsync();

            var board = categories.Select(c => new
            {
                c.Id,
                c.Name,
                c.ColorCode,
                Tasks = allTasks.Where(t => t.CategoryId == c.Id).ToList()
            }).ToList();

            var unclassifiedTasks = allTasks.Where(t => t.CategoryId == null).ToList();

            return Ok(new
            {
                Board = board,
                Unclassified = unclassifiedTasks
            });
        }

        /// <summary>
        /// 注入新意图（创建卡片）
        /// </summary>
        [HttpPost("tasks")]
        public async Task<IActionResult> CreateTask(string projectId, [FromBody] CreateTaskDto dto)
        {
            var maxSortOrder = await _context.ProjectTasks
                .Where(t => t.ProjectId == projectId && t.CategoryId == dto.CategoryId)
                .Select(t => (double?)t.SortOrder)
                .MaxAsync() ?? 0.0;

            var newTask = new ProjectTask
            {
                ProjectId = projectId,
                Title = dto.Title,
                Status = dto.Status,
                CategoryId = dto.CategoryId,
                SortOrder = maxSortOrder + 1000.0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ProjectTasks.Add(newTask);
            await _context.SaveChangesAsync();

            return Ok(newTask);
        }

        /// <summary>
        /// 🌟 全量更新意图卡片细节（用于超级弹窗的保存）
        /// </summary>
        [HttpPut("tasks/{taskId}")]
        public async Task<IActionResult> UpdateTaskDetails(string projectId, string taskId, [FromBody] UpdateTaskDto dto)
        {
            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null) return NotFound("未寻得该意图节点");

            // 映射前端传来的细节数据
            if (!string.IsNullOrWhiteSpace(dto.Title)) task.Title = dto.Title;

            task.Description = dto.Description;
            task.Priority = dto.Priority ?? 1; // 默认常规优先级
            task.DueDate = dto.DueDate;
            task.Tags = dto.Tags;

            // 处理可能被清空的外键
            task.CategoryId = string.IsNullOrWhiteSpace(dto.CategoryId) ? null : dto.CategoryId;
            task.AssigneeId = string.IsNullOrWhiteSpace(dto.AssigneeId) ? null : dto.AssigneeId;

            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(task);
        }

        /// <summary>
        /// 处理卡片在任意分栏、任意位置之间的拖拽排序同步
        /// </summary>
        [HttpPut("tasks/{taskId}/move")]
        public async Task<IActionResult> MoveTask(string projectId, string taskId, [FromBody] DragMoveTaskDto dto)
        {
            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null) return NotFound("未寻得该意图节点");

            task.CategoryId = string.IsNullOrEmpty(dto.TargetCategoryId) ? null : dto.TargetCategoryId;

            if (dto.PrevSortOrder.HasValue && dto.NextSortOrder.HasValue)
            {
                task.SortOrder = (dto.PrevSortOrder.Value + dto.NextSortOrder.Value) / 2.0;
            }
            else if (dto.PrevSortOrder.HasValue)
            {
                task.SortOrder = dto.PrevSortOrder.Value + 1000.0;
            }
            else if (dto.NextSortOrder.HasValue)
            {
                task.SortOrder = dto.NextSortOrder.Value / 2.0;
            }
            else
            {
                task.SortOrder = 1000.0;
            }

            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return NoContent();
        }

        #endregion
    }

  
    
}