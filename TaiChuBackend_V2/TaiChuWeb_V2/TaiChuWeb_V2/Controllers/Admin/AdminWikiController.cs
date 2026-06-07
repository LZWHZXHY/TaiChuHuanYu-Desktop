using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Wiki;

namespace TaiChuWeb_V2.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/wiki/categories")]
    // [Authorize(Roles = "Admin")] 
    public class AdminWikiController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminWikiController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllCategories()
        {
            var categories = await _context.WikiCategories
                .OrderBy(c => c.SortOrder)
                .ThenBy(c => c.Id)
                .ToListAsync();

            return Ok(categories);
        }


      


        [HttpPost]
        public async Task<IActionResult> CreateCategory([FromBody] CategoryRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { message = "分类名称不能为空" });

            var exists = await _context.WikiCategories.AnyAsync(c => c.Name == request.Name.Trim());
            if (exists)
                return BadRequest(new { message = "已存在同名的分类" });

            // 🌟 完整映射新增字段
            var category = new WikiCategory
            {
                Name = request.Name.Trim(),
                ParentId = request.ParentId,
                SortOrder = request.SortOrder,
                OwnerId = request.OwnerId,           // 绑定所有者
                OwnershipType = request.OwnershipType, // 绑定归属模式
                NeedsReview = true                   // 默认开启审核
            };

            _context.WikiCategories.Add(category);
            await _context.SaveChangesAsync();

            return Ok(category);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateCategory(int id, [FromBody] CategoryRequestDto request)
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return BadRequest(new { message = "分类名称不能为空" });

            var category = await _context.WikiCategories.FindAsync(id);
            if (category == null)
                return NotFound(new { message = "未找到该分类" });

            if (request.ParentId == id)
                return BadRequest(new { message = "分类不能作为自己的父级节点" });

            var nameExists = await _context.WikiCategories
                .AnyAsync(c => c.Name == request.Name.Trim() && c.Id != id);
            if (nameExists)
                return BadRequest(new { message = "已存在同名的其他分类" });

            // 🌟 更新字段
            category.Name = request.Name.Trim();
            category.ParentId = request.ParentId;
            category.SortOrder = request.SortOrder;
            category.OwnerId = request.OwnerId;
            category.OwnershipType = request.OwnershipType;

            await _context.SaveChangesAsync();

            return Ok(category);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteCategory(int id)
        {
            var category = await _context.WikiCategories.FindAsync(id);
            if (category == null)
                return NotFound(new { message = "未找到该分类" });

            var hasChildren = await _context.WikiCategories.AnyAsync(c => c.ParentId == id);
            if (hasChildren)
                return BadRequest(new { message = "该分类下包含子分类，请先处理子分类" });

            var hasArticles = await _context.WikiArticles.AnyAsync(a => a.CategoryId == id);
            if (hasArticles)
                return BadRequest(new { message = "该分类下已有关联词条，无法直接删除" });

            _context.WikiCategories.Remove(category);
            await _context.SaveChangesAsync();

            return Ok(new { message = "分类删除成功" });
        }


        // ==========================================
        // 🌟 下面是新增的【分类申请审核】相关接口
        // ==========================================

        [HttpGet("/api/admin/wiki/requests")]
        public async Task<IActionResult> GetCategoryRequests()
        {
            // 只查询状态为 0 (待审) 的申请
            var requests = await _context.WikiCategoryRequests
                .Where(r => r.Status == 0)
                .OrderByDescending(r => r.Id)
                .ToListAsync();

            // 映射成前端 ICategoryRequest 期望的格式
            var result = requests.Select(r => new
            {
                id = r.Id,
                name = r.CategoryName, // 数据库里叫 CategoryName，前端期望叫 name
                reason = r.Reason,
                parentId = r.ParentId,
                sortOrder = r.SortOrder
            });

            return Ok(result);
        }

        [HttpPost("/api/admin/wiki/requests/{id}/approve")]
        public async Task<IActionResult> ApproveCategoryRequest(int id)
        {
            // 开启数据库事务，确保原子性
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var requestItem = await _context.WikiCategoryRequests.FindAsync(id);
                if (requestItem == null || requestItem.Status != 0)
                    return NotFound(new { message = "该申请不存在或已被处理" });

                // 1. 创建分类
                var newCategory = new WikiCategory
                {
                    Name = requestItem.CategoryName,
                    ParentId = requestItem.ParentId, // 前端传来的父级ID在此生效
                    SortOrder = requestItem.SortOrder,
                    OwnershipType = 0,
                    OwnerId = null,
                    NeedsReview = false // 已审核通过
                };

                _context.WikiCategories.Add(newCategory);

                // 2. 批准当前申请
                requestItem.Status = 1;

                // 3. 🌟 扩展逻辑：如果存在针对同一个文章ID的其它重复申请，在此一并驳回
                // 避免脏数据
                var otherRequests = await _context.WikiCategoryRequests
                    .Where(r => r.CategoryName == requestItem.CategoryName && r.Status == 0)
                    .ToListAsync();

                foreach (var req in otherRequests)
                {
                    req.Status = 2; // 自动驳回同名的重复申请
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { message = "申请已批准，分类已正式开辟" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, new { message = "处理失败", details = ex.Message });
            }
        }

        [HttpPost("/api/admin/wiki/requests/{id}/reject")]
        public async Task<IActionResult> RejectCategoryRequest(int id)
        {
            var requestItem = await _context.WikiCategoryRequests.FindAsync(id);

            if (requestItem == null || requestItem.Status != 0)
                return NotFound(new { message = "该申请不存在或已被处理" });

            // 将申请状态改为 2 (已拒绝/驳回)
            requestItem.Status = 2;

            await _context.SaveChangesAsync();

            return Ok(new { message = "已驳回该申请" });
        }
    }

    public class CategoryRequestDto
    {
        public string Name { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public int SortOrder { get; set; }
        // 🌟 DTO 字段确保前端能传进来
        public string? OwnerId { get; set; }
        public int OwnershipType { get; set; }
    }
}