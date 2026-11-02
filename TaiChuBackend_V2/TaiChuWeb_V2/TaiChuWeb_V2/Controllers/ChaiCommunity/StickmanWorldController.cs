using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.Controllers.World;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.ChaiCommunity;
using TaiChuWeb_V2.Models.World;
using TaiChuWeb_V2.Services.ChaiCommunity;
using TaiChuWeb_V2.Services.Notify;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.ChaiCommunity;

/// <summary>
/// 柴圈 OC ↔ 世界观 联动（单独一个控制器，OC 原来的接口、世界观原来的接口都不改）：
///   1. OC 作者把 OC「关联」到自己是创建者 / 协作者的世界观，世界观首页显示「柴圈人设」；作者可以取消关联，
///      世界观创建者和编写者可以把 OC「移出」（都只删关联记录，不动 OC）。
///   2. 「复制到我的世界观」：OC → 一张新的「人物」条目（走条目服务的正常新建流程：配额、修改记录、缓存都照常）。
///   3. 「发布成柴圈 OC」：世界观「人物」条目 → 一个 OC 草稿（和 StickManController.Create 一样，没有数量限制和扣费）。
///   4. 「从另一边更新」：先预览（会新增 / 修改 / 保留哪些），确认后再执行；执行前备份：
///      条目走世界观「修改记录」（条目服务保存时自动记，可以撤销），OC 存进 stickman_backups（可以查看、恢复）。
///   5. 断开对应关系：只删对应记录，两边内容都不动。
/// 转换规则全部在 Services/ChaiCommunity/OcWorldConverter.cs；新表见 StickmanWorldStore / StickmanWorldSchema。
/// </summary>
[Route("api/stickman-world")]
[ApiController]
[Authorize]
[WorldExceptionFilter] // 条目服务抛出的「配额满了」「条目被别人改过」等，原样变成 400 / 409 给前端
public class StickmanWorldController(AppDbContext context, IWorldCardService cardService, WorldHistoryService history,
    ILogger<StickmanWorldController> logger) : ControllerBase
{
    public sealed class LinkRequest { public Guid ProjectId { get; set; } }
    public sealed class CopyRequest
    {
        public Guid ProjectId { get; set; }
        /// <summary>网站地址（前端传 location.origin），条目「来源」里链接回 OC 用；不传就只写名字</summary>
        public string? LinkBase { get; set; }
    }
    public sealed class SyncRequest
    {
        public string Direction { get; set; } = "";
        /// <summary>预览时拿到的指纹：确认时内容和预览时不一样，就不执行，让用户重新预览</summary>
        public string Fingerprint { get; set; } = "";
    }

    private const string Published = "published", Draft = "draft";

    // ======================================================================
    //  OC 这边
    // ======================================================================

    /// <summary>
    /// OC 关联的世界观 + 对应的条目。所有人能看自己看得到的关联世界观（OC 详情页「收录于世界观」）；
    /// 作者还能看到对应的条目和每个世界观能不能编辑。草稿 OC 只有作者能看。
    /// </summary>
    [HttpGet("oc/{ocId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> OcInfo(Guid ocId)
    {
        var me = Me;
        var oc = await context.StickmanCharacters.AsNoTracking().Where(c => c.Id == ocId)
            .Select(c => new { c.Id, c.AuthorId, c.Status }).FirstOrDefaultAsync();
        if (oc == null || (oc.Status == Draft && oc.AuthorId != me)) return NotFound(new { message = "OC 角色不存在" });
        var isAuthor = me != Guid.Empty && oc.AuthorId == me;

        var links = await StickmanWorldStore.LinksOfCharacterAsync(context, ocId);
        var pairs = isAuthor ? await StickmanWorldStore.CardLinksOfCharacterAsync(context, ocId) : new List<StickmanWorldStore.CardLink>();
        var projectIds = links.Select(l => l.ProjectId).Concat(pairs.Select(p => p.ProjectId)).Distinct().ToList();
        var projects = (await context.WorldProjects.AsNoTracking().Where(p => projectIds.Contains(p.Id))
            .Select(p => new { p.Id, p.Name, p.CoverImage, p.IsPublic }).ToListAsync()).ToDictionary(p => p.Id);
        var readable = (await context.WorldProjects.AsNoTracking().ReadableBy(me).Where(p => projectIds.Contains(p.Id))
            .Select(p => p.Id).ToListAsync()).ToHashSet();
        var editable = me == Guid.Empty ? new HashSet<Guid>() : (await context.WorldProjects.AsNoTracking().EditableBy(me)
            .Where(p => projectIds.Contains(p.Id)).Select(p => p.Id).ToListAsync()).ToHashSet();
        var cardIds = pairs.Select(p => p.CardId).ToList();
        var cards = (await context.WorldCards.AsNoTracking().Where(c => cardIds.Contains(c.Id))
            .Select(c => new { c.Id, c.Title }).ToListAsync()).ToDictionary(c => c.Id);

        // 看不到的世界观（比如作者后来被移出了私有世界）只告诉作者「有这么一条关联」，不透露名字
        string? NameOf(Guid id) => readable.Contains(id) && projects.TryGetValue(id, out var p) ? p.Name : null;
        return Ok(new
        {
            isAuthor,
            worlds = links.Where(l => isAuthor || (readable.Contains(l.ProjectId) && projects.ContainsKey(l.ProjectId))).Select(l => new
            {
                projectId = l.ProjectId,
                name = NameOf(l.ProjectId),
                coverImage = readable.Contains(l.ProjectId) && projects.TryGetValue(l.ProjectId, out var p) ? p.CoverImage : null,
                exists = projects.ContainsKey(l.ProjectId),
                canRead = readable.Contains(l.ProjectId),
                canEdit = editable.Contains(l.ProjectId),
                linkedAt = l.CreatedAt,
            }),
            cards = pairs.Select(x => new
            {
                cardId = x.CardId,
                projectId = x.ProjectId,
                cardTitle = cards.TryGetValue(x.CardId, out var c) && readable.Contains(x.ProjectId) ? c.Title : null,
                cardExists = cards.ContainsKey(x.CardId),
                projectName = NameOf(x.ProjectId),
                canSync = cards.ContainsKey(x.CardId) && editable.Contains(x.ProjectId),
                createdAt = x.CreatedAt,
                lastSyncAt = x.LastSyncAt,
                lastSyncDirection = x.LastSyncDirection,
            }),
        });
    }

    /// <summary>OC 作者把 OC 关联到一个世界观（自己是创建者或协作者的）</summary>
    [HttpPost("oc/{ocId:guid}/links")]
    public async Task<IActionResult> Link(Guid ocId, [FromBody] LinkRequest body)
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized(new { message = "请先登录" });
        var oc = await context.StickmanCharacters.AsNoTracking().Where(c => c.Id == ocId).Select(c => new { c.AuthorId, c.Title }).FirstOrDefaultAsync();
        if (oc == null) return NotFound(new { message = "OC 角色不存在" });
        if (oc.AuthorId != me) return StatusCode(403, new { message = "只有 OC 的作者能把它关联到世界观" });
        var project = await context.WorldProjects.AsNoTracking().Where(p => p.Id == body.ProjectId).Select(p => new { p.Name, p.OwnerId }).FirstOrDefaultAsync();
        if (project == null) return NotFound(new { message = "世界观不存在" });
        if (await WorldAccess.GetRoleAsync(context, body.ProjectId, me) == null)
            return StatusCode(403, new { message = "只能关联到你是创建者或协作者的世界观" });

        var added = await StickmanWorldStore.AddLinkAsync(context, ocId, body.ProjectId, me);
        // 协作者把自己的 OC 放进来：告诉世界观创建者一声
        if (added && project.OwnerId != me)
            await NotifyAsync(project.OwnerId, me, "world", body.ProjectId.ToString(),
                $"把柴圈 OC《{Clip(oc.Title, 40)}》关联到了世界观《{Clip(project.Name, 40)}》", $"/world/project/{body.ProjectId}");
        return Ok(new { ok = true, added, message = added ? "已关联" : "之前已经关联过了" });
    }

    /// <summary>
    /// 取消关联 / 移出：OC 作者可以取消自己 OC 的关联；世界观创建者和编写者可以把 OC 移出自己的世界观。
    /// 只删关联记录，OC 本身、对应的条目都不动。
    /// </summary>
    [HttpDelete("oc/{ocId:guid}/links/{projectId:guid}")]
    public async Task<IActionResult> Unlink(Guid ocId, Guid projectId)
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized(new { message = "请先登录" });
        var oc = await context.StickmanCharacters.AsNoTracking().Where(c => c.Id == ocId).Select(c => new { c.AuthorId, c.Title }).FirstOrDefaultAsync();
        var isAuthor = oc != null && oc.AuthorId == me;
        if (!isAuthor && !await WorldAccess.CanEditProjectAsync(context, projectId, me))
            return StatusCode(403, new { message = "只有 OC 的作者，或者世界观的创建者 / 编写者能移出" });

        var removed = await StickmanWorldStore.RemoveLinkAsync(context, ocId, projectId);
        if (removed && !isAuthor && oc != null)
        {
            var name = await context.WorldProjects.AsNoTracking().Where(p => p.Id == projectId).Select(p => p.Name).FirstOrDefaultAsync() ?? "世界观";
            await NotifyAsync(oc.AuthorId, me, "stickman", ocId.ToString(),
                $"把你的柴圈 OC《{Clip(oc.Title, 40)}》移出了世界观《{Clip(name, 40)}》（只是取消关联，OC 本身没有任何变化）", $"/ocs/{ocId}");
        }
        return Ok(new { ok = true, removed });
    }

    /// <summary>「复制到我的世界观」：OC → 新的「人物」条目。原 OC 不动。只有 OC 作者、且对目标世界观有编辑权限才行。</summary>
    [HttpPost("oc/{ocId:guid}/copy")]
    public async Task<IActionResult> Copy(Guid ocId, [FromBody] CopyRequest body)
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized(new { message = "请先登录" });
        var oc = await context.StickmanCharacters.AsNoTracking().Include(c => c.Attributes).Include(c => c.Images)
            .FirstOrDefaultAsync(c => c.Id == ocId);
        if (oc == null) return NotFound(new { message = "OC 角色不存在" });
        if (oc.AuthorId != me) return StatusCode(403, new { message = "只有 OC 的作者能把它复制到世界观" });
        if (!await WorldAccess.CanEditProjectAsync(context, body.ProjectId, me))
            return StatusCode(403, new { message = "你在这个世界观里没有编辑权限（要是创建者或编写者）" });

        // 同一个世界观里已经有它对应的条目了：不再复制一份（用「从柴圈 OC 更新」）；对应的条目已经被删了就可以重新复制
        var linkedCards = (await StickmanWorldStore.CardLinksOfCharacterAsync(context, ocId)).Where(l => l.ProjectId == body.ProjectId).Select(l => l.CardId).ToList();
        var alive = await context.WorldCards.AsNoTracking().Where(c => linkedCards.Contains(c.Id)).Select(c => (Guid?)c.Id).FirstOrDefaultAsync();
        if (alive != null)
            return Conflict(new { message = "这个世界观里已经有它对应的条目了，要更新内容请用「从柴圈 OC 更新」", cardId = alive });

        var side = OcWorldConverter.ReadOc(oc, oc.Attributes ?? new List<StickmanAttribute>(), oc.Images ?? new List<StickmanImage>(),
            await SafePanelAsync(ocId, StickmanWorldStore.ShowPanel));
        // 走条目服务的正常新建：检查配额和编辑权限、记修改记录（新建）、清缓存
        var card = await cardService.CreateCardAsync(body.ProjectId, me, OcWorldConverter.NewCardFromOc(side, ocId, body.LinkBase));

        string? warning = null;
        try
        {
            await StickmanWorldStore.AddCardLinkAsync(context, ocId, card.Id, body.ProjectId, me, OcWorldConverter.OcToCard);
            // 复制进来的 OC 顺便关联到这个世界观（世界观首页「柴圈人设」能看到；不想要可以取消关联）
            await StickmanWorldStore.AddLinkAsync(context, ocId, body.ProjectId, me);
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "OC 复制成条目后记对应关系失败 oc={Oc} card={Card}", ocId, card.Id);
            warning = "条目已经建好了，但对应关系没记上，以后没法直接「从另一边更新」";
        }
        return Ok(new { cardId = card.Id, projectId = body.ProjectId, message = "已复制到世界观", warning });
    }

    // ======================================================================
    //  OC 备份：查看 / 恢复（只有作者）
    // ======================================================================

    [HttpGet("oc/{ocId:guid}/backups")]
    public async Task<IActionResult> Backups(Guid ocId)
    {
        var denied = await RequireAuthorAsync(ocId);
        if (denied != null) return denied;
        var list = await StickmanWorldStore.BackupsAsync(context, ocId);
        var userIds = list.Where(b => b.CreatedBy.HasValue).Select(b => b.CreatedBy!.Value).Distinct().ToList();
        var names = await context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
        return Ok(new
        {
            items = list.Select(b =>
            {
                var snap = StickmanWorldStore.ParseSnapshot(b.Json);
                return new
                {
                    id = b.Id,
                    reason = b.Reason,
                    createdAt = b.CreatedAt,
                    createdBy = b.CreatedBy.HasValue && names.TryGetValue(b.CreatedBy.Value, out var n) ? n : null,
                    title = snap?.Title,
                    attributeCount = snap?.Attributes.Count ?? 0,
                    imageCount = snap?.Images.Count ?? 0,
                    hasShowPanel = snap?.ShowPanel != null,
                    readable = snap != null,
                };
            }),
        });
    }

    /// <summary>一份备份的完整内容（给作者看：名字、简介、封面、全部属性、图库、展示面板）</summary>
    [HttpGet("oc/{ocId:guid}/backups/{backupId:long}")]
    public async Task<IActionResult> BackupDetail(Guid ocId, long backupId)
    {
        var denied = await RequireAuthorAsync(ocId);
        if (denied != null) return denied;
        var backup = await StickmanWorldStore.BackupAsync(context, ocId, backupId);
        if (backup == null) return NotFound(new { message = "备份不存在" });
        var snap = StickmanWorldStore.ParseSnapshot(backup.Json);
        if (snap == null) return BadRequest(new { message = "这份备份读不出来" });
        return Ok(new
        {
            id = backup.Id,
            reason = backup.Reason,
            createdAt = backup.CreatedAt,
            title = snap.Title,
            description = snap.Description,
            coverUrl = snap.CoverUrl,
            status = snap.Status,
            attributes = snap.Attributes.OrderBy(a => a.SortOrder).Select(a => new { key = a.Key, value = a.Value, type = a.Type }),
            images = snap.Images.OrderBy(i => i.SortOrder).Select(i => i.Url),
            showPanel = OcWorldConverter.ParsePanel(snap.ShowPanel),
            hasBattlePanel = snap.BattlePanel != null,
        });
    }

    /// <summary>
    /// 从备份恢复：先把 OC 现在的样子再备份一份（恢复错了还能恢复回来），
    /// 然后名字、简介、封面、全部属性、图库、展示面板、约战面板都换成备份里的。发布状态不变。
    /// </summary>
    [HttpPost("oc/{ocId:guid}/backups/{backupId:long}/restore")]
    public async Task<IActionResult> Restore(Guid ocId, long backupId)
    {
        var denied = await RequireAuthorAsync(ocId);
        if (denied != null) return denied;
        var me = Me;
        var backup = await StickmanWorldStore.BackupAsync(context, ocId, backupId);
        if (backup == null) return NotFound(new { message = "备份不存在" });
        var snap = StickmanWorldStore.ParseSnapshot(backup.Json);
        if (snap == null) return BadRequest(new { message = "这份备份读不出来，没法恢复" });

        // 备份当前状态 + 恢复放在一个事务里：要么都成功，要么都不算（数据库连接重试时整段重来）
        var strategy = context.Database.CreateExecutionStrategy();
        var found = false;
        await strategy.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            await using var tx = await context.Database.BeginTransactionAsync();
            var oc = await context.StickmanCharacters.Include(c => c.Attributes).Include(c => c.Images).FirstOrDefaultAsync(c => c.Id == ocId);
            found = oc != null;
            if (oc == null) return;
            var attrs = oc.Attributes?.ToList() ?? new List<StickmanAttribute>();
            var images = oc.Images?.ToList() ?? new List<StickmanImage>();
            var show = await StickmanWorldStore.PanelAsync(context, ocId, StickmanWorldStore.ShowPanel);
            var battle = await StickmanWorldStore.PanelAsync(context, ocId, StickmanWorldStore.BattlePanel);
            await StickmanWorldStore.AddBackupAsync(context, ocId, StickmanWorldStore.Snap(oc, attrs, images, show, battle),
                $"恢复备份 #{backupId} 之前的自动备份", me);

            var now = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(snap.Title)) oc.Title = snap.Title.Length > 200 ? snap.Title[..200] : snap.Title;
            oc.Description = snap.Description;
            oc.CoverUrl = snap.CoverUrl is { Length: <= 500 } ? snap.CoverUrl : null;
            oc.UpdatedAt = now;
            context.StickmanAttributes.RemoveRange(attrs);
            context.StickmanImages.RemoveRange(images);
            context.StickmanAttributes.AddRange(snap.Attributes.Where(a => !string.IsNullOrWhiteSpace(a.Key)).Select(a => new StickmanAttribute
            {
                Id = Guid.NewGuid(), CharacterId = ocId, Key = a.Key.Length > 100 ? a.Key[..100] : a.Key, Value = a.Value,
                Type = a.Type == "long" ? "long" : "short", SortOrder = a.SortOrder, CreatedAt = now,
            }));
            context.StickmanImages.AddRange(snap.Images.Where(i => !string.IsNullOrWhiteSpace(i.Url) && i.Url.Length <= 500).Select(i => new StickmanImage
            {
                Id = Guid.NewGuid(), CharacterId = ocId, Url = i.Url, Alt = i.Alt, SortOrder = i.SortOrder, CreatedAt = now,
            }));
            await context.SaveChangesAsync();
            if (snap.ShowPanel != null) await StickmanWorldStore.WritePanelAsync(context, ocId, StickmanWorldStore.ShowPanel, snap.ShowPanel);
            else await StickmanWorldStore.DeletePanelAsync(context, ocId, StickmanWorldStore.ShowPanel);
            if (snap.BattlePanel != null) await StickmanWorldStore.WritePanelAsync(context, ocId, StickmanWorldStore.BattlePanel, snap.BattlePanel);
            else await StickmanWorldStore.DeletePanelAsync(context, ocId, StickmanWorldStore.BattlePanel);
            await tx.CommitAsync();
        });
        if (!found) return NotFound(new { message = "OC 角色不存在" });
        return Ok(new { message = "已恢复。恢复前的样子也自动备份了一份，恢复错了可以再恢复回去" });
    }

    // ======================================================================
    //  世界观这边
    // ======================================================================

    /// <summary>世界观首页「柴圈人设」：关联到这个世界观的、已发布的 OC。看得到世界观的人才看得到。</summary>
    [HttpGet("world/{projectId:guid}/ocs")]
    [AllowAnonymous]
    public async Task<IActionResult> WorldOcs(Guid projectId)
    {
        var me = Me;
        await WorldAccess.RequireReadableProjectAsync(context, projectId, me);
        var links = await StickmanWorldStore.LinksOfProjectAsync(context, projectId);
        var ids = links.Select(l => l.CharacterId).ToList();
        var ocs = (await context.StickmanCharacters.AsNoTracking().Where(c => ids.Contains(c.Id) && c.Status == Published)
            .Select(c => new { c.Id, c.Title, c.Description, c.CoverUrl, c.AuthorId, c.AuthorName }).ToListAsync()).ToDictionary(c => c.Id);
        var canManage = me != Guid.Empty && await WorldAccess.CanEditProjectAsync(context, projectId, me);
        return Ok(new
        {
            canManage,
            items = links.Where(l => ocs.ContainsKey(l.CharacterId)).Select(l =>
            {
                var c = ocs[l.CharacterId];
                return new
                {
                    id = c.Id, title = c.Title, description = Clip(c.Description ?? "", 120), coverUrl = c.CoverUrl,
                    authorId = c.AuthorId, authorName = c.AuthorName, linkedAt = l.CreatedAt, mine = c.AuthorId == me,
                };
            }),
        });
    }

    /// <summary>条目对应的 OC（条目页「来自柴圈 OC《xxx》」、编辑器里的同步按钮用）。看得到条目才能看；草稿 OC 只有作者看得到。</summary>
    [HttpGet("card/{cardId:guid}")]
    [AllowAnonymous]
    public async Task<IActionResult> CardInfo(Guid cardId)
    {
        var me = Me;
        if (!await WorldAccess.CanReadCardAsync(context, cardId, me)) return NotFound(new { message = "条目不存在或无权访问" });
        var card = await context.WorldCards.AsNoTracking().Where(c => c.Id == cardId).Select(c => new { c.ProjectId, c.Type, c.CreatedAt }).FirstAsync();
        var pairs = await StickmanWorldStore.CardLinksOfCardAsync(context, cardId);
        var ids = pairs.Select(p => p.CharacterId).ToList();
        var ocs = (await context.StickmanCharacters.AsNoTracking().Where(c => ids.Contains(c.Id))
            .Select(c => new { c.Id, c.Title, c.Status, c.CoverUrl, c.AuthorId, c.AuthorName, c.CreatedAt }).ToListAsync()).ToDictionary(c => c.Id);
        // 谁是原件：对应关系是在复制 / 发布的同时记下的，条目和 OC 里哪个的创建时间离它更近，哪个就是新生成的那份
        string Origin(DateTime linkedAt, DateTime ocCreatedAt) =>
            (linkedAt - card.CreatedAt).Duration() <= (linkedAt - ocCreatedAt).Duration() ? "oc" : "card";
        var canEdit = me != Guid.Empty && await WorldAccess.CanEditProjectAsync(context, card.ProjectId, me);
        return Ok(new
        {
            canEdit,
            isCharacter = card.Type == OcWorldConverter.CharacterType,
            items = pairs.Where(p => ocs.TryGetValue(p.CharacterId, out var o) && (o.Status == Published || o.AuthorId == me)).Select(p =>
            {
                var o = ocs[p.CharacterId];
                return new
                {
                    ocId = o.Id, title = o.Title, status = o.Status, coverUrl = o.CoverUrl, authorId = o.AuthorId, authorName = o.AuthorName,
                    mine = o.AuthorId == me, canSync = o.AuthorId == me && canEdit,
                    origin = Origin(p.CreatedAt, o.CreatedAt),
                    createdAt = p.CreatedAt, lastSyncAt = p.LastSyncAt, lastSyncDirection = p.LastSyncDirection,
                };
            }),
        });
    }

    /// <summary>
    /// 「发布成柴圈 OC」：「人物」条目 → OC 草稿（作者 = 当前用户，要对这个世界观有编辑权限）。
    /// 和 StickManController.Create 一样建 OC（没有数量限制、不扣费），展示面板写进 stickman_panels，
    /// 记下对应关系，并自动把 OC 关联到这个世界观（草稿不会出现在「柴圈人设」里，发布后才显示）。
    /// </summary>
    [HttpPost("card/{cardId:guid}/publish")]
    public async Task<IActionResult> Publish(Guid cardId)
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized(new { message = "请先登录" });
        var card = await context.WorldCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cardId);
        if (card == null || !await WorldAccess.CanReadCardAsync(context, cardId, me)) return NotFound(new { message = "条目不存在或无权访问" });
        if (!await WorldAccess.CanEditProjectAsync(context, card.ProjectId, me))
            return StatusCode(403, new { message = "只有世界观的创建者和编写者能把条目发布成柴圈 OC" });
        if (card.Type != OcWorldConverter.CharacterType) return BadRequest(new { message = "只有「人物」条目能发布成柴圈 OC" });

        // 一个条目只对应一个 OC：已经对应过（而且 OC 还在）就不再生成
        var linkedOcs = (await StickmanWorldStore.CardLinksOfCardAsync(context, cardId)).Select(p => p.CharacterId).ToList();
        var existing = await context.StickmanCharacters.AsNoTracking().Where(c => linkedOcs.Contains(c.Id))
            .Select(c => new { c.Id, c.Title, c.AuthorId }).FirstOrDefaultAsync();
        if (existing != null)
            return Conflict(new { message = $"这个条目已经对应了柴圈 OC《{existing.Title}》，要更新内容请用「从世界观条目更新」", ocId = existing.AuthorId == me ? existing.Id : (Guid?)null });

        var side = await ReadCardSideAsync(card);
        var (oc, panel) = OcWorldConverter.NewOcFromCard(side, me, User.FindFirstValue(ClaimTypes.Name) ?? "未知用户");
        context.StickmanCharacters.Add(oc);
        await context.SaveChangesAsync();

        string? warning = null;
        try
        {
            if (panel != null) await StickmanWorldStore.WritePanelAsync(context, oc.Id, StickmanWorldStore.ShowPanel, panel);
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "条目发布成 OC 时写能力面板失败 oc={Oc}", oc.Id);
            warning = "能力面板没能带过去，可以在 OC 编辑页里再加";
        }
        try
        {
            await StickmanWorldStore.AddCardLinkAsync(context, oc.Id, cardId, card.ProjectId, me, OcWorldConverter.CardToOc);
            await StickmanWorldStore.AddLinkAsync(context, oc.Id, card.ProjectId, me);
        }
        catch (DbException ex)
        {
            logger.LogError(ex, "条目发布成 OC 后记对应关系失败 oc={Oc} card={Card}", oc.Id, cardId);
            warning = (warning == null ? "" : warning + "；") + "OC 草稿已经建好了，但对应关系没记上，以后没法直接「从另一边更新」";
        }
        return Ok(new { ocId = oc.Id, message = "已生成 OC 草稿，检查一下再发布吧", warning });
    }

    // ======================================================================
    //  OC ↔ 条目：预览 / 同步 / 断开
    // ======================================================================

    /// <summary>
    /// 预览「从另一边更新」：direction = card-to-oc（用条目更新 OC）或 oc-to-card（用 OC 更新条目）。
    /// 返回会新增 / 修改 / 保留 / 跳过的每一项，和一个指纹（确认执行时带回来）。只读，不改任何东西。
    /// </summary>
    [HttpGet("pair/{ocId:guid}/{cardId:guid}/preview")]
    public async Task<IActionResult> Preview(Guid ocId, Guid cardId, [FromQuery] string direction)
    {
        if (direction != OcWorldConverter.OcToCard && direction != OcWorldConverter.CardToOc) return BadRequest(new { message = "同步方向不正确" });
        var denied = await RequireSyncAsync(ocId, cardId);
        if (denied != null) return denied;

        var oc = await context.StickmanCharacters.AsNoTracking().Include(c => c.Attributes).Include(c => c.Images).FirstAsync(c => c.Id == ocId);
        var card = await context.WorldCards.AsNoTracking().FirstAsync(c => c.Id == cardId);
        var ocSide = OcWorldConverter.ReadOc(oc, oc.Attributes ?? new List<StickmanAttribute>(), oc.Images ?? new List<StickmanImage>(),
            await SafePanelAsync(ocId, StickmanWorldStore.ShowPanel));
        var cardSide = await ReadCardSideAsync(card);
        var plan = direction == OcWorldConverter.OcToCard ? OcWorldConverter.Plan(ocSide, cardSide, direction) : OcWorldConverter.Plan(cardSide, ocSide, direction);
        return Ok(new
        {
            direction,
            ocTitle = oc.Title,
            cardTitle = card.Title,
            projectId = card.ProjectId,
            changes = plan.Select(OcWorldConverter.PreviewDto),
            hasChanges = plan.Any(OcWorldConverter.IsWrite),
            fingerprint = OcWorldConverter.Fingerprint(plan, direction, ocId, cardId),
        });
    }

    /// <summary>
    /// 确认执行「从另一边更新」。重新算一遍，和预览不一样就不执行（409，让用户重新预览）。
    /// 用条目更新 OC：先把 OC 现在的样子存进备份，再改（同一个事务）。
    /// 用 OC 更新条目：走条目服务的正常保存（保存前后都记世界观修改记录，可以撤销；期间条目被别人改过会拒绝保存）。
    /// </summary>
    [HttpPost("pair/{ocId:guid}/{cardId:guid}/sync")]
    public async Task<IActionResult> Sync(Guid ocId, Guid cardId, [FromBody] SyncRequest body)
    {
        if (body.Direction != OcWorldConverter.OcToCard && body.Direction != OcWorldConverter.CardToOc) return BadRequest(new { message = "同步方向不正确" });
        var denied = await RequireSyncAsync(ocId, cardId);
        if (denied != null) return denied;
        var me = Me;
        return body.Direction == OcWorldConverter.CardToOc ? await SyncToOcAsync(ocId, cardId, body.Fingerprint, me) : await SyncToCardAsync(ocId, cardId, body.Fingerprint, me);
    }

    private async Task<IActionResult> SyncToOcAsync(Guid ocId, Guid cardId, string fingerprint, Guid me)
    {
        var outcome = "";
        var changed = 0;
        var strategy = context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            context.ChangeTracker.Clear();
            outcome = ""; changed = 0;
            await using var tx = await context.Database.BeginTransactionAsync();
            var oc = await context.StickmanCharacters.Include(c => c.Attributes).Include(c => c.Images).FirstOrDefaultAsync(c => c.Id == ocId);
            var card = await context.WorldCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cardId);
            if (oc == null || card == null) { outcome = "gone"; return; }
            var attrs = oc.Attributes?.ToList() ?? new List<StickmanAttribute>();
            var images = oc.Images?.ToList() ?? new List<StickmanImage>();
            var show = await StickmanWorldStore.PanelAsync(context, ocId, StickmanWorldStore.ShowPanel);
            var battle = await StickmanWorldStore.PanelAsync(context, ocId, StickmanWorldStore.BattlePanel);
            var plan = OcWorldConverter.Plan(await ReadCardSideAsync(card), OcWorldConverter.ReadOc(oc, attrs, images, show), OcWorldConverter.CardToOc);
            if (OcWorldConverter.Fingerprint(plan, OcWorldConverter.CardToOc, ocId, cardId) != fingerprint) { outcome = "changed"; return; }
            changed = plan.Count(OcWorldConverter.IsWrite);
            if (changed == 0) { outcome = "nothing"; return; }

            // 先备份（和下面的修改在同一个事务里）
            await StickmanWorldStore.AddBackupAsync(context, ocId, StickmanWorldStore.Snap(oc, attrs, images, show, battle),
                $"从世界观条目《{Clip(card.Title, 60)}》更新之前的自动备份", me);
            var result = OcWorldConverter.ApplyToOc(oc, attrs, images, plan);
            context.StickmanAttributes.AddRange(result.NewAttributes);
            context.StickmanImages.AddRange(result.NewImages);
            await context.SaveChangesAsync();
            if (result.ShowPanelJson != null) await StickmanWorldStore.WritePanelAsync(context, ocId, StickmanWorldStore.ShowPanel, result.ShowPanelJson);
            await StickmanWorldStore.TouchCardLinkAsync(context, ocId, cardId, OcWorldConverter.CardToOc);
            await tx.CommitAsync();
            outcome = "ok";
        });
        return outcome switch
        {
            "gone" => NotFound(new { message = "OC 或条目已经不在了" }),
            "changed" => Conflict(new { code = "SYNC_CHANGED", message = "预览之后两边的内容有变化，请重新预览一次再确认" }),
            "nothing" => Ok(new { changed = 0, message = "两边已经一样了，没有要更新的" }),
            _ => Ok(new { changed, message = $"已更新 {changed} 项。更新前的 OC 已经自动备份，在「备份」里可以查看和恢复" }),
        };
    }

    private async Task<IActionResult> SyncToCardAsync(Guid ocId, Guid cardId, string fingerprint, Guid me)
    {
        var oc = await context.StickmanCharacters.AsNoTracking().Include(c => c.Attributes).Include(c => c.Images).FirstOrDefaultAsync(c => c.Id == ocId);
        var card = await context.WorldCards.AsNoTracking().FirstOrDefaultAsync(c => c.Id == cardId);
        if (oc == null || card == null) return NotFound(new { message = "OC 或条目已经不在了" });
        var ocSide = OcWorldConverter.ReadOc(oc, oc.Attributes ?? new List<StickmanAttribute>(), oc.Images ?? new List<StickmanImage>(),
            await SafePanelAsync(ocId, StickmanWorldStore.ShowPanel));
        var cardSide = await ReadCardSideAsync(card);
        var plan = OcWorldConverter.Plan(ocSide, cardSide, OcWorldConverter.OcToCard);
        if (OcWorldConverter.Fingerprint(plan, OcWorldConverter.OcToCard, ocId, cardId) != fingerprint)
            return Conflict(new { code = "SYNC_CHANGED", message = "预览之后两边的内容有变化，请重新预览一次再确认" });
        var changed = plan.Count(OcWorldConverter.IsWrite);
        if (changed == 0) return Ok(new { changed = 0, message = "两边已经一样了，没有要更新的" });

        await EnsureCardBackupAsync(card, me);
        var dto = OcWorldConverter.ApplyToCard(cardSide, plan, card.UpdatedAt);
        // 条目服务保存：检查编辑权限、核对版本（期间被别人改过就 409）、保存前补「记录起点」、保存后记一笔修改记录、清缓存
        var updated = await cardService.UpdateCardAsync(cardId, me, dto, card.ProjectId);
        if (updated == null) return StatusCode(403, new { message = "你在这个世界观里没有编辑权限，不能更新条目" });
        await StickmanWorldStore.TouchCardLinkAsync(context, ocId, cardId, OcWorldConverter.OcToCard);
        return Ok(new { changed, message = $"已更新 {changed} 项。条目的「修改记录」里能看到这次修改，需要的话可以撤销" });
    }

    /// <summary>断开对应关系：只删对应记录，OC 和条目的内容都不动。OC 作者或世界观的创建者 / 编写者都能断开。</summary>
    [HttpDelete("pair/{ocId:guid}/{cardId:guid}")]
    public async Task<IActionResult> Disconnect(Guid ocId, Guid cardId)
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized(new { message = "请先登录" });
        var pair = await StickmanWorldStore.CardLinkAsync(context, ocId, cardId);
        if (pair == null) return NotFound(new { message = "它们之间没有对应关系（可能已经断开了）" });
        var authorId = await context.StickmanCharacters.AsNoTracking().Where(c => c.Id == ocId).Select(c => (Guid?)c.AuthorId).FirstOrDefaultAsync();
        if (authorId != me && !await WorldAccess.CanEditProjectAsync(context, pair.ProjectId, me))
            return StatusCode(403, new { message = "只有 OC 的作者，或者世界观的创建者 / 编写者能断开" });
        await StickmanWorldStore.RemoveCardLinkAsync(context, ocId, cardId);
        return Ok(new { ok = true, message = "已断开，两边的内容都没有变" });
    }

    // ======================================================================
    //  工具
    // ======================================================================

    private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    /// <summary>只有 OC 作者能做；不是作者返回对应的错误结果，是作者返回 null</summary>
    private async Task<IActionResult?> RequireAuthorAsync(Guid ocId)
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized(new { message = "请先登录" });
        var authorId = await context.StickmanCharacters.AsNoTracking().Where(c => c.Id == ocId).Select(c => (Guid?)c.AuthorId).FirstOrDefaultAsync();
        if (authorId == null) return NotFound(new { message = "OC 角色不存在" });
        if (authorId != me) return StatusCode(403, new { message = "只有 OC 的作者能查看和恢复备份" });
        return null;
    }

    /// <summary>同步要同时满足：两边有对应关系、自己是 OC 作者、能编辑条目所在的世界观</summary>
    private async Task<IActionResult?> RequireSyncAsync(Guid ocId, Guid cardId)
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized(new { message = "请先登录" });
        if (await StickmanWorldStore.CardLinkAsync(context, ocId, cardId) == null)
            return NotFound(new { message = "这个 OC 和条目之间没有对应关系（可能已经断开了）" });
        var authorId = await context.StickmanCharacters.AsNoTracking().Where(c => c.Id == ocId).Select(c => (Guid?)c.AuthorId).FirstOrDefaultAsync();
        if (authorId == null) return NotFound(new { message = "OC 已经不在了，可以断开对应关系" });
        if (authorId != me) return StatusCode(403, new { message = "只有 OC 的作者能同步" });
        var projectId = await context.WorldCards.AsNoTracking().Where(c => c.Id == cardId).Select(c => (Guid?)c.ProjectId).FirstOrDefaultAsync();
        if (projectId == null) return NotFound(new { message = "条目已经不在了，可以断开对应关系" });
        if (!await WorldAccess.CanEditProjectAsync(context, projectId.Value, me))
            return StatusCode(403, new { message = "你在这个世界观里没有编辑权限，不能同步" });
        return null;
    }

    /// <summary>读条目（先查好正文里引用到的条目名字，{CARD:编号} 才能换成《名字》）</summary>
    private async Task<OcWorldConverter.Side> ReadCardSideAsync(WorldCard card)
    {
        var refIds = OcWorldConverter.ReferencedCardIds(card);
        var projectId = card.ProjectId;
        var titles = refIds.Count == 0 ? new Dictionary<Guid, string>() : await context.WorldCards.AsNoTracking()
            .Where(c => c.ProjectId == projectId && refIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id, c => c.Title);
        try { return OcWorldConverter.ReadCard(card, titles); }
        catch (JsonException) { throw new WorldOperationException(WorldErrorKind.Invalid, "这个条目的属性格式不对，没法转换（先在编辑器里打开保存一次试试）"); }
    }

    /// <summary>读 OC 展示面板；面板表出问题时当作没有面板（不影响别的内容）</summary>
    private async Task<string?> SafePanelAsync(Guid ocId, string kind)
    {
        try { return await StickmanWorldStore.PanelAsync(context, ocId, kind); }
        catch (DbException ex) { logger.LogWarning(ex, "读取 OC 能力面板失败，当作没有面板"); return null; }
    }

    /// <summary>
    /// 用 OC 更新条目之前：条目服务保存时只会在「完全没有记录」时补记录起点。
    /// 万一条目现在的样子和最后一条修改记录对不上（比如以前有别的途径改过），先补记一份现在的样子，保证这次更新一定能撤销回来。
    /// </summary>
    private async Task EnsureCardBackupAsync(WorldCard card, Guid me)
    {
        try
        {
            var last = await context.WorldCardRevisions.AsNoTracking().Where(r => r.CardId == card.Id).OrderByDescending(r => r.Id)
                .Select(r => new { r.Action, r.Snapshot }).FirstOrDefaultAsync();
            if (last == null) return; // 没有任何记录：条目服务保存前会自己存「记录起点」
            var snap = WorldHistoryService.Parse(last.Snapshot);
            if (snap != null && last.Action != WorldHistoryService.Delete && snap.Title == card.Title && snap.Description == card.Description
                && snap.CoverImage == card.CoverImage && snap.Attributes == card.Attributes && snap.Layout == card.Layout && snap.GalleryImages == card.GalleryImages)
                return;
            await history.RecordAsync(card.ProjectId, card.Id, me, WorldHistoryService.Baseline, "从柴圈 OC 更新之前的内容（自动备份）");
        }
        catch (Exception ex) { logger.LogWarning(ex, "同步前补记条目备份失败 card={Card}", card.Id); }
    }

    private async Task NotifyAsync(Guid recipient, Guid actor, string targetType, string targetId, string content, string link)
    {
        try
        {
            if (new NotificationService(context).Add(recipient, actor, "world_oc", targetType, targetId, content, link))
                await context.SaveChangesAsync();
        }
        catch (Exception ex) { logger.LogWarning(ex, "发送 OC 关联通知失败"); }
    }

    private static string Clip(string text, int max) => text.Length > max ? text[..max] + "…" : text;
}
