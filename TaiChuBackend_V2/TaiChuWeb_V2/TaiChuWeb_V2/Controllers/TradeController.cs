using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Trade;
using TaiChuWeb_V2.Models.User;
using TaiChuWeb_V2.Services.Games;
using TaiChuWeb_V2.Services.Personalization;
using TaiChuWeb_V2.Services.Shop;

namespace TaiChuWeb_V2.Controllers
{
    /// <summary>
    /// 交易行：用经验或土豆兑换站内资源。
    /// · 经验：什么都能买，包括游戏 CDKey（卡密）和资源链接；
    /// · 土豆：只能买后台设了土豆价、并且不发卡密 / 链接的商品（头衔、头像框、改名卡、配额等站内东西）。
    /// · 价格 = 基础价 × 增长系数 ^ 自己已经买过的次数（经验价、土豆价同一个系数）。
    /// · 头衔、头像框买了进「我的装扮」，在这里佩戴；官方背景买了在「个人中心 → 网站背景」里使用；改名卡买了攒着，改名时用掉一张。
    /// </summary>
    [Authorize]
    [ApiController]
    [Route("api/trade")]
    public class TradeController(AppDbContext _context, ShopService shop, PotatoService potatoes, UserAppearanceService appearance, ProfileDecorService decor, ILogger<TradeController> logger) : ControllerBase
    {
        public sealed class EquipDto
        {
            public string Kind { get; set; } = string.Empty;
            public string? Key { get; set; }
        }

        private string GetCurrentUserId() => User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? string.Empty;

        public static long Price(long baseCost, double multiplier, int count)
            => (long)(baseCost * Math.Pow(multiplier <= 0 ? 1 : multiplier, Math.Max(0, count)));

        /// <summary>只能用经验买的商品：发卡密、发链接（游戏 CDKey、网盘资源这类有现实价值的东西）</summary>
        public static bool IsExpOnly(StoreItem item) => item.Delivery != DeliveryType.None;

        /// <summary>1. 上架中的商品（带自己的购买次数、当前经验价 / 土豆价、是否已拥有、剩余库存）</summary>
        [HttpGet("items")]
        public async Task<IActionResult> GetActiveItems()
        {
            Guid.TryParse(GetCurrentUserId(), out var userId);
            var items = await _context.StoreItems.AsNoTracking()
                .Where(i => i.IsActive)
                .OrderByDescending(i => i.BaseWeight).ThenBy(i => i.SortOrder).ThenBy(i => i.Id)
                .ToListAsync();
            var ids = items.Select(i => i.Id).ToList();
            var counts = await _context.UserPurchaseProgress.AsNoTracking()
                .Where(p => p.UserId == userId && ids.Contains(p.StoreItemId))
                .ToDictionaryAsync(p => p.StoreItemId, p => p.PurchaseCount);
            var secretsLeft = await _context.StoreItemSecrets.AsNoTracking()
                .Where(s => ids.Contains(s.StoreItemId) && !s.IsUsed)
                .GroupBy(s => s.StoreItemId)
                .Select(g => new { g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            var ext = await shop.ExtAsync(ids);
            var owned = userId == Guid.Empty
                ? new HashSet<string>()
                : (await shop.CosmeticsAsync(userId)).Select(c => $"{c.Kind}:{c.Key}").ToHashSet();
            // 背景商品：带上背景的图，前端做预览
            var previews = new Dictionary<string, object>();
            if (ext.Values.Any(x => x.Effect == ShopService.EffectBackground))
                foreach (var p in await appearance.ListPresetsAsync(enabledOnly: false))
                    previews[$"{ShopService.EffectBackground}:{p.Id}"] = new { p.Title, p.Kind, p.Url, p.Poster, Slot = "site" };
            if (ext.Values.Any(x => x.Effect == ShopService.EffectAvatar))
                foreach (var a in await shop.ListAvatarDefsAsync(enabledOnly: false))
                    previews[$"{ShopService.EffectAvatar}:{a.Id}"] = new { Title = a.Name, Kind = a.Animated ? "animated" : "image", a.Url, Poster = (string?)null, Slot = "avatar" };
            if (ext.Values.Any(x => x.Effect == ShopService.EffectProfileBg))
                foreach (var p in await decor.ListPresetsAsync(enabledOnly: false))
                    previews[$"{ShopService.EffectProfileBg}:{p.Id}"] = new { p.Title, p.Kind, p.Url, p.Poster, p.Slot };

            return Ok(items.Select(item =>
            {
                ext.TryGetValue(item.Id, out var x);
                var count = counts.GetValueOrDefault(item.Id);
                var expOnly = IsExpOnly(item);
                var potatoBase = expOnly ? null : x?.PotatoCost;
                int? stock = item.Delivery == DeliveryType.SecretKey ? secretsLeft.GetValueOrDefault(item.Id) : item.GlobalStock;
                var isCosmetic = ShopService.IsCosmetic(x?.Effect);
                var preview = x?.EffectValue != null ? previews.GetValueOrDefault($"{x.Effect}:{x.EffectValue}") : null;
                return new
                {
                    item.Id,
                    item.Name,
                    item.Description,
                    item.Benefit,
                    item.Category,
                    item.BaseCost,
                    item.PriceMultiplier,
                    item.Delivery,
                    item.BaseWeight,
                    PurchaseCount = count,
                    ExpCost = Price(item.BaseCost, item.PriceMultiplier, count),
                    PotatoCost = potatoBase is > 0 ? Price(potatoBase.Value, item.PriceMultiplier, count) : (long?)null,
                    ExpOnly = expOnly,
                    Effect = x?.Effect,
                    EffectValue = x?.EffectValue,
                    Owned = isCosmetic && owned.Contains($"{x!.Effect}:{x.EffectValue}"),
                    Stock = stock,
                    Preview = preview,
                };
            }));
        }

        /// <summary>2. 当前账户：经验、等级、土豆、配额（实时统计）</summary>
        [HttpGet("status")]
        public async Task<IActionResult> GetMyStatus()
        {
            var userIdStr = GetCurrentUserId();
            if (!Guid.TryParse(userIdStr, out var userId))
                return Unauthorized(TradeResult.Fail("令牌无效"));

            var user = await _context.Users.Include(u => u.Stats).FirstOrDefaultAsync(u => u.Id == userId);
            if (user?.Stats == null) return NotFound(TradeResult.Fail("统计数据丢失"));

            var userSpaceIds = await _context.Spaces.Where(s => s.UserId == userIdStr).Select(s => s.Id).ToListAsync();
            int realUsedSpaces = userSpaceIds.Count;
            int realUsedNotes = await _context.Notes.CountAsync(n => userSpaceIds.Contains(n.SpaceId));
            int realUsedProjects = await _context.Projects.CountAsync(p => p.OwnerId == userId);
            int realUsedWorlds = await _context.WorldProjects.CountAsync(p => p.OwnerId == userId);
            long potatoBalance = 0;
            try { potatoBalance = (await potatoes.GetAsync(userId)).Balance; } catch (Exception ex) { logger.LogWarning(ex, "读取土豆余额失败"); }

            return Ok(new
            {
                Experience = user.Stats.Experience,
                Level = user.Stats.Level,
                Potatoes = potatoBalance,
                MaxSpaces = user.Stats.MaxSpaces,
                MaxNotes = user.Stats.MaxNotes,
                MaxProjectCount = user.Stats.MaxProjectCount,
                UsedSpaces = realUsedSpaces,
                UsedNotes = realUsedNotes,
                UsedProjectCount = realUsedProjects,
                // 世界观：创建数量 / 上限，每个世界的节点上限（所有世界通用）
                UsedWorldCount = realUsedWorlds,
                MaxWorldCount = user.Stats.MaxWorldCount,
                MaxCardsPerWorld = user.Stats.MaxCardsPerWorld
            });
        }

        /// <summary>
        /// 3. 兑换。currency = exp（默认）/ potato。
        /// 土豆在 EF 事务之外扣（土豆表不归 EF 管），后面任何一步失败都会把土豆退回去。
        /// </summary>
        [HttpPost("purchase/{itemId}")]
        public async Task<IActionResult> Purchase(int itemId, [FromQuery] string? currency = "exp")
        {
            if (!Guid.TryParse(GetCurrentUserId(), out var userId))
                return Unauthorized(TradeResult.Fail("身份核验失败"));
            var payPotato = string.Equals(currency, "potato", StringComparison.OrdinalIgnoreCase);

            var item = await _context.StoreItems.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemId);
            if (item == null || !item.IsActive) return BadRequest(TradeResult.Fail("这件商品不存在或已经下架"));
            var ext = (await shop.ExtAsync(new[] { itemId })).GetValueOrDefault(itemId);
            var effect = ext?.Effect;
            if (ShopService.IsCosmetic(effect) && ext!.EffectValue != null
                && await shop.OwnsAsync(userId, effect!, ext.EffectValue))
                return BadRequest(TradeResult.Fail("你已经拥有了，去「我的装扮」里佩戴吧"));

            long potatoPaid = 0;
            if (payPotato)
            {
                if (IsExpOnly(item)) return BadRequest(TradeResult.Fail("游戏 CDKey 和资源链接只能用经验兑换"));
                if (ext?.PotatoCost is not > 0) return BadRequest(TradeResult.Fail("这件商品不能用土豆买"));
                var count = await _context.UserPurchaseProgress.AsNoTracking()
                    .Where(p => p.UserId == userId && p.StoreItemId == itemId).Select(p => p.PurchaseCount).FirstOrDefaultAsync();
                var cost = Price(ext.PotatoCost.Value, item.PriceMultiplier, count);
                var (ok, _, err) = await potatoes.SpendAsync(userId, cost, $"交易行：{item.Name}", "shop", $"store:{itemId}");
                if (!ok) return BadRequest(TradeResult.Fail(err ?? "土豆不够"));
                potatoPaid = cost;
            }

            async Task Refund()
            {
                if (potatoPaid <= 0) return;
                try { await potatoes.RefundAsync(userId, potatoPaid, $"交易行退款：{item.Name}", "shop-refund", $"store:{itemId}"); }
                catch (Exception ex) { logger.LogError(ex, "交易行土豆退款失败 user={User} item={Item} amount={Amount}", userId, itemId, potatoPaid); }
            }

            string deliveryData;
            try
            {
                var strategy = _context.Database.CreateExecutionStrategy();
                var outcome = await strategy.ExecuteAsync<(string? Error, string Data)>(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync();
                    var tracked = await _context.StoreItems.FirstOrDefaultAsync(i => i.Id == itemId);
                    var user = await _context.Users.Include(u => u.Stats).FirstOrDefaultAsync(u => u.Id == userId);
                    if (tracked == null || user?.Stats == null) return (Error: "商品或账户不存在", Data: "");

                    var progress = await _context.UserPurchaseProgress.FirstOrDefaultAsync(p => p.UserId == userId && p.StoreItemId == itemId)
                        ?? new UserPurchaseProgress { UserId = userId, StoreItemId = itemId, PurchaseCount = 0 };

                    // A. 扣经验（原子：余额不够一行都不会改）
                    if (!payPotato)
                    {
                        var cost = Price(tracked.BaseCost, tracked.PriceMultiplier, progress.PurchaseCount);
                        if (cost > int.MaxValue) return (Error: "单次兑换经验超出流水支持范围", Data: "");
                        if (cost > 0)
                        {
                            var updated = await _context.UserStats
                                .Where(s => s.UserId == userId && s.Experience >= cost)
                                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Experience, x => x.Experience - cost));
                            if (updated == 0) return (Error: $"经验不足，需要 {cost}", Data: "");
                            _context.UserExpLogs.Add(new UserExpLog { UserId = userId, Change = checked((int)-cost), Reason = "经验兑换：" + tracked.Name, CreatedAt = DateTime.UtcNow });
                        }
                    }

                    // B. 限量库存（原子扣减）
                    if (tracked.GlobalStock != null)
                    {
                        var left = await _context.StoreItems
                            .Where(i => i.Id == itemId && i.GlobalStock > 0)
                            .ExecuteUpdateAsync(s => s.SetProperty(x => x.GlobalStock, x => x.GlobalStock - 1));
                        if (left == 0) return (Error: "已经卖完了", Data: "");
                    }

                    progress.PurchaseCount++;
                    if (_context.Entry(progress).State == EntityState.Detached) _context.UserPurchaseProgress.Add(progress);

                    // C. 配额类：按「字段名:增量」给 UserStats 加上限
                    if (tracked.Delivery == DeliveryType.None && !string.IsNullOrWhiteSpace(tracked.StaticPayload))
                    {
                        var parts = tracked.StaticPayload.Split(':');
                        if (parts.Length == 2 && int.TryParse(parts[1], out var valueToAdd))
                        {
                            var propertyName = parts[0].Trim();
                            var prop = typeof(UserStats).GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                            if (prop != null && prop.CanWrite && prop.PropertyType == typeof(int) && prop.Name != nameof(UserStats.Experience))
                            {
                                var currentVal = (int)prop.GetValue(user.Stats)!;
                                prop.SetValue(user.Stats, currentVal + valueToAdd);
                                _context.Entry(user.Stats).Property(prop.Name).IsModified = true;
                            }
                        }
                    }

                    // D. 交付卡密 / 链接
                    var data = "";
                    if (tracked.Delivery == DeliveryType.SecretKey)
                    {
                        // 加行锁取一条没用过的卡密，两个人同时买也不会拿到同一个
                        var et = _context.Model.FindEntityType(typeof(StoreItemSecret))!;
                        var table = et.GetTableName()!;
                        var store = StoreObjectIdentifier.Table(table, et.GetSchema());
                        string Col(string p) => et.FindProperty(p)!.GetColumnName(store)!;
                        var sql = $"SELECT * FROM `{table}` WHERE `{Col(nameof(StoreItemSecret.StoreItemId))}` = {{0}} AND `{Col(nameof(StoreItemSecret.IsUsed))}` = 0 ORDER BY `{Col(nameof(StoreItemSecret.Id))}` LIMIT 1 FOR UPDATE";
                        var secret = (await _context.StoreItemSecrets.FromSqlRaw(sql, itemId).ToListAsync()).FirstOrDefault();
                        if (secret == null) return (Error: "卡密已经发完了", Data: "");
                        secret.IsUsed = true;
                        secret.AssignedUserId = userId;
                        secret.AssignedAt = DateTime.UtcNow;
                        data = secret.SecretCode;
                    }
                    else if (tracked.Delivery == DeliveryType.Link)
                    {
                        data = tracked.StaticPayload ?? "";
                    }

                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                    return (Error: null, Data: data);
                });
                if (outcome.Error != null)
                {
                    await Refund();
                    return BadRequest(TradeResult.Fail(outcome.Error));
                }
                deliveryData = outcome.Data;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "交易行兑换失败 user={User} item={Item}", userId, itemId);
                await Refund();
                return StatusCode(500, TradeResult.Fail("兑换失败，请稍后重试"));
            }

            // E. 装饰、改名卡（单独的表，事务提交后发放；发放可以重复执行，不会重复给）
            var message = $"「{item.Name}」兑换成功";
            try
            {
                switch (effect)
                {
                    case ShopService.EffectTitle when ext!.EffectValue != null:
                        await shop.GrantCosmeticAsync(userId, ShopService.EffectTitle, ext.EffectValue, $"交易行 #{itemId}");
                        message = $"获得头衔「{ext.EffectValue}」，去「我的装扮」里佩戴吧";
                        break;
                    case ShopService.EffectFrame when ext!.EffectValue != null:
                        await shop.GrantCosmeticAsync(userId, ShopService.EffectFrame, ext.EffectValue, $"交易行 #{itemId}");
                        message = $"获得头像框「{item.Name}」，去「我的装扮」里佩戴吧";
                        break;
                    case ShopService.EffectAvatar when ext!.EffectValue != null:
                        await shop.GrantCosmeticAsync(userId, ShopService.EffectAvatar, ext.EffectValue, $"交易行 #{itemId}");
                        message = $"获得头像「{item.Name}」，在个人中心点自己的头像就能换上";
                        break;
                    case ShopService.EffectProfileBg when ext!.EffectValue != null:
                        await shop.GrantCosmeticAsync(userId, ShopService.EffectProfileBg, ext.EffectValue, $"交易行 #{itemId}");
                        message = $"获得主页素材「{item.Name}」，去「个人中心 → 主页装扮」里换上吧";
                        break;
                    case ShopService.EffectBackground when ext!.EffectValue != null:
                        await shop.GrantCosmeticAsync(userId, ShopService.EffectBackground, ext.EffectValue, $"交易行 #{itemId}");
                        message = $"获得背景「{item.Name}」，去「个人中心 → 网站背景」里换上吧";
                        break;
                    case ShopService.EffectRename:
                        await shop.AddCountAsync(userId, ShopService.RenameCardKey, 1);
                        message = "获得 1 张改名卡，在个人中心点「✎ 改名卡」使用";
                        break;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "交易行发放道具失败 user={User} item={Item}", userId, itemId);
                return StatusCode(500, TradeResult.Fail("已经扣费但道具发放失败，请联系管理员补发（已记录日志）"));
            }

            var result = TradeResult.Success(deliveryData);
            result.Message = message;
            return Ok(result);
        }

        /// <summary>4. 我的装扮：拥有的头衔、头像框，正在佩戴的，改名卡数量</summary>
        [HttpGet("inventory")]
        public async Task<IActionResult> Inventory()
        {
            if (!Guid.TryParse(GetCurrentUserId(), out var userId)) return Unauthorized();
            var title = await _context.UserStats.AsNoTracking().Where(s => s.UserId == userId).Select(s => s.Title).FirstOrDefaultAsync();
            var owned = await shop.CosmeticsAsync(userId);
            // 以前就有的头衔（比如管理员给的）也算拥有，换了别的还能换回来
            if (!string.IsNullOrWhiteSpace(title) && !owned.Any(c => c.Kind == ShopService.EffectTitle && c.Key == title))
            {
                await shop.GrantCosmeticAsync(userId, ShopService.EffectTitle, title, "原有头衔");
                owned = await shop.CosmeticsAsync(userId);
            }
            return Ok(new
            {
                titles = owned.Where(c => c.Kind == ShopService.EffectTitle).Select(c => new { key = c.Key, source = c.Source, at = c.CreatedAt }),
                frames = owned.Where(c => c.Kind == ShopService.EffectFrame).Select(c => new { key = c.Key, source = c.Source, at = c.CreatedAt }),
                backgrounds = owned.Where(c => c.Kind == ShopService.EffectBackground).Select(c => new { key = c.Key, source = c.Source, at = c.CreatedAt }),
                profileBackgrounds = owned.Where(c => c.Kind == ShopService.EffectProfileBg).Select(c => new { key = c.Key, source = c.Source, at = c.CreatedAt }),
                avatars = owned.Where(c => c.Kind == ShopService.EffectAvatar).Select(c => new { key = c.Key, source = c.Source, at = c.CreatedAt }),
                equippedTitle = string.IsNullOrWhiteSpace(title) ? null : title,
                equippedFrame = await shop.EquippedFrameAsync(userId),
                renameCards = await shop.CountAsync(userId, ShopService.RenameCardKey),
            });
        }

        /// <summary>5. 佩戴 / 取下（key 为空表示取下）</summary>
        [HttpPost("equip")]
        public async Task<IActionResult> Equip([FromBody] EquipDto dto)
        {
            if (!Guid.TryParse(GetCurrentUserId(), out var userId)) return Unauthorized();
            var key = string.IsNullOrWhiteSpace(dto.Key) ? null : dto.Key.Trim();
            if (dto.Kind is not (ShopService.EffectTitle or ShopService.EffectFrame)) return BadRequest(new { message = "不支持的装扮类型" });
            if (key != null && !await shop.OwnsAsync(userId, dto.Kind, key)) return BadRequest(new { message = "你还没有这个装扮" });
            if (dto.Kind == ShopService.EffectTitle)
            {
                var stats = await _context.UserStats.FirstOrDefaultAsync(s => s.UserId == userId);
                if (stats == null) return NotFound(new { message = "账户数据不存在" });
                // 取下时存空字符串（Title 列在有的库里不允许 NULL），前端会显示默认的「太初散修」
                stats.Title = key ?? string.Empty;
                await _context.SaveChangesAsync();
            }
            else await shop.SetFrameAsync(userId, key);
            return Ok(new { kind = dto.Kind, key });
        }

        /// <summary>6. 某个人佩戴的头衔和头像框（个人主页等地方显示用，不用登录）</summary>
        [AllowAnonymous]
        [HttpGet("appearance/{userId:guid}")]
        public async Task<IActionResult> Appearance(Guid userId)
        {
            var title = await _context.UserStats.AsNoTracking().Where(s => s.UserId == userId).Select(s => s.Title).FirstOrDefaultAsync();
            return Ok(new { title, frame = await shop.EquippedFrameAsync(userId) });
        }

        /// <summary>9. 官方头像列表（静态 / 动图）。上架成商品的带 locked、storeItemId；已兑换或是原作者的带 owned</summary>
        [HttpGet("avatars")]
        public async Task<IActionResult> Avatars()
        {
            if (!Guid.TryParse(GetCurrentUserId(), out var userId)) return Unauthorized();
            var list = await shop.ListAvatarDefsAsync(enabledOnly: true);
            var forSale = await shop.ItemsForEffectAsync(ShopService.EffectAvatar);
            var owned = (await shop.CosmeticsAsync(userId)).Where(c => c.Kind == ShopService.EffectAvatar).Select(c => c.Key).ToHashSet();
            var authorIds = list.Where(a => a.AuthorId != null).Select(a => a.AuthorId!.Value).Distinct().ToList();
            var names = authorIds.Count == 0 ? new Dictionary<Guid, string>()
                : await _context.Users.AsNoTracking().Where(u => authorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
            return Ok(list.Select(a => new
            {
                id = a.Id, name = a.Name, url = a.Url, animated = a.Animated,
                locked = forSale.ContainsKey(a.Id),
                owned = owned.Contains(a.Id.ToString()),
                storeItemId = forSale.TryGetValue(a.Id, out var itemId) ? itemId : (int?)null,
                author = a.AuthorId is Guid g ? names.GetValueOrDefault(g) : null,
            }));
        }

        /// <summary>10. 换上一个官方头像</summary>
        [HttpPost("avatars/{id:int}/use")]
        public async Task<IActionResult> UseAvatar(int id)
        {
            if (!Guid.TryParse(GetCurrentUserId(), out var userId)) return Unauthorized();
            var def = await shop.GetAvatarDefAsync(id);
            if (def == null || !def.Enabled) return NotFound(new { message = "这个头像已经下架了" });
            if (!await shop.CanUseAvatarAsync(userId, def)) return StatusCode(403, new { message = "这个头像要先在交易行用经验或土豆兑换" });
            var profile = await _context.UserProfiles.FirstOrDefaultAsync(p => p.UserId == userId);
            if (profile == null)
            {
                profile = new UserProfile { UserId = userId };
                _context.UserProfiles.Add(profile);
            }
            profile.Avatar = def.Url;
            await _context.SaveChangesAsync();
            return Ok(new { avatar = def.Url });
        }

        /// <summary>8. 后台上传的图片头像框（全站显示头像框时用，不用登录）。key 是 img-{Id}</summary>
        [AllowAnonymous]
        [HttpGet("frames")]
        public async Task<IActionResult> Frames()
        {
            // 下架的也返回：已经拥有、正在佩戴的人照常显示
            var list = await shop.ListFrameDefsAsync(enabledOnly: false);
            var authors = list.Where(f => f.AuthorId != null).Select(f => f.AuthorId!.Value).Distinct().ToList();
            var names = authors.Count == 0 ? new Dictionary<Guid, string>()
                : await _context.Users.AsNoTracking().Where(u => authors.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
            return Ok(list.Select(f => new
            {
                key = f.Key, name = f.Name, url = f.Url, scale = f.Scale, enabled = f.Enabled,
                author = f.AuthorId is Guid a ? names.GetValueOrDefault(a) : null,
            }));
        }

        /// <summary>
        /// 7. 一批人佩戴的头衔和头像框（论坛、评论、聊天室、榜单、游戏房间显示用，不用登录）。
        /// ids 用逗号分隔，一次最多 100 个；返回 { "用户ID": { title, frame } }，没戴的是 null。
        /// </summary>
        [AllowAnonymous]
        [HttpGet("appearances")]
        public async Task<IActionResult> Appearances([FromQuery] string? ids)
        {
            var list = (ids ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
                .Where(g => g != Guid.Empty)
                .Distinct()
                .Take(100)
                .ToList();
            var result = new Dictionary<string, object>();
            if (list.Count == 0) return Ok(result);
            var titles = await _context.UserStats.AsNoTracking()
                .Where(s => list.Contains(s.UserId))
                .Select(s => new { s.UserId, s.Title })
                .ToListAsync();
            var titleOf = titles.GroupBy(t => t.UserId).ToDictionary(g => g.Key, g => g.First().Title);
            var frames = await shop.EquippedFramesAsync(list);
            foreach (var id in list)
            {
                var title = titleOf.GetValueOrDefault(id);
                result[id.ToString()] = new
                {
                    title = string.IsNullOrWhiteSpace(title) ? null : title,
                    frame = frames.GetValueOrDefault(id),
                };
            }
            return Ok(result);
        }
    }
}
