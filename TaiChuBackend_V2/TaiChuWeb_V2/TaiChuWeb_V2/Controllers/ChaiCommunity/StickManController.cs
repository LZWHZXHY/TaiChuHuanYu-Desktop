using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Chai.stickman;
using TaiChuWeb_V2.Models.ChaiCommunity;
using TaiChuWeb_V2.Models.Interact;

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
        // 1. 获取列表（公开）- 只显示已发布的 OC，返回简略 DTO
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

            // ✅ 不需要 Include，因为使用 Select 投影
            var query = _context.StickmanCharacters
                .Where(c => c.Status == "published");

            if (!string.IsNullOrEmpty(keyword))
            {
                query = query.Where(c => c.Title.Contains(keyword) ||
                                         (c.Description != null && c.Description.Contains(keyword)));
            }

            query = sort switch
            {
                "hot" => query.OrderByDescending(c => c.Views),
                _ => query.OrderByDescending(c => c.CreatedAt)
            };

            var total = await query.CountAsync();

            // ✅ 使用 StickmanBriefDto，不包含 Attributes 和 Images
            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(c => new StickmanBriefDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    CoverUrl = c.CoverUrl,
                    AuthorName = c.AuthorName,
                    AuthorId = c.AuthorId,
                    Status = c.Status,
                    IsBattleEnabled = c.IsBattleEnabled,
                    CreatedAt = c.CreatedAt,
                    // ⭐ 新增
                    BattleWins = c.BattleWins,
                    BattleLosses = c.BattleLosses,
                    BattleDraws = c.BattleDraws,
                    BattlePoints = c.BattleWins * 3 + c.BattleDraws,
                    BattleTier = (c.BattleWins * 3 + c.BattleDraws) >= 100 ? "钻石" : (c.BattleWins * 3 + c.BattleDraws) >= 60 ? "铂金" : (c.BattleWins * 3 + c.BattleDraws) >= 30 ? "黄金" : (c.BattleWins * 3 + c.BattleDraws) >= 10 ? "白银" : "青铜"
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
        // 2. 获取详情 - 返回完整 DTO（含 Attributes 和 Images）
        // ============================================================
        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<StickmanCharacterDto>> GetDetail(Guid id)
        {
            var character = await _context.StickmanCharacters
    .Include(c => c.Attributes.OrderBy(a => a.SortOrder))
    .Include(c => c.Images.OrderBy(i => i.SortOrder))
    .FirstOrDefaultAsync(c => c.Id == id);

            if (character == null)
                return NotFound(new { message = "OC 角色不存在" });

            // 草稿只有作者本人可见
            if (character.Status == "draft")
            {
                var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (!Guid.TryParse(userIdStr, out var userId) || character.AuthorId != userId)
                {
                    return NotFound(new { message = "OC 角色不存在" });
                }
            }

            // 浏览量+1
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
                IsBattleEnabled = character.IsBattleEnabled,
                // ⭐ 新增
                BattleWins = character.BattleWins,
                BattleLosses = character.BattleLosses,
                BattleDraws = character.BattleDraws,
                BattlePoints = character.BattlePoints,
                BattleTier = character.BattleTier,
                LikeCount = _context.UserInteractions.Count(i => i.TargetType == "StickmanCharacter" && i.TargetId == character.Id.ToString() && i.ActionType == "Like"),
                FavoriteCount = _context.UserInteractions.Count(i => i.TargetType == "StickmanCharacter" && i.TargetId == character.Id.ToString() && i.ActionType == "Favorite"),
                Attributes = character.Attributes?.Select(a => new StickmanAttributeDto
                {
                    Id = a.Id,
                    Key = a.Key,
                    Value = a.Value,
                    SortOrder = a.SortOrder,
                    Type = a.Type
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
        // 2.5 约战排行榜（按积分 → 胜场排序）
        // ============================================================
        [HttpGet("leaderboard")]
        [AllowAnonymous]
        public async Task<ActionResult<List<StickmanBriefDto>>> GetLeaderboard([FromQuery] int top = 20)
        {
            if (top < 1 || top > 50) top = 20;
            var items = await _context.StickmanCharacters
                .Where(c => c.Status == "published")
                .OrderByDescending(c => c.BattleWins * 3 + c.BattleDraws)
                .ThenByDescending(c => c.BattleWins)
                .ThenByDescending(c => c.Views)
                .Take(top)
                .Select(c => new StickmanBriefDto
                {
                    Id = c.Id,
                    Title = c.Title,
                    CoverUrl = c.CoverUrl,
                    AuthorName = c.AuthorName,
                    AuthorId = c.AuthorId,
                    Status = c.Status,
                    IsBattleEnabled = c.IsBattleEnabled,
                    CreatedAt = c.CreatedAt,
                    BattleWins = c.BattleWins,
                    BattleLosses = c.BattleLosses,
                    BattleDraws = c.BattleDraws,
                    BattlePoints = c.BattleWins * 3 + c.BattleDraws,
                    BattleTier = (c.BattleWins * 3 + c.BattleDraws) >= 100 ? "钻石" : (c.BattleWins * 3 + c.BattleDraws) >= 60 ? "铂金" : (c.BattleWins * 3 + c.BattleDraws) >= 30 ? "黄金" : (c.BattleWins * 3 + c.BattleDraws) >= 10 ? "白银" : "青铜"
                })
                .ToListAsync();
            return Ok(items);
        }

        // ============================================================
        // 2.6 点赞 / 收藏（复用 UserInteraction，可取消）
        // ============================================================
        [HttpPost("{id:guid}/{kind}")]
        public async Task<IActionResult> ToggleInteraction(Guid id, string kind)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId)) return Unauthorized();
            if (kind is not ("like" or "favorite")) return BadRequest(new { message = "不支持的操作" });

            var oc = await _context.StickmanCharacters.FirstOrDefaultAsync(c => c.Id == id && c.Status == "published");
            if (oc == null) return NotFound(new { message = "角色不存在或未公开" });

            var actionType = kind == "like" ? "Like" : "Favorite";
            var target = id.ToString();
            var existing = await _context.UserInteractions
                .Where(i => i.UserId == userId && i.TargetType == "StickmanCharacter" && i.TargetId == target && i.ActionType == actionType)
                .FirstOrDefaultAsync();

            bool active;
            if (existing != null) { _context.UserInteractions.Remove(existing); active = false; }
            else { _context.UserInteractions.Add(new UserInteraction { UserId = userId, TargetId = target, TargetType = "StickmanCharacter", ActionType = actionType }); active = true; }

            await _context.SaveChangesAsync();
            var count = await _context.UserInteractions.CountAsync(i => i.TargetType == "StickmanCharacter" && i.TargetId == target && i.ActionType == actionType);
            return Ok(new { active, count });
        }

        /// <summary>当前用户对这条 OC 的点赞 / 收藏状态（刷新后保持高亮）</summary>
        [HttpGet("{id:guid}/interaction")]
        [AllowAnonymous]
        public async Task<IActionResult> GetInteraction(Guid id)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId)) return Ok(new { liked = false, favorited = false });

            var target = id.ToString();
            var actions = await _context.UserInteractions
                .Where(i => i.UserId == userId && i.TargetType == "StickmanCharacter" && i.TargetId == target)
                .Select(i => i.ActionType)
                .ToListAsync();
            return Ok(new { liked = actions.Contains("Like"), favorited = actions.Contains("Favorite") });
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
                IsBattleEnabled = request.IsBattleEnabled,
                CreatedAt = DateTime.UtcNow,
                Attributes = request.Attributes?.Select(a => new StickmanAttribute
                {
                    Id = Guid.NewGuid(),
                    Key = a.Key,
                    Value = a.Value,
                    SortOrder = a.SortOrder,
                    Type = a.Type ?? "short",
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
                IsBattleEnabled = character.IsBattleEnabled,
                // ⭐ 新增（刚创建都是0）
                BattleWins = character.BattleWins,
                BattleLosses = character.BattleLosses,
                BattleDraws = character.BattleDraws,
                Attributes = character.Attributes.Select(a => new StickmanAttributeDto
                {
                    Id = a.Id,
                    Key = a.Key,
                    Value = a.Value,
                    SortOrder = a.SortOrder,
                    Type = a.Type
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

        // ============================================================
        // 4. 更新 OC 角色
        // ============================================================
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
                if (request.IsBattleEnabled.HasValue)
                    character.IsBattleEnabled = request.IsBattleEnabled.Value;

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
                            Type = attr.Type ?? "short",
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
                    IsBattleEnabled = updated.IsBattleEnabled,
                    // ⭐ 新增
                    BattleWins = updated.BattleWins,
                    BattleLosses = updated.BattleLosses,
                    BattleDraws = updated.BattleDraws,
                    Attributes = updated.Attributes?.Select(a => new StickmanAttributeDto
                    {
                        Id = a.Id,
                        Key = a.Key,
                        Value = a.Value,
                        SortOrder = a.SortOrder,
                        Type = a.Type
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
        // 6. 获取我的 OC 列表（包含草稿，返回简略 DTO）
        // ============================================================
        [HttpGet("my")]
        public async Task<ActionResult<List<StickmanBriefDto>>> GetMyCharacters(
            [FromQuery] string? status = null)
        {
            var userIdStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(new { message = "用户未登录" });

            var query = _context.StickmanCharacters
                .Where(c => c.AuthorId == userId);

            if (!string.IsNullOrEmpty(status) && status != "all")
            {
                query = query.Where(c => c.Status == status);
            }

            // ✅ 返回简略 DTO，不含 Attributes 和 Images
            var items = await query
    .OrderByDescending(c => c.CreatedAt)
    .Select(c => new StickmanBriefDto
    {
        Id = c.Id,
        Title = c.Title,
        CoverUrl = c.CoverUrl,
        AuthorName = c.AuthorName,
        AuthorId = c.AuthorId,
        Status = c.Status,
        IsBattleEnabled = c.IsBattleEnabled,
        CreatedAt = c.CreatedAt,
        // ⭐ 新增
        BattleWins = c.BattleWins,
        BattleLosses = c.BattleLosses,
        BattleDraws = c.BattleDraws
    })
    .ToListAsync();

            return Ok(items);
        }
    }
}