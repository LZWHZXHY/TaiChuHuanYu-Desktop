using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers
{
    // 首页“社区动态”信息流：聚合博客、帖子、画廊作品、世界观设定（百科）的最近公开更新。
    public sealed class CommunityFeedItem
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = ""; // blog | post | artwork | wiki-new | wiki-update | user-joined | signin | comment-blog | comment-post | comment-artwork | comment-work | comment-character | comment-history | joint | survey | dev-project | world-new | world-edit | quest-start | quest-complete | history
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
            var takeCount = offset + limit + 1; // 多取一条用于判断是否还有下一页

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
                    .Join(db.Users, s => s.UserId, u => u.Id, (s, u) => new { s.UserId, s.SignDate, u.Username })
                    .ToListAsync())
                .Select(x => new CommunityFeedItem
                {
                    Id = "signin:" + x.UserId + ":" + x.SignDate.ToString("yyyyMMdd"),
                    Kind = "signin",
                    ActorName = x.Username,
                    ActorId = x.UserId.ToString(),
                    TargetTitle = "",
                    TargetId = x.UserId.ToString(),
                    CreatedAt = x.SignDate
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

            var feed = blogPosts.Concat(artworks).Concat(wikiItems).Concat(newUsers).Concat(signins).Concat(commentItems).Concat(joints).Concat(surveys).Concat(devProjects).Concat(worldItems).Concat(questItems).Concat(historyItems).Concat(historyCommentItems)
                .OrderByDescending(i => i.CreatedAt)
                .ToList();

            var hasMore = feed.Count > offset + limit;
            var page = feed.Skip(offset).Take(limit).ToList();

            return Ok(new { items = page, hasMore });
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
