import fs from 'node:fs';import path from 'node:path';
const base='../../TaiChuBackend_V2/TaiChuWeb_V2/TaiChuWeb_V2';const saved={};
function edit(p,fn){const old=fs.readFileSync(base+'/'+p,'utf8');saved[p]=old;const out='.tmp/comment-design/backend/'+p;fs.mkdirSync(path.dirname(out),{recursive:true});fs.writeFileSync(out,fn(old));}
for(const p of ['Controllers/ArtworkController.cs','Services/Artwork/ArtworkEngagementService.cs','Controllers/knowledgeBase/CatalogController.cs'])edit(p,s=>s.replaceAll('a.Status == "published"','(a.Status == "published" || a.Status == "" || a.Status == null)').replaceAll('artwork.Status == "published"','(artwork.Status == "published" || artwork.Status == "" || artwork.Status == null)'));
edit('Controllers/LingMai/LingMaiPublishController.cs',s=>s.replace('p.OriginalNoteId == null || _context.Notes.Any(', 'p.OriginalNoteId == null || !_context.Notes.Any(n => n.Id.ToString() == EF.Functions.Collate(p.OriginalNoteId.ToString(), "utf8mb4_general_ci")) || _context.Notes.Any('));
let cat='.tmp/comment-design/backend/Controllers/knowledgeBase/CatalogController.cs';fs.writeFileSync(cat,fs.readFileSync(cat,'utf8').replaceAll('n.OriginalNoteId == null || db.Notes.Any(', 'n.OriginalNoteId == null || !db.Notes.Any(s => s.Id.ToString() == EF.Functions.Collate(n.OriginalNoteId.ToString(), "utf8mb4_general_ci")) || db.Notes.Any('));
edit('Controllers/UserController.cs',s=>s.replace('a.Status == "published" ? "已发布"','(a.Status == "published" || a.Status == "" || a.Status == null) ? "已发布"').replace('    // 把它加到你现有的 UserController 里',`    [HttpGet("experience-history")]
    public async Task<IActionResult> ExperienceHistory(int offset = 0, int limit = 20)
    {
        if (!Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id)) return Unauthorized();
        offset = Math.Max(0, offset); limit = Math.Clamp(limit, 1, 50);
        var rows = await _context.UserExpLogs.AsNoTracking().Where(log => log.UserId == id)
            .OrderByDescending(log => log.CreatedAt).ThenByDescending(log => log.Id).Skip(offset).Take(limit + 1)
            .Select(log => new { log.Id, log.Change, log.Reason, log.CreatedAt }).ToListAsync();
        return Ok(new { items = rows.Take(limit), hasMore = rows.Count > limit });
    }

    // 把它加到你现有的 UserController 里`));
edit('Controllers/SignController.cs',s=>s.replace('stats.Experience += experienceBonus;','stats.Experience += experienceBonus;\n                        _context.UserExpLogs.Add(new UserExpLog { UserId = userId, Change = experienceBonus, Reason = "每日签到" });'));
edit('Controllers/TradeController.cs',s=>s.replace('user.Stats.Experience -= currentCost;','user.Stats.Experience -= currentCost;\n                    _context.UserExpLogs.Add(new TaiChuWeb_V2.Models.User.UserExpLog { UserId = user.Id, Change = -currentCost, Reason = "经验兑换：" + item.Name });'));
edit('Controllers/Game/GameController.cs',s=>s.replace('user.Stats.Experience -= cost;','user.Stats.Experience -= cost;\n            _context.UserExpLogs.Add(new UserExpLog { UserId = userId, Change = -cost, Reason = "创建游戏" });'));
edit('Controllers/Admin/AdminUserController.cs',s=>s.replace('            stats.Experience = payload.Experience;',`            var experienceChange = payload.Experience - stats.Experience;
            if (experienceChange != 0) _context.UserExpLogs.Add(new UserExpLog { UserId = userId, Change = experienceChange, Reason = "管理员调整经验" });
            stats.Experience = payload.Experience;`));
edit('Program.cs',s=>s.replace('options.Filters.Add<GlobalExceptionFilter>();','options.Filters.Add<GlobalExceptionFilter>();\n    options.Filters.Add<TaiChuWeb_V2.Filters.AdminAccessFilter>();'));
edit('Dtos/Artwork/GalleryItemDto.cs',s=>s.replace('public class ArtworkItemDto : ArtworkStatsDto\r\n    {','public class ArtworkItemDto : ArtworkStatsDto\r\n    {\r\n        public int? RankingLikes { get; set; }'));
const art='.tmp/comment-design/backend/Controllers/ArtworkController.cs';let a=fs.readFileSync(art,'utf8');a=a.replace('string? search = null)','string? search = null, [FromQuery] string? rankPeriod = null)');a=a.replace('            // 2. 获取总数',`            if (rankPeriod != null && rankPeriod is not ("all" or "day" or "week" or "month" or "year")) return BadRequest(new { message = "不支持的榜单周期" });
            var since = rankPeriod switch { "day" => DateTime.UtcNow.AddDays(-1), "week" => DateTime.UtcNow.AddDays(-7), "month" => DateTime.UtcNow.AddMonths(-1), "year" => DateTime.UtcNow.AddYears(-1), _ => DateTime.MinValue };
            var likes = _context.UserInteractions.Where(i => i.TargetType == "Artwork" && i.ActionType == "Like" && i.CreatedAt >= since);
            if (rankPeriod != null && rankPeriod != "all") query = query.Where(a => likes.Any(i => i.TargetId == a.Id.ToString()));
            var ordered = rankPeriod == null ? query.OrderByDescending(a => a.UploadAt).ThenByDescending(a => a.Id)
                : query.OrderByDescending(a => likes.Where(i => i.TargetId == a.Id.ToString()).Select(i => i.UserId).Distinct().Count()).ThenByDescending(a => a.UploadAt).ThenByDescending(a => a.Id);

            // 2. 获取总数`);
a=a.replace('var artworks = await query\r\n                .OrderByDescending(a => a.UploadAt).ThenByDescending(a => a.Id)','var artworks = await ordered');a=a.replace('                    Title = a.Title,','                    Title = a.Title,\n                    RankingLikes = rankPeriod == null ? null : likes.Where(i => i.TargetId == a.Id.ToString()).Select(i => i.UserId).Distinct().Count(),');fs.writeFileSync(art,a);
fs.writeFileSync('.tmp/comment-design/backend-before.json',JSON.stringify(saved));
