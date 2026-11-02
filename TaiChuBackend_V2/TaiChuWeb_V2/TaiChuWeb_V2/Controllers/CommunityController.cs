using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers
{
    // 首页“社区动态”信息流：聚合博客、帖子、画廊作品、世界观设定（百科）的最近公开更新。
    public sealed class CommunityFeedItem
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = ""; // blog | post | artwork | wiki-new | wiki-update | user-joined | signin | comment-blog | comment-post | comment-artwork | comment-work | comment-character | comment-history | joint | survey | dev-project | world-new | world-edit | quest-start | quest-complete | history | chai-work | oc | battle | novel-new | novel-chapter | chronicle | activity | checkin | comment-activity | quest-report | news | release | must-eat | trade-item | sponsor | resource
        public string ActorName { get; set; } = "";
        public string? ActorId { get; set; } // 动态发起人的用户 ID，点击进入个人主页；官方动态为空
        public string TargetTitle { get; set; } = "";
        public string TargetId { get; set; } = "";
        public DateTime CreatedAt { get; set; }
        /// <summary>补充说明（世界观动态：新建了几个、更新了几个条目，都是哪些）</summary>
        public string? Detail { get; set; }
    }

    [ApiController]
    [Route("api/[controller]")]
    public class CommunityController(AppDbContext db) : ControllerBase
    {
        [HttpGet("feed")]
        public async Task<IActionResult> Feed([FromQuery] int offset = 0, [FromQuery] int limit = 6)
        {
            offset = Math.Max(0, offset);
            limit = Math.Clamp(limit, 1, 50);
            var need = offset + limit + 1; // 多取一条用于判断是否还有下一页

            var feed = await CachedFeedAsync(need);
            var hasMore = feed.Count > offset + limit;
            var page = feed.Skip(offset).Take(limit).ToList();
            return Ok(new { items = page, hasMore });
        }

        // ===== 缓存：动态要查 50 多次数据库（每类各查一次，一次接一次），数据库在远处时一次要一两秒甚至更久。
        // 合并排好的整条动态留在内存里，所有人、所有页（含「加载更多」）都直接切片，不再查库：
        // · 算好后 FreshSeconds 秒内直接用；
        // · 过了 FreshSeconds 秒但不到 StaleSeconds 秒：先把手上这份给出去（不让人等），后台另开一个数据库连接重新算一份换上；
        // · 超过 StaleSeconds 秒、第一次、或者翻页翻得比缓存的还深：当场算（同一时间只让一个请求算，其他的等它算完直接用）。
        // 每类各取 N 条再合并排序，前 N 条一定是准的，所以缓存时取的 N ≥ offset + limit + 1 就能直接用；一次按 FeedChunk 的整数倍多取一些。
        private const int FreshSeconds = 20;
        private const int StaleSeconds = 600;
        private const int FeedChunk = 36;
        private static readonly SemaphoreSlim FeedGate = new(1, 1);
        /// <summary>算好的整条动态（一次整体替换，读的时候不会读到一半新一半旧）</summary>
        private sealed record FeedSnapshot(List<CommunityFeedItem> Items, int Take, DateTime At);
        private static volatile FeedSnapshot? _feed;
        private static int _refreshing;

        private async Task<List<CommunityFeedItem>> CachedFeedAsync(int need)
        {
            var cached = _feed;
            if (cached != null && cached.Take >= need)
            {
                var age = DateTime.UtcNow - cached.At;
                if (age < TimeSpan.FromSeconds(FreshSeconds)) return cached.Items;
                if (age < TimeSpan.FromSeconds(StaleSeconds)) { RefreshInBackground(cached.Take); return cached.Items; }
            }
            await FeedGate.WaitAsync(HttpContext.RequestAborted);
            try
            {
                cached = _feed;
                if (cached != null && cached.Take >= need && DateTime.UtcNow - cached.At < TimeSpan.FromSeconds(FreshSeconds)) return cached.Items;
                var take = Math.Max((need + FeedChunk - 1) / FeedChunk * FeedChunk, cached?.Take ?? 0);
                var built = await BuildFeedAsync(take);
                _feed = new FeedSnapshot(built, take, DateTime.UtcNow);
                return built;
            }
            finally { FeedGate.Release(); }
        }

        /// <summary>后台重新算一份（这个请求结束后它的数据库连接就没了，所以另开一个作用域、另建一个控制器来算）。同一时间只算一份，出错就算了，下次再试</summary>
        private void RefreshInBackground(int take)
        {
            if (Interlocked.Exchange(ref _refreshing, 1) == 1) return;
            var scopes = HttpContext.RequestServices.GetRequiredService<IServiceScopeFactory>();
            var logger = HttpContext.RequestServices.GetService<ILogger<CommunityController>>();
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var worker = new CommunityController(scope.ServiceProvider.GetRequiredService<AppDbContext>())
                    {
                        ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider } }
                    };
                    await FeedGate.WaitAsync();
                    try
                    {
                        var built = await worker.BuildFeedAsync(take);
                        _feed = new FeedSnapshot(built, take, DateTime.UtcNow);
                    }
                    finally { FeedGate.Release(); }
                }
                catch (Exception ex) { logger?.LogWarning(ex, "后台刷新社区动态失败（先继续用旧的）"); }
                finally { Interlocked.Exchange(ref _refreshing, 0); }
            });
        }

        private async Task<List<CommunityFeedItem>> BuildFeedAsync(int takeCount)
        {
            // 1. 博客与帖子（仅公开）
            var blogPostRows = await db.PublishedNotes
                .Where(n => n.Type == "blog" || n.Type == "post")
                .Where(n => n.OriginalNoteId == null
                    || !db.Notes.Any(s => s.Id.ToString() == EF.Functions.Collate(n.OriginalNoteId.ToString(), "utf8mb4_general_ci"))
                    || db.Notes.Any(s => s.Id.ToString() == EF.Functions.Collate(n.OriginalNoteId.ToString(), "utf8mb4_general_ci") && s.IsPublic && s.Status != 2))
                .OrderByDescending(n => n.PublishedAt)
                .Take(takeCount)
                .Select(n => new
                {
                    Item = new CommunityFeedItem
                    {
                        Id = (n.Type == "post" ? "post:" : "blog:") + n.Id,
                        Kind = n.Type == "post" ? "post" : "blog",
                        ActorName = n.AuthorName ?? "太初隐者",
                        TargetTitle = n.Title ?? (n.Type == "post" ? "短帖" : "无标题文章"),
                        TargetId = n.Id.ToString(),
                        CreatedAt = n.PublishedAt
                    },
                    n.SpaceId
                })
                .ToListAsync();
            // 作者 = 所在空间的主人。spaces 与 PublishedNotes 排序规则不同，单独查再在内存里对应。
            var postSpaceIds = blogPostRows.Select(r => r.SpaceId).Distinct().ToList();
            var spaceOwners = (await db.Spaces.Where(s => postSpaceIds.Contains(s.Id)).Select(s => new { s.Id, s.UserId }).ToListAsync())
                .GroupBy(s => s.Id).ToDictionary(g => g.Key, g => g.First().UserId);
            var blogPosts = blogPostRows.Select(r => { r.Item.ActorId = spaceOwners.TryGetValue(r.SpaceId, out var owner) ? owner : null; return r.Item; }).ToList();

            // 2. 画廊作品（审核通过且已公开）
            var artworks = await db.Artworks
                .Where(a => a.IsApproved && (a.Status == "published" || a.Status == "" || a.Status == null))
                .OrderByDescending(a => a.UploadAt)
                .Take(takeCount)
                .Select(a => new CommunityFeedItem
                {
                    Id = "art:" + a.Id,
                    Kind = "artwork",
                    ActorName = a.Uploader.Username ?? "太初隐者",
                    ActorId = a.UploaderId.ToString(),
                    TargetTitle = a.Title,
                    TargetId = a.Id.ToString(),
                    CreatedAt = a.UploadAt
                })
                .ToListAsync();

            // 3. 世界观设定（百科条目）
            var wikis = await db.WikiArticles
                .Where(a => !a.IsDeleted && a.CurrentRevisionId != null)
                .OrderByDescending(a => a.UpdatedAt)
                .Take(takeCount)
                .Select(a => new { a.Id, a.Title, a.CreatorId, a.CreatedAt, a.UpdatedAt })
                .ToListAsync();

            var userIds = wikis.Select(w => w.CreatorId).Distinct().ToList();
            var userMap = await db.Users
                .Where(u => userIds.Contains(u.Id.ToString()))
                .Select(u => new { u.Id, u.Username })
                .ToDictionaryAsync(u => u.Id.ToString(), u => u.Username);

            var wikiItems = wikis.Select(w => new CommunityFeedItem
            {
                Id = "wiki:" + w.Id,
                Kind = (w.UpdatedAt - w.CreatedAt).TotalSeconds < 10 ? "wiki-new" : "wiki-update",
                ActorName = userMap.TryGetValue(w.CreatorId, out var name) ? name : "太初隐者",
                ActorId = w.CreatorId,
                TargetTitle = w.Title,
                TargetId = w.Id,
                CreatedAt = w.UpdatedAt
            });

            // 4. 新成员加入（持久事件，不依赖在线状态；限制条数避免注册潮刷屏）
            var joinLimit = Math.Min(5, takeCount);
            var newUsers = await db.Users
                .OrderByDescending(u => u.CreatedAt)
                .Take(joinLimit)
                .Select(u => new CommunityFeedItem
                {
                    Id = "user:" + u.Id,
                    Kind = "user-joined",
                    ActorName = u.Username,
                    ActorId = u.Id.ToString(),
                    TargetTitle = "",
                    TargetId = u.Id.ToString(),
                    CreatedAt = u.CreatedAt
                })
                .ToListAsync();

            // 4.5 签到动态（近几次签到，避免刷屏）
            var signLimit = Math.Min(8, takeCount);
            var signins = (await db.UserSignLogs
                    .OrderByDescending(s => s.SignDate)
                    .Take(signLimit)
                    .Join(db.Users, s => s.UserId, u => u.Id, (s, u) => new { s.UserId, s.SignDate, s.CreatedAt, u.Username })
                    .ToListAsync())
                .Select(x => new CommunityFeedItem
                {
                    Id = "signin:" + x.UserId + ":" + x.SignDate.ToString("yyyyMMdd"),
                    Kind = "signin",
                    ActorName = x.Username,
                    ActorId = x.UserId.ToString(),
                    TargetTitle = "",
                    TargetId = x.UserId.ToString(),
                    // 用真正签到的时刻（UTC）。以前用的是 SignDate（北京时间的「日期」，零点），输出时没有时区，
                    // 美国那边看就成了「今天零点」甚至「未来」，一直显示「刚刚」。
                    // 老记录没有可信的 CreatedAt 时，按北京时间当天零点换算成 UTC
                    CreatedAt = SignedAt(x.SignDate, x.CreatedAt)
                })
                .ToList();

            // 5. 评论动态（博客/帖子的顶层评论 + 画廊作品的顶层评论）

            // 先只查 comments 表拿到顶层笔记评论，避免 Guid 跨表连接触发 MySQL 排序规则冲突。
            var noteCommentsRaw = await db.Comments
                .Where(c => c.NoteId != null && c.Content != "" && c.ParentId == null)
                .OrderByDescending(c => c.CreatedAt)
                .Take(takeCount)
                .Select(c => new { CommentId = c.Id, UserId = c.UserId, NoteId = c.NoteId!.Value, CreatedAt = c.CreatedAt })
                .ToListAsync();

            var noteIds = noteCommentsRaw.Select(r => r.NoteId).Distinct().ToList();

            // 用「列 = 参数」的方式解析 NoteId -> PublishedNote，不产生跨列比较，天然避开排序规则冲突。
            var publishedByNote = (await db.PublishedNotes
                .Where(pn => (pn.Type == "blog" || pn.Type == "post") && noteIds.Contains(pn.OriginalNoteId!.Value))
                .Select(pn => new { pn.Id, pn.OriginalNoteId, pn.Title, pn.Type })
                .ToListAsync())
                .ToDictionary(p => p.OriginalNoteId!.Value);

            var publicNoteIds = await db.Notes
                .Where(s => noteIds.Contains(s.Id) && s.IsPublic && s.Status != 2)
                .Select(s => s.Id)
                .ToListAsync();
            var publicSet = publicNoteIds.ToHashSet();

            var noteCommentRows = noteCommentsRaw
                .Where(r => publicSet.Contains(r.NoteId) && publishedByNote.ContainsKey(r.NoteId))
                .Select(r =>
                {
                    var pn = publishedByNote[r.NoteId];
                    return new
                    {
                        CommentId = r.CommentId,
                        UserId = r.UserId,
                        Kind = pn.Type == "post" ? "comment-post" : "comment-blog",
                        TargetTitle = pn.Title ?? (pn.Type == "post" ? "短帖" : "无标题文章"),
                        TargetId = pn.Id.ToString(),
                        CreatedAt = r.CreatedAt
                    };
                })
                .ToList();

            var artworkCommentRows = await (
                from c in db.Comments
                where c.ArtworkId != null && c.Content != "" && c.ParentId == null
                join a in db.Artworks on c.ArtworkId equals a.Id
                where a.IsApproved && (a.Status == "published" || a.Status == "" || a.Status == null)
                orderby c.CreatedAt descending
                select new
                {
                    CommentId = c.Id,
                    UserId = c.UserId,
                    Kind = "comment-artwork",
                    TargetTitle = a.Title,
                    TargetId = a.Id.ToString(),
                    CreatedAt = c.CreatedAt
                }).Take(takeCount).ToListAsync();

            // 柴圈作品 / OC 下面的评论（comments 表的 TargetType = work / character；只算已发布的作品）
            var chaiCommentsRaw = await db.Comments
                .Where(c => (c.TargetType == "work" || c.TargetType == "character") && c.Content != "" && c.ParentId == null)
                .OrderByDescending(c => c.CreatedAt).Take(takeCount)
                .Select(c => new { c.Id, c.UserId, c.TargetType, c.TargetId, c.CreatedAt }).ToListAsync();
            var chaiIds = chaiCommentsRaw.Select(c => Guid.TryParse(c.TargetId, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().ToList();
            var chaiWorkTitles = chaiIds.Count == 0 ? new Dictionary<Guid, string>() : await db.ChaiWorks
                .Where(w => chaiIds.Contains(w.Id) && w.Status == "published").ToDictionaryAsync(w => w.Id, w => w.Title);
            var chaiCharacterTitles = chaiIds.Count == 0 ? new Dictionary<Guid, string>() : await db.StickmanCharacters
                .Where(x => chaiIds.Contains(x.Id) && x.Status == "published").ToDictionaryAsync(x => x.Id, x => x.Title);
            var chaiCommentRows = chaiCommentsRaw.Select(c =>
            {
                var id = Guid.TryParse(c.TargetId, out var g) ? g : Guid.Empty;
                var titles = c.TargetType == "work" ? chaiWorkTitles : chaiCharacterTitles;
                return titles.TryGetValue(id, out var title) ? new
                {
                    CommentId = c.Id,
                    UserId = c.UserId,
                    Kind = c.TargetType == "work" ? "comment-work" : "comment-character",
                    TargetTitle = title,
                    TargetId = id.ToString(),
                    CreatedAt = c.CreatedAt
                } : null;
            }).Where(r => r != null).Select(r => r!).ToList();

            var commenterIds = noteCommentRows.Concat(artworkCommentRows).Concat(chaiCommentRows).Select(r => r.UserId).Distinct().ToList();
            var commenterMap = await db.Users
                .Where(u => commenterIds.Contains(u.Id.ToString()))
                .Select(u => new { u.Id, u.Username })
                .ToDictionaryAsync(u => u.Id.ToString(), u => u.Username);

            var commentItems = noteCommentRows.Concat(artworkCommentRows).Concat(chaiCommentRows).Select(r => new CommunityFeedItem
            {
                Id = "comment:" + r.CommentId,
                Kind = r.Kind,
                ActorName = commenterMap.TryGetValue(r.UserId, out var name) ? name : "社区用户",
                ActorId = r.UserId,
                TargetTitle = r.TargetTitle,
                TargetId = r.TargetId,
                CreatedAt = r.CreatedAt
            });

            // 6. 联合活动（开放中的）
            var joints = await db.JointActivities
                .Where(a => a.Status == "open")
                .OrderByDescending(a => a.CreatedAt)
                .Take(takeCount)
                .Select(a => new CommunityFeedItem
                {
                    Id = "joint:" + a.Id,
                    Kind = "joint",
                    ActorName = a.OrganizerName,
                    ActorId = a.OrganizerType == "user" ? a.OrganizerId.ToString() : null,
                    TargetTitle = a.Title,
                    TargetId = a.Id.ToString(),
                    CreatedAt = a.CreatedAt
                })
                .ToListAsync();

            // 7. 问卷调查（发布中且公开）
            var surveyRows = await db.Surveys
                .Where(s => s.Status == 1 && s.IsPublic)
                .OrderByDescending(s => s.UpdatedAt ?? s.CreatedAt)
                .Take(takeCount)
                .Select(s => new { s.Id, s.Title, s.CreatedBy, s.CreatedAt, s.UpdatedAt })
                .ToListAsync();

            var surveyCreatorIds = surveyRows.Select(s => s.CreatedBy).Distinct().ToList();
            var surveyCreatorMap = await db.Users
                .Where(u => surveyCreatorIds.Contains(u.Id))
                .Select(u => new { u.Id, u.Username })
                .ToDictionaryAsync(u => u.Id, u => u.Username);

            var surveys = surveyRows.Select(s => new CommunityFeedItem
            {
                Id = "survey:" + s.Id,
                Kind = "survey",
                ActorName = surveyCreatorMap.TryGetValue(s.CreatedBy, out var name) ? name : "太初隐者",
                ActorId = s.CreatedBy.ToString(),
                TargetTitle = s.Title,
                TargetId = s.Id.ToString(),
                CreatedAt = s.UpdatedAt ?? s.CreatedAt
            });

            // 8. 官方开发进度
            var devProjects = await db.DevProjects
                .OrderByDescending(p => p.UpdatedAt)
                .Take(takeCount)
                .Select(p => new CommunityFeedItem
                {
                    Id = "dev:" + p.Id,
                    Kind = "dev-project",
                    ActorName = "太初官方",
                    TargetTitle = p.Name + " · 进度 " + p.Progress + "%",
                    TargetId = p.Id.ToString(),
                    CreatedAt = p.UpdatedAt
                })
                .ToListAsync();

            // 9. 世界观（只算公开的世界）：新建了世界；某人某天在某个世界里新建 / 修改了哪些条目（同一人同一天合成一条，免得刷屏）
            var worldItems = await WorldFeedAsync(takeCount);

            // 10. 我的世界服务器「世界任务」：开始了新任务、任务完成（附解锁内容）
            var questRows = await db.ServerQuests.AsNoTracking().Where(q => q.StartedAt != null)
                .OrderByDescending(q => q.CompletedAt ?? q.StartedAt).Take(takeCount)
                .Select(q => new { q.Id, q.Title, q.Kind, q.Line, q.StartedAt, q.CompletedAt, q.RewardText, q.RewardTeaser, q.Target, q.Unit }).ToListAsync();
            var questItems = questRows.SelectMany(q =>
            {
                var list = new List<CommunityFeedItem> { new()
                {
                    Id = "quest-start:" + q.Id, Kind = "quest-start", ActorName = "太初官方", TargetTitle = q.Title, TargetId = q.Id.ToString(),
                    CreatedAt = q.StartedAt!.Value, Detail = (q.Line == "side" ? "支线任务 · " : "") + (q.Kind == "goal" ? "一次性目标，做到就完成" : $"目标 {q.Target:N0} {q.Unit}") + (string.IsNullOrWhiteSpace(q.RewardTeaser) ? "" : $"，完成后解锁：{q.RewardTeaser}"),
                } };
                if (q.CompletedAt.HasValue)
                {
                    var unlock = (q.RewardText ?? "").Split('\n').Select(l => l.Trim()).FirstOrDefault(l => l.Length > 0);
                    list.Add(new()
                    {
                        Id = "quest-done:" + q.Id, Kind = "quest-complete", ActorName = "太初官方", TargetTitle = q.Title, TargetId = q.Id.ToString(),
                        CreatedAt = q.CompletedAt.Value, Detail = unlock == null ? null : "解锁：" + (unlock.Length > 60 ? unlock[..60] + "…" : unlock),
                    });
                }
                return list;
            });

            // 11. 岁月史书：谁记了一段（被下架的不算）
            var historyRows = await db.CommunityHistoryEntries.AsNoTracking().Where(e => !e.Hidden)
                .OrderByDescending(e => e.CreatedAt).Take(takeCount)
                .Select(e => new { e.Id, e.UserId, e.Title, e.Content, e.Images, e.CreatedAt }).ToListAsync();
            var historyAuthorIds = historyRows.Select(r => r.UserId).Distinct().ToList();
            var historyAuthors = historyAuthorIds.Count == 0 ? new Dictionary<Guid, string>() : await db.Users.AsNoTracking()
                .Where(u => historyAuthorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
            var historyItems = historyRows.Select(r =>
            {
                var text = r.Title.Length > 0 ? r.Title : System.Text.RegularExpressions.Regex.Replace(r.Content ?? "", @"\s+", " ").Trim();
                if (text.Length > 40) text = text[..40] + "…";
                return new CommunityFeedItem
                {
                    Id = "history:" + r.Id, Kind = "history", ActorName = historyAuthors.GetValueOrDefault(r.UserId) ?? "社区用户", ActorId = r.UserId.ToString(),
                    TargetTitle = text.Length > 0 ? text : "一组截图", TargetId = r.Id.ToString(), CreatedAt = r.CreatedAt,
                };
            });

            // 岁月史书下面的评论（被下架的那一段的评论不算）
            var historyCommentRows = await db.CommunityHistoryComments.AsNoTracking()
                .OrderByDescending(c => c.CreatedAt).Take(takeCount)
                .Select(c => new { c.Id, c.EntryId, c.UserId, c.CreatedAt }).ToListAsync();
            var hcEntryIds = historyCommentRows.Select(c => c.EntryId).Distinct().ToList();
            var hcEntries = hcEntryIds.Count == 0 ? new Dictionary<long, string>() : (await db.CommunityHistoryEntries.AsNoTracking()
                    .Where(e => hcEntryIds.Contains(e.Id) && !e.Hidden).Select(e => new { e.Id, e.Title, e.Content }).ToListAsync())
                .ToDictionary(e => e.Id, e =>
                {
                    var t = e.Title.Length > 0 ? e.Title : System.Text.RegularExpressions.Regex.Replace(e.Content ?? "", @"\s+", " ").Trim();
                    return t.Length == 0 ? "一组截图" : t.Length > 40 ? t[..40] + "…" : t;
                });
            var hcUserIds = historyCommentRows.Select(c => c.UserId).Distinct().ToList();
            var hcUsers = hcUserIds.Count == 0 ? new Dictionary<Guid, string>() : await db.Users.AsNoTracking()
                .Where(u => hcUserIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
            var historyCommentItems = historyCommentRows.Where(c => hcEntries.ContainsKey(c.EntryId)).Select(c => new CommunityFeedItem
            {
                Id = "history-comment:" + c.Id, Kind = "comment-history", ActorName = hcUsers.GetValueOrDefault(c.UserId) ?? "社区用户", ActorId = c.UserId.ToString(),
                TargetTitle = hcEntries[c.EntryId], TargetId = c.EntryId.ToString(), CreatedAt = c.CreatedAt,
            });

            // 13. 其他板块：柴圈作品 / OC / 对战、小说、编年史、打卡活动、世界任务贡献、公告、版本更新、必吃榜、交易行上新、赞助
            var moreItems = await MoreFeedAsync(takeCount);

            var feed = blogPosts.Concat(artworks).Concat(wikiItems).Concat(newUsers).Concat(signins).Concat(commentItems).Concat(joints).Concat(surveys).Concat(devProjects).Concat(worldItems).Concat(questItems).Concat(historyItems).Concat(historyCommentItems).Concat(moreItems)
                .ToList();
            // 只有日期、没有具体时刻的（正好零点：赞助日期、版本发布日期等，都是按北京时间填的日期）：
            // 换成北京时间当天零点对应的 UTC 时刻再输出，否则各地浏览器会当成自己当地的零点，海外看就跑到「未来」、显示「刚刚」
            foreach (var item in feed)
                if (item.CreatedAt.Kind != DateTimeKind.Utc && item.CreatedAt.TimeOfDay == TimeSpan.Zero)
                    item.CreatedAt = DateTime.SpecifyKind(item.CreatedAt - China, DateTimeKind.Utc);
            // 缓存里的是共用的：排好序后不再改动里面的条目
            return feed.OrderByDescending(i => i.CreatedAt).ToList();
        }

        private static readonly TimeSpan China = TimeSpan.FromHours(8);

        /// <summary>签到的实际时刻：签到记录的 CreatedAt（UTC）落在这个北京日期之内就用它，否则用北京时间当天零点（换成 UTC）</summary>
        private static DateTime SignedAt(DateTime signDate, DateTime createdAt)
        {
            var dayStartUtc = signDate.Date - China;
            return createdAt >= dayStartUtc && createdAt < dayStartUtc.AddDays(1)
                ? DateTime.SpecifyKind(createdAt, DateTimeKind.Utc)
                : DateTime.SpecifyKind(dayStartUtc, DateTimeKind.Utc);
        }
        private static string Clip(string? text, int max)
        {
            var t = System.Text.RegularExpressions.Regex.Replace(text ?? "", @"\s+", " ").Trim();
            return t.Length > max ? t[..max] + "…" : t;
        }

        /// <summary>
        /// 其他板块的动态。每一类最多取 takeCount 条（分页时 offset 越大取得越多），刷屏型的（打卡、小说更新、编年史）按「同一天」合并成一条。
        /// </summary>
        private async Task<List<CommunityFeedItem>> MoreFeedAsync(int takeCount)
        {
            var items = new List<CommunityFeedItem>();
            var userIds = new HashSet<Guid>();
            var pending = new List<(CommunityFeedItem Item, Guid? UserId)>(); // 最后统一查用户名

            // 柴圈作品（已发布）
            var works = await db.ChaiWorks.AsNoTracking().Where(w => w.Status == "published")
                .OrderByDescending(w => w.CreatedAt).Take(takeCount)
                .Select(w => new { w.Id, w.Title, w.AuthorId, w.AuthorName, w.CreatedAt }).ToListAsync();
            items.AddRange(works.Select(w => new CommunityFeedItem
            {
                Id = "chai-work:" + w.Id, Kind = "chai-work", ActorName = w.AuthorName, ActorId = w.AuthorId.ToString(),
                TargetTitle = w.Title, TargetId = w.Id.ToString(), CreatedAt = w.CreatedAt,
            }));

            // OC（已发布）
            var ocs = await db.StickmanCharacters.AsNoTracking().Where(c => c.Status == "published")
                .OrderByDescending(c => c.CreatedAt).Take(takeCount)
                .Select(c => new { c.Id, c.Title, c.AuthorId, c.AuthorName, c.CreatedAt }).ToListAsync();
            items.AddRange(ocs.Select(c => new CommunityFeedItem
            {
                Id = "oc:" + c.Id, Kind = "oc", ActorName = c.AuthorName, ActorId = c.AuthorId.ToString(),
                TargetTitle = c.Title, TargetId = c.Id.ToString(), CreatedAt = c.CreatedAt,
            }));

            // 柴圈对战（发起人 = 最早加入的参与者）
            var battles = await db.Battles.AsNoTracking().Where(b => b.Status != "cancelled")
                .OrderByDescending(b => b.CreatedAt).Take(takeCount)
                .Select(b => new { b.Id, b.Title, b.CreatedAt }).ToListAsync();
            if (battles.Count > 0)
            {
                var battleIds = battles.Select(b => b.Id).ToList();
                var initiators = (await db.BattleParticipants.AsNoTracking().Where(p => battleIds.Contains(p.BattleId))
                        .Select(p => new { p.BattleId, p.UserId, p.UserName, p.JoinedAt }).ToListAsync())
                    .GroupBy(p => p.BattleId).ToDictionary(g => g.Key, g => g.OrderBy(p => p.JoinedAt).First());
                items.AddRange(battles.Select(b =>
                {
                    initiators.TryGetValue(b.Id, out var who);
                    return new CommunityFeedItem
                    {
                        Id = "battle:" + b.Id, Kind = "battle", ActorName = who?.UserName ?? "柴圈玩家", ActorId = who?.UserId.ToString(),
                        TargetTitle = b.Title, TargetId = b.Id, CreatedAt = b.CreatedAt,
                    };
                }));
            }

            // 世界观小说（公开的）：开始连载；新章节（同一本同一天合成一条）
            var novels = await db.WorldNovels.AsNoTracking().Where(n => n.IsPublic)
                .OrderByDescending(n => n.CreatedAt).Take(takeCount)
                .Select(n => new { n.Id, n.Title, n.AuthorId, n.AuthorName, n.CreatedAt }).ToListAsync();
            items.AddRange(novels.Select(n => new CommunityFeedItem
            {
                Id = "novel:" + n.Id, Kind = "novel-new", ActorName = n.AuthorName, ActorId = n.AuthorId.ToString(),
                TargetTitle = n.Title, TargetId = n.Id.ToString(), CreatedAt = n.CreatedAt,
            }));
            var publicNovelIds = await db.WorldNovels.AsNoTracking().Where(n => n.IsPublic).Select(n => n.Id).ToListAsync();
            var chapters = await db.WorldNovelChapters.AsNoTracking()
                .Where(c => publicNovelIds.Contains(c.NovelId))
                .OrderByDescending(c => c.CreatedAt).Take(takeCount * 5)
                .Select(c => new { c.NovelId, c.Number, c.Title, c.CreatedAt }).ToListAsync();
            if (chapters.Count > 0)
            {
                var novelIds = chapters.Select(c => c.NovelId).Distinct().ToList();
                var novelInfo = await db.WorldNovels.AsNoTracking().Where(n => novelIds.Contains(n.Id))
                    .ToDictionaryAsync(n => n.Id, n => new { n.Title, n.AuthorId, n.AuthorName, n.CreatedAt });
                foreach (var g in chapters.GroupBy(c => new { c.NovelId, Day = (c.CreatedAt + China).Date }))
                {
                    if (!novelInfo.TryGetValue(g.Key.NovelId, out var n)) continue;
                    var list = g.OrderBy(c => c.Number).ToList();
                    items.Add(new CommunityFeedItem
                    {
                        Id = $"novel-chapter:{g.Key.NovelId}:{g.Key.Day:yyyyMMdd}", Kind = "novel-chapter", ActorName = n.AuthorName, ActorId = n.AuthorId.ToString(),
                        TargetTitle = n.Title, TargetId = g.Key.NovelId.ToString(), CreatedAt = g.Max(c => c.CreatedAt),
                        Detail = list.Count == 1 ? $"第 {list[0].Number} 章《{Clip(list[0].Title, 30)}》" : $"第 {list[0].Number}–{list[^1].Number} 章，共 {list.Count} 章",
                    });
                }
            }

            // 编年史（公开世界里没隐藏的事件；同一人同一世界同一天合成一条）
            // 先查出公开的世界再筛（编年史表是启动时建的，和 EF 建的表排序规则可能不同，不做跨表子查询）
            var publicWorldIds = await db.WorldProjects.AsNoTracking().Where(p => p.IsPublic).Select(p => p.Id).ToListAsync();
            var chronicle = await db.WorldTimelineEvents.AsNoTracking()
                .Where(e => !e.Hidden && e.CreatedBy != null && publicWorldIds.Contains(e.ProjectId))
                .OrderByDescending(e => e.CreatedAt).Take(takeCount * 5)
                .Select(e => new { e.ProjectId, UserId = e.CreatedBy!.Value, e.Title, e.CreatedAt }).ToListAsync();
            if (chronicle.Count > 0)
            {
                var worldIds = chronicle.Select(e => e.ProjectId).Distinct().ToList();
                var worldNames = await db.WorldProjects.AsNoTracking().Where(p => worldIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id, p => p.Name);
                // 标题里可能有 {CARD:id} 引用，换成条目名
                var refIds = chronicle.SelectMany(e => System.Text.RegularExpressions.Regex.Matches(e.Title, @"\{CARD:([0-9a-fA-F-]{36})\}").Select(m => Guid.TryParse(m.Groups[1].Value, out var g) ? g : Guid.Empty))
                    .Where(g => g != Guid.Empty).Distinct().ToList();
                var cardNames = refIds.Count == 0 ? new Dictionary<Guid, string>() : await db.WorldCards.AsNoTracking().Where(c => refIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Title);
                string Plain(string title) => System.Text.RegularExpressions.Regex.Replace(title, @"\{CARD:([0-9a-fA-F-]{36})\}",
                    m => Guid.TryParse(m.Groups[1].Value, out var g) && cardNames.TryGetValue(g, out var name) ? name : "");
                foreach (var g in chronicle.GroupBy(e => new { e.ProjectId, e.UserId, Day = (e.CreatedAt + China).Date }))
                {
                    if (!worldNames.TryGetValue(g.Key.ProjectId, out var world)) continue;
                    var titles = g.OrderByDescending(e => e.CreatedAt).Select(e => Clip(Plain(e.Title), 24)).Where(t => t.Length > 0).Distinct().ToList();
                    var item = new CommunityFeedItem
                    {
                        Id = $"chronicle:{g.Key.ProjectId}:{g.Key.UserId}:{g.Key.Day:yyyyMMdd}", Kind = "chronicle", ActorId = g.Key.UserId.ToString(),
                        TargetTitle = world, TargetId = g.Key.ProjectId.ToString(), CreatedAt = g.Max(e => e.CreatedAt),
                        Detail = $"记下了 {g.Count()} 件事" + (titles.Count > 0 ? "：" + string.Join("", titles.Take(3).Select(t => "「" + t + "」")) + (titles.Count > 3 ? " 等" : "") : ""),
                    };
                    items.Add(item); pending.Add((item, g.Key.UserId));
                }
            }

            // 打卡活动：发起了活动；打卡（同一个活动同一天合成一条）；讨论区留言 / 回复
            var activities = await db.Activities.AsNoTracking().OrderByDescending(a => a.CreatedAt).Take(takeCount)
                .Select(a => new { a.Id, a.Title, a.OwnerId, a.CreatedAt }).ToListAsync();
            foreach (var a in activities)
            {
                var item = new CommunityFeedItem { Id = "activity:" + a.Id, Kind = "activity", ActorId = a.OwnerId.ToString(), TargetTitle = a.Title, TargetId = a.Id.ToString(), CreatedAt = a.CreatedAt };
                items.Add(item); pending.Add((item, a.OwnerId));
            }
            var checkins = await db.Records.AsNoTracking().Where(r => r.IsCompleted)
                .OrderByDescending(r => r.CreatedAt).Take(takeCount * 10)
                .Select(r => new { r.Id, r.CreatedAt, r.Member.ActivityId, r.Member.UserId }).ToListAsync();
            var activityPosts = await db.Posts.AsNoTracking().OrderByDescending(p => p.CreatedAt).Take(takeCount)
                .Select(p => new { p.Id, p.ActivityId, p.AuthorId, p.CreatedAt }).ToListAsync();
            var activityReplies = await db.Replies.AsNoTracking().OrderByDescending(r => r.CreatedAt).Take(takeCount)
                .Select(r => new { r.Id, r.Post.ActivityId, r.AuthorId, r.CreatedAt }).ToListAsync();
            var activityIds = checkins.Select(c => c.ActivityId).Concat(activityPosts.Select(p => p.ActivityId)).Concat(activityReplies.Select(r => r.ActivityId)).Distinct().ToList();
            var activityTitles = activityIds.Count == 0 ? new Dictionary<int, string>() : await db.Activities.AsNoTracking().Where(a => activityIds.Contains(a.Id)).ToDictionaryAsync(a => a.Id, a => a.Title);
            foreach (var g in checkins.GroupBy(c => new { c.ActivityId, Day = (c.CreatedAt + China).Date }))
            {
                if (!activityTitles.TryGetValue(g.Key.ActivityId, out var title)) continue;
                var latest = g.OrderByDescending(c => c.CreatedAt).First();
                var people = g.Select(c => c.UserId).Distinct().Count();
                var item = new CommunityFeedItem
                {
                    Id = $"checkin:{g.Key.ActivityId}:{g.Key.Day:yyyyMMdd}", Kind = "checkin", ActorId = latest.UserId.ToString(),
                    TargetTitle = title, TargetId = g.Key.ActivityId.ToString(), CreatedAt = latest.CreatedAt,
                    Detail = people > 1 ? $"（这一天共 {people} 人打卡）" : null,
                };
                items.Add(item); pending.Add((item, latest.UserId));
            }
            foreach (var p in activityPosts.Select(p => new { Key = "post:" + p.Id, p.ActivityId, p.AuthorId, p.CreatedAt })
                         .Concat(activityReplies.Select(r => new { Key = "reply:" + r.Id, r.ActivityId, r.AuthorId, r.CreatedAt })))
            {
                if (!activityTitles.TryGetValue(p.ActivityId, out var title)) continue;
                var item = new CommunityFeedItem { Id = "activity-" + p.Key, Kind = "comment-activity", ActorId = p.AuthorId.ToString(), TargetTitle = title, TargetId = p.ActivityId.ToString(), CreatedAt = p.CreatedAt };
                items.Add(item); pending.Add((item, p.AuthorId));
            }

            // 世界任务：玩家提交、已通过的贡献
            var contributions = await db.ServerQuestContributions.AsNoTracking().Where(c => c.Source == "player" && c.Status == "approved")
                .OrderByDescending(c => c.ReviewedAt ?? c.CreatedAt).Take(takeCount)
                .Select(c => new { c.Id, c.QuestId, c.UserId, c.PlayerName, c.Amount, c.CreatedAt, c.ReviewedAt }).ToListAsync();
            if (contributions.Count > 0)
            {
                var questIds = contributions.Select(c => c.QuestId).Distinct().ToList();
                var quests = await db.ServerQuests.AsNoTracking().Where(q => questIds.Contains(q.Id)).ToDictionaryAsync(q => q.Id, q => new { q.Title, q.Kind, q.Unit });
                foreach (var c in contributions)
                {
                    if (!quests.TryGetValue(c.QuestId, out var q)) continue;
                    var item = new CommunityFeedItem
                    {
                        Id = "quest-report:" + c.Id, Kind = "quest-report", ActorName = c.PlayerName ?? "", ActorId = c.UserId?.ToString(),
                        TargetTitle = q.Title, TargetId = c.QuestId.ToString(), CreatedAt = c.ReviewedAt ?? c.CreatedAt,
                        Detail = q.Kind == "goal" ? "提交了完成记录" : $"提交了贡献 +{c.Amount:N0} {q.Unit}",
                    };
                    items.Add(item); pending.Add((item, c.UserId));
                }
            }

            // 官方：公告（发布中、没过期）、版本更新、交易行上新
            var now = DateTime.UtcNow;
            items.AddRange(await db.News.AsNoTracking()
                .Where(n => n.IsPublished && (n.NoticeKind != "scheduled" || n.ExpireAt == null || n.ExpireAt > now))
                .OrderByDescending(n => n.CreatedAt).Take(takeCount)
                .Select(n => new CommunityFeedItem { Id = "news:" + n.Id, Kind = "news", ActorName = "太初官方", TargetTitle = n.Title, TargetId = n.Id, CreatedAt = n.CreatedAt })
                .ToListAsync());
            items.AddRange(await db.Releases.AsNoTracking().Where(r => r.IsPublished)
                .OrderByDescending(r => r.ReleasedAt).Take(takeCount)
                .Select(r => new CommunityFeedItem { Id = "release:" + r.Id, Kind = "release", ActorName = "太初官方", TargetTitle = "v" + r.Version + " " + r.Title, TargetId = r.Id.ToString(), CreatedAt = r.ReleasedAt })
                .ToListAsync());
            var storeItems = await db.StoreItems.AsNoTracking().Where(i => i.IsActive)
                .OrderByDescending(i => i.CreatedAt).Take(takeCount)
                .Select(i => new { i.Id, i.Name, i.Benefit, i.CreatedAt }).ToListAsync();
            items.AddRange(storeItems.Select(i => new CommunityFeedItem
            {
                Id = "trade-item:" + i.Id, Kind = "trade-item", ActorName = "太初官方", TargetTitle = i.Name, TargetId = i.Id.ToString(),
                CreatedAt = i.CreatedAt, Detail = string.IsNullOrWhiteSpace(i.Benefit) ? null : "（" + Clip(i.Benefit, 30) + "）",
            }));

            // 必吃榜：收录的新条目
            var mustEat = await db.MustEatItems.AsNoTracking().Where(m => m.IsVisible && m.Status == "approved")
                .OrderByDescending(m => m.CreatedAt).Take(takeCount)
                .Select(m => new { m.Id, m.Title, m.Category, m.SubmittedByUserId, m.CreatedAt }).ToListAsync();
            foreach (var m in mustEat)
            {
                var item = new CommunityFeedItem { Id = "must-eat:" + m.Id, Kind = "must-eat", ActorName = "太初官方", TargetTitle = m.Title, TargetId = m.Id.ToString(), CreatedAt = m.CreatedAt, Detail = "（" + m.Category + "）" };
                items.Add(item);
                if (m.SubmittedByUserId.HasValue) pending.Add((item, m.SubmittedByUserId));
            }

            // 赞助（赞助墙上公开显示的；匿名的不写名字，不写金额）
            var sponsors = await db.Sponsors.AsNoTracking().Where(s => s.IsVisible)
                .OrderByDescending(s => s.SponsoredAt).Take(takeCount)
                .Select(s => new { s.Id, s.Name, s.IsAnonymous, s.Tier, s.SponsoredAt }).ToListAsync();
            items.AddRange(sponsors.Select(s => new CommunityFeedItem
            {
                Id = "sponsor:" + s.Id, Kind = "sponsor", ActorName = s.IsAnonymous || string.IsNullOrWhiteSpace(s.Name) ? "一位匿名支持者" : s.Name,
                TargetTitle = s.Tier, TargetId = s.Id.ToString(), CreatedAt = s.SponsoredAt,
            }));

            // 知识库资源：审核通过、公开的用户上传资源（表是启动时建的，用资源服务自己的连接查；没注册或出错就跳过，不影响别的动态）
            var resourceStore = HttpContext.RequestServices.GetService<TaiChuWeb_V2.Services.Resources.CommunityResourceStore>();
            if (resourceStore != null)
            {
                try
                {
                    foreach (var r in await resourceStore.RecentPublishedAsync(takeCount, HttpContext.RequestAborted))
                    {
                        var item = new CommunityFeedItem { Id = "resource:" + r.Id, Kind = "resource", TargetTitle = r.Title, TargetId = r.Id.ToString(), CreatedAt = r.PublishedAt, Detail = "（" + r.Category + "）" };
                        items.Add(item); pending.Add((item, r.UploaderId));
                    }
                }
                catch (Exception) { /* 资源表还没建好等情况：这一类动态先不显示 */ }
            }

            // 统一补上用户名
            foreach (var (_, uid) in pending) if (uid.HasValue) userIds.Add(uid.Value);
            var names = userIds.Count == 0 ? new Dictionary<Guid, string>() : await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
            foreach (var (item, uid) in pending)
            {
                if (uid.HasValue && names.TryGetValue(uid.Value, out var name)) { item.ActorName = name; item.ActorId = uid.Value.ToString(); }
                else if (string.IsNullOrEmpty(item.ActorName)) item.ActorName = "社区用户";
            }
            return items;
        }

        private async Task<List<CommunityFeedItem>> WorldFeedAsync(int takeCount)
        {
            var items = new List<CommunityFeedItem>();
            var publicWorlds = await db.WorldProjects.AsNoTracking().Where(p => p.IsPublic)
                .Select(p => new { p.Id, p.Name, p.OwnerId, p.CreatedAt, OwnerName = p.Owner != null ? p.Owner.Username : null })
                .ToListAsync();
            if (publicWorlds.Count == 0) return items;

            // 新公开的世界
            items.AddRange(publicWorlds.OrderByDescending(p => p.CreatedAt).Take(takeCount).Select(p => new CommunityFeedItem
            {
                Id = "world:" + p.Id,
                Kind = "world-new",
                ActorName = p.OwnerName ?? "太初隐者",
                ActorId = p.OwnerId.ToString(),
                TargetTitle = p.Name,
                TargetId = p.Id.ToString(),
                CreatedAt = p.CreatedAt
            }));

            // 条目的新建 / 修改：取最近的修改记录（不跨表连接，避免排序规则冲突）
            var worldIds = publicWorlds.Select(p => p.Id).ToList();
            var revisions = await db.WorldCardRevisions.AsNoTracking()
                .Where(r => worldIds.Contains(r.ProjectId) && r.UserId != null && (r.Action == "create" || r.Action == "update"))
                .OrderByDescending(r => r.Id).Take(Math.Max(200, takeCount * 20))
                .Select(r => new { r.ProjectId, r.CardId, UserId = r.UserId!.Value, r.Action, r.Title, r.CreatedAt })
                .ToListAsync();
            if (revisions.Count == 0) return items;
            var cardIds = revisions.Select(r => r.CardId).Distinct().ToList();
            var alive = (await db.WorldCards.AsNoTracking().Where(c => cardIds.Contains(c.Id)).Select(c => new { c.Id, c.Title }).ToListAsync())
                .ToDictionary(c => c.Id, c => c.Title);
            var userIds = revisions.Select(r => r.UserId).Distinct().ToList();
            var names = await db.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
            var worldNames = publicWorlds.ToDictionary(p => p.Id, p => p.Name);
            var china = TimeSpan.FromHours(8);

            foreach (var g in revisions.Where(r => alive.ContainsKey(r.CardId))
                         .GroupBy(r => new { r.ProjectId, r.UserId, Day = (r.CreatedAt + china).Date }))
            {
                var created = g.Where(r => r.Action == "create").Select(r => r.CardId).Distinct().ToList();
                var updated = g.Where(r => r.Action == "update").Select(r => r.CardId).Distinct().Except(created).ToList();
                var touched = g.OrderByDescending(r => r.CreatedAt).Select(r => r.CardId).Distinct().ToList();
                var parts = new List<string>();
                if (created.Count > 0) parts.Add($"新建了 {created.Count} 个");
                if (updated.Count > 0) parts.Add($"更新了 {updated.Count} 个");
                var titles = touched.Take(3).Select(id => "《" + alive[id] + "》");
                items.Add(new CommunityFeedItem
                {
                    Id = $"world-edit:{g.Key.ProjectId}:{g.Key.UserId}:{g.Key.Day:yyyyMMdd}",
                    Kind = "world-edit",
                    ActorName = names.TryGetValue(g.Key.UserId, out var name) ? name : "太初隐者",
                    ActorId = g.Key.UserId.ToString(),
                    TargetTitle = worldNames[g.Key.ProjectId],
                    // 只动了一个条目时直接打开那个条目
                    TargetId = touched.Count == 1 ? $"{g.Key.ProjectId}/card/{touched[0]}" : g.Key.ProjectId.ToString(),
                    CreatedAt = g.Max(r => r.CreatedAt),
                    Detail = $"{string.Join("、", parts)}条目：{string.Join("", titles)}{(touched.Count > 3 ? " 等" : "")}"
                });
            }
            return items;
        }
    }
}
