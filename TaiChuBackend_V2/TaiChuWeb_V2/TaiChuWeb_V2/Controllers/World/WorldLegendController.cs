using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Controllers.World;

/// <summary>
/// 时间线颜色图例：每个世界自己规定每种颜色代表什么（比如 红 = 战争、灰 = 灾难），
/// 条目里的时间线区块和世界编年史旁边会显示出来。
/// 读：能看这个世界的人（公开世界任何人，私有世界创建者和协作者）；改：和「世界设置 → 名字与简介」一样，只有创建者。
/// 数据存在启动时建的表 WorldColorLegends 里（见 WorldLegendSchema），用原生 SQL 读写，不需要改 AppDbContext。
/// </summary>
[Route("api/world/projects/{projectId:guid}/legend")]
public class WorldLegendController(AppDbContext context, ILogger<WorldLegendController> logger) : WorldControllerBase
{
    /// <summary>
    /// 能起名字的颜色（按色系，不按具体色值）：条目时间线和编年史的调色板色值略有不同，
    /// 同一个色系算同一种颜色，一份图例两边都能用。顺序就是显示顺序。要和前端 utils/worldLegend.ts 的 LEGEND_HUES 保持一致。
    /// </summary>
    private static readonly List<string> Hues = new() { "red", "amber", "green", "teal", "blue", "purple", "pink", "brown", "gray" };
    private const int MaxLabelLength = 20;

    public sealed class LegendDto
    {
        public Dictionary<string, string?>? Items { get; set; }
    }

    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> Get(Guid projectId)
    {
        var me = ReaderUserId;
        await WorldAccess.RequireReadableProjectAsync(context, projectId, me);
        var canEdit = me != Guid.Empty && await IsOwnerAsync(projectId, me);
        string? json = null;
        try
        {
            var pid = projectId.ToString();
            var rows = await context.Database
                .SqlQuery<string>($"SELECT Json AS `Value` FROM WorldColorLegends WHERE ProjectId = {pid}")
                .ToListAsync();
            json = rows.FirstOrDefault();
        }
        catch (DbException ex)
        {
            // 表还没建好（启动建表失败）时不让整个页面报错，当作没有图例
            logger.LogWarning(ex, "读取时间线颜色图例失败，当作没有图例");
        }
        return Ok(new { items = Parse(json), canEdit });
    }

    [HttpPut]
    [Authorize]
    public async Task<IActionResult> Save(Guid projectId, [FromBody] LegendDto dto)
    {
        var me = CurrentUserId;
        await WorldAccess.RequireReadableProjectAsync(context, projectId, me);
        if (!await IsOwnerAsync(projectId, me))
            throw new WorldOperationException(WorldErrorKind.Forbidden, "只有创建者能修改时间线图例");

        // 只留认识的颜色；名字去掉首尾空格，空的不存
        var clean = new Dictionary<string, string>();
        foreach (var hue in Hues)
        {
            if (dto.Items == null || !dto.Items.TryGetValue(hue, out var raw)) continue;
            var label = System.Text.RegularExpressions.Regex.Replace(raw ?? "", @"\s+", " ").Trim();
            if (label.Length == 0) continue;
            if (label.Length > MaxLabelLength) return BadRequest(new { message = $"每种颜色的含义最多 {MaxLabelLength} 个字" });
            clean[hue] = label;
        }

        var json = JsonSerializer.Serialize(new { items = clean });
        var pid = projectId.ToString();
        var now = DateTime.UtcNow;
        await context.Database.ExecuteSqlInterpolatedAsync($@"INSERT INTO WorldColorLegends (ProjectId, Json, UpdatedAt) VALUES ({pid}, {json}, {now})
ON DUPLICATE KEY UPDATE Json = VALUES(Json), UpdatedAt = VALUES(UpdatedAt)");
        return Ok(new { items = clean, canEdit = true });
    }

    private Task<bool> IsOwnerAsync(Guid projectId, Guid userId) =>
        context.WorldProjects.AsNoTracking().OwnedBy(userId).AnyAsync(p => p.Id == projectId);

    /// <summary>读出来的 JSON 再过滤一遍，坏数据当作没有</summary>
    private static Dictionary<string, string> Parse(string? json)
    {
        var result = new Dictionary<string, string>();
        if (string.IsNullOrWhiteSpace(json)) return result;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object
                || !doc.RootElement.TryGetProperty("items", out var items)
                || items.ValueKind != JsonValueKind.Object) return result;
            foreach (var hue in Hues)
            {
                if (items.TryGetProperty(hue, out var v) && v.ValueKind == JsonValueKind.String)
                {
                    var label = (v.GetString() ?? "").Trim();
                    if (label.Length is > 0 and <= MaxLabelLength) result[hue] = label;
                }
            }
        }
        catch (JsonException) { }
        return result;
    }
}
