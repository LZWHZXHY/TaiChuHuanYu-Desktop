using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Project;
using TaiChuWeb_V2.Dtos.Project;
using TaiChuWeb_V2.Services.Project;

namespace TaiChuWeb_V2.Controllers.Projects
{
    [Authorize]
    [ApiController]
    [Route("api/project/{projectId}/kanban")]
    public class ProjectKanbanController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IProjectPermissionService _permService;

        public ProjectKanbanController(AppDbContext context, IProjectPermissionService permService)
        {
            _context = context;
            _permService = permService;
        }

        private string CurrentUserId => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        #region --- 1. 动态分栏（Category）管理与列表排序 ---

        /// <summary>
        /// 获取当前项目的所有自定义分栏（按 SortOrder 升序）
        /// </summary>
        [HttpGet("categories")]
        public async Task<IActionResult> GetCategories(string projectId)
        {
            var categories = await _context.ProjectCategories
                .Where(c => c.ProjectId == projectId)
                .OrderBy(c => c.SortOrder)
                .ToListAsync();
            return Ok(categories);
        }

        /// <summary>
        /// 创建新的画布分栏（校验 task:manage，自动初始化 SortOrder）
        /// </summary>
        [HttpPost("categories")]
        public async Task<IActionResult> CreateCategory(string projectId, [FromBody] CreateCategoryDto dto)
        {
            if (!await _permService.HasPermissionAsync(projectId, CurrentUserId, "task:manage"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "权限不足，无法配置看板分栏" });
            }

            var maxSortOrder = await _context.ProjectCategories
                .Where(c => c.ProjectId == projectId)
                .Select(c => (double?)c.SortOrder)
                .MaxAsync() ?? 0.0;

            var category = new ProjectCategory
            {
                ProjectId = projectId,
                Name = dto.Name,
                ColorCode = dto.ColorCode ?? "#1a1a1a",
                CategoryType = dto.CategoryType, // 🌟 写入类型
                SortOrder = maxSortOrder + 1000.0
            };

            _context.ProjectCategories.Add(category);
            await _context.SaveChangesAsync();
            return Ok(category);
        }

        /// <summary>
        /// 局部修改分栏（改名、改色、改类型，校验 task:manage）
        /// </summary>
        [HttpPut("categories/{categoryId}")]
        public async Task<IActionResult> UpdateCategory(string projectId, string categoryId, [FromBody] UpdateCategoryDto dto)
        {
            if (!await _permService.HasPermissionAsync(projectId, CurrentUserId, "task:manage"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "权限不足，无法修改看板分栏" });
            }

            var category = await _context.ProjectCategories
                .FirstOrDefaultAsync(c => c.Id == categoryId && c.ProjectId == projectId);
            if (category == null) return NotFound("未寻得该分栏");

            if (!string.IsNullOrWhiteSpace(dto.Name))
            {
                category.Name = dto.Name;
            }

            if (!string.IsNullOrWhiteSpace(dto.ColorCode))
            {
                category.ColorCode = dto.ColorCode;
            }

            // 🌟 核心补充：允许更新分栏类型（0=常规, 1=已完成划线, 2=已归档置灰）
            if (dto.CategoryType.HasValue)
            {
                category.CategoryType = dto.CategoryType.Value;
            }

            await _context.SaveChangesAsync();
            return Ok(category);
        }

        /// <summary>
        /// 🌟 移动分栏列表的左右顺序（按二分折半算法计算权重）
        /// </summary>
        [HttpPut("categories/{categoryId}/move")]
        public async Task<IActionResult> MoveCategory(string projectId, string categoryId, [FromBody] DragMoveCategoryDto dto)
        {
            if (!await _permService.HasPermissionAsync(projectId, CurrentUserId, "task:manage"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "权限不足，无法调整分栏顺序" });
            }

            var category = await _context.ProjectCategories
                .FirstOrDefaultAsync(c => c.Id == categoryId && c.ProjectId == projectId);
            if (category == null) return NotFound("未寻得该分栏");

            if (dto.PrevSortOrder.HasValue && dto.NextSortOrder.HasValue)
            {
                category.SortOrder = (dto.PrevSortOrder.Value + dto.NextSortOrder.Value) / 2.0;
            }
            else if (dto.PrevSortOrder.HasValue)
            {
                category.SortOrder = dto.PrevSortOrder.Value + 1000.0;
            }
            else if (dto.NextSortOrder.HasValue)
            {
                category.SortOrder = dto.NextSortOrder.Value / 2.0;
            }
            else
            {
                category.SortOrder = 1000.0;
            }

            await _context.SaveChangesAsync();
            return NoContent();
        }

        /// <summary>
        /// 解构（删除）分栏（校验 task:manage）
        /// </summary>
        [HttpDelete("categories/{categoryId}")]
        public async Task<IActionResult> DeleteCategory(string projectId, string categoryId)
        {
            if (!await _permService.HasPermissionAsync(projectId, CurrentUserId, "task:manage"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "权限不足，无法解构看板分栏" });
            }

            var category = await _context.ProjectCategories
                .FirstOrDefaultAsync(c => c.Id == categoryId && c.ProjectId == projectId);
            if (category == null) return NotFound("未寻得该分栏");

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
        /// 获取完整的看板分栏及卡片数据（分栏按 SortOrder 排序）
        /// </summary>
        [HttpGet("board")]
        public async Task<IActionResult> GetKanbanBoard(string projectId)
        {
            var categories = await _context.ProjectCategories
                .Where(c => c.ProjectId == projectId)
                .OrderBy(c => c.SortOrder)
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
                c.SortOrder,
                c.CategoryType, // 🌟 必须将 CategoryType 输送给前端，用于渲染特殊视觉效果
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
        /// 注入新意图（创建卡片，校验 task:manage）
        /// </summary>
        [HttpPost("tasks")]
        public async Task<IActionResult> CreateTask(string projectId, [FromBody] CreateTaskDto dto)
        {
            if (!await _permService.HasPermissionAsync(projectId, CurrentUserId, "task:manage"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "您在此项目中未被赋予发布意图任务的权限" });
            }

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
                Points = 1,
                EstimatedHours = 0m,
                ActualHours = 0m,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ProjectTasks.Add(newTask);
            await _context.SaveChangesAsync();

            return Ok(newTask);
        }

        /// <summary>
        /// 更新意图卡片细节（包含量化指标鉴权）
        /// </summary>
        [HttpPut("tasks/{taskId}")]
        public async Task<IActionResult> UpdateTaskDetails(string projectId, string taskId, [FromBody] UpdateTaskDto dto)
        {
            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null) return NotFound("未寻得该意图节点");

            bool canManage = await _permService.HasPermissionAsync(projectId, CurrentUserId, "task:manage");
            bool canMoveSelf = await _permService.HasPermissionAsync(projectId, CurrentUserId, "task:move_self");

            if (!canManage && (!canMoveSelf || task.AssigneeId != CurrentUserId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "无权修改该任务卡片" });
            }

            // 常规属性更新
            if (!string.IsNullOrWhiteSpace(dto.Title)) task.Title = dto.Title;
            task.Description = dto.Description;
            task.Priority = dto.Priority ?? 1;
            task.StartDate = dto.StartDate;
            task.DueDate = dto.DueDate;
            task.Tags = dto.Tags;
            task.CategoryId = string.IsNullOrWhiteSpace(dto.CategoryId) ? null : dto.CategoryId;

            // 🌟 核心权限拦截：仅管理者可转派负责人及修改预估工时、贡献点数
            if (canManage)
            {
                task.AssigneeId = string.IsNullOrWhiteSpace(dto.AssigneeId) ? null : dto.AssigneeId;
                if (dto.Points.HasValue) task.Points = dto.Points.Value;
                if (dto.EstimatedHours.HasValue) task.EstimatedHours = dto.EstimatedHours.Value;
            }

            // 实际耗时：协作者与管理者均可自由填报登记
            if (dto.ActualHours.HasValue) task.ActualHours = dto.ActualHours.Value;

            task.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return Ok(task);
        }

        /// <summary>
        /// 拖拽移动任务（校验 task:manage 或执行者流转个人任务）
        /// </summary>
        [HttpPut("tasks/{taskId}/move")]
        public async Task<IActionResult> MoveTask(string projectId, string taskId, [FromBody] DragMoveTaskDto dto)
        {
            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);
            if (task == null) return NotFound("未寻得该意图节点");

            bool canManage = await _permService.HasPermissionAsync(projectId, CurrentUserId, "task:manage");
            bool canMoveSelf = await _permService.HasPermissionAsync(projectId, CurrentUserId, "task:move_self");

            if (!canManage)
            {
                if (!canMoveSelf || task.AssigneeId != CurrentUserId)
                {
                    return StatusCode(StatusCodes.Status403Forbidden, new { message = "观察者或无权人员禁止变更任务状态" });
                }
            }

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

        /// <summary>
        /// 抹除意图（校验 task:manage）
        /// </summary>
        [HttpDelete("tasks/{taskId}")]
        public async Task<IActionResult> DeleteTask(string projectId, string taskId)
        {
            if (!await _permService.HasPermissionAsync(projectId, CurrentUserId, "task:manage"))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "权限不足，无法抹除意图任务" });
            }

            var task = await _context.ProjectTasks
                .FirstOrDefaultAsync(t => t.Id == taskId && t.ProjectId == projectId);

            if (task == null)
                return NotFound("未寻得该意图节点");

            _context.ProjectTasks.Remove(task);
            await _context.SaveChangesAsync();

            return Ok("意图已从画布中抹除");
        }

        /// <summary>
        /// 平铺拉取任务列表（输送完整量化指标）
        /// </summary>
        [HttpGet("/api/project/{projectId}/tasks")]
        public async Task<IActionResult> GetProjectTasks(string projectId)
        {
            var projectExists = await _context.Projects.AnyAsync(p => p.Id == projectId);
            if (!projectExists) return NotFound("未寻得指定的项目灵脉");

            var tasksWithCategory = await _context.ProjectTasks
                .Where(t => t.ProjectId == projectId)
                .GroupJoin(
                    _context.ProjectCategories,
                    task => task.CategoryId,
                    category => category.Id,
                    (task, categories) => new { task, categories }
                )
                .SelectMany(
                    x => x.categories.DefaultIfEmpty(),
                    (x, category) => new
                    {
                        x.task.Id,
                        x.task.Title,
                        x.task.Description,
                        x.task.Status,
                        x.task.Priority,
                        x.task.StartDate,
                        x.task.DueDate,
                        x.task.AssigneeId,
                        x.task.Tags,
                        x.task.SortOrder,
                        x.task.CategoryId,
                        x.task.Points,
                        x.task.EstimatedHours,
                        x.task.ActualHours,
                        CategoryName = category != null ? category.Name : "游离意图",
                        CategoryColor = category != null ? category.ColorCode : "#eee"
                    }
                )
                .OrderBy(t => t.SortOrder)
                .ToListAsync();

            return Ok(tasksWithCategory);
        }

        #endregion


        public class DragMoveCategoryDto
        {
            public double? PrevSortOrder { get; set; }
            public double? NextSortOrder { get; set; }
        }
    }
} 