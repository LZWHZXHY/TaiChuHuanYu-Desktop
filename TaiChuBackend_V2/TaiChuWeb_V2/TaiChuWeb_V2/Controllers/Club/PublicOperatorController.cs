using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers.Club
{
    /// <summary>
    /// 公开打手列表 —— 完全不需要登录
    /// 路由前缀：api/club/operators
    /// </summary>
    [ApiController]
    [Route("api/club/operators")]
    public class PublicOperatorController : ControllerBase
    {
        private readonly AppDbContext _db;
        public PublicOperatorController(AppDbContext db) => _db = db;

        // ==================================================
        // 打手列表
        // ==================================================
        [HttpGet("list")]
        public async Task<IActionResult> GetPublicList()
        {
            var approvedSkills = await _db.OperatorGameSkills
                .AsNoTracking()
                .Where(s => s.AuditStatus == "approved")
                .ToListAsync();

            if (approvedSkills.Count == 0)
                return Ok(new { total = 0, items = Array.Empty<object>() });

            var userIds = approvedSkills.Select(s => s.UserId).Distinct().ToList();
            var profiles = await _db.OperatorProfiles
                .AsNoTracking()
                .Where(p => userIds.Contains(p.UserId))
                .ToListAsync();

            var items = profiles.Select(p =>
            {
                var skills = approvedSkills.Where(s => s.UserId == p.UserId).ToList();

                var topLevel = skills
                    .Where(s => !string.IsNullOrEmpty(s.OperatorLevel))
                    .Select(s => s.OperatorLevel!)
                    .OrderByDescending(l => l)
                    .FirstOrDefault() ?? "L1";

                double repeatRate = p.UniqueCustomers > 0
                    ? (double)p.RepeatCustomers / p.UniqueCustomers
                    : 0;

                return new
                {
                    userId = p.UserId,
                    name = p.Nickname,
                    intro = p.Intro,
                    reputation = p.Reputation,
                    totalOrders = p.TotalOrders,
                    topLevel,
                    gameCount = skills.Count,
                    recheckStatus = skills.FirstOrDefault()?.RecheckStatus ?? "none",
                    reviewCount = p.ReviewCount,
                    avgOverall = Math.Round(p.AvgOverall, 2),
                    avgSkill = Math.Round(p.AvgSkill, 2),
                    avgAttitude = Math.Round(p.AvgAttitude, 2),
                    avgPunctual = Math.Round(p.AvgPunctual, 2),
                    repeatRate = Math.Round(repeatRate, 3),
                    uniqueCustomers = p.UniqueCustomers,
                    repeatCustomers = p.RepeatCustomers,
                    completedOrders = p.CompletedOrders,
                    cancelledOrders = p.CancelledOrders,
                    games = skills.Select(s => new
                    {
                        gameCode = s.GameCode,
                        gameName = s.GameName,
                        level = s.OperatorLevel,
                        code = s.Code,
                        ordersInGame = s.OrdersInGame,
                        completedOrdersInGame = s.CompletedOrdersInGame,
                        failedOrdersInGame = s.FailedOrdersInGame
                    }).ToList()
                };
            })
            .OrderByDescending(x => x.reviewCount > 0 ? x.avgOverall : 0)
            .ThenByDescending(x => x.reputation)
            .ThenByDescending(x => x.totalOrders)
            .ToList();

            return Ok(new { total = items.Count, items });
        }

        // ==================================================
        // ⭐ 打手雷达图（按游戏）
        // GET /api/club/operators/{userId}/radar?gameCode=delta
        // ==================================================
        [HttpGet("{userId:guid}/radar")]
        public async Task<IActionResult> GetRadar(Guid userId, [FromQuery] string gameCode)
        {
            if (string.IsNullOrWhiteSpace(gameCode))
                return BadRequest(new { message = "gameCode 不能为空" });

            var skill = await _db.OperatorGameSkills
                .AsNoTracking()
                .FirstOrDefaultAsync(s =>
                    s.UserId == userId &&
                    s.GameCode == gameCode &&
                    s.AuditStatus == "approved");

            if (skill == null)
                return NotFound(new { message = "该打手未通过此游戏认证" });

            var game = await _db.ClubGames
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Code == gameCode);

            var profile = await _db.OperatorProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(p => p.UserId == userId);

            var fields = await _db.ClubGameFields
                .AsNoTracking()
                .Where(f => f.GameCode == gameCode && f.IsScoreDimension)
                .OrderBy(f => f.SortOrder)
                .ToListAsync();

            var userMetrics = ParseJsonDict(skill.MetricsJson);

            var peerSkills = await _db.OperatorGameSkills
                .AsNoTracking()
                .Where(s => s.GameCode == gameCode && s.AuditStatus == "approved")
                .ToListAsync();
            var peerMetricList = peerSkills
                .Select(s => ParseJsonDict(s.MetricsJson))
                .ToList();

            var dimensions = new List<object>();

            // ① 打手自填字段
            foreach (var f in fields)
            {
                var raw = userMetrics.TryGetValue(f.Key, out var v) ? v : null;
                var userScore = CalcScore(raw, f.ScoreMapJson);

                var peerScores = peerMetricList
                    .Select(m => m.TryGetValue(f.Key, out var pv) ? pv : null)
                    .Select(x => CalcScore(x, f.ScoreMapJson))
                    .Where(x => x.HasValue)
                    .Select(x => x!.Value)
                    .ToList();

                var avgScore = peerScores.Count > 0
                    ? (int)Math.Round(peerScores.Average())
                    : 0;

                dimensions.Add(new
                {
                    key = f.Key,
                    label = f.Label,
                    userScore = userScore ?? 0,
                    avgScore,
                    rawValue = raw ?? "—",
                    isSystem = false
                });
            }

            // ② 系统维度（按游戏配置）
            var enabledSysDims = ParseStringArray(game?.RadarSystemDimsJson);

            foreach (var code in enabledSysDims)
            {
                var dim = await BuildSystemDim(code, profile, skill, peerSkills, _db);
                if (dim != null) dimensions.Add(dim);
            }

            return Ok(new
            {
                gameCode,
                gameName = skill.GameName,
                dimensions
            });
        }

        // ==================================================
        //  内部
        // ==================================================

        private static async Task<object?> BuildSystemDim(
            string code,
            Models.Club.OperatorProfile? profile,
            Models.Club.OperatorGameSkill skill,
            List<Models.Club.OperatorGameSkill> peers,
            AppDbContext db)
        {
            switch (code)
            {
                case "reputation":
                    {
                        if (profile == null) return null;

                        // ⭐ 修复：先取列表，内存里平均（避免空集合 AverageAsync 抛异常）
                        var reputations = await db.OperatorProfiles
                            .AsNoTracking()
                            .Where(p => p.AuditStatus == "approved")
                            .Select(p => p.Reputation)
                            .ToListAsync();

                        var avg = reputations.Count > 0
                            ? reputations.Average()
                            : 0;

                        return new
                        {
                            key = "__reputation",
                            label = "信誉",
                            userScore = Math.Clamp(profile.Reputation, 0, 100),
                            avgScore = (int)Math.Round(avg),
                            rawValue = profile.Reputation.ToString(),
                            isSystem = true
                        };
                    }

                case "experience":
                    {
                        var avgOrders = peers.Count > 0
                            ? peers.Average(s => s.OrdersInGame)
                            : 0;
                        return new
                        {
                            key = "__experience",
                            label = "经验",
                            userScore = Math.Min(100, skill.OrdersInGame * 2),
                            avgScore = Math.Min(100, (int)Math.Round(avgOrders * 2)),
                            rawValue = skill.OrdersInGame.ToString(),
                            isSystem = true
                        };
                    }

                case "completionRate":
                    {
                        var total = skill.CompletedOrdersInGame + skill.FailedOrdersInGame;
                        var rate = total > 0 ? (double)skill.CompletedOrdersInGame / total : 0;

                        var peerRates = peers
                            .Select(s =>
                            {
                                var t = s.CompletedOrdersInGame + s.FailedOrdersInGame;
                                return t > 0 ? (double?)s.CompletedOrdersInGame / t : null;
                            })
                            .Where(r => r.HasValue)
                            .Select(r => r!.Value)
                            .ToList();

                        var avgRate = peerRates.Count > 0 ? peerRates.Average() : 0;

                        return new
                        {
                            key = "__completionRate",
                            label = "完成率",
                            userScore = (int)Math.Round(rate * 100),
                            avgScore = (int)Math.Round(avgRate * 100),
                            rawValue = $"{(int)Math.Round(rate * 100)}%",
                            isSystem = true
                        };
                    }

                case "repeatRate":
                    {
                        if (profile == null) return null;
                        var rate = profile.UniqueCustomers > 0
                            ? (double)profile.RepeatCustomers / profile.UniqueCustomers
                            : 0;

                        // ⭐ 修复：先取列表，内存里平均
                        var rateRows = await db.OperatorProfiles
                            .AsNoTracking()
                            .Where(p => p.AuditStatus == "approved" && p.UniqueCustomers > 0)
                            .Select(p => new { p.RepeatCustomers, p.UniqueCustomers })
                            .ToListAsync();

                        var avg = rateRows.Count > 0
                            ? rateRows.Average(r => (double)r.RepeatCustomers / r.UniqueCustomers)
                            : 0;

                        return new
                        {
                            key = "__repeatRate",
                            label = "复购率",
                            userScore = (int)Math.Round(rate * 100),
                            avgScore = (int)Math.Round(avg * 100),
                            rawValue = $"{(int)Math.Round(rate * 100)}%",
                            isSystem = true
                        };
                    }

                case "userRating":
                    {
                        if (profile == null) return null;
                        return new
                        {
                            key = "__userRating",
                            label = "用户体验",
                            userScore = (int)Math.Round(profile.AvgOverall * 20),
                            avgScore = 0,
                            rawValue = profile.ReviewCount > 0
                                ? profile.AvgOverall.ToString("0.0")
                                : "暂无",
                            isSystem = true
                        };
                    }

                case "userSkillRating":
                    {
                        if (profile == null) return null;
                        return new
                        {
                            key = "__userSkillRating",
                            label = "用户实力评分",
                            userScore = (int)Math.Round(profile.AvgSkill * 20),
                            avgScore = 0,
                            rawValue = profile.ReviewCount > 0
                                ? profile.AvgSkill.ToString("0.0")
                                : "暂无",
                            isSystem = true
                        };
                    }

                case "responseSpeed":
                    {
                        return new
                        {
                            key = "__responseSpeed",
                            label = "响应速度",
                            userScore = 0,
                            avgScore = 0,
                            rawValue = "暂无",
                            isSystem = true
                        };
                    }
            }

            return null;
        }

        private static int? CalcScore(string? rawValue, string? scoreMapJson)
        {
            if (string.IsNullOrWhiteSpace(rawValue)) return null;
            if (string.IsNullOrWhiteSpace(scoreMapJson)) return null;

            try
            {
                var doc = JsonDocument.Parse(scoreMapJson);
                var root = doc.RootElement;

                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty(rawValue, out var hit)
                    && hit.ValueKind == JsonValueKind.Number)
                {
                    return hit.GetInt32();
                }

                if (root.ValueKind == JsonValueKind.Object
                    && root.TryGetProperty("max", out var maxEl)
                    && double.TryParse(rawValue, out var num))
                {
                    var min = root.TryGetProperty("min", out var minEl) ? minEl.GetDouble() : 0;
                    var max = maxEl.GetDouble();
                    var reverse = root.TryGetProperty("reverse", out var rev) && rev.GetBoolean();

                    if (max <= min) return null;
                    var ratio = Math.Clamp((num - min) / (max - min), 0, 1);
                    if (reverse) ratio = 1 - ratio;
                    return (int)Math.Round(ratio * 100);
                }
            }
            catch { }

            return null;
        }

        private static Dictionary<string, string> ParseJsonDict(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try
            {
                return JsonSerializer.Deserialize<Dictionary<string, string>>(json) ?? new();
            }
            catch { return new(); }
        }

        private static List<string> ParseStringArray(string? json)
        {
            if (string.IsNullOrWhiteSpace(json)) return new();
            try
            {
                return JsonSerializer.Deserialize<List<string>>(json) ?? new();
            }
            catch { return new(); }
        }
    }
}