using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext; // 你的实际 DbContext
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TaiChuWeb_V2.Dtos.Admin.AdminUser; // 引入独立的 DTO 命名空间
using TaiChuWeb_V2.Models.User;

namespace TaiChuWeb_V2.Controllers.Admin
{
    [ApiController]
    [Route("api/admin/users")]
    // [Authorize] 
    // 💡 建议加上自定义的策略或角色拦截，例如：[Authorize(Roles = "SuperAdmin,User_Audit")]
    public class AdminUserController : ControllerBase
    {
        private readonly AppDbContext _context;

        public AdminUserController(AppDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // 1. 获取用户详情 (支持分页与联合过滤)
        // GET: api/admin/users?page=1&pageSize=30&search=...&permission=...&reputation=...
        // ==========================================
        [HttpGet]
        public async Task<IActionResult> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 30,
            [FromQuery] string? search = null,
            [FromQuery] string? permission = null,
            [FromQuery] string? reputation = null)
        {
            var query = _context.Users
                .Include(u => u.Profile)
                .Include(u => u.Stats)
                .AsNoTracking()
                .AsQueryable();

            // 1. 关键字模糊查询 (匹配 GUID、用户名、邮箱)
            if (!string.IsNullOrWhiteSpace(search))
            {
                var searchLower = search.ToLower();
                query = query.Where(u =>
                    u.Username.ToLower().Contains(searchLower) ||
                    (u.Email != null && u.Email.ToLower().Contains(searchLower)) ||
                    u.Id.ToString() == search);
            }

            // 2. 权限过滤
            if (!string.IsNullOrWhiteSpace(permission) && Enum.TryParse<AdminPermission>(permission, out var permEnum))
            {
                var userIdsWithPerm = _context.UserPermissions
                    .Where(p => p.Permission == permEnum)
                    .Select(p => p.UserId);

                query = query.Where(u => userIdsWithPerm.Contains(u.Id));
            }

            // 3. 信誉分过滤
            if (reputation == "low")
            {
                query = query.Where(u => u.Stats != null && u.Stats.Reputation < 90);
            }
            else if (reputation == "normal")
            {
                // 默认信誉为100，所以 Stats 为 null 的新用户也算 normal
                query = query.Where(u => u.Stats == null || u.Stats.Reputation >= 90);
            }

            // 计算匹配条件的总人数
            var totalCount = await query.CountAsync();

            // 分页拉取数据
            var users = await query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // 为了避免 N+1 查询问题，一次性查出这批用户的系统权限
            var userIds = users.Select(u => u.Id).ToList();
            var permissions = await _context.UserPermissions
                .Where(p => userIds.Contains(p.UserId))
                .AsNoTracking()
                .ToListAsync();

            var permLookup = permissions
                .GroupBy(p => p.UserId)
                .ToDictionary(g => g.Key, g => g.Select(p => p.Permission.ToString()).ToList());

            // 映射为前端需要的 DTO
            var items = users.Select(u => new AdminUserDto
            {
                Id = u.Id.ToString(),
                Username = u.Username,
                Email = u.Email,
                CreatedAt = u.CreatedAt,
                Permissions = permLookup.ContainsKey(u.Id) ? permLookup[u.Id] : new List<string>(),

                Profile = u.Profile == null ? null : new AdminUserProfileDto
                {
                    Avatar = u.Profile.Avatar,
                    Gender = u.Profile.Gender,
                    Bio = u.Profile.Bio,
                    Mood = u.Profile.Mood,
                    Birthday = u.Profile.Birthday?.ToString("yyyy-MM-dd"),
                    PhoneNumber = u.Profile.PhoneNumber,
                    Age = u.Profile.Age,
                    Zodiac = u.Profile.Zodiac,
                    ChineseZodiac = u.Profile.ChineseZodiac
                },

                Stats = u.Stats == null ? null : new AdminUserStatsDto
                {
                    Level = u.Stats.Level,
                    Experience = u.Stats.Experience,
                    Reputation = u.Stats.Reputation,
                    Title = u.Stats.Title,
                    CurrentSignStreak = u.Stats.CurrentSignStreak,
                    MaxSignStreak = u.Stats.MaxSignStreak,
                    UsedNotes = u.Stats.UsedNotes,
                    UsedSpaces = u.Stats.UsedSpaces,
                    MaxNotes = u.Stats.MaxNotes,
                    MaxSpaces = u.Stats.MaxSpaces,
                    MaxProjectCount = u.Stats.MaxProjectCount
                }
            }).ToList();

            // 返回标准的带有分页元数据的匿名对象
            return Ok(new
            {
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                Items = items
            });
        }

        // ==========================================
        // 2. 深度更新用户核心资产与配额 (Stats)
        // PUT: api/admin/users/{userId}/stats
        // ==========================================
        [HttpPut("{userId:guid}/stats")]
        public async Task<IActionResult> UpdateStats(Guid userId, [FromBody] UpdateStatsPayload payload)
        {
            var stats = await _context.UserStats.FirstOrDefaultAsync(s => s.UserId == userId);

            if (stats == null)
            {
                // 如果用户还没有 Stats 数据，则为他初始化一条
                stats = new UserStats { UserId = userId };
                _context.UserStats.Add(stats);
            }

            stats.Reputation = payload.Reputation;
            stats.Experience = payload.Experience;
            stats.MaxSpaces = payload.MaxSpaces;
            stats.MaxNotes = payload.MaxNotes;
            stats.MaxProjectCount = payload.MaxProjectCount;

            await _context.SaveChangesAsync();
            return Ok(new { message = "用户核心资产与配额已更新" });
        }

        // ==========================================
        // 3. 指派用户系统级管理权限 (UserPermission)
        // PUT: api/admin/users/{userId}/permissions
        // ==========================================
        [HttpPut("{userId:guid}/permissions")]
        public async Task<IActionResult> UpdatePermissions(Guid userId, [FromBody] List<string> newPermissions)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists) return NotFound(new { message = "系统未找到该用户凭证" });

            // 1. 删除旧的所有权限
            var existingPerms = await _context.UserPermissions.Where(p => p.UserId == userId).ToListAsync();
            _context.UserPermissions.RemoveRange(existingPerms);

            // 2. 注入新权限
            foreach (var permStr in newPermissions)
            {
                if (Enum.TryParse<AdminPermission>(permStr, out var permEnum))
                {
                    _context.UserPermissions.Add(new UserPermission
                    {
                        UserId = userId,
                        Permission = permEnum
                    });
                }
            }

            await _context.SaveChangesAsync();
            return Ok(new { message = "用户权限指派已生效" });
        }

        // ==========================================
        // 4. 快捷审计干预：违规扣除信誉分
        // POST: api/admin/users/{userId}/punish
        // ==========================================
        [HttpPost("{userId:guid}/punish")]
        public async Task<IActionResult> Punish(Guid userId, [FromBody] PunishPayload payload)
        {
            var stats = await _context.UserStats.FirstOrDefaultAsync(s => s.UserId == userId);
            if (stats == null) return NotFound(new { message = "该用户未激活资产账单，无法扣除" });

            if (payload.Deduction <= 0) return BadRequest(new { message = "扣除分数必须大于0" });

            // 扣除信誉分，最低限制为 0
            stats.Reputation = Math.Max(0, stats.Reputation - payload.Deduction);

            await _context.SaveChangesAsync();
            return Ok(new { message = $"已成功扣除 {payload.Deduction} 点信誉分" });
        }
    }
}