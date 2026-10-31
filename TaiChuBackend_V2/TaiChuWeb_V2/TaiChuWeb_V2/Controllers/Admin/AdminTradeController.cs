using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TaiChuWeb_V2.DbContext;
using TaiChuWeb_V2.Models.Trade;
using TaiChuWeb_V2.Services.Cos;
using TaiChuWeb_V2.Services.Personalization;
using TaiChuWeb_V2.Services.Shop;

namespace TaiChuWeb_V2.Controllers.Admin
{
    /// <summary>后台编辑商品用的表单（StoreItem 的字段 + 土豆价、效果）</summary>
    public sealed class StoreItemDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Benefit { get; set; } = string.Empty;
        public ItemCategory Category { get; set; }
        public long BaseCost { get; set; }
        public double PriceMultiplier { get; set; } = 1.0;
        public int? GlobalStock { get; set; }
        public int BaseWeight { get; set; }
        public int SortOrder { get; set; }
        public bool IsActive { get; set; } = true;
        public DeliveryType Delivery { get; set; } = DeliveryType.None;
        public string? StaticPayload { get; set; }
        /// <summary>土豆价（空 = 不能用土豆买）；发卡密 / 链接的商品会被强制清空</summary>
        public long? PotatoCost { get; set; }
        /// <summary>效果：title 头衔 / frame 头像框 / bg 官方背景 / rename 改名卡 / 空</summary>
        public string? Effect { get; set; }
        public string? EffectValue { get; set; }
    }

    /// <summary>图片头像框（后台上传）</summary>
    public sealed class FrameDefDto
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        /// <summary>框图相对头像的大小：1.0–2.0</summary>
        public double Scale { get; set; } = 1.4;
        public Guid? AuthorId { get; set; }
        public int SortOrder { get; set; }
        public bool Enabled { get; set; } = true;
    }

    /// <summary>官方头像</summary>
    public sealed class AvatarDefDto
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool Animated { get; set; }
        public Guid? AuthorId { get; set; }
        public int SortOrder { get; set; }
        public bool Enabled { get; set; } = true;
    }

    public sealed class RestockDto
    {
        public List<string> Secrets { get; set; } = new();
    }

    /// <summary>后台：交易行商品管理（没有单独授权映射时由 AdminAccessFilter 限制为超级管理员）</summary>
    [Authorize]
    [ApiController]
    [Route("api/admin/trade")]
    public class AdminTradeController(AppDbContext _context, ShopService shop, UserAppearanceService appearance, ProfileDecorService decor, CosService cos) : ControllerBase
    {
        private async Task<List<object>> ViewAsync(List<StoreItem> items)
        {
            var ids = items.Select(i => i.Id).ToList();
            var ext = await shop.ExtAsync(ids);
            var secrets = await _context.StoreItemSecrets.AsNoTracking()
                .Where(s => ids.Contains(s.StoreItemId))
                .GroupBy(s => s.StoreItemId)
                .Select(g => new { g.Key, Total = g.Count(), Unused = g.Count(s => !s.IsUsed) })
                .ToDictionaryAsync(x => x.Key);
            var sold = await _context.UserPurchaseProgress.AsNoTracking()
                .Where(p => ids.Contains(p.StoreItemId))
                .GroupBy(p => p.StoreItemId)
                .Select(g => new { g.Key, Count = g.Sum(p => p.PurchaseCount) })
                .ToDictionaryAsync(x => x.Key, x => x.Count);
            return items.Select(i =>
            {
                ext.TryGetValue(i.Id, out var x);
                secrets.TryGetValue(i.Id, out var s);
                return (object)new
                {
                    i.Id, i.Name, i.Description, i.Benefit, i.Category, i.BaseCost, i.PriceMultiplier, i.GlobalStock,
                    i.BaseWeight, i.SortOrder, i.IsActive, i.CreatedAt, i.Delivery, i.StaticPayload,
                    PotatoCost = x?.PotatoCost, Effect = x?.Effect, EffectValue = x?.EffectValue,
                    SecretsTotal = s?.Total ?? 0, SecretsUnused = s?.Unused ?? 0,
                    Sold = sold.GetValueOrDefault(i.Id),
                };
            }).ToList();
        }

        private static string? Validate(StoreItemDto dto, out string? effect, out string? effectValue)
        {
            effect = null; effectValue = null;
            dto.Name = (dto.Name ?? "").Trim();
            dto.Benefit = (dto.Benefit ?? "").Trim();
            dto.Description = (dto.Description ?? "").Trim();
            if (dto.Name.Length is 0 or > 50) return "名称需要 1–50 个字";
            if (dto.Benefit.Length > 20) return "收益简述最多 20 个字";
            if (dto.Description.Length > 200) return "描述最多 200 个字";
            if (dto.BaseCost < 0) return "经验价不能是负数";
            if (dto.PriceMultiplier is < 1 or > 10 || double.IsNaN(dto.PriceMultiplier)) return "增长系数要在 1–10 之间（1 表示固定价格）";
            if (dto.GlobalStock is < 0) return "库存不能是负数";
            if (dto.PotatoCost is < 0) return "土豆价不能是负数";
            if (ShopService.ValidateExt(dto.Effect, dto.EffectValue, out effect, out effectValue) is { } err) return err;
            if (effect != null && dto.Delivery != DeliveryType.None) return "头衔、头像框、背景、改名卡的交付模式要选「直接交付」";
            if (dto.Delivery == DeliveryType.Link && string.IsNullOrWhiteSpace(dto.StaticPayload)) return "链接交付需要填链接";
            // 发卡密 / 链接的商品（游戏 CDKey、网盘资源）只能用经验买
            if (dto.Delivery != DeliveryType.None) dto.PotatoCost = null;
            return null;
        }

        /// <summary>效果对应的素材要存在；一个背景只能对应一件商品</summary>
        private async Task<string?> CheckBackgroundAsync(string? effect, string? value, int selfId)
        {
            if (effect == ShopService.EffectFrame)
            {
                if (ShopService.ImageFrameId(value) is int fid && await shop.GetFrameDefAsync(fid) == null) return "这个图片头像框不存在";
                return null;
            }
            if (effect is not (ShopService.EffectBackground or ShopService.EffectProfileBg or ShopService.EffectAvatar) || !int.TryParse(value, out var bgId)) return null;
            if (effect == ShopService.EffectAvatar && await shop.GetAvatarDefAsync(bgId) == null) return "这个官方头像不存在，先在「头像素材」里上传";
            if (effect == ShopService.EffectBackground && await appearance.GetPresetAsync(bgId) == null) return "这个官方背景不存在，先在「背景特权」里上传";
            if (effect == ShopService.EffectProfileBg && await decor.GetPresetAsync(bgId) == null) return "这个主页素材不存在，先在「背景特权 → 主页素材」里上传";
            var used = await shop.ItemsForEffectAsync(effect);
            if (used.TryGetValue(bgId, out var other) && other != selfId) return $"这件素材已经在商品 #{other:D3} 里出售了";
            return null;
        }

        private static void Apply(StoreItem item, StoreItemDto dto)
        {
            item.Name = dto.Name;
            item.Description = dto.Description;
            item.Benefit = dto.Benefit;
            item.Category = dto.Category;
            item.BaseCost = dto.BaseCost;
            item.PriceMultiplier = dto.PriceMultiplier;
            item.GlobalStock = dto.GlobalStock;
            item.BaseWeight = dto.BaseWeight;
            item.SortOrder = dto.SortOrder;
            item.IsActive = dto.IsActive;
            item.Delivery = dto.Delivery;
            item.StaticPayload = string.IsNullOrWhiteSpace(dto.StaticPayload) ? null : dto.StaticPayload.Trim();
        }

        // 1. 所有商品（包括下架的），带土豆价、效果、卡密库存、售出数量
        [HttpGet("items")]
        public async Task<IActionResult> GetAllItems()
        {
            var items = await _context.StoreItems.AsNoTracking().OrderByDescending(i => i.CreatedAt).ToListAsync();
            return Ok(await ViewAsync(items));
        }

        // 2. 上架新商品
        [HttpPost("items")]
        public async Task<IActionResult> CreateItem([FromBody] StoreItemDto dto)
        {
            if (Validate(dto, out var effect, out var value) is { } err) return BadRequest(new { message = err });
            if (await CheckBackgroundAsync(effect, value, 0) is { } bgErr) return BadRequest(new { message = bgErr });
            var item = new StoreItem { CreatedAt = DateTime.UtcNow };
            Apply(item, dto);
            _context.StoreItems.Add(item);
            await _context.SaveChangesAsync();
            await shop.SaveExtAsync(item.Id, dto.PotatoCost, effect, value);
            return Ok((await ViewAsync(new List<StoreItem> { item }))[0]);
        }

        // 3. 修改商品
        [HttpPut("items/{id}")]
        public async Task<IActionResult> UpdateItem(int id, [FromBody] StoreItemDto dto)
        {
            if (Validate(dto, out var effect, out var value) is { } err) return BadRequest(new { message = err });
            if (await CheckBackgroundAsync(effect, value, id) is { } bgErr) return BadRequest(new { message = bgErr });
            var item = await _context.StoreItems.FirstOrDefaultAsync(i => i.Id == id);
            if (item == null) return NotFound(new { message = "商品不存在" });
            Apply(item, dto);
            await _context.SaveChangesAsync();
            await shop.SaveExtAsync(item.Id, dto.PotatoCost, effect, value);
            return Ok((await ViewAsync(new List<StoreItem> { item }))[0]);
        }

        // 4. 上架 / 下架
        [HttpPatch("items/{id}/toggle")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var item = await _context.StoreItems.FindAsync(id);
            if (item == null) return NotFound();
            item.IsActive = !item.IsActive;
            await _context.SaveChangesAsync();
            return Ok(new { id = item.Id, isActive = item.IsActive });
        }

        // 5. 补卡密：一行一个，去掉空行和重复（包括池子里已经有的）
        [HttpPost("items/{id}/restock")]
        public async Task<IActionResult> Restock(int id, [FromBody] RestockDto dto)
        {
            var item = await _context.StoreItems.FindAsync(id);
            if (item == null) return NotFound(new { message = "商品不存在" });
            if (item.Delivery != DeliveryType.SecretKey) return BadRequest(new { message = "只有「独立密钥池」交付的商品才能补卡密" });
            var codes = (dto.Secrets ?? new()).Select(s => (s ?? "").Trim()).Where(s => s.Length > 0).Distinct().ToList();
            if (codes.Count == 0) return BadRequest(new { message = "没有收到卡密" });
            if (codes.Count > 2000) return BadRequest(new { message = "一次最多补 2000 个" });
            if (codes.Any(c => c.Length > 500)) return BadRequest(new { message = "单个卡密太长了（最多 500 字）" });
            var existing = await _context.StoreItemSecrets.AsNoTracking()
                .Where(s => s.StoreItemId == id && codes.Contains(s.SecretCode)).Select(s => s.SecretCode).ToListAsync();
            var fresh = codes.Except(existing).ToList();
            foreach (var code in fresh) _context.StoreItemSecrets.Add(new StoreItemSecret { StoreItemId = id, SecretCode = code });
            await _context.SaveChangesAsync();
            var unused = await _context.StoreItemSecrets.CountAsync(s => s.StoreItemId == id && !s.IsUsed);
            return Ok(new { added = fresh.Count, skipped = codes.Count - fresh.Count, unused });
        }

        // 6. 卡密明细（只显示前后几位，领取人和时间）
        [HttpGet("items/{id}/secrets")]
        public async Task<IActionResult> Secrets(int id)
        {
            var rows = await _context.StoreItemSecrets.AsNoTracking()
                .Where(s => s.StoreItemId == id)
                .OrderBy(s => s.IsUsed).ThenByDescending(s => s.Id)
                .Take(500)
                .Select(s => new { s.Id, s.SecretCode, s.IsUsed, s.AssignedUserId, s.AssignedAt })
                .ToListAsync();
            var userIds = rows.Where(r => r.AssignedUserId != null).Select(r => r.AssignedUserId!.Value).Distinct().ToList();
            var names = await _context.Users.AsNoTracking().Where(u => userIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
            static string Mask(string c) => c.Length <= 8 ? new string('•', c.Length) : $"{c[..4]}••••{c[^4..]}";
            return Ok(rows.Select(r => new
            {
                r.Id, code = Mask(r.SecretCode), r.IsUsed, r.AssignedAt,
                assignedTo = r.AssignedUserId is Guid g ? names.GetValueOrDefault(g) ?? g.ToString() : null,
            }));
        }

        // 7. 删掉一个还没发出去的卡密（填错了的时候用）
        [HttpDelete("items/{id}/secrets/{secretId}")]
        public async Task<IActionResult> DeleteSecret(int id, int secretId)
        {
            var s = await _context.StoreItemSecrets.FirstOrDefaultAsync(x => x.Id == secretId && x.StoreItemId == id);
            if (s == null) return NotFound(new { message = "卡密不存在" });
            if (s.IsUsed) return BadRequest(new { message = "已经发出去的卡密不能删" });
            _context.StoreItemSecrets.Remove(s);
            await _context.SaveChangesAsync();
            return Ok(new { ok = true });
        }

        // 8. 图片头像框：列表（包括下架的）
        [HttpGet("frames")]
        public async Task<IActionResult> FrameDefs()
        {
            var list = await shop.ListFrameDefsAsync(enabledOnly: false);
            var authors = list.Where(f => f.AuthorId != null).Select(f => f.AuthorId!.Value).Distinct().ToList();
            var names = authors.Count == 0 ? new Dictionary<Guid, string>()
                : await _context.Users.AsNoTracking().Where(u => authors.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
            return Ok(list.Select(f => new
            {
                f.Id, f.Key, f.Name, f.Url, f.Scale, f.SortOrder, f.Enabled, f.AuthorId,
                AuthorName = f.AuthorId is Guid a ? names.GetValueOrDefault(a) : null,
            }));
        }

        // 9. 新增 / 修改图片头像框（填了原作者会自动发给作者）
        [HttpPut("frames")]
        public async Task<IActionResult> SaveFrameDef([FromBody] FrameDefDto dto, [FromQuery] int? id)
        {
            var name = (dto.Name ?? "").Trim();
            if (name.Length is 0 or > 40) return BadRequest(new { message = "名称需要 1–40 个字" });
            if (!UserAppearanceService.IsSafeUrl(dto.Url)) return BadRequest(new { message = "请先上传框图" });
            if (double.IsNaN(dto.Scale) || dto.Scale is < 1 or > 2) return BadRequest(new { message = "框的大小要在 1.0–2.0 之间" });
            if (id is int existing && await shop.GetFrameDefAsync(existing) == null) return NotFound(new { message = "这个头像框不存在" });
            var author = dto.AuthorId == Guid.Empty ? null : dto.AuthorId;
            if (author is Guid a && !await _context.Users.AnyAsync(u => u.Id == a)) return BadRequest(new { message = "原作者账号不存在" });
            var newId = await shop.SaveFrameDefAsync(id, name, dto.Url, dto.Scale, author, dto.SortOrder, dto.Enabled);
            await shop.GrantToAuthorAsync(author, ShopService.EffectFrame, ShopService.ImageFrameKey(newId));
            return Ok(new { id = newId, key = ShopService.ImageFrameKey(newId) });
        }

        // 10. 删除图片头像框（戴着的人自动取下）
        [HttpDelete("frames/{id:int}")]
        public async Task<IActionResult> DeleteFrameDef(int id)
        {
            await shop.DeleteFrameDefAsync(id);
            return NoContent();
        }

        // 11. 按用户名查账号（填原作者时用）
        [HttpGet("users")]
        public async Task<IActionResult> FindUsers([FromQuery] string? q)
        {
            q = (q ?? string.Empty).Trim();
            if (q.Length < 1) return Ok(Array.Empty<object>());
            var users = await _context.Users.AsNoTracking()
                .Where(u => u.Username.Contains(q))
                .OrderBy(u => u.Username.Length)
                .Take(10)
                .Select(u => new { userId = u.Id, name = u.Username })
                .ToListAsync();
            return Ok(users);
        }
    
        // 12. 官方头像：列表（包括下架的）
        [HttpGet("avatars")]
        public async Task<IActionResult> AvatarDefs()
        {
            var list = await shop.ListAvatarDefsAsync(enabledOnly: false);
            var authors = list.Where(a => a.AuthorId != null).Select(a => a.AuthorId!.Value).Distinct().ToList();
            var names = authors.Count == 0 ? new Dictionary<Guid, string>()
                : await _context.Users.AsNoTracking().Where(u => authors.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => u.Username);
            return Ok(list.Select(a => new
            {
                a.Id, a.Name, a.Url, a.Animated, a.SortOrder, a.Enabled, a.AuthorId,
                AuthorName = a.AuthorId is Guid g ? names.GetValueOrDefault(g) : null,
            }));
        }

        // 13. 上传官方头像图片（服务器检查格式、判断是不是动图，存到 official/avatars/）
        [HttpPost("avatars/upload")]
        [RequestSizeLimit(AvatarImage.MaxOfficialBytes + 64 * 1024)]
        public async Task<IActionResult> UploadAvatarImage(IFormFile? file)
        {
            if (file == null || file.Length == 0) return BadRequest(new { message = "没有收到图片" });
            if (file.Length > AvatarImage.MaxOfficialBytes) return BadRequest(new { message = "头像图片不能超过 8MB" });
            await using var ms = new MemoryStream();
            await file.CopyToAsync(ms);
            var bytes = ms.ToArray();
            var info = AvatarImage.Sniff(bytes);
            if (info == null) return BadRequest(new { message = "只支持 JPG、PNG、WebP、GIF 图片" });
            var url = await cos.UploadAsync(bytes, $"{Guid.NewGuid():N}.{info.Ext}", "official/avatars");
            return Ok(new { url, animated = info.Animated });
        }

        // 14. 新增 / 修改官方头像（填了原作者会自动发给作者）
        [HttpPut("avatars")]
        public async Task<IActionResult> SaveAvatarDef([FromBody] AvatarDefDto dto, [FromQuery] int? id)
        {
            var name = (dto.Name ?? "").Trim();
            if (name.Length is 0 or > 40) return BadRequest(new { message = "名称需要 1–40 个字" });
            if (!UserAppearanceService.IsSafeUrl(dto.Url)) return BadRequest(new { message = "请先上传头像图片" });
            if (id is int existing && await shop.GetAvatarDefAsync(existing) == null) return NotFound(new { message = "这个头像不存在" });
            var author = dto.AuthorId == Guid.Empty ? null : dto.AuthorId;
            if (author is Guid a && !await _context.Users.AnyAsync(u => u.Id == a)) return BadRequest(new { message = "原作者账号不存在" });
            var newId = await shop.SaveAvatarDefAsync(id, name, dto.Url, dto.Animated, author, dto.SortOrder, dto.Enabled);
            await shop.GrantToAuthorAsync(author, ShopService.EffectAvatar, newId.ToString());
            return Ok(new { id = newId });
        }

        // 15. 删除官方头像（已经在用的人不受影响，只是不能再选）
        [HttpDelete("avatars/{id:int}")]
        public async Task<IActionResult> DeleteAvatarDef(int id)
        {
            await shop.DeleteAvatarDefAsync(id);
            return NoContent();
        }
    }
}
