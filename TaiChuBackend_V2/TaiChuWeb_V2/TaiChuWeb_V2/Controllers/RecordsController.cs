using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.Analytics;
using TaiChuWeb_V2.Services.Games;

namespace TaiChuWeb_V2.Controllers
{
    /// <summary>
    /// 战绩：把服务器判定的联机对局（game_matches：斗地主、联机五子棋……）和本地双人棋局（BoardGameRecords：围棋、象棋）合在一起。
    /// 测试问卷的结果在原来的 /api/gamesessions 里，前端单独显示。
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class RecordsController(MatchRecordService records, AppDbContext db, GameSocialService social) : ControllerBase
    {
        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        private static string NormalizeLocalGame(string type) => type switch { "chess" or "xiangqi" => "xiangqi", "gobang" => "gobang", "go" => "go", _ => type };

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Mine([FromQuery] string? game = null, [FromQuery] int limit = 50)
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            limit = Math.Clamp(limit, 1, 200);
            var online = await records.ForUserAsync(uid, game, limit);
            var items = online.Select(m =>
            {
                var me = m.Players.FirstOrDefault(p => p.UserId == uid);
                return new
                {
                    id = "m" + m.Id,
                    game = m.Game,
                    source = "online",
                    mode = m.Mode,
                    summary = m.Summary,
                    time = m.EndedAt,
                    result = me?.Result ?? "draw",
                    role = me?.Role,
                    potatoChange = me?.PotatoChange ?? 0,
                    hasBots = m.HasBots,
                    moves = (int?)null,
                    players = m.Players.Select(p => new { p.Name, p.IsBot, p.Role, p.Result, p.UserId, self = p.UserId == uid }).ToList(),
                };
            }).ToList();

            // 本地双人棋局（自己和自己 / 和身边的人下）：不算胜负，只记录
            // 用 List 不用数组：C# 14 里数组的 .Contains 会被当成 Span 方法，EF 翻译不了（fix123 顺手修）
            var localTypes = game switch { null => null, "xiangqi" => new List<string> { "chess", "xiangqi" }, _ => new List<string> { game } };
            var localQuery = db.BoardGameRecords.AsNoTracking().Where(r => r.Player1Id == uid || r.Player2Id == uid);
            if (localTypes != null) localQuery = localQuery.Where(r => localTypes.Contains(r.GameType));
            var local = await localQuery.OrderByDescending(r => r.CreatedAt).Take(limit)
                .Select(r => new { r.Id, r.GameType, r.Player1Name, r.Player2Name, r.Winner, r.MovesCount, r.CreatedAt }).ToListAsync();
            items.AddRange(local.Select(r => new
            {
                id = "b" + r.Id,
                game = NormalizeLocalGame(r.GameType),
                source = "local",
                mode = "本地双人",
                summary = r.Winner == "player1" ? $"{r.Player1Name} 胜" : r.Winner == "player2" ? $"{r.Player2Name} 胜" : r.Winner == "draw" ? "和棋" : "未分胜负",
                time = DateTime.SpecifyKind(r.CreatedAt, DateTimeKind.Utc),
                result = "local",
                role = (string?)null,
                potatoChange = 0L,
                hasBots = false,
                moves = (int?)r.MovesCount,
                players = new[]
                {
                    new { Name = r.Player1Name, IsBot = false, Role = "先手", Result = r.Winner == "player1" ? "win" : r.Winner == "player2" ? "lose" : "draw", UserId = (Guid?)null, self = true },
                    new { Name = r.Player2Name, IsBot = false, Role = "后手", Result = r.Winner == "player2" ? "win" : r.Winner == "player1" ? "lose" : "draw", UserId = (Guid?)null, self = false },
                }.ToList(),
            }));
            return Ok(items.OrderByDescending(i => i.time).Take(limit));
        }

        private async Task<object> StatsFor(Guid uid)
        {
            var stats = await records.StatsAsync(uid);
            var (streak, kind) = await records.StreakAsync(uid);
            var localCounts = await db.BoardGameRecords.AsNoTracking().Where(r => r.Player1Id == uid || r.Player2Id == uid)
                .GroupBy(r => r.GameType).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
            var totalWins = stats.Sum(s => s.Wins);
            var totalGames = stats.Sum(s => s.Total);
            return new
            {
                totalGames,
                totalWins,
                winRate = totalGames == 0 ? 0 : Math.Round(totalWins * 100.0 / totalGames, 1),
                streak = new { count = streak, kind },
                games = stats.Select(s => new { s.Game, s.Total, s.Wins, s.Losses, s.Draws, winRate = s.Total == 0 ? 0 : Math.Round(s.Wins * 100.0 / s.Total, 1), s.PotatoNet }),
                local = localCounts.Select(l => new { game = NormalizeLocalGame(l.Key), count = l.Count }),
            };
        }

        [Authorize]
        [HttpGet("stats/me")]
        public async Task<IActionResult> MyStats()
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            return Ok(await StatsFor(uid));
        }

        /// <summary>别人的战绩概况（个人主页用）</summary>
        [HttpGet("stats/{userId:guid}")]
        public async Task<IActionResult> UserStats(Guid userId) => Ok(await StatsFor(userId));

        private static DateTime? RangeStart(string range)
        {
            var today = ChinaClock.Today;
            if (range == "week") return ChinaClock.DayStartUtc(today.AddDays(-(((int)today.DayOfWeek + 6) % 7)));
            if (range == "month") return ChinaClock.DayStartUtc(new DateTime(today.Year, today.Month, 1));
            return null;
        }

        /// <summary>常败将军榜：所有对战游戏的失败总场次（total 全部 / week 本周 / month 本月）</summary>
        [HttpGet("leaderboard/losses")]
        public async Task<IActionResult> LossesLeaderboard([FromQuery] string range = "total", [FromQuery] int limit = 50)
        {
            var rows = await records.LossesLeaderboardAsync(RangeStart(range), limit);
            var ids = rows.Select(r => r.UserId).ToList();
            var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.Username, Avatar = u.Profile != null ? u.Profile.Avatar : null }).ToListAsync();
            var map = users.ToDictionary(u => u.Id);
            return Ok(rows.Where(r => map.ContainsKey(r.UserId)).Select((r, i) => new
            {
                rank = i + 1,
                userId = r.UserId,
                name = map[r.UserId].Username,
                avatar = map[r.UserId].Avatar,
                losses = r.Losses,
                total = r.Total,
                lossRate = r.Total == 0 ? 0 : Math.Round(r.Losses * 100.0 / r.Total, 1),
            }));
        }

        // ================= 牌桌互动：被点赞 / 被砸番茄 =================

        private static object ReactionStatsView(GameSocialService.ReactionStats s) => new
        {
            likes = s.Likes,
            tomatoes = s.Tomatoes,
            likes30 = s.Likes30,
            tomatoes30 = s.Tomatoes30,
            givenLikes = s.GivenLikes,
            givenTomatoes = s.GivenTomatoes,
        };

        /// <summary>我在牌桌上累计被点赞 / 被砸番茄多少次（全部 + 最近 30 天），以及送出去多少</summary>
        [Authorize]
        [HttpGet("reactions/me")]
        public async Task<IActionResult> MyReactions()
        {
            if (CurrentUserId is not Guid uid) return Unauthorized();
            return Ok(ReactionStatsView(await social.StatsAsync(uid)));
        }

        /// <summary>别人的被赞 / 被砸（个人主页以后可以用）</summary>
        [HttpGet("reactions/{userId:guid}")]
        public async Task<IActionResult> UserReactions(Guid userId) => Ok(ReactionStatsView(await social.StatsAsync(userId)));

        /// <summary>
        /// 两个小排行：kind=like「最受欢迎」（被赞最多）/ kind=tomato「番茄收割机」（被砸最多）。
        /// range=total 全部时间，range=30d 最近 30 天。
        /// </summary>
        [HttpGet("leaderboard/reactions")]
        public async Task<IActionResult> ReactionsLeaderboard([FromQuery] string kind = "like", [FromQuery] string range = "total", [FromQuery] int limit = 10)
        {
            if (!GameSocialService.ValidKind(kind)) return BadRequest(new { message = "kind 只能是 like 或 tomato" });
            DateTime? since = range == "30d" ? DateTime.UtcNow.AddDays(-30) : null;
            var rows = await social.LeaderboardAsync(kind, since, Math.Clamp(limit, 1, 50));
            var ids = rows.Select(r => r.UserId).ToList();
            var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.Username, Avatar = u.Profile != null ? u.Profile.Avatar : null }).ToListAsync();
            var map = users.ToDictionary(u => u.Id);
            return Ok(rows.Where(r => map.ContainsKey(r.UserId)).Select((r, i) => new
            {
                rank = i + 1,
                userId = r.UserId,
                name = map[r.UserId].Username,
                avatar = map[r.UserId].Avatar,
                count = r.Count,
            }));
        }

        /// <summary>常胜将军榜：所有对战游戏的胜利总场次（total 全部 / week 本周 / month 本月）</summary>
        [HttpGet("leaderboard/wins")]
        public async Task<IActionResult> WinsLeaderboard([FromQuery] string range = "total", [FromQuery] int limit = 50)
        {
            var rows = await records.WinsLeaderboardAsync(RangeStart(range), limit);
            var ids = rows.Select(r => r.UserId).ToList();
            var users = await db.Users.AsNoTracking().Where(u => ids.Contains(u.Id))
                .Select(u => new { u.Id, u.Username, Avatar = u.Profile != null ? u.Profile.Avatar : null }).ToListAsync();
            var map = users.ToDictionary(u => u.Id);
            return Ok(rows.Where(r => map.ContainsKey(r.UserId)).Select((r, i) => new
            {
                rank = i + 1,
                userId = r.UserId,
                name = map[r.UserId].Username,
                avatar = map[r.UserId].Avatar,
                wins = r.Wins,
                total = r.Total,
                winRate = r.Total == 0 ? 0 : Math.Round(r.Wins * 100.0 / r.Total, 1),
            }));
        }
    }
}
