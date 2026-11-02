using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services;
using TaiChuWeb_V2.Services.Games;
using TaiChuWeb_V2.Services.Notify;

namespace TaiChuWeb_V2.Controllers.Game
{
    /// <summary>
    /// 太虚游 · 转盘。
    /// · 用户转盘：大厅列表、我的转盘、创建（花经验或土豆，价格在 SystemConfigs 的 Wheel:CreateCostExp / Wheel:CreateCostPotato）、编辑（免费）、删除（不退费）、转动次数 +1；
    /// · 官方转盘：每人每个转盘每天免费转 1 次，服务器决定结果并发奖；我的抽奖记录、全站最近中奖；
    /// · 管理：官方转盘增删改、上下架（超级管理员）；用户转盘被举报后下架 / 恢复（超级管理员或用户审核权限）；创建价格设置（超级管理员）。
    /// 具体的数据库读写都在 Services/Games/WheelService.cs。
    /// </summary>
    [ApiController]
    [Route("api/wheel")]
    public class WheelController(WheelService wheels, PotatoService potatoes, SystemConfigService config, AppDbContext db) : ControllerBase
    {
        private const int PageSize = 24;
        private const string OfficialName = "太初寰宇官方";

        private Guid? CurrentUserId => Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

        /// <summary>管理权限：Super = 超级管理员（管官方转盘、价格）；Audit = 能处理举报（超级管理员或用户审核）</summary>
        private async Task<(bool Super, bool Audit)> AdminLevelAsync()
        {
            if (CurrentUserId is not Guid me) return (false, false);
            var perms = await db.UserPermissions.AsNoTracking().Where(p => p.UserId == me).Select(p => p.Permission).ToListAsync();
            var super = perms.Contains(AdminPermission.SuperAdmin);
            return (super, super || perms.Contains(AdminPermission.User_Audit));
        }

        private async Task<(int CostExp, int CostPotato)> CostsAsync() => (
            await config.GetIntAsync(WheelService.CostExpKey, WheelService.DefaultCostExp),
            await config.GetIntAsync(WheelService.CostPotatoKey, WheelService.DefaultCostPotato));

        private async Task<Dictionary<Guid, string>> NamesAsync(IEnumerable<Guid> ids)
        {
            var list = ids.Where(g => g != Guid.Empty).Distinct().ToList();
            if (list.Count == 0) return new Dictionary<Guid, string>();
            return await db.Users.AsNoTracking().Where(u => list.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
        }

        private static string Clip(string s, int max) => s.Length > max ? s[..max] + "…" : s;

        private static object SegmentsView(WheelService.WheelRow w) => w.Segments.Select(s => new
        {
            text = s.Text,
            color = s.Color,
            weight = s.Weight,
            prizeType = s.PrizeType,
            prizeAmount = s.PrizeAmount,
            prizeLabel = WheelService.PrizeLabel(s.PrizeType, s.PrizeAmount),
        }).ToList();

        /// <summary>转盘的完整信息（转盘页 / 编辑器用）</summary>
        private object DetailView(WheelService.WheelRow w, Dictionary<Guid, string> names, bool isOwner, bool showAdmin) => new
        {
            id = w.Id,
            kind = w.Kind,
            title = w.Title,
            intro = w.Intro,
            visibility = w.Visibility,
            status = w.Status,
            hiddenReason = isOwner || showAdmin ? w.HiddenReason : null,
            removeOnHit = w.RemoveOnHit,
            segments = SegmentsView(w),
            spinCount = w.SpinCount,
            // 官方转盘不显示是哪个管理员建的
            owner = w.Kind == "official" ? null : new { id = w.OwnerId, name = names.GetValueOrDefault(w.OwnerId) ?? "已注销的用户" },
            isMine = isOwner,
            shareCode = isOwner ? w.ShareCode : null,
            sortOrder = w.SortOrder,
            createdAt = w.CreatedAt,
            updatedAt = w.UpdatedAt,
        };

        /// <summary>列表卡片（简介截短；扇区只给颜色和权重画缩略图）</summary>
        private static object CardView(WheelService.WheelRow w, Dictionary<Guid, string> names, object? today = null) => new
        {
            id = w.Id,
            kind = w.Kind,
            title = w.Title,
            intro = Clip(w.Intro, 80),
            visibility = w.Visibility,
            status = w.Status,
            segmentCount = w.Segments.Count,
            colors = w.Segments.Select(s => new { color = s.Color, weight = s.Weight }).ToList(),
            hasPrize = w.Segments.Any(s => s.PrizeType != "none"),
            spinCount = w.SpinCount,
            owner = w.Kind == "official" ? null : new { id = w.OwnerId, name = names.GetValueOrDefault(w.OwnerId) ?? "已注销的用户" },
            createdAt = w.CreatedAt,
            today,
        };

        private static object SpinView(WheelService.SpinLog s) => new
        {
            id = s.Id,
            wheelId = s.WheelId,
            wheelTitle = s.WheelTitle,
            segmentIndex = s.SegmentIndex,
            text = s.SegmentText,
            prizeType = s.PrizeType,
            prizeAmount = s.PrizeAmount,
            prizeLabel = WheelService.PrizeLabel(s.PrizeType, s.PrizeAmount),
            // 只有日期的字段按字符串给，免得被当成 UTC 时间换算
            spinDate = s.SpinDate.ToString("yyyy-MM-dd"),
            createdAt = s.CreatedAt,
        };

        // ==================== 大厅 ====================

        /// <summary>转盘大厅：第 1 页附带官方转盘（置顶）；公开的用户转盘按热度（hot）或最新（new）排，可以按标题搜</summary>
        [HttpGet("list")]
        public async Task<IActionResult> List([FromQuery] string sort = "hot", [FromQuery] string? q = null, [FromQuery] int page = 1)
        {
            var (items, hasMore) = await wheels.ListPublicAsync(sort == "new" ? "new" : "hot", q, page, PageSize);
            var official = page <= 1 && string.IsNullOrWhiteSpace(q) ? await wheels.ListOfficialAsync(false) : new List<WheelService.WheelRow>();
            var names = await NamesAsync(items.Select(w => w.OwnerId));

            // 登录了就顺便告诉他今天哪些官方转盘已经转过
            var todayMap = new Dictionary<long, WheelService.SpinLog>();
            if (official.Count > 0 && CurrentUserId is Guid me)
                foreach (var s in await wheels.TodaySpinsAsync(me)) todayMap[s.WheelId] = s;

            return Ok(new
            {
                official = official.Select(w => CardView(w, names, todayMap.TryGetValue(w.Id, out var t) ? SpinView(t) : null)),
                items = items.Select(w => CardView(w, names)),
                hasMore,
            });
        }

        /// <summary>我的转盘（包括仅自己可见、被下架的）</summary>
        [Authorize]
        [HttpGet("mine")]
        public async Task<IActionResult> Mine()
        {
            if (CurrentUserId is not Guid me) return Unauthorized();
            var rows = await wheels.ListMineAsync(me);
            var names = await NamesAsync(new[] { me });
            return Ok(new
            {
                items = rows.Select(w => new
                {
                    card = CardView(w, names),
                    shareCode = w.ShareCode,
                    hiddenReason = w.HiddenReason,
                }),
                count = rows.Count,
                max = WheelService.MaxPerUser,
            });
        }

        /// <summary>创建前要知道的：两种付法的价格（负数 = 不能用这种付法）、还能做几个、我现在有多少经验和土豆</summary>
        [Authorize]
        [HttpGet("pricing")]
        public async Task<IActionResult> Pricing()
        {
            if (CurrentUserId is not Guid me) return Unauthorized();
            var (costExp, costPotato) = await CostsAsync();
            var count = await wheels.CountMineAsync(me);
            var exp = await db.UserStats.AsNoTracking().Where(s => s.UserId == me).Select(s => (long?)s.Experience).FirstOrDefaultAsync() ?? 0;
            long potato = 0;
            try { potato = (await potatoes.GetAsync(me)).Balance; } catch { /* 土豆表还没建好时当 0 */ }
            return Ok(new
            {
                costExp,
                costPotato,
                count,
                max = WheelService.MaxPerUser,
                experience = exp,
                potato,
                limits = new
                {
                    minSegments = WheelService.MinSegments,
                    maxSegments = WheelService.MaxSegments,
                    titleMax = WheelService.TitleMax,
                    introMax = WheelService.IntroMax,
                    segmentTextMax = WheelService.SegmentTextMax,
                    weightMax = WheelService.WeightMax,
                    potatoPrizeMax = WheelService.PotatoPrizeMax,
                    expPrizeMax = WheelService.ExpPrizeMax,
                },
            });
        }

        // ==================== 单个转盘 ====================

        /// <summary>
        /// 打开一个转盘。谁能看：官方转盘上架时所有人；公开的用户转盘所有人；仅自己的要么是主人、要么链接里带对了分享码 k；
        /// 被下架的只有主人和管理员能看。
        /// </summary>
        [HttpGet("{id:long}")]
        public async Task<IActionResult> Get(long id, [FromQuery] string? k = null)
        {
            var w = await wheels.GetAsync(id);
            if (w == null) return NotFound(new { message = "这个转盘不存在，可能已经被删掉了" });
            var me = CurrentUserId;
            var isOwner = w.Kind == "user" && me is Guid u && u == w.OwnerId;
            var (super, audit) = me == null ? (false, false) : await AdminLevelAsync();
            bool canView = w.Kind == "official"
                ? w.Status == "active" || super
                : isOwner || audit || (w.Status == "active" && (w.Visibility == "public" || (!string.IsNullOrEmpty(k) && k == w.ShareCode)));
            if (!canView)
                return w.Status != "active"
                    ? NotFound(new { message = "这个转盘已经下架了" })
                    : NotFound(new { message = "这个转盘是仅自己可见的，需要主人分享的完整链接才能打开" });

            var names = await NamesAsync(new[] { w.OwnerId });
            object? today = null;
            if (w.Kind == "official" && me is Guid uid)
            {
                var t = (await wheels.TodaySpinsAsync(uid)).FirstOrDefault(s => s.WheelId == w.Id);
                if (t != null) today = SpinView(t);
            }
            return Ok(new
            {
                wheel = DetailView(w, names, isOwner, super || audit),
                today,
                // 前端据此显示「下架 / 恢复」「编辑官方转盘」按钮；真正的权限检查在各个接口里
                canModerate = w.Kind == "official" ? super : audit,
                canManageOfficial = super,
            });
        }

        public sealed class CreateWheelDto : WheelService.WheelInput
        {
            /// <summary>exp 用经验付 / potato 用土豆付</summary>
            public string? PayWith { get; set; }
        }

        /// <summary>做一个转盘（按选的方式扣经验或土豆）</summary>
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateWheelDto dto)
        {
            if (CurrentUserId is not Guid me) return Unauthorized();
            var (costExp, costPotato) = await CostsAsync();
            var (id, error) = await wheels.CreateUserWheelAsync(me, dto, dto.PayWith ?? "", costExp, costPotato);
            if (error != null) return BadRequest(new { message = error });
            var cost = dto.PayWith == "exp" ? costExp : costPotato;
            var paid = cost <= 0 ? "这次免费" : dto.PayWith == "exp" ? $"花了 {cost} 经验" : $"花了 {cost:N0} 土豆";
            var w = await wheels.GetAsync(id);
            return Ok(new { id, shareCode = w?.ShareCode, message = $"转盘做好了（{paid}）" });
        }

        /// <summary>编辑自己的转盘（不收费）</summary>
        [Authorize]
        [HttpPut("{id:long}")]
        public async Task<IActionResult> Update(long id, [FromBody] WheelService.WheelInput dto)
        {
            if (CurrentUserId is not Guid me) return Unauthorized();
            var error = await wheels.UpdateUserWheelAsync(me, id, dto);
            return error != null ? BadRequest(new { message = error }) : Ok(new { message = "已保存" });
        }

        /// <summary>删除自己的转盘（创建时花的经验 / 土豆不退）</summary>
        [Authorize]
        [HttpDelete("{id:long}")]
        public async Task<IActionResult> Delete(long id)
        {
            if (CurrentUserId is not Guid me) return Unauthorized();
            return await wheels.DeleteUserWheelAsync(me, id)
                ? Ok(new { message = "已删除" })
                : NotFound(new { message = "这个转盘不存在，或者不是你的" });
        }

        /// <summary>用户转盘转了一次：次数 +1（不记录转到什么）。游客也算</summary>
        [HttpPost("{id:long}/spin-count")]
        public async Task<IActionResult> CountSpin(long id)
        {
            var who = CurrentUserId?.ToString() ?? "ip:" + (HttpContext.Connection.RemoteIpAddress?.ToString() ?? "?");
            var counted = await wheels.CountSpinAsync(id, who);
            return Ok(new { counted });
        }

        // ==================== 官方转盘 ====================

        /// <summary>转一次官方转盘：结果由服务器决定，前端只负责把动画转到 segmentIndex</summary>
        [Authorize]
        [HttpPost("official/{id:long}/spin")]
        public async Task<IActionResult> SpinOfficial(long id)
        {
            if (CurrentUserId is not Guid me) return Unauthorized();
            var r = await wheels.SpinOfficialAsync(me, id);
            if (!r.Ok || r.Segment == null)
            {
                // 今天转过了：把今天的结果一起给前端，直接显示
                var today = (await wheels.TodaySpinsAsync(me)).FirstOrDefault(s => s.WheelId == id);
                return BadRequest(new { message = r.Error ?? "没转成，稍后再试", today = today == null ? null : SpinView(today) });
            }
            return Ok(new
            {
                segmentIndex = r.SegmentIndex,
                segment = new
                {
                    text = r.Segment.Text,
                    color = r.Segment.Color,
                    prizeType = r.Segment.PrizeType,
                    prizeAmount = r.Segment.PrizeAmount,
                    prizeLabel = WheelService.PrizeLabel(r.Segment.PrizeType, r.Segment.PrizeAmount),
                },
                // 管理员可能刚改过扇区，把服务器这一刻的扇区也给前端，保证动画停的位置和结果对得上
                segments = (r.Segments ?? new List<WheelService.Segment>()).Select(s => new
                {
                    text = s.Text, color = s.Color, weight = s.Weight, prizeType = s.PrizeType, prizeAmount = s.PrizeAmount,
                    prizeLabel = WheelService.PrizeLabel(s.PrizeType, s.PrizeAmount),
                }),
                message = r.Message,
            });
        }

        /// <summary>我今天转过哪些官方转盘 + 最近的抽奖记录（顺便补发之前没发成功的土豆）</summary>
        [Authorize]
        [HttpGet("official/me")]
        public async Task<IActionResult> OfficialMe()
        {
            if (CurrentUserId is not Guid me) return Unauthorized();
            await wheels.RetryPendingAsync(me);
            var today = await wheels.TodaySpinsAsync(me);
            var records = await wheels.MySpinsAsync(me, 20);
            return Ok(new { today = today.Select(SpinView), records = records.Select(SpinView) });
        }

        /// <summary>全站最近中奖：只给用户名和奖品</summary>
        [HttpGet("official/feed")]
        public async Task<IActionResult> Feed()
        {
            var rows = await wheels.RecentWinsAsync(20);
            var names = await NamesAsync(rows.Select(r => r.UserId));
            return Ok(rows.Where(r => names.ContainsKey(r.UserId)).Select(r => new
            {
                id = r.Id,
                name = names[r.UserId],
                prize = WheelService.PrizeLabel(r.PrizeType, r.PrizeAmount),
            }));
        }

        // ==================== 管理 ====================

        /// <summary>所有官方转盘（包括下架的），管理用</summary>
        [Authorize]
        [HttpGet("admin/official")]
        public async Task<IActionResult> AdminOfficialList()
        {
            if (!(await AdminLevelAsync()).Super) return Forbid();
            var rows = await wheels.ListOfficialAsync(true);
            var names = new Dictionary<Guid, string>();
            return Ok(rows.Select(w => DetailView(w, names, false, true)));
        }

        [Authorize]
        [HttpPost("admin/official")]
        public async Task<IActionResult> AdminOfficialCreate([FromBody] WheelService.WheelInput dto)
        {
            if (!(await AdminLevelAsync()).Super || CurrentUserId is not Guid me) return Forbid();
            var (id, error) = await wheels.CreateOfficialAsync(me, dto);
            return error != null ? BadRequest(new { message = error }) : Ok(new { id, message = "官方转盘已创建并上架" });
        }

        [Authorize]
        [HttpPut("admin/official/{id:long}")]
        public async Task<IActionResult> AdminOfficialUpdate(long id, [FromBody] WheelService.WheelInput dto)
        {
            if (!(await AdminLevelAsync()).Super) return Forbid();
            var error = await wheels.UpdateOfficialAsync(id, dto);
            return error != null ? BadRequest(new { message = error }) : Ok(new { message = "已保存（今天已经转过的人不受影响）" });
        }

        [Authorize]
        [HttpDelete("admin/official/{id:long}")]
        public async Task<IActionResult> AdminOfficialDelete(long id)
        {
            if (!(await AdminLevelAsync()).Super) return Forbid();
            return await wheels.DeleteOfficialAsync(id)
                ? Ok(new { message = "已删除（抽奖记录保留）" })
                : NotFound(new { message = "这个官方转盘不存在" });
        }

        public sealed class StatusDto
        {
            /// <summary>active 上架 / 恢复；hidden 下架</summary>
            public string? Status { get; set; }
            /// <summary>下架原因（用户转盘会告诉主人）</summary>
            public string? Reason { get; set; }
        }

        /// <summary>
        /// 上架 / 下架。官方转盘：超级管理员；用户转盘（处理举报）：超级管理员或有「用户审核」权限的管理员，会通知转盘主人。
        /// </summary>
        [Authorize]
        [HttpPost("admin/{id:long}/status")]
        public async Task<IActionResult> AdminSetStatus(long id, [FromBody] StatusDto dto)
        {
            if (dto.Status is not ("active" or "hidden")) return BadRequest(new { message = "状态不对" });
            var w = await wheels.GetAsync(id);
            if (w == null) return NotFound(new { message = "这个转盘不存在" });
            var (super, audit) = await AdminLevelAsync();
            if (w.Kind == "official" ? !super : !audit) return Forbid();
            await wheels.SetStatusAsync(id, dto.Status, dto.Reason);

            if (w.Kind == "user" && w.Status != dto.Status)
            {
                var reason = WheelService.CleanText(dto.Reason, 200);
                var content = dto.Status == "hidden"
                    ? $"你的转盘「{w.Title}」被管理员下架了{(reason.Length > 0 ? "：" + reason : "")}。现在只有你自己能看到，改好后可以联系管理员恢复。"
                    : $"你的转盘「{w.Title}」已经恢复了";
                new NotificationService(db).Add(w.OwnerId, CurrentUserId, "system", "wheel", w.Id.ToString(), content, $"/games?game=Wheel&wheel={w.Id}");
                await db.SaveChangesAsync();
            }
            return Ok(new { message = dto.Status == "hidden" ? "已下架" : "已上架" });
        }

        public sealed class SettingsDto
        {
            public int CostExp { get; set; }
            public int CostPotato { get; set; }
        }

        /// <summary>创建转盘的价格（站长调）。-1 表示不允许用这种方式付，0 表示免费</summary>
        [Authorize]
        [HttpGet("admin/settings")]
        public async Task<IActionResult> AdminSettings()
        {
            if (!(await AdminLevelAsync()).Super) return Forbid();
            var (costExp, costPotato) = await CostsAsync();
            return Ok(new
            {
                costExp,
                costPotato,
                keys = new { exp = WheelService.CostExpKey, potato = WheelService.CostPotatoKey },
                defaults = new { exp = WheelService.DefaultCostExp, potato = WheelService.DefaultCostPotato },
            });
        }

        [Authorize]
        [HttpPut("admin/settings")]
        public async Task<IActionResult> AdminSaveSettings([FromBody] SettingsDto dto)
        {
            if (!(await AdminLevelAsync()).Super) return Forbid();
            if (dto.CostExp < -1 || dto.CostExp > 1_000_000 || dto.CostPotato < -1 || dto.CostPotato > 1_000_000)
                return BadRequest(new { message = "价格要在 -1 到 1000000 之间（-1 = 不允许这种付法，0 = 免费）" });
            if (dto.CostExp < 0 && dto.CostPotato < 0) return BadRequest(new { message = "至少要留一种付费方式，不然谁都做不了转盘" });
            await UpsertConfigAsync(WheelService.CostExpKey, dto.CostExp, "做一个转盘要花多少经验（-1 = 不允许用经验付，0 = 免费）");
            await UpsertConfigAsync(WheelService.CostPotatoKey, dto.CostPotato, "做一个转盘要花多少土豆（-1 = 不允许用土豆付，0 = 免费）");
            await db.SaveChangesAsync();
            // SystemConfigService 缓存 10 分钟，改完马上清掉，新价格立刻生效
            config.ClearCache();
            return Ok(new { message = "价格已保存，马上生效" });
        }

        private async Task UpsertConfigAsync(string key, int value, string description)
        {
            var row = await db.SystemConfigs.FirstOrDefaultAsync(c => c.Key == key);
            if (row == null)
            {
                row = new TaiChuWeb_V2.Models.System.SystemConfig { Key = key, Description = description };
                db.SystemConfigs.Add(row);
            }
            row.Value = value.ToString();
            row.UpdatedAt = DateTime.UtcNow;
            row.UpdatedBy = CurrentUserId?.ToString();
        }
    }
}
