using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services;
using TaiChuWeb_V2.Services.Panels;

namespace TaiChuWeb_V2.Controllers.ChaiCommunity;

/// <summary>
/// 柴圈 OC 的能力面板（雷达图）。单独一个控制器、单独一张表（stickman_panels），
/// OC 的保存接口（StickManController.Update）完全不碰面板，面板也不碰 OC 的属性和图片，互不影响。
///
/// 每个 OC 两份，分开存、互不覆盖：
///   show   = 展示面板：自由设计（自定义维度、刻度），OC 详情页显示；
///   battle = 约战面板：只能用官方约战模板（BattlePanelTemplate），约战详情里双方对比用。
///
/// 读：公开；草稿 OC 只有作者能看（和 StickManController.GetDetail 一样，看不到就当不存在）。
/// 改 / 删：只有作者。
/// 官方约战模板：公开读；只有超级管理员能改（管理面板 →「约战面板模板」），存在 SystemConfigs 表，见 BattlePanelTemplate。
/// </summary>
[Route("api/StickMan")]
[ApiController]
[Authorize]
public class StickmanPanelController(AppDbContext context, SystemConfigService config, ILogger<StickmanPanelController> logger) : ControllerBase
{
    private const string Show = "show";
    private const string Battle = "battle";
    /// <summary>批量读约战面板一次最多几个 OC（约战一方最多几个 OC，留足余量）</summary>
    private const int MaxBatch = 40;

    /// <summary>
    /// 官方约战模板：维度（带稳定的 key）、能选的等级、删掉的维度（retired）。前端都从这里拿。
    /// canEdit = 当前用户是超级管理员（管理面板据此决定能不能保存）。
    /// </summary>
    [HttpGet("battle-panel-template")]
    [AllowAnonymous]
    public async Task<IActionResult> Template()
    {
        var t = await BattlePanelTemplate.LoadAsync(config);
        return Ok(TemplateDto(t, await IsSuperAdminAsync()));
    }

    /// <summary>
    /// 超级管理员修改约战模板。只改模板本身（SystemConfigs 里那一行），不碰任何 OC 已经填的约战面板：
    /// 删掉的维度记进 retired（OC 的值还在，只是不显示），新维度没传 key 的自动生成一个从没用过的 key。
    /// </summary>
    [HttpPut("battle-panel-template")]
    public async Task<IActionResult> SaveTemplate([FromBody] JsonElement body)
    {
        if (!await IsSuperAdminAsync()) return StatusCode(403, new { message = "只有超级管理员能修改约战面板模板" });
        // 自己解析（不用自动模型绑定）：新维度的 key 是空字符串，自动绑定会把它当成「必填项没填」直接拒绝
        BattleTemplate? dto;
        try { dto = JsonSerializer.Deserialize<BattleTemplate>(body.GetRawText(), BattlePanelTemplate.Json); }
        catch (JsonException) { return BadRequest(new { message = "模板格式不正确" }); }
        var current = await BattlePanelTemplate.LoadAsync(config);
        var clean = BattlePanelTemplate.Clean(dto, current, out var error);
        if (clean == null) return BadRequest(new { message = error });
        await BattlePanelTemplate.SaveAsync(context, config, clean, Me);
        return Ok(TemplateDto(clean, true));
    }

    /// <summary>一个 OC 的两份面板：{ show: 面板或 null, battle: { values, filled } 或 null, canEdit }</summary>
    [HttpGet("{id:guid}/panel")]
    [AllowAnonymous]
    public async Task<IActionResult> Get(Guid id)
    {
        var oc = await FindVisibleAsync(id);
        if (oc == null) return NotFound(new { message = "OC 角色不存在" });
        var show = ParseShow(await ReadAsync(id, Show));
        var battleJson = await ReadAsync(id, Battle);
        var template = await BattlePanelTemplate.LoadAsync(config);
        return Ok(new
        {
            show,
            battle = battleJson == null ? null : BattleDto(template, battleJson),
            canEdit = oc.Value.AuthorId == Me,
        });
    }

    /// <summary>
    /// 批量读约战面板（约战详情的出战人设、发起 / 报名约战时提醒「还没填约战面板」用）：
    /// GET /api/StickMan/battle-panels?ids=a,b,c → { items: { "OC编号": { values, filled } } }，没填的、看不到的不返回。
    /// </summary>
    [HttpGet("battle-panels")]
    [AllowAnonymous]
    public async Task<IActionResult> BattlePanels([FromQuery] string? ids)
    {
        // 用 List 不用数组：EF 里对数组 .Contains 在 C# 14 下会出错
        var idList = (ids ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty).Where(g => g != Guid.Empty).Distinct().Take(MaxBatch).ToList();
        var items = new Dictionary<string, object>();
        if (idList.Count == 0) return Ok(new { items });

        var me = Me;
        var template = await BattlePanelTemplate.LoadAsync(config);
        var visible = await context.StickmanCharacters.AsNoTracking()
            .Where(c => idList.Contains(c.Id))
            .Select(c => new { c.Id, c.Status, c.AuthorId })
            .ToListAsync();
        foreach (var oc in visible.Where(c => c.Status != "draft" || c.AuthorId == me))
        {
            var json = await ReadAsync(oc.Id, Battle);
            if (json != null) items[oc.Id.ToString()] = BattleDto(template, json);
        }
        return Ok(new { items });
    }

    /// <summary>保存展示面板（整份替换）：先检查，再整理成干净格式存</summary>
    [HttpPut("{id:guid}/panel/show")]
    public async Task<IActionResult> SaveShow(Guid id, [FromBody] JsonElement body)
    {
        var denied = await RequireAuthorAsync(id);
        if (denied != null) return denied;
        JsonNode? node;
        try { node = JsonNode.Parse(body.GetRawText()); }
        catch (JsonException) { return BadRequest(new { message = "能力面板格式不正确" }); }
        var error = StatPanelRules.Check(node);
        if (error != null) return BadRequest(new { message = error });
        var clean = StatPanelRules.Normalize((JsonObject)node!);
        await WriteAsync(id, Show, clean.ToJsonString(StatPanelRules.Raw));
        return Ok(new { show = clean });
    }

    /// <summary>保存约战面板：只认官方模板里的 key 和等级；模板里已经没有的旧 key 原样保留</summary>
    [HttpPut("{id:guid}/panel/battle")]
    public async Task<IActionResult> SaveBattle(Guid id, [FromBody] JsonElement body)
    {
        var denied = await RequireAuthorAsync(id);
        if (denied != null) return denied;
        JsonNode? node;
        try { node = JsonNode.Parse(body.GetRawText()); }
        catch (JsonException) { return BadRequest(new { message = "约战面板格式不正确" }); }
        var template = await BattlePanelTemplate.LoadAsync(config);
        var json = BattlePanelTemplate.Merge(template, await ReadAsync(id, Battle), node, out var error);
        if (json == null) return BadRequest(new { message = error });
        await WriteAsync(id, Battle, json);
        return Ok(new { battle = BattleDto(template, json) });
    }

    /// <summary>删除某一份面板（作者自己点「删除面板」时）；只删这一行，另一份和 OC 本身都不受影响</summary>
    [HttpDelete("{id:guid}/panel/{kind}")]
    public async Task<IActionResult> Delete(Guid id, string kind)
    {
        if (kind != Show && kind != Battle) return BadRequest(new { message = "面板类型不正确" });
        var denied = await RequireAuthorAsync(id);
        if (denied != null) return denied;
        var cid = id.ToString();
        await context.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM stickman_panels WHERE CharacterId = {cid} AND Kind = {kind}");
        return Ok(new { ok = true });
    }

    // ===================== 工具 =====================

    private Guid Me => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : Guid.Empty;

    /// <summary>超级管理员（写法同 DevProjectController.IsSuperAdmin）</summary>
    private async Task<bool> IsSuperAdminAsync()
    {
        var me = Me;
        if (me == Guid.Empty) return false;
        return await context.UserPermissions.AnyAsync(p => p.UserId == me && p.Permission == AdminPermission.SuperAdmin);
    }

    private static object TemplateDto(BattleTemplate t, bool canEdit) => new
    {
        version = t.Version,
        title = t.Title,
        scale = "grade",
        grades = t.Grades,
        dims = t.Dims.Select(d => new { key = d.Key, name = d.Name, hint = d.Hint }),
        retired = t.Retired.Select(d => new { key = d.Key, name = d.Name, hint = d.Hint }),
        canEdit,
    };

    /// <summary>OC 存在、而且当前用户能看（不是草稿，或者自己是作者）才返回</summary>
    private async Task<(Guid AuthorId, string Status)?> FindVisibleAsync(Guid id)
    {
        var oc = await context.StickmanCharacters.AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new { c.AuthorId, c.Status })
            .FirstOrDefaultAsync();
        if (oc == null) return null;
        if (oc.Status == "draft" && oc.AuthorId != Me) return null;
        return (oc.AuthorId, oc.Status);
    }

    /// <summary>只有作者能改；不是作者返回对应的错误结果，是作者返回 null</summary>
    private async Task<IActionResult?> RequireAuthorAsync(Guid id)
    {
        var me = Me;
        if (me == Guid.Empty) return Unauthorized(new { message = "用户未登录" });
        var authorId = await context.StickmanCharacters.AsNoTracking()
            .Where(c => c.Id == id).Select(c => (Guid?)c.AuthorId).FirstOrDefaultAsync();
        if (authorId == null) return NotFound(new { message = "OC 角色不存在" });
        if (authorId != me) return StatusCode(403, new { message = "只有作者能修改这个 OC 的能力面板" });
        return null;
    }

    private async Task<string?> ReadAsync(Guid id, string kind)
    {
        try
        {
            var cid = id.ToString();
            var rows = await context.Database
                .SqlQuery<string>($"SELECT Json AS `Value` FROM stickman_panels WHERE CharacterId = {cid} AND Kind = {kind}")
                .ToListAsync();
            return rows.FirstOrDefault();
        }
        catch (DbException ex)
        {
            // 表还没建好（启动建表失败）时不让整个页面报错，当作没有面板
            logger.LogWarning(ex, "读取 OC 能力面板失败，当作没有面板");
            return null;
        }
    }

    private async Task WriteAsync(Guid id, string kind, string json)
    {
        var cid = id.ToString();
        var now = DateTime.UtcNow;
        await context.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO stickman_panels (CharacterId, Kind, Json, UpdatedAt) VALUES ({cid}, {kind}, {json}, {now})
ON DUPLICATE KEY UPDATE Json = VALUES(Json), UpdatedAt = VALUES(UpdatedAt)");
    }

    /// <summary>存着的展示面板：读出来再检查一遍，坏数据当作没有（不让一个坏面板把详情页弄崩）</summary>
    private static JsonNode? ParseShow(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var node = JsonNode.Parse(json);
            return StatPanelRules.Check(node) == null ? node : null;
        }
        catch (JsonException) { return null; }
    }

    /// <summary>约战面板给前端：所有存着的值（包括模板里暂时没有的 key，前端只按当前模板显示）+ 是否算「填了」</summary>
    private static object BattleDto(BattleTemplate template, string json)
    {
        var values = BattlePanelTemplate.ReadValues(json);
        return new { values, filled = BattlePanelTemplate.IsFilled(template, values) };
    }
}
