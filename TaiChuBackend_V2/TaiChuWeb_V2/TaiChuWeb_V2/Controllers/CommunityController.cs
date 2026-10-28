using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;

namespace TaiChuWeb_V2.Controllers
{
    // 首页“社区动态”信息流：聚合博客、帖子、画廊作品、世界观设定（百科）的最近公开更新。
    public sealed class CommunityFeedItem
    {
        public string Id { get; set; } = "";
        public string Kind { get; set; } = ""; // blog | post | artwork | wiki-new | wiki-update | user-joined | signin | comment-blog | comment-post | comment-artwork | joint | survey | dev-project
        public string ActorName { get; set; } = "";
        public string TargetTitle { get; set; } = "";
        public string TargetId { get; set; } = "";
        public DateTime CreatedAt { get; set; }
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
            var blogPosts = await db.PublishedNotes
                .Where(n => n.Type == "blog" || n.Type == "post")
                .Where(n => n.OriginalNoteId == null
                    || !db.Notes.Any(s => s.Id.ToString() == EF.Functions.Collate(n.OriginalNoteId.ToString(), "utf8mb4_general_ci"))
                    || db.Notes.Any(s => s.Id.ToString() == EF.Functions.Collate(n.OriginalNoteId.ToString(), "utf8mb4_general_ci") && s.IsPublic && s.Status != 2))
                .OrderByDescending(n => n.PublishedAt)
                .Take(takeCount)
                .Select(n => new CommunityFeedItem
                {
                    Id = (n.Type == "post" ? "post:" : "blog:") + n.Id,
                    Kind = n.Type == "post" ? "post" : "blog",
                    ActorName = n.AuthorName ?? "太初隐者",
                    TargetTitle = n.Title ?? (n.Type == "post" ? "短帖" : "无标题文章"),
                    TargetId = n.Id.ToString(),
                    CreatedAt = n.PublishedAt
                })
                .ToListAsync();

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

            var commenterIds = noteCommentRows.Concat(artworkCommentRows).Select(r => r.UserId).Distinct().ToList();
            var commenterMap = await db.Users
                .Where(u => commenterIds.Contains(u.Id.ToString()))
                .Select(u => new { u.Id, u.Username })
                .ToDictionaryAsync(u => u.Id.ToString(), u => u.Username);

            var commentItems = noteCommentRows.Concat(artworkCommentRows).Select(r => new CommunityFeedItem
            {
                Id = "comment:" + r.CommentId,
                Kind = r.Kind,
                ActorName = commenterMap.TryGetValue(r.UserId, out var name) ? name : "社区用户",
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

            var feed = blogPosts.Concat(artworks).Concat(wikiItems).Concat(newUsers).Concat(signins).Concat(commentItems).Concat(joints).Concat(surveys).Concat(devProjects)
                .OrderByDescending(i => i.CreatedAt)
                .ToList();

            var hasMore = feed.Count > offset + limit;
            var page = feed.Skip(offset).Take(limit).ToList();

            return Ok(new { items = page, hasMore });
        }
    }
}
