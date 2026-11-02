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
    /// <summary>
    /// 太虚游「自定义工坊」里玩家自己做的小游戏（目前是心性测试 / 问卷）。
    /// · 列表只给已发布的；草稿只有作者自己能打开；
    /// · 创建扣经验（价格在系统配置 Game:CreateCostExp，默认 50），扣经验和写入题目在同一个事务里，出错整体回滚、不会白扣；
    /// · 编辑可以改题目和结果（以前只改标题，题目的修改会被悄悄丢掉）。
    /// 答题、记成绩在 GameSessionsController。
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GamesController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly SystemConfigService _configService;

        public const string Published = "已发布", Draft = "草稿";
        private const int MaxQuestions = 60, MaxOptions = 10, MaxResults = 20, MaxGamesPerUser = 30;
        private static readonly List<string> QuestionTypes = new() { "single", "yesno", "likert", "multiple" };

        public GamesController(AppDbContext context, SystemConfigService configService)
        {
            _context = context;
            _configService = configService;
        }

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        // ==================== 创建要花多少经验（前端显示用） ====================
        [HttpGet("config")]
        public async Task<IActionResult> Config()
        {
            var cost = await _configService.GetIntAsync("Game:CreateCostExp", 50);
            long? exp = null;
            if (CurrentUserId is Guid uid)
                exp = await _context.UserStats.AsNoTracking().Where(s => s.UserId == uid).Select(s => (long?)s.Experience).FirstOrDefaultAsync();
            return Ok(new { createCost = Math.Max(0, cost), experience = exp, maxGames = MaxGamesPerUser });
        }

        // ==================== 创建游戏（消耗经验） ====================
        [HttpPost]
        public async Task<IActionResult> CreateGame([FromBody] CreateGameDto dto)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized(new { message = "请先登录" });
            if (Validate(dto.Title, dto.Questions, dto.Results) is string error) return BadRequest(new { message = error });
            if (await _context.Games.CountAsync(g => g.CreatorId == userId) >= MaxGamesPerUser)
                return BadRequest(new { message = $"每人最多创建 {MaxGamesPerUser} 个游戏，删掉一些旧的再来吧" });

            var cost = Math.Max(0, await _configService.GetIntAsync("Game:CreateCostExp", 50));
            int newId = 0;
            string? fail = null;
            var strategy = _context.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                _context.ChangeTracker.Clear();
                await using var tx = await _context.Database.BeginTransactionAsync();
                // 扣经验：带条件的原子更新，余额不够就一行都不改
                if (cost > 0)
                {
                    var paid = await _context.UserStats.Where(s => s.UserId == userId && s.Experience >= cost)
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.Experience, x => x.Experience - cost));
                    if (paid == 0)
                    {
                        var have = await _context.UserStats.Where(s => s.UserId == userId).Select(s => (long?)s.Experience).FirstOrDefaultAsync() ?? 0;
                        fail = $"经验不足，当前 {have}，需要 {cost}";
                        await tx.RollbackAsync();
                        return;
                    }
                    _context.UserExpLogs.Add(new UserExpLog { UserId = userId, Change = -cost, Reason = $"创建游戏：{dto.Title.Trim()}", CreatedAt = DateTime.UtcNow });
                }

                var game = new Models.Game.Game
                {
                    Type = string.IsNullOrWhiteSpace(dto.Type) ? "questionnaire" : dto.Type,
                    Icon = string.IsNullOrWhiteSpace(dto.Icon) ? "🎮" : dto.Icon.Trim(),
                    Title = dto.Title.Trim(),
                    Description = (dto.Description ?? "").Trim(),
                    Status = Draft,
                    CreatorId = userId,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ExpCost = cost,
                    PlayCount = 0,
                    Questionnaire = new GameQuestionnaire
                    {
                        Scoring = dto.Scoring == "average" ? "average" : "sum",
                        Questions = BuildQuestions(dto.Questions!),
                        Results = BuildResults(dto.Results!),
                    },
                };
                _context.Games.Add(game);
                await _context.SaveChangesAsync();
                await tx.CommitAsync();
                newId = game.Id;
            });
            if (fail != null) return BadRequest(new { message = fail });

            var created = await LoadFull(newId);
            return CreatedAtAction(nameof(GetGame), new { id = newId }, MapToResponseDto(created!, created!.Creator?.Username ?? "未知用户"));
        }

        // ==================== 获取单个游戏详情（草稿只有作者能看） ====================
        [HttpGet("{id}")]
        public async Task<IActionResult> GetGame(int id)
        {
            var game = await LoadFull(id);
            if (game == null || (game.Status != Published && game.CreatorId != CurrentUserId))
                return NotFound(new { message = "这个游戏不存在，或者还没发布" });
            return Ok(MapToResponseDto(game, game.Creator?.Username ?? "未知用户"));
        }

        // ==================== 游戏列表：只列已发布的 ====================
        [HttpGet]
        public async Task<IActionResult> GetGames(
            [FromQuery] string? type = null,
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? sort = null,
            [FromQuery] string? q = null)
        {
            // status 参数保留兼容，但别人的草稿一律不列出来
            var query = _context.Games.AsNoTracking().Where(g => g.Status == Published);
            if (!string.IsNullOrEmpty(type)) query = query.Where(g => g.Type == type);
            if (!string.IsNullOrWhiteSpace(q)) { var k = q.Trim(); query = query.Where(g => g.Title.Contains(k) || g.Description.Contains(k)); }

            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);
            var total = await query.CountAsync();
            var ordered = sort == "hot" ? query.OrderByDescending(g => g.PlayCount).ThenByDescending(g => g.UpdatedAt) : query.OrderByDescending(g => g.UpdatedAt);
            var items = await ListItems(ordered.Skip((page - 1) * pageSize).Take(pageSize));
            return Ok(new { total, items });
        }

        // ==================== 获取当前用户创建的游戏 ====================
        [HttpGet("my")]
        public async Task<IActionResult> GetMyGames(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var query = _context.Games.AsNoTracking().Where(g => g.CreatorId == userId);
            if (!string.IsNullOrEmpty(status)) query = query.Where(g => g.Status == status);

            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);
            var total = await query.CountAsync();
            var items = await ListItems(query.OrderByDescending(g => g.UpdatedAt).Skip((page - 1) * pageSize).Take(pageSize));
            return Ok(new { total, items });
        }

        // ==================== 更新游戏（标题 / 状态，也可以连题目和结果一起改） ====================
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateGame(int id, [FromBody] UpdateGameDto dto)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var game = await LoadFull(id, tracking: true);
            if (game == null) return NotFound(new { message = "游戏不存在" });
            if (game.CreatorId != userId) return Forbid();

            var replaceContent = dto.Questions != null || dto.Results != null;
            if (replaceContent)
            {
                if (dto.Questions == null || dto.Results == null) return BadRequest(new { message = "题目和结果要一起提交" });
                if (Validate(dto.Title ?? game.Title, dto.Questions, dto.Results) is string error) return BadRequest(new { message = error });
            }
            else if (dto.Title != null && string.IsNullOrWhiteSpace(dto.Title)) return BadRequest(new { message = "标题不能为空" });
            if (dto.Status != null && dto.Status != Published && dto.Status != Draft) return BadRequest(new { message = "状态只能是「草稿」或「已发布」" });
            if (dto.Status == Published && (game.Questionnaire?.Questions?.Count ?? 0) == 0 && !replaceContent)
                return BadRequest(new { message = "还没有题目，不能发布" });

            if (dto.Title != null) game.Title = dto.Title.Trim();
            if (dto.Description != null) game.Description = dto.Description.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Icon)) game.Icon = dto.Icon.Trim();
            if (dto.Status != null) game.Status = dto.Status;
            game.UpdatedAt = DateTime.UtcNow;

            if (game.Questionnaire == null)
            {
                game.Questionnaire = new GameQuestionnaire { Scoring = "sum", Questions = new List<GameQuestion>(), Results = new List<GameResult>() };
            }
            if (!string.IsNullOrEmpty(dto.Scoring)) game.Questionnaire.Scoring = dto.Scoring == "average" ? "average" : "sum";

            if (replaceContent)
            {
                // 整套换掉：旧题目、选项、结果删除，按新内容重建。
                // 以前的答题记录还在（ResultId 指向的旧结果会被删，记录里改成空，避免外键挡住删除）
                var oldResultIds = game.Questionnaire.Results?.Select(r => r.Id).ToList() ?? new List<int>();
                if (oldResultIds.Count > 0)
                    await _context.GameSessions.Where(s => s.ResultId != null && oldResultIds.Contains(s.ResultId.Value))
                        .ExecuteUpdateAsync(s => s.SetProperty(x => x.ResultId, (int?)null));
                foreach (var q in game.Questionnaire.Questions ?? new List<GameQuestion>())
                    _context.GameOptions.RemoveRange(q.Options ?? new List<GameOption>());
                _context.GameQuestions.RemoveRange(game.Questionnaire.Questions ?? new List<GameQuestion>());
                _context.GameResults.RemoveRange(game.Questionnaire.Results ?? new List<GameResult>());
                game.Questionnaire.Questions = BuildQuestions(dto.Questions!);
                game.Questionnaire.Results = BuildResults(dto.Results!);
            }

            await _context.SaveChangesAsync();
            var fresh = await LoadFull(id);
            return Ok(MapToResponseDto(fresh!, fresh!.Creator?.Username ?? "未知用户"));
        }

        // ==================== 删除游戏 ====================
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteGame(int id)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var game = await _context.Games.FirstOrDefaultAsync(g => g.Id == id);
            if (game == null) return NotFound();
            if (game.CreatorId != userId) return Forbid();

            // 答题记录先删（它们指向这个游戏和它的结果）
            await _context.GameSessions.Where(s => s.GameId == id).ExecuteDeleteAsync();
            _context.Games.Remove(game);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        // ==================== 私有辅助方法 ====================

        /// <summary>检查标题、题目、选项、结果，返回给用户看的错误（null = 通过）</summary>
        private static string? Validate(string? title, List<QuestionDto>? questions, List<ResultDto>? results)
        {
            if (string.IsNullOrWhiteSpace(title)) return "请填写标题";
            if (title.Trim().Length > 40) return "标题最多 40 个字";
            if (questions == null || questions.Count == 0) return "至少要有一道题";
            if (questions.Count > MaxQuestions) return $"最多 {MaxQuestions} 道题";
            for (var i = 0; i < questions.Count; i++)
            {
                var q = questions[i];
                if (string.IsNullOrWhiteSpace(q.Text)) return $"第 {i + 1} 题还没写题目";
                if (q.Text.Length > 500) return $"第 {i + 1} 题的题目太长了（最多 500 字）";
                if (!QuestionTypes.Contains(q.Type ?? "single")) return $"第 {i + 1} 题的题型不对";
                if (q.Options == null || q.Options.Count < 2) return $"第 {i + 1} 题至少要有两个选项";
                if (q.Options.Count > MaxOptions) return $"第 {i + 1} 题最多 {MaxOptions} 个选项";
                if (q.Options.Any(o => string.IsNullOrWhiteSpace(o.Label))) return $"第 {i + 1} 题有选项没写文字";
                if (q.Options.Any(o => o.Label.Length > 200)) return $"第 {i + 1} 题有选项太长了（最多 200 字）";
            }
            if (results == null || results.Count == 0) return "至少要有一个结果";
            if (results.Count > MaxResults) return $"最多 {MaxResults} 个结果";
            for (var i = 0; i < results.Count; i++)
            {
                var r = results[i];
                if (string.IsNullOrWhiteSpace(r.Title)) return $"结果 {i + 1} 还没写标题";
                if (r.Min > r.Max) return $"结果 {i + 1} 的最低分比最高分还大";
            }
            return null;
        }

        private static List<GameQuestion> BuildQuestions(List<QuestionDto> list) => list.Select((q, i) => new GameQuestion
        {
            Type = QuestionTypes.Contains(q.Type ?? "single") ? q.Type ?? "single" : "single",
            Text = q.Text.Trim(),
            Image = q.Image?.Trim() ?? "",
            Order = i,
            Options = q.Options.Select((o, j) => new GameOption { Label = o.Label.Trim(), Value = o.Value, Image = o.Image?.Trim() ?? "", Order = j }).ToList(),
        }).ToList();

        private static List<GameResult> BuildResults(List<ResultDto> list) => list.Select((r, i) => new GameResult
        {
            Min = r.Min,
            Max = r.Max,
            Title = r.Title.Trim(),
            Description = (r.Desc ?? r.Description ?? "").Trim(),
            Icon = string.IsNullOrWhiteSpace(r.Icon) ? "🏷️" : r.Icon.Trim(),
            Image = r.Image?.Trim() ?? "",
            Order = i,
        }).ToList();

        private Task<Models.Game.Game?> LoadFull(int id, bool tracking = false)
        {
            IQueryable<Models.Game.Game> q = _context.Games
                .Include(g => g.Creator)
                .Include(g => g.Questionnaire).ThenInclude(x => x.Questions).ThenInclude(x => x.Options)
                .Include(g => g.Questionnaire).ThenInclude(x => x.Results)
                .AsSplitQuery();
            if (!tracking) q = q.AsNoTracking();
            return q.FirstOrDefaultAsync(g => g.Id == id);
        }

        private static Task<List<GameListItemDto>> ListItems(IQueryable<Models.Game.Game> query) => query
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
                CreatorId = g.CreatorId,
                CreatorName = g.Creator != null ? g.Creator.Username : "未知用户",
                QuestionnaireId = g.Questionnaire != null ? g.Questionnaire.Id : (int?)null,
                QuestionCount = g.Questionnaire != null ? g.Questionnaire.Questions.Count : 0,
            })
            .ToListAsync();

        private static GameResponseDto MapToResponseDto(Models.Game.Game game, string creatorName)
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
