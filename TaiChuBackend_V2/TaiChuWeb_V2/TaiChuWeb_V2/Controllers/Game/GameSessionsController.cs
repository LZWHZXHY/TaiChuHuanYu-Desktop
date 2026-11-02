using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Game;

namespace TaiChuWeb_V2.Controllers.Game
{
    public class SubmitSessionDto
    {
        public int GameId { get; set; }
        /// <summary>每道题选了哪些选项（按题目顺序；单选 / 是非 / 量表一个，多选可以多个）。新版前端传这个，分数由服务器算</summary>
        public List<List<int>>? OptionIds { get; set; }
        // 旧版前端传的（分数前端自己算）：没有 OptionIds 时才用
        public int TotalScore { get; set; }
        public int? ResultId { get; set; }
        public List<int>? Answers { get; set; }
    }

    /// <summary>
    /// 心性测试的答题记录（前端 games_api.ts 一直在调 /api/gamesessions，但以前后端没有这个接口，成绩从来没存上）。
    /// · 交卷：服务器按选项重新算分、找结果、记一条成绩，游戏的「被玩次数」+1；
    /// · 分数落在所有结果区间外时，取离得最近的结果，保证答题的人一定拿到结果；
    /// · 我的记录（战绩页「问卷」那一栏）和单条详情。
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class GameSessionsController(AppDbContext db) : ControllerBase
    {
        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        [HttpPost]
        public async Task<IActionResult> Submit([FromBody] SubmitSessionDto dto)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var game = await db.Games.AsNoTracking()
                .Include(g => g.Questionnaire).ThenInclude(q => q.Questions).ThenInclude(q => q.Options)
                .Include(g => g.Questionnaire).ThenInclude(q => q.Results)
                .AsSplitQuery()
                .FirstOrDefaultAsync(g => g.Id == dto.GameId);
            if (game == null || (game.Status != GamesController.Published && game.CreatorId != userId))
                return NotFound(new { message = "这个测试不存在或还没发布" });
            var questionnaire = game.Questionnaire;
            var questions = questionnaire?.Questions?.OrderBy(q => q.Order).ToList() ?? new List<GameQuestion>();
            var results = questionnaire?.Results?.OrderBy(r => r.Order).ToList() ?? new List<GameResult>();
            if (questions.Count == 0) return BadRequest(new { message = "这个测试还没有题目" });

            int score;
            string answersJson;
            if (dto.OptionIds != null)
            {
                if (dto.OptionIds.Count != questions.Count) return BadRequest(new { message = "还有题目没答完" });
                var total = 0;
                for (var i = 0; i < questions.Count; i++)
                {
                    var q = questions[i];
                    var picked = dto.OptionIds[i]?.Distinct().ToList() ?? new List<int>();
                    var options = q.Options?.Where(o => picked.Contains(o.Id)).ToList() ?? new List<GameOption>();
                    if (options.Count != picked.Count || picked.Count == 0) return BadRequest(new { message = $"第 {i + 1} 题的答案不对，请刷新后重答" });
                    if (q.Type != "multiple" && picked.Count != 1) return BadRequest(new { message = $"第 {i + 1} 题只能选一个" });
                    total += options.Sum(o => o.Value);
                }
                score = questionnaire!.Scoring == "average"
                    ? (int)Math.Round((double)total / questions.Count, MidpointRounding.AwayFromZero)
                    : total;
                answersJson = JsonSerializer.Serialize(new { optionIds = dto.OptionIds });
            }
            else
            {
                // 旧版前端：信它算的分
                score = dto.TotalScore;
                answersJson = JsonSerializer.Serialize(dto.Answers ?? new List<int>());
            }

            var result = MatchResult(results, score);
            var session = new GameSession
            {
                GameId = game.Id,
                UserId = userId,
                TotalScore = score,
                ResultId = result?.Id,
                AnswersJson = answersJson,
                CreatedAt = DateTime.UtcNow,
            };
            db.GameSessions.Add(session);
            await db.SaveChangesAsync();
            await db.Games.Where(g => g.Id == game.Id).ExecuteUpdateAsync(s => s.SetProperty(g => g.PlayCount, g => g.PlayCount + 1));

            return Ok(new
            {
                id = session.Id,
                totalScore = score,
                result = result == null ? null : new { result.Id, result.Title, result.Description, result.Icon, result.Image, result.Min, result.Max },
            });
        }

        /// <summary>分数所在区间的结果；都不在就取离得最近的（前面的优先）</summary>
        public static GameResult? MatchResult(List<GameResult> results, int score)
        {
            if (results.Count == 0) return null;
            return results.FirstOrDefault(r => score >= r.Min && score <= r.Max)
                ?? results.OrderBy(r => score < r.Min ? r.Min - score : score - r.Max).ThenBy(r => r.Order).First();
        }

        [HttpGet("my")]
        public async Task<IActionResult> My([FromQuery] int? gameId = null, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            pageSize = Math.Clamp(pageSize, 1, 100);
            page = Math.Max(1, page);
            var query = db.GameSessions.AsNoTracking().Where(s => s.UserId == userId);
            if (gameId is int gid) query = query.Where(s => s.GameId == gid);
            var total = await query.CountAsync();
            var items = await query.OrderByDescending(s => s.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize)
                .Select(s => new
                {
                    s.Id,
                    s.GameId,
                    GameTitle = s.Game != null ? s.Game.Title : "（测试已删除）",
                    GameIcon = s.Game != null ? s.Game.Icon : "📋",
                    s.TotalScore,
                    ResultTitle = s.Result != null ? s.Result.Title : null,
                    ResultIcon = s.Result != null ? s.Result.Icon : null,
                    s.CreatedAt,
                })
                .ToListAsync();
            return Ok(new { total, items });
        }

        [HttpGet("{id:long}")]
        public async Task<IActionResult> Detail(long id)
        {
            if (CurrentUserId is not Guid userId) return Unauthorized();
            var s = await db.GameSessions.AsNoTracking()
                .Include(x => x.Result)
                .Include(x => x.Game).ThenInclude(g => g.Questionnaire).ThenInclude(q => q.Questions).ThenInclude(q => q.Options)
                .AsSplitQuery()
                .FirstOrDefaultAsync(x => x.Id == id && x.UserId == userId);
            if (s == null) return NotFound(new { message = "记录不存在" });

            List<List<int>>? picked = null;
            try
            {
                using var doc = JsonDocument.Parse(s.AnswersJson ?? "");
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty("optionIds", out var ids))
                    picked = JsonSerializer.Deserialize<List<List<int>>>(ids.GetRawText());
            }
            catch (JsonException) { /* 旧记录只存了分数 */ }

            var questions = s.Game?.Questionnaire?.Questions?.OrderBy(q => q.Order).ToList() ?? new List<GameQuestion>();
            return Ok(new
            {
                s.Id,
                GameTitle = s.Game?.Title ?? "（测试已删除）",
                GameIcon = s.Game?.Icon ?? "📋",
                s.TotalScore,
                Result = s.Result == null ? null : new { s.Result.Title, s.Result.Description, s.Result.Icon, s.Result.Image },
                s.CreatedAt,
                Questions = questions.Select((q, i) =>
                {
                    var mine = picked != null && i < picked.Count ? picked[i] ?? new List<int>() : new List<int>();
                    var options = q.Options?.OrderBy(o => o.Order).ToList() ?? new List<GameOption>();
                    return new
                    {
                        q.Id,
                        q.Type,
                        q.Text,
                        q.Image,
                        SelectedScore = options.Where(o => mine.Contains(o.Id)).Sum(o => o.Value),
                        SelectedOptionIds = mine,
                        Options = options.Select(o => new { o.Id, o.Label, o.Value, o.Image }),
                    };
                }),
            });
        }
    }
}
