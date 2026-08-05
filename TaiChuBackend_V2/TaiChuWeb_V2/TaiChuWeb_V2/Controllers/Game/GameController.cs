using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Dtos.Game;
using TaiChuWeb_V2.Models.Game;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services;

namespace TaiChuWeb_V2.Controllers.Game
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GamesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly SystemConfigService _configService;

        public GamesController(AppDbContext context, SystemConfigService configService)
        {
            _context = context;
            _configService = configService;
        }

        // ==================== 创建游戏（消耗经验） ====================
        [HttpPost]
        public async Task<IActionResult> CreateGame([FromBody] CreateGameDto dto)
        {
            // 1. 获取当前用户
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null)
                return Unauthorized("用户未登录");

            var userId = Guid.Parse(userIdClaim.Value);
            var user = await _context.Users
                .Include(u => u.Stats)
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null || user.Stats == null)
                return NotFound("用户不存在");

            // 2. 读取消耗配置
            int cost = await _configService.GetIntAsync("Game:CreateCostExp", 50);

            // 3. 校验并扣除经验
            if (user.Stats.Experience < cost)
            {
                return BadRequest(new { message = $"经验不足，当前 {user.Stats.Experience}，需要 {cost}" });
            }
            user.Stats.Experience -= cost;

            // 4. 创建游戏主记录
            var game = new Models.Game.Game
            {
                Type = dto.Type ?? "questionnaire",
                Icon = dto.Icon ?? "🎮",
                Title = dto.Title,
                Description = dto.Description,
                Status = "草稿",
                CreatorId = userId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                ExpCost = cost,
                PlayCount = 0
            };
            _context.Games.Add(game);
            await _context.SaveChangesAsync();

            // 5. 创建问卷配置
            var questionnaire = new GameQuestionnaire
            {
                GameId = game.Id,
                Scoring = dto.Scoring ?? "sum"
            };
            _context.GameQuestionnaires.Add(questionnaire);
            await _context.SaveChangesAsync();

            // 6. 创建题目、选项、结果
            int order = 0;
            foreach (var qDto in dto.Questions)
            {
                var question = new GameQuestion
                {
                    QuestionnaireId = questionnaire.Id,
                    Type = qDto.Type ?? "single",
                    Text = qDto.Text,
                    Image = qDto.Image ?? "",
                    Order = order++
                };
                _context.GameQuestions.Add(question);
                await _context.SaveChangesAsync();

                int optOrder = 0;
                foreach (var optDto in qDto.Options)
                {
                    var option = new GameOption
                    {
                        QuestionId = question.Id,
                        Label = optDto.Label,
                        Value = optDto.Value,
                        Image = optDto.Image ?? "",
                        Order = optOrder++
                    };
                    _context.GameOptions.Add(option);
                }
            }

            order = 0;
            foreach (var rDto in dto.Results)
            {
                // 使用 Desc 或 Description
                var descValue = rDto.Desc ?? rDto.Description;
                var result = new GameResult
                {
                    QuestionnaireId = questionnaire.Id,
                    Min = rDto.Min,
                    Max = rDto.Max,
                    Title = rDto.Title,
                    Description = descValue,
                    Icon = rDto.Icon ?? "🏷️",
                    Image = rDto.Image ?? "",
                    Order = order++
                };
                _context.GameResults.Add(result);
            }

            // 7. 记录经验日志
            _context.UserExpLogs.Add(new UserExpLog
            {
                UserId = userId,
                Change = -cost,
                Reason = $"创建游戏：{dto.Title}",
                CreatedAt = DateTime.UtcNow
            });

            // 8. 保存所有更改
            await _context.SaveChangesAsync();

            // 9. 加载完整数据
            var created = await _context.Games
                .Include(g => g.Creator)
                .Include(g => g.Questionnaire)
                    .ThenInclude(q => q.Questions)
                        .ThenInclude(q => q.Options)
                .Include(g => g.Questionnaire)
                    .ThenInclude(q => q.Results)
                .FirstOrDefaultAsync(g => g.Id == game.Id);

            // 10. 映射为 DTO 返回
            var response = MapToResponseDto(created, user.Username);

            return CreatedAtAction(nameof(GetGame), new { id = game.Id }, response);
        }

        // ==================== 获取单个游戏详情 ====================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetGame(int id)
        {
            var game = await _context.Games
                .Include(g => g.Creator)
                .Include(g => g.Questionnaire)
                    .ThenInclude(q => q.Questions)
                        .ThenInclude(q => q.Options)
                .Include(g => g.Questionnaire)
                    .ThenInclude(q => q.Results)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
                return NotFound();

            var response = MapToResponseDto(game, game.Creator?.Username ?? "未知用户");
            return Ok(response);
        }

        // ==================== 获取游戏列表（支持分页和筛选） ====================
        [HttpGet]
        public async Task<IActionResult> GetGames(
            [FromQuery] string? type = null,
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var query = _context.Games
                .Include(g => g.Creator)
                .Include(g => g.Questionnaire)
                .AsQueryable();

            if (!string.IsNullOrEmpty(type))
                query = query.Where(g => g.Type == type);
            if (!string.IsNullOrEmpty(status))
                query = query.Where(g => g.Status == status);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(g => g.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(g => new GameListItemDto
                {
                    Id = g.Id,
                    Type = g.Type,
                    Icon = g.Icon,
                    Title = g.Title,
                    Description = g.Description,
                    Status = g.Status,
                    CreatedAt = g.CreatedAt,
                    UpdatedAt = g.UpdatedAt,
                    ExpCost = g.ExpCost,
                    PlayCount = g.PlayCount,
                    CreatorName = g.Creator != null ? g.Creator.Username : "未知用户",
                    QuestionnaireId = g.Questionnaire != null ? g.Questionnaire.Id : (int?)null
                })
                .ToListAsync();

            return Ok(new { total, items });
        }

        // ==================== 获取当前用户创建的游戏 ====================
        [HttpGet("my")]
        public async Task<IActionResult> GetMyGames(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var query = _context.Games
                .Where(g => g.CreatorId == userId)
                .Include(g => g.Creator)
                .Include(g => g.Questionnaire)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status))
                query = query.Where(g => g.Status == status);

            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(g => g.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(g => new GameListItemDto
                {
                    Id = g.Id,
                    Type = g.Type,
                    Icon = g.Icon,
                    Title = g.Title,
                    Description = g.Description,
                    Status = g.Status,
                    CreatedAt = g.CreatedAt,
                    UpdatedAt = g.UpdatedAt,
                    ExpCost = g.ExpCost,
                    PlayCount = g.PlayCount,
                    CreatorName = g.Creator != null ? g.Creator.Username : "未知用户",
                    QuestionnaireId = g.Questionnaire != null ? g.Questionnaire.Id : (int?)null
                })
                .ToListAsync();

            return Ok(new { total, items });
        }

        // ==================== 更新游戏（修改元数据或内容） ====================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateGame(int id, [FromBody] UpdateGameDto dto)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var game = await _context.Games
                .Include(g => g.Creator)
                .Include(g => g.Questionnaire)
                    .ThenInclude(q => q.Questions)
                        .ThenInclude(q => q.Options)
                .Include(g => g.Questionnaire)
                    .ThenInclude(q => q.Results)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
                return NotFound();

            if (game.CreatorId != userId)
                return Forbid();

            // 更新元数据
            game.Title = dto.Title ?? game.Title;
            game.Description = dto.Description ?? game.Description;
            game.Icon = dto.Icon ?? game.Icon;
            game.Status = dto.Status ?? game.Status;
            game.UpdatedAt = DateTime.UtcNow;

            if (game.Questionnaire != null && !string.IsNullOrEmpty(dto.Scoring))
                game.Questionnaire.Scoring = dto.Scoring;

            await _context.SaveChangesAsync();

            var response = MapToResponseDto(game, game.Creator?.Username ?? "未知用户");
            return Ok(response);
        }

        // ==================== 删除游戏 ====================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGame(int id)
        {
            var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value);

            var game = await _context.Games
                .Include(g => g.Questionnaire)
                .FirstOrDefaultAsync(g => g.Id == id);

            if (game == null)
                return NotFound();

            if (game.CreatorId != userId)
                return Forbid();

            _context.Games.Remove(game);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ==================== 私有辅助方法 ====================
        private GameResponseDto MapToResponseDto(Models.Game.Game game, string creatorName)
        {
            return new GameResponseDto
            {
                Id = game.Id,
                Type = game.Type,
                Icon = game.Icon,
                Title = game.Title,
                Description = game.Description,
                Status = game.Status,
                CreatorId = game.CreatorId,
                CreatorName = creatorName,
                CreatedAt = game.CreatedAt,
                UpdatedAt = game.UpdatedAt,
                ExpCost = game.ExpCost,
                PlayCount = game.PlayCount,
                Questionnaire = game.Questionnaire == null ? null : new GameQuestionnaireDto
                {
                    Id = game.Questionnaire.Id,
                    Scoring = game.Questionnaire.Scoring,
                    Questions = game.Questionnaire.Questions?
                        .OrderBy(q => q.Order)
                        .Select(q => new GameQuestionDto
                        {
                            Id = q.Id,
                            Type = q.Type,
                            Text = q.Text,
                            Image = q.Image,
                            Order = q.Order,
                            Options = q.Options?
                                .OrderBy(o => o.Order)
                                .Select(o => new GameOptionDto
                                {
                                    Id = o.Id,
                                    Label = o.Label,
                                    Value = o.Value,
                                    Image = o.Image,
                                    Order = o.Order
                                }).ToList() ?? new List<GameOptionDto>()
                        }).ToList() ?? new List<GameQuestionDto>(),
                    Results = game.Questionnaire.Results?
                        .OrderBy(r => r.Order)
                        .Select(r => new GameResultDto
                        {
                            Id = r.Id,
                            Min = r.Min,
                            Max = r.Max,
                            Title = r.Title,
                            Description = r.Description,
                            Icon = r.Icon,
                            Image = r.Image,
                            Order = r.Order
                        }).ToList() ?? new List<GameResultDto>()
                }
            };
        }
    }
}