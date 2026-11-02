using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using TaiChuWeb_V2.Dtos.World;
using TaiChuWeb_V2.Models.ChaiCommunity;
using TaiChuWeb_V2.Models.World;
using TaiChuWeb_V2.Services.Panels;
using TaiChuWeb_V2.Services.World;

namespace TaiChuWeb_V2.Services.ChaiCommunity;

/// <summary>
/// 柴圈 OC ↔ 世界观「人物」条目 的转换和对比，所有规则都在这一个文件里（前端只显示预览，不自己转换）。
///
/// 对应规则（两边是同一套，来回同步不会越改越乱）：
///   OC 名字           ↔ 条目名字
///   OC 简介           ↔ 条目正文（条目里的 {CARD:编号} 引用，到 OC 那边变成《条目名》）
///   OC 封面           ↔ 条目封面
///   OC 短属性         ↔ 条目顶部的「属性」（短文本）
///   OC 长属性         ↔ 条目的「文本」区块（区块标题 = 属性名，内容 = 属性值）
///   OC 展示面板       ↔ 条目里第一个「能力面板」区块
///   OC 图库           ↔ 条目图库（只往对面补没有的图，不删图）
/// 条目 → OC 时：条目顶部属性里的「长文本」类型、没写标题的文本块（按「文本」算）也会变成 OC 的长属性；
/// 对访客隐藏的区块不会同步到 OC（OC 发布后谁都看得到，免得把世界观里的秘密设定带出去）。
///
/// 同步（「从另一边更新」）只动能对应上的部分：
///   属性按名字对应（去掉首尾空格；同名的按第几个对应），对面有、这边没有的 → 新增；两边都有但内容不同 → 修改；
///   这边独有的 → 保留不动；对面是空的 → 不拿空内容覆盖这边。
/// </summary>
public static class OcWorldConverter
{
    public const string OcToCard = "oc-to-card";
    public const string CardToOc = "card-to-oc";
    /// <summary>世界观里「人物」条目的类型编号</summary>
    public const string CharacterType = "character";
    public const string Add = "add", Update = "update", Keep = "keep", Skip = "skip";

    /// <summary>属性名对应时最多看前 60 个字（条目区块标题前端最多存 60 个字，超过的会被截掉）</summary>
    private const int MaxKey = 60;
    private const int MaxOcKey = 100, MaxTitle = 200, MaxUrl = 500;
    /// <summary>预览里每一项最多显示多少字（完整内容照样会写过去）</summary>
    private const int PreviewLength = 300;
    private const string UntitledText = "文本";
    private static readonly List<string> HideableKinds = new() { "text", "media", "map", "sources", "canvas", "tree", "statpanel" };
    private static readonly Regex CardToken = new(@"\{CARD:([^}]+)\}");
    /// <summary>中文不转义（和 WorldLayout 存的格式一致，知识库搜索能直接搜到）</summary>
    private static readonly JsonSerializerOptions Raw = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    // ===================== 两边读出来的统一样子 =====================

    /// <summary>一项属性（OC 的属性，或者条目的属性 / 文本块）</summary>
    public sealed class Item
    {
        /// <summary>显示用的名字</summary>
        public string Key { get; set; } = "";
        /// <summary>对应用的名字：去掉首尾空格、截到 60 字；同名的第 2 个起加「#2」「#3」</summary>
        public string MatchKey { get; set; } = "";
        /// <summary>内容（条目这边已经把 {CARD:编号} 换成《条目名》，用这个和 OC 比较）</summary>
        public string Value { get; set; } = "";
        public bool Long { get; set; }
        /// <summary>条目里「对访客隐藏」的文本块</summary>
        public bool Hidden { get; set; }
        /// <summary>条目顶部属性里的第几个（不是属性就是 null）</summary>
        public int? AttrIndex { get; set; }
        /// <summary>条目里的文本块（直接改它，保存时整个排列一起存）</summary>
        public JsonObject? Block { get; set; }
        /// <summary>OC 的属性（EF 跟踪着的，直接改值）</summary>
        public StickmanAttribute? Attr { get; set; }
    }

    /// <summary>一边（OC 或条目）的全部可同步内容</summary>
    public sealed class Side
    {
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public string? Cover { get; set; }
        public List<Item> Items { get; set; } = new();
        /// <summary>能力面板（整理好的格式）；没有 = null</summary>
        public JsonObject? Panel { get; set; }
        public bool PanelHidden { get; set; }
        /// <summary>条目里第一个能力面板区块（面板坏了也记着，同步时直接换掉）</summary>
        public JsonObject? PanelBlock { get; set; }
        /// <summary>图库地址（去重后，用来比较）</summary>
        public List<string> Images { get; set; } = new();
        /// <summary>条目图库原样（写回时在它后面追加，原有的一张不少）</summary>
        public List<string> RawImages { get; set; } = new();
        // ----- 条目专用 -----
        public JsonObject? Root { get; set; }
        public JsonArray? Rows { get; set; }
        public List<AttributeDto>? Attrs { get; set; }
    }

    /// <summary>预览里的一项：新增 / 修改 / 保留 / 跳过</summary>
    public sealed class Change
    {
        public string Action { get; set; } = "";
        public string Field { get; set; } = "";
        public string? Before { get; set; }
        public string? After { get; set; }
        public string? Note { get; set; }
        // ----- 执行时用，不发给前端 -----
        public string Kind { get; set; } = "";
        public Item? Source { get; set; }
        public Item? Target { get; set; }
        public JsonObject? Panel { get; set; }
        public List<string>? NewImages { get; set; }
    }

    // ===================== 读 OC =====================

    public static Side ReadOc(StickmanCharacter oc, IEnumerable<StickmanAttribute> attributes, IEnumerable<StickmanImage> images, string? showPanelJson)
    {
        var side = new Side { Title = oc.Title ?? "", Description = Text(oc.Description), Cover = Blank(oc.CoverUrl) };
        var seen = new Dictionary<string, int>();
        foreach (var a in attributes.OrderBy(a => a.SortOrder).ThenBy(a => a.CreatedAt))
        {
            if (string.IsNullOrWhiteSpace(a.Key)) continue;
            side.Items.Add(new Item { Key = a.Key.Trim(), MatchKey = MatchKey(a.Key, seen), Value = Text(a.Value), Long = a.Type == "long", Attr = a });
        }
        side.Panel = ParsePanel(showPanelJson);
        side.RawImages = images.OrderBy(i => i.SortOrder).Select(i => (i.Url ?? "").Trim()).Where(u => u.Length > 0).ToList();
        side.Images = side.RawImages.Distinct().ToList();
        return side;
    }

    /// <summary>存着的 OC 展示面板：检查通过才算有（坏数据当作没有，和 StickmanPanelController 一样）</summary>
    public static JsonObject? ParsePanel(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var node = JsonNode.Parse(json);
            return StatPanelRules.Check(node) == null ? StatPanelRules.Normalize((JsonObject)node!) : null;
        }
        catch (JsonException) { return null; }
    }

    // ===================== 读条目 =====================

    /// <summary>条目正文、文本块里引用到的条目编号（用来查名字，把 {CARD:编号} 换成《名字》）</summary>
    public static List<Guid> ReferencedCardIds(WorldCard card)
    {
        var text = (card.Description ?? "") + " " + (card.Layout ?? "") + " " + (card.Attributes ?? "");
        var ids = new List<Guid>();
        foreach (Match m in CardToken.Matches(text))
            if (Guid.TryParse(m.Groups[1].Value, out var id) && !ids.Contains(id)) ids.Add(id);
        return ids;
    }

    /// <summary>
    /// 读条目。属性 JSON 和条目服务一样用默认格式解析（解析失败直接报错，不冒险往下改）。
    /// 排列为空的老条目按前端 defaultLayout 的规则补一个默认排列，改完存回去时老内容的位置不变。
    /// </summary>
    public static Side ReadCard(WorldCard card, IReadOnlyDictionary<Guid, string> titles)
    {
        var side = new Side { Title = card.Title ?? "", Description = Plain(card.Description, titles), Cover = CoverUrl(card.CoverImage) };
        side.Attrs = JsonSerializer.Deserialize<List<AttributeDto>>(string.IsNullOrWhiteSpace(card.Attributes) ? "[]" : card.Attributes) ?? new();
        var seen = new Dictionary<string, int>();
        for (var i = 0; i < side.Attrs.Count; i++)
        {
            var a = side.Attrs[i];
            if (a == null || string.IsNullOrWhiteSpace(a.Key)) continue;
            side.Items.Add(new Item { Key = a.Key.Trim(), MatchKey = MatchKey(a.Key, seen), Value = Plain(a.Value, titles), Long = a.Type == "long", AttrIndex = i });
        }

        side.Root = LayoutRoot(card);
        side.Rows = (JsonArray)side.Root["rows"]!;
        foreach (var block in side.Rows.OfType<JsonObject>().SelectMany(r => (r["blocks"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()))
        {
            var kind = Str(block, "kind");
            if (kind == "text")
            {
                var title = Str(block, "title").Trim();
                var key = title.Length > 0 ? title : UntitledText;
                side.Items.Add(new Item { Key = key, MatchKey = MatchKey(key, seen), Value = Plain(Str(block, "text"), titles), Long = true, Hidden = IsHidden(block), Block = block });
            }
            else if (kind == "statpanel" && side.PanelBlock == null)
            {
                // 只认第一个能力面板（OC 只有一份展示面板）
                side.PanelBlock = block;
                side.PanelHidden = IsHidden(block);
                if (block["panel"] is JsonObject panel && StatPanelRules.Check(panel) == null)
                {
                    var clean = StatPanelRules.Normalize(panel);
                    // 条目里面板的标题显示在区块标题栏上，以区块标题为准
                    var blockTitle = Str(block, "title").Trim();
                    if (blockTitle.Length > 0) clean["title"] = Clip(blockTitle, StatPanelRules.MaxTitle);
                    side.Panel = clean;
                }
            }
        }

        try { side.RawImages = JsonSerializer.Deserialize<List<string>>(string.IsNullOrWhiteSpace(card.GalleryImages) ? "[]" : card.GalleryImages) ?? new(); }
        catch (JsonException) { side.RawImages = new(); }
        side.Images = side.RawImages.Where(u => !string.IsNullOrWhiteSpace(u)).Select(u => u.Trim()).Distinct().ToList();
        return side;
    }

    /// <summary>条目的区块排列（可以直接改）；没有排列的老条目按前端 defaultLayout 生成：正文 → 图库 → 时间线 → 引用设定（只放有内容的）</summary>
    private static JsonObject LayoutRoot(WorldCard card)
    {
        if (!string.IsNullOrWhiteSpace(card.Layout))
        {
            try
            {
                if (JsonNode.Parse(card.Layout) is JsonObject root && root["rows"] is JsonArray) return root;
            }
            catch (JsonException) { }
            // 排列坏了：不敢在坏数据上改，直接报错（条目服务保存时也会拒绝坏排列）
            throw new WorldOperationException(WorldErrorKind.Invalid, "这个条目的区块排列格式不对，先在编辑器里打开保存一次再同步");
        }
        var rows = new JsonArray { Row("r-description", new JsonObject { ["id"] = "description", ["kind"] = "description" }) };
        if (HasItems(card.GalleryImages)) rows.Add(Row("r-gallery", new JsonObject { ["id"] = "gallery", ["kind"] = "gallery" }));
        if (HasItems(card.TimelineEvents)) rows.Add(Row("r-timeline", new JsonObject { ["id"] = "timeline", ["kind"] = "timeline" }));
        if (HasItems(card.ContentBlocks)) rows.Add(Row("r-embeds", new JsonObject { ["id"] = "embeds", ["kind"] = "embeds" }));
        return new JsonObject { ["v"] = 1, ["rows"] = rows };
    }

    // ===================== 对比（预览） =====================

    /// <summary>算出「用 src 更新 dst」会新增 / 修改 / 保留 / 跳过哪些。toCard = OC → 条目</summary>
    public static List<Change> Plan(Side src, Side dst, string direction)
    {
        var toCard = direction == OcToCard;
        var list = new List<Change>();

        // 名字
        if (Norm(src.Title).Length > 0 && Norm(src.Title) != Norm(dst.Title))
            list.Add(new Change { Action = Update, Kind = "title", Field = "名字", Before = dst.Title, After = Clip(src.Title.Trim(), MaxTitle) });

        // 简介 ↔ 正文
        var descField = toCard ? "正文（来自 OC 简介）" : "简介（来自条目正文）";
        if (Norm(src.Description).Length > 0)
        {
            if (Norm(src.Description) != Norm(dst.Description))
                list.Add(new Change { Action = Norm(dst.Description).Length == 0 ? Add : Update, Kind = "description", Field = descField, Before = dst.Description, After = src.Description });
        }
        else if (Norm(dst.Description).Length > 0)
            list.Add(new Change { Action = Keep, Field = toCard ? "正文" : "简介", Before = dst.Description, Note = "对面是空的，这边保留不动" });

        // 封面
        var cover = src.Cover != null && (toCard || src.Cover.Length <= MaxUrl) ? src.Cover : null;
        if (cover != null)
        {
            if (cover != dst.Cover)
                list.Add(new Change { Action = dst.Cover == null ? Add : Update, Kind = "cover", Field = "封面", Before = dst.Cover, After = cover });
        }
        else if (dst.Cover != null)
            list.Add(new Change { Action = Keep, Field = "封面", Before = dst.Cover, Note = "对面没有封面，这边保留" });

        // 属性：按名字对应
        var targets = dst.Items.ToDictionary(i => i.MatchKey);
        var matched = new HashSet<string>();
        foreach (var s in src.Items)
        {
            if (s.Hidden && !toCard)
            {
                list.Add(new Change { Action = Skip, Field = $"文本区块「{s.Key}」", Before = Preview(s.Value), Note = "这块在世界观里「对访客隐藏」，不会同步到 OC（OC 发布后所有人都能看到）" });
                continue;
            }
            if (targets.TryGetValue(s.MatchKey, out var t))
            {
                matched.Add(s.MatchKey);
                if (Norm(s.Value).Length == 0)
                {
                    if (Norm(t.Value).Length > 0) list.Add(new Change { Action = Keep, Field = FieldOf(t, toCard), Before = t.Value, Note = "对面是空的，这边保留不动" });
                }
                else if (Norm(s.Value) != Norm(t.Value))
                    list.Add(new Change { Action = Update, Kind = "item", Field = FieldOf(t, toCard), Before = t.Value, After = s.Value, Source = s, Target = t });
            }
            else if (Norm(s.Value).Length > 0)
                list.Add(new Change { Action = Add, Kind = "item", Field = NewFieldOf(s, toCard), After = s.Value, Source = s });
        }
        foreach (var t in dst.Items.Where(t => !matched.Contains(t.MatchKey) && Norm(t.Value).Length > 0))
            list.Add(new Change { Action = Keep, Field = FieldOf(t, toCard), Before = t.Value, Note = toCard ? "OC 里没有这一项，条目里的保留" : "条目里没有这一项，OC 里的保留" });

        // 能力面板
        var panelField = toCard ? "能力面板区块" : "OC 展示面板";
        if (src.Panel != null && src.PanelHidden && !toCard)
            list.Add(new Change { Action = Skip, Field = "能力面板区块", Before = Describe(src.Panel), Note = "这块在世界观里「对访客隐藏」，不会同步到 OC" });
        else if (src.Panel != null)
        {
            if (dst.Panel == null)
                list.Add(new Change { Action = toCard && dst.PanelBlock != null ? Update : Add, Kind = "panel", Field = panelField, Before = toCard && dst.PanelBlock != null ? "（原来的面板数据坏了，读不出来）" : null, After = Describe(src.Panel), Panel = src.Panel });
            else if (dst.Panel.ToJsonString(Raw) != src.Panel.ToJsonString(Raw))
                list.Add(new Change { Action = Update, Kind = "panel", Field = panelField, Before = Describe(dst.Panel), After = Describe(src.Panel), Panel = src.Panel });
        }
        else if (dst.Panel != null)
            list.Add(new Change { Action = Keep, Field = panelField, Before = Describe(dst.Panel), Note = "对面没有能力面板，这边保留" });

        // 图库：只补对面没有的
        var fresh = src.Images.Where(u => !dst.Images.Contains(u) && (toCard || u.Length <= MaxUrl)).ToList();
        if (fresh.Count > 0)
            list.Add(new Change { Action = Add, Kind = "images", Field = "图库", After = $"补上 {fresh.Count} 张图片", NewImages = fresh });
        var own = dst.Images.Count(u => !src.Images.Contains(u));
        if (own > 0)
            list.Add(new Change { Action = Keep, Field = "图库", Before = $"这边独有的 {own} 张图片", Note = "保留不动" });

        return list;
    }

    /// <summary>预览的「指纹」：只算会改动的项。确认执行时重新算一遍，不一样说明期间有人改过，要重新预览</summary>
    public static string Fingerprint(IEnumerable<Change> changes, string direction, Guid ocId, Guid cardId)
    {
        var parts = changes.Where(IsWrite).Select(c => new[] { c.Kind, c.Field, c.Before ?? "", c.After ?? "", c.Panel?.ToJsonString(Raw) ?? "", string.Join("\n", c.NewImages ?? new()) });
        var json = JsonSerializer.Serialize(new { direction, ocId, cardId, parts });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)))[..32];
    }

    public static bool IsWrite(Change c) => c.Action is Add or Update && c.Kind.Length > 0;

    /// <summary>给前端的预览（长内容截短，完整内容执行时照样写过去）</summary>
    public static object PreviewDto(Change c) => new
    {
        action = c.Action,
        field = c.Field,
        before = c.Before == null ? null : Preview(c.Before),
        after = c.After == null ? null : Preview(c.After),
        note = c.Note,
    };

    // ===================== 执行：写到条目 =====================

    /// <summary>
    /// 把预览里的新增 / 修改写进条目要保存的内容（UpdateCardDto），只填改动到的字段，其余字段不传（= 不改）。
    /// 带上读出来时的版本号：期间有人改过条目，条目服务会拒绝保存（不会盖掉别人的修改）。
    /// </summary>
    public static UpdateCardDto ApplyToCard(Side dst, IEnumerable<Change> changes, DateTime expectedUpdatedAt)
    {
        var dto = new UpdateCardDto { ExpectedUpdatedAt = expectedUpdatedAt };
        bool attrs = false, layout = false;
        foreach (var c in changes.Where(IsWrite))
        {
            switch (c.Kind)
            {
                case "title": dto.Title = c.After; break;
                case "description": dto.Description = c.After ?? ""; break;
                case "cover": dto.CoverImage = c.After; break;
                case "item":
                    if (c.Target?.AttrIndex is int index) { dst.Attrs![index].Value = c.After ?? ""; attrs = true; }
                    else if (c.Target?.Block is { } block) { block["text"] = c.After ?? ""; layout = true; }
                    else if (c.Source is { } s)
                    {
                        // 新增：长属性 → 文本区块；短属性 → 顶部属性
                        if (s.Long) { dst.Rows!.Add(Row(NewId("r"), TextBlock(s.Key, c.After ?? ""))); layout = true; }
                        else { dst.Attrs!.Add(new AttributeDto { Key = s.Key, Value = c.After ?? "", Type = "short" }); attrs = true; }
                    }
                    break;
                case "panel":
                    if (dst.PanelBlock != null)
                    {
                        dst.PanelBlock["panel"] = c.Panel!.DeepClone();
                        dst.PanelBlock["title"] = Str(c.Panel!, "title");
                    }
                    else dst.Rows!.Add(Row(NewId("r"), PanelBlock(c.Panel!)));
                    layout = true;
                    break;
                case "images":
                    dto.GalleryImages = dst.RawImages.Concat(c.NewImages ?? new()).ToList();
                    if (!HasKind(dst.Rows!, "gallery")) { dst.Rows!.Add(Row("r-gallery", new JsonObject { ["id"] = "gallery", ["kind"] = "gallery" })); layout = true; }
                    break;
            }
        }
        if (attrs) dto.Attributes = dst.Attrs;
        if (layout) dto.Layout = dst.Root!.ToJsonString(Raw);
        return dto;
    }

    // ===================== 执行：写到 OC =====================

    public sealed class OcApplyResult
    {
        public List<StickmanAttribute> NewAttributes { get; } = new();
        public List<StickmanImage> NewImages { get; } = new();
        /// <summary>要写进 stickman_panels 的展示面板；null = 面板不动</summary>
        public string? ShowPanelJson { get; set; }
    }

    /// <summary>把预览里的新增 / 修改改到 OC 上（已有属性直接改值，新属性和新图片由调用方加进数据库）</summary>
    public static OcApplyResult ApplyToOc(StickmanCharacter oc, IEnumerable<StickmanAttribute> existing, IEnumerable<StickmanImage> existingImages, IEnumerable<Change> changes)
    {
        var result = new OcApplyResult();
        var now = DateTime.UtcNow;
        var sort = existing.Select(a => a.SortOrder).DefaultIfEmpty(-1).Max();
        var imageSort = existingImages.Select(i => i.SortOrder).DefaultIfEmpty(-1).Max();
        foreach (var c in changes.Where(IsWrite))
        {
            switch (c.Kind)
            {
                case "title": oc.Title = Clip(c.After ?? oc.Title, MaxTitle); break;
                case "description": oc.Description = c.After; break;
                case "cover": oc.CoverUrl = c.After; break;
                case "item":
                    if (c.Target?.Attr is { } attr) attr.Value = c.After;
                    else if (c.Source is { } s)
                        result.NewAttributes.Add(new StickmanAttribute
                        {
                            Id = Guid.NewGuid(), CharacterId = oc.Id, Key = Clip(s.Key, MaxOcKey), Value = c.After,
                            Type = s.Long ? "long" : "short", SortOrder = ++sort, CreatedAt = now,
                        });
                    break;
                case "panel":
                    result.ShowPanelJson = c.Panel!.ToJsonString(StatPanelRules.Raw);
                    break;
                case "images":
                    foreach (var url in c.NewImages ?? new())
                        result.NewImages.Add(new StickmanImage { Id = Guid.NewGuid(), CharacterId = oc.Id, Url = url, SortOrder = ++imageSort, CreatedAt = now });
                    break;
            }
        }
        oc.UpdatedAt = now;
        return result;
    }

    // ===================== 新建：OC → 条目 / 条目 → OC =====================

    /// <summary>
    /// 「复制到我的世界观」：OC 变成一张新的「人物」条目。
    /// 正文 = 简介，短属性 → 顶部属性，能力面板 → 能力面板区块，长属性 → 文本区块，图库照搬，
    /// 最后加一个「来源」区块写明「来自柴圈 OC《名字》」并链接回去（linkBase = 网站地址，没有就只写名字）。
    /// </summary>
    public static CreateCardDto NewCardFromOc(Side oc, Guid ocId, string? linkBase)
    {
        var rows = new JsonArray { Row("r-description", new JsonObject { ["id"] = "description", ["kind"] = "description" }) };
        if (oc.Panel != null) rows.Add(Row(NewId("r"), PanelBlock(oc.Panel)));
        foreach (var item in oc.Items.Where(i => i.Long && Norm(i.Value).Length > 0))
            rows.Add(Row(NewId("r"), TextBlock(item.Key, item.Value)));
        if (oc.RawImages.Count > 0) rows.Add(Row("r-gallery", new JsonObject { ["id"] = "gallery", ["kind"] = "gallery" }));

        var source = new JsonObject { ["id"] = NewId("s"), ["title"] = Clip($"柴圈 OC《{oc.Title.Trim()}》", 120), ["note"] = "从柴圈 OC 复制过来的，原 OC 不受影响" };
        // 网站用的是哈希路由（/#/ocs/…），不带 # 的地址打开是首页
        if (SafeBase(linkBase) is { } site) source["url"] = $"{site}/#/ocs/{ocId}";
        rows.Add(Row(NewId("r"), new JsonObject { ["id"] = NewId("b"), ["kind"] = "sources", ["title"] = "来源", ["sources"] = new JsonArray { source } }));

        return new CreateCardDto
        {
            Title = Clip(oc.Title.Trim(), MaxTitle),
            Type = CharacterType,
            Description = Norm(oc.Description).Length > 0 ? oc.Description : null,
            CoverImage = oc.Cover,
            Attributes = oc.Items.Where(i => !i.Long && Norm(i.Value).Length > 0).Select(i => new AttributeDto { Key = i.Key, Value = i.Value, Type = "short" }).ToList(),
            GalleryImages = oc.RawImages.ToList(),
            Layout = new JsonObject { ["v"] = 1, ["rows"] = rows }.ToJsonString(Raw),
        };
    }

    /// <summary>
    /// 「发布成柴圈 OC」：条目变成一个 OC 草稿（作者 = 当前用户）。
    /// 返回 OC（含属性、图库，还没存）和展示面板 JSON（没有面板 = null）。对访客隐藏的区块不带过去，空属性不带过去。
    /// </summary>
    public static (StickmanCharacter Oc, string? ShowPanelJson) NewOcFromCard(Side card, Guid authorId, string authorName)
    {
        var now = DateTime.UtcNow;
        var oc = new StickmanCharacter
        {
            Id = Guid.NewGuid(),
            Title = Clip(card.Title.Trim(), MaxTitle),
            Description = Norm(card.Description).Length > 0 ? card.Description : null,
            CoverUrl = card.Cover != null && card.Cover.Length <= MaxUrl ? card.Cover : null,
            AuthorId = authorId,
            AuthorName = Clip(authorName, 100),
            Views = 0,
            Status = "draft",
            IsBattleEnabled = true,
            CreatedAt = now,
        };
        var sort = 0;
        oc.Attributes = card.Items.Where(i => !i.Hidden && Norm(i.Value).Length > 0).Select(i => new StickmanAttribute
        {
            Id = Guid.NewGuid(), CharacterId = oc.Id, Key = Clip(i.Key, MaxOcKey), Value = i.Value,
            Type = i.Long ? "long" : "short", SortOrder = sort++, CreatedAt = now,
        }).ToList();
        var imageSort = 0;
        oc.Images = card.Images.Where(u => u.Length <= MaxUrl).Select(u => new StickmanImage
        {
            Id = Guid.NewGuid(), CharacterId = oc.Id, Url = u, SortOrder = imageSort++, CreatedAt = now,
        }).ToList();
        var panel = card.Panel != null && !card.PanelHidden ? card.Panel.ToJsonString(StatPanelRules.Raw) : null;
        return (oc, panel);
    }

    // ===================== 小工具 =====================

    /// <summary>「从 OC 复制」时链接用的网站地址：只认 http / https，去掉结尾的 /</summary>
    private static string? SafeBase(string? linkBase)
    {
        if (string.IsNullOrWhiteSpace(linkBase) || linkBase.Length > 200) return null;
        if (!Uri.TryCreate(linkBase.Trim(), UriKind.Absolute, out var uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)) return null;
        return uri.GetLeftPart(UriPartial.Authority);
    }

    private static JsonObject Row(string id, JsonObject block) => new() { ["id"] = id, ["blocks"] = new JsonArray { block } };

    private static JsonObject TextBlock(string title, string text) =>
        new() { ["id"] = NewId("b"), ["kind"] = "text", ["title"] = Clip(title.Trim(), MaxKey), ["text"] = text };

    private static JsonObject PanelBlock(JsonObject panel) =>
        new() { ["id"] = NewId("b"), ["kind"] = "statpanel", ["title"] = Str(panel, "title"), ["panel"] = panel.DeepClone() };

    /// <summary>区块编号：和前端 newId 一样是字母数字（前端只要求不重复）</summary>
    private static string NewId(string prefix) => prefix + "oc" + Guid.NewGuid().ToString("N")[..12];

    private static bool HasKind(JsonArray rows, string kind) =>
        rows.OfType<JsonObject>().SelectMany(r => (r["blocks"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()).Any(b => Str(b, "kind") == kind);

    private static bool HasItems(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try { return JsonNode.Parse(json) is JsonArray array && array.Count > 0; }
        catch (JsonException) { return false; }
    }

    private static bool IsHidden(JsonObject block) =>
        block["hidden"] is JsonValue v && v.TryGetValue<bool>(out var hidden) && hidden && HideableKinds.Contains(Str(block, "kind"));

    private static string MatchKey(string key, Dictionary<string, int> seen)
    {
        var k = Clip(key.Trim(), MaxKey);
        var n = seen.TryGetValue(k, out var count) ? count + 1 : 1;
        seen[k] = n;
        return n == 1 ? k : $"{k}#{n}";
    }

    /// <summary>预览里显示这一项在哪：toCard 时目标是条目，否则是 OC</summary>
    private static string FieldOf(Item t, bool toCard)
    {
        if (!toCard) return t.Long ? $"OC 长属性「{t.Key}」" : $"OC 属性「{t.Key}」";
        if (t.Block != null) return $"文本区块「{t.Key}」" + (t.Hidden ? "（仅成员可见）" : "");
        return $"属性「{t.Key}」";
    }

    private static string NewFieldOf(Item s, bool toCard)
    {
        if (!toCard) return s.Long ? $"OC 长属性「{s.Key}」" : $"OC 属性「{s.Key}」";
        return s.Long ? $"文本区块「{s.Key}」" : $"属性「{s.Key}」";
    }

    /// <summary>能力面板的一句话说明：标题 + 维度</summary>
    public static string Describe(JsonObject panel)
    {
        var dims = (panel["dims"] as JsonArray ?? new JsonArray()).OfType<JsonObject>()
            .Select(d => $"{Str(d, "name")} {d["value"]?.ToString()}").ToList();
        var title = Str(panel, "title");
        return $"{(title.Length > 0 ? title : StatPanelRules.DefaultTitle)}：{dims.Count} 个维度（{string.Join("、", dims.Take(12))}{(dims.Count > 12 ? "……" : "")}）";
    }

    /// <summary>条目文字 → OC 文字：{CARD:编号} 换成《条目名》（OC 那边没有条目引用）</summary>
    private static string Plain(string? text, IReadOnlyDictionary<Guid, string> titles) =>
        CardToken.Replace(Text(text), m => Guid.TryParse(m.Groups[1].Value, out var id) && titles.TryGetValue(id, out var title) ? $"《{title}》" : "《已删除的条目》");

    /// <summary>条目封面：可能是一个地址，也可能是老格式的 JSON 数组（取第一张）</summary>
    public static string? CoverUrl(string? cover)
    {
        var value = Blank(cover);
        if (value == null || !value.StartsWith('[')) return value;
        try { return Blank(JsonSerializer.Deserialize<List<string>>(value)?.FirstOrDefault()); }
        catch (JsonException) { return null; }
    }

    private static string Text(string? text) => (text ?? "").Replace("\r\n", "\n");
    private static string Norm(string? text) => Text(text).Trim();
    private static string? Blank(string? text) => string.IsNullOrWhiteSpace(text) ? null : text.Trim();
    private static string Clip(string text, int max) => text.Length > max ? text[..max] : text;
    private static string Preview(string text) => text.Length > PreviewLength ? text[..PreviewLength] + "……" : text;

    private static string Str(JsonObject obj, string field) =>
        obj[field] is JsonValue v && v.TryGetValue<string>(out var text) ? text : "";
}
