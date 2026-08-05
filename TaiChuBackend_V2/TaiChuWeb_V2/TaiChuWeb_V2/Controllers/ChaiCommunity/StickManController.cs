using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Chai.stickman;
using TaiChuWeb_V2.Models.ChaiCommunity;

namespace TaiChuWeb_V2.Controllers.ChaiCommunity
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class StickManController : ControllerBase
    {
        private readonly AppDbContext _context;

        public StickManController(AppDbContext context)
        {
            _context = context;
        }

        // ============================================================
        // 1. 获取列表（公开）- 只显示已发布的 OC
        // ============================================================
        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<StickmanListResponse>> GetList(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 12,
            [FromQuery] string? keyword = null,
            [FromQuery] string? sort = "latest")
        {
            if (page < 1) page = 1;
            if (pageSize < 1 || pageSize > 50) pageSize = 12;

            // ✅ 只查询已发布的 OC
            var query = _context.StickmanCharacters
                .Include(c => c.Attributes)
                .Include(c => c.Images)
                .Where(c => c.Status == "published");

            // 关键词搜索
            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(c => c.Title.Contains(keyword) ||
                                         (c.Description != null && c.Description.Contains(keyword)));
            }

            // 排序
            query = sort switch
            {
                "hot" => query.OrderByDescending(c => c.Views),
                _ => query.OrderByDescending(c => c.CreatedAt)
            };

            var total = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new StickmanCharacterDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    CoverUrl = c.CoverUrl,
                    AuthorName = c.AuthorName,
                    AuthorId = c.AuthorId,
                    Views = c.Views,
                    Status = c.Status,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    Attributes = c.Attributes!.Select(a => new StickmanAttributeDto
                    {
                        Id = a.Id,
                        Key = a.Key,
                        Value = a.Value,
                        SortOrder = a.SortOrder,
                        Type = a.Type  // ← 添加这一行
                    }).ToList(),
                    Images = c.Images!.Select(i => new StickmanImageDto
                    {
                        Id = i.Id,
                        Url = i.Url,
                        Alt = i.Alt,
                        SortOrder = i.SortOrder
                    }).ToList()
                })
                .ToListAsync();

            return Ok(new StickmanListResponse
            {
                Items = items,
                Total = total,
                Page = page,
                PageSize = pageSize
            });
        }

        // ============================================================
        // 2. 获取详情 - 草稿只有作者本人可见
        // ============================================================
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<StickmanCharacterDto>> GetDetail(Guid id)
        {
            var character = await _context.StickmanCharacters
                .Include(c => c.Attributes)
                .Include(c => c.Images)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (character == null)
                return NotFound(new { message = "OC 角色不存在" });

            // ✅ 如果是草稿，检查当前用户是否是作者
            if (character.Status == "draft")
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdStr, out var userId) || character.AuthorId != userId)
                {
                    return NotFound(new { message = "OC 角色不存在" });
                }
            }

            // 浏览量增加（只有 published 才增加）
            if (character.Status == "published")
            {
                character.Views += 1;
                await _context.SaveChangesAsync();
            }

            var dto = new StickmanCharacterDto
            {
                Id = character.Id,
                Title = character.Title,
                Description = character.Description,
                CoverUrl = character.CoverUrl,
                AuthorName = character.AuthorName,
                AuthorId = character.AuthorId,
                Views = character.Views,
                Status = character.Status,
                CreatedAt = character.CreatedAt,
                UpdatedAt = character.UpdatedAt,
                Attributes = character.Attributes?.Select(a => new StickmanAttributeDto
                {
                    Id = a.Id,
                    Key = a.Key,
                    Value = a.Value,
                    SortOrder = a.SortOrder,
                    Type = a.Type  // ← 添加这一行
                }).ToList() ?? new List<StickmanAttributeDto>(),
                Images = character.Images?.Select(i => new StickmanImageDto
                {
                    Id = i.Id,
                    Url = i.Url,
                    Alt = i.Alt,
                    SortOrder = i.SortOrder
                }).ToList() ?? new List<StickmanImageDto>()
            };

            return Ok(dto);
        }

        // ============================================================
        // 3. 创建 OC 角色
        // ============================================================
        [HttpPost]
        public async Task<ActionResult<StickmanCharacterDto>> Create([FromBody] CreateStickmanRequest request)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var username = User.FindFirstValue(ClaimTypes.Name) ?? "未知用户";

            var character = new StickmanCharacter
            {
                Id = Guid.NewGuid(),
                Title = request.Title,
                Description = request.Description,
                CoverUrl = request.CoverUrl,
                AuthorId = userId,
                AuthorName = username,
                Views = 0,
                Status = request.Status ?? "draft",
                CreatedAt = DateTime.UtcNow,
                Attributes = request.Attributes?.Select(a => new StickmanAttribute
                {
                    Id = Guid.NewGuid(),
                    Key = a.Key,
                    Value = a.Value,
                    SortOrder = a.SortOrder,
                    Type = a.Type ?? "short",  // ✅ 新增
                    CreatedAt = DateTime.UtcNow
                }).ToList() ?? new List<StickmanAttribute>(),
                Images = request.Images?.Select(i => new StickmanImage
                {
                    Id = Guid.NewGuid(),
                    Url = i.Url,
                    Alt = i.Alt,
                    SortOrder = i.SortOrder,
                    CreatedAt = DateTime.UtcNow
                }).ToList() ?? new List<StickmanImage>()
            };

            _context.StickmanCharacters.Add(character);
            await _context.SaveChangesAsync();

            var dto = new StickmanCharacterDto
            {
                Id = character.Id,
                Title = character.Title,
                Description = character.Description,
                CoverUrl = character.CoverUrl,
                AuthorName = character.AuthorName,
                AuthorId = character.AuthorId,
                Views = character.Views,
                Status = character.Status,
                CreatedAt = character.CreatedAt,
                UpdatedAt = character.UpdatedAt,
                Attributes = character.Attributes.Select(a => new StickmanAttributeDto
                {
                    Id = a.Id,
                    Key = a.Key,
                    Value = a.Value,
                    SortOrder = a.SortOrder,
                    Type = a.Type  // ✅ 返回时也带上 Type
                }).ToList(),
                Images = character.Images.Select(i => new StickmanImageDto
                {
                    Id = i.Id,
                    Url = i.Url,
                    Alt = i.Alt,
                    SortOrder = i.SortOrder
                }).ToList()
            };

            return CreatedAtAction(nameof(GetDetail), new { id = character.Id }, dto);
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<StickmanCharacterDto>> Update(Guid id, [FromBody] UpdateStickmanRequest request)
        {
            try
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdStr, out var userId))
                    return Unauthorized(new { message = "用户未登录" });

                var character = await _context.StickmanCharacters
                    .FirstOrDefaultAsync(c => c.Id == id);

                if (character == null)
                    return NotFound(new { message = "OC 角色不存在" });

                if (character.AuthorId != userId)
                    return Forbid();

                // 更新标量字段
                if (!string.IsNullOrEmpty(request.Title))
                    character.Title = request.Title;
                if (request.Description != null)
                    character.Description = request.Description;
                if (request.CoverUrl != null)
                    character.CoverUrl = request.CoverUrl;
                if (!string.IsNullOrEmpty(request.Status))
                    character.Status = request.Status;

                character.UpdatedAt = DateTime.UtcNow;

                // ===== 处理 Attributes =====
                var oldAttrs = await _context.StickmanAttributes
                    .Where(a => a.CharacterId == id)
                    .ToListAsync();
                if (oldAttrs.Any())
                    _context.StickmanAttributes.RemoveRange(oldAttrs);

                if (request.Attributes != null)
                {
                    var validAttributes = request.Attributes
                        .Where(a => !string.IsNullOrWhiteSpace(a.Key))
                        .ToList();

                    foreach (var (attr, index) in validAttributes.Select((a, i) => (a, i)))
                    {
                        var newAttr = new StickmanAttribute
                        {
                            Id = Guid.NewGuid(),
                            CharacterId = character.Id,
                            Key = attr.Key.Trim(),
                            Value = attr.Value?.Trim(),
                            SortOrder = attr.SortOrder != 0 ? attr.SortOrder : index,
                            Type = attr.Type ?? "short",  // ✅ 新增
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.StickmanAttributes.Add(newAttr);
                    }
                }

                // ===== 处理 Images =====
                var oldImages = await _context.StickmanImages
                    .Where(i => i.CharacterId == id)
                    .ToListAsync();
                if (oldImages.Any())
                    _context.StickmanImages.RemoveRange(oldImages);

                if (request.Images != null)
                {
                    var validImages = request.Images
                        .Where(i => !string.IsNullOrWhiteSpace(i.Url))
                        .ToList();

                    foreach (var (img, index) in validImages.Select((i, idx) => (i, idx)))
                    {
                        var newImg = new StickmanImage
                        {
                            Id = Guid.NewGuid(),
                            CharacterId = character.Id,
                            Url = img.Url.Trim(),
                            Alt = img.Alt?.Trim(),
                            SortOrder = img.SortOrder != 0 ? img.SortOrder : index,
                            CreatedAt = DateTime.UtcNow
                        };
                        _context.StickmanImages.Add(newImg);
                    }
                }

                await _context.SaveChangesAsync();

                var updated = await _context.StickmanCharacters
                    .Include(c => c.Attributes)
                    .Include(c => c.Images)
                    .FirstOrDefaultAsync(c => c.Id == id);

                var dto = new StickmanCharacterDto
                {
                    Id = updated!.Id,
                    Title = updated.Title,
                    Description = updated.Description,
                    CoverUrl = updated.CoverUrl,
                    AuthorName = updated.AuthorName,
                    AuthorId = updated.AuthorId,
                    Views = updated.Views,
                    Status = updated.Status,
                    CreatedAt = updated.CreatedAt,
                    UpdatedAt = updated.UpdatedAt,
                    Attributes = updated.Attributes?.Select(a => new StickmanAttributeDto
                    {
                        Id = a.Id,
                        Key = a.Key,
                        Value = a.Value,
                        SortOrder = a.SortOrder,
                        Type = a.Type  // ✅ 返回时带 Type
                    }).ToList() ?? new List<StickmanAttributeDto>(),
                    Images = updated.Images?.Select(i => new StickmanImageDto
                    {
                        Id = i.Id,
                        Url = i.Url,
                        Alt = i.Alt,
                        SortOrder = i.SortOrder
                    }).ToList() ?? new List<StickmanImageDto>()
                };

                return Ok(dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "更新失败",
                    detail = ex.Message,
                    inner = ex.InnerException?.Message
                });
            }
        }













        // ============================================================
        // 5. 删除 OC 角色
        // ============================================================
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var character = await _context.StickmanCharacters
                .FirstOrDefaultAsync(c => c.Id == id);

            if (character == null)
                return NotFound(new { message = "OC 角色不存在" });

            if (character.AuthorId != userId)
                return Forbid();

            _context.StickmanCharacters.Remove(character);
            await _context.SaveChangesAsync();

            return Ok(new { message = "删除成功" });
        }

        // ============================================================
        // 6. 获取我的 OC 列表（包含草稿）
        // ============================================================
        [HttpGet("my")]
        public async Task<ActionResult<List<StickmanCharacterDto>>> GetMyCharacters(
            [FromQuery] string? status = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var query = _context.StickmanCharacters
                .Include(c => c.Attributes)
                .Include(c => c.Images)
                .Where(c => c.AuthorId == userId);

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                query = query.Where(c => c.Status == status);
            }

            var items = await query
                .OrderByDescending(c => c.CreatedAt)
                .Select(c => new StickmanCharacterDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    Description = c.Description,
                    CoverUrl = c.CoverUrl,
                    AuthorName = c.AuthorName,
                    AuthorId = c.AuthorId,
                    Views = c.Views,
                    Status = c.Status,
                    CreatedAt = c.CreatedAt,
                    UpdatedAt = c.UpdatedAt,
                    Attributes = c.Attributes!.Select(a => new StickmanAttributeDto
                    {
                        Id = a.Id,
                        Key = a.Key,
                        Value = a.Value,
                        SortOrder = a.SortOrder,
                        Type = a.Type  // ← 添加这一行
                    }).ToList(),
                    Images = c.Images!.Select(i => new StickmanImageDto
                    {
                        Id = i.Id,
                        Url = i.Url,
                        Alt = i.Alt,
                        SortOrder = i.SortOrder
                    }).ToList()
                })
                .ToListAsync();

            return Ok(items);
        }
    }
}