using System.Globalization;
using System.Text;

namespace TaiChuWeb_V2.Services.Resources;

/// <summary>
/// 社区资源（用户上传 / 下载）的规矩，都集中写在这里，改数值只改这一个文件。
/// 价格上限、单个文件上限两项也可以在 SystemConfigs 表里改（键见下面的 *ConfigKey），不用重新部署。
/// </summary>
public static class ResourceRules
{
    // ---------------- 上传 ----------------

    /// <summary>每一片的大小：64MB。Cloudflare 免费版一个请求最多 100MB，留足余量。</summary>
    public const long PartSize = 64L * 1024 * 1024;
    /// <summary>浏览器同时传几片（前端读接口返回的值）</summary>
    public const int UploadConcurrency = 3;
    /// <summary>单个文件默认上限 4GB</summary>
    public const long DefaultMaxFileBytes = 4L * 1024 * 1024 * 1024;
    /// <summary>SystemConfigs 里改单个文件上限（单位 MB）</summary>
    public const string MaxFileMbConfigKey = "Resource.MaxFileMb";
    /// <summary>每一片的上传链接多久过期。大文件慢网速也够用；过期了前端会自动重新要。</summary>
    public static readonly TimeSpan PartUrlLife = TimeSpan.FromHours(6);
    /// <summary>开始上传后超过这么久还没传完，后台就放弃这次上传、清理已传的碎片</summary>
    public static readonly TimeSpan StaleUploadAge = TimeSpan.FromHours(24);
    /// <summary>用户上传的文件都放在桶里的这个目录下；不在这个目录下的是站长直接放的「官方资源」</summary>
    public const string UploadPrefix = "uploads/";
    /// <summary>
    /// 存到腾讯云时放在 COS 桶的这个目录下（和网站图片同一个桶时，前端临时密钥在 CosController 里被禁止写这个目录，
    /// 别人改不了、覆盖不了资源文件）
    /// </summary>
    public const string CosKeyPrefix = "community-resources/";

    // ---------------- 价格 / 奖励 ----------------

    /// <summary>价格（经验）上限默认 500</summary>
    public const int DefaultMaxPrice = 500;
    /// <summary>SystemConfigs 里改价格上限</summary>
    public const string MaxPriceConfigKey = "Resource.MaxPrice";
    /// <summary>审核通过给上传者的经验</summary>
    public const int ApproveExp = 20;
    /// <summary>每人每天最多有几个资源能拿审核经验（防止拆成很多小资源刷经验）</summary>
    public const int ApproveExpDailyCount = 5;
    /// <summary>免费资源每多一个不同的登录用户下载，给上传者几点经验</summary>
    public const int FreeDownloadExp = 2;
    /// <summary>免费资源下载奖励：每个上传者每天最多拿多少经验</summary>
    public const int FreeDownloadDailyCap = 50;

    // ---------------- 下载 ----------------

    /// <summary>下载链接 15 分钟过期</summary>
    public static readonly TimeSpan DownloadUrlLife = TimeSpan.FromMinutes(15);

    // ---------------- 文字长度 ----------------

    public const int TitleMax = 60;
    public const int DescriptionMax = 2000;
    public const int TagCountMax = 8;
    public const int TagLengthMax = 16;
    public const int FileNameMax = 120;
    public const int ReasonMax = 300;

    /// <summary>分类（前端显示的选项也从接口拿这个列表）</summary>
    public static readonly IReadOnlyList<string> Categories = new List<string> { "音效", "素材", "模板", "工具", "软件", "字体", "模型", "其他" };

    // ---------------- 位置（展示 / 整理用的虚拟文件夹，NAS 上真实的文件位置不变） ----------------

    /// <summary>位置每一层最多几个字</summary>
    public const int FolderSegmentMax = 40;
    /// <summary>位置最多几层</summary>
    public const int FolderDepthMax = 4;
    /// <summary>位置总长度上限（和 community_resources.Folder 列一样长）</summary>
    public const int FolderMax = 200;

    /// <summary>
    /// 清洗位置：反斜杠换成 /，去掉首尾斜杠、空段、「.」「..」、控制字符和看不见的字符；每层最多 40 字，最多 4 层，总长不超过 200。
    /// 清洗完是空的返回 null（显示时用分类名当位置）。
    /// </summary>
    public static string? CleanFolder(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var segments = new List<string>();
        foreach (var part in raw.Replace('\\', '/').Split('/'))
        {
            var sb = new StringBuilder(part.Length);
            foreach (var ch in part)
            {
                if (char.IsControl(ch) || CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.Format) continue;
                sb.Append(ch);
            }
            var seg = sb.ToString().Replace("..", "").Trim();
            if (seg.Length == 0 || seg == ".") continue;
            if (seg.Length > FolderSegmentMax)
            {
                var keep = FolderSegmentMax;
                if (char.IsHighSurrogate(seg[keep - 1])) keep--;
                seg = seg[..keep].TrimEnd();
            }
            segments.Add(seg);
            if (segments.Count >= FolderDepthMax) break;
        }
        while (segments.Count > 1 && string.Join('/', segments).Length > FolderMax) segments.RemoveAt(segments.Count - 1);
        if (segments.Count == 0) return null;
        var result = string.Join('/', segments);
        return result.Length > FolderMax ? result[..FolderMax] : result;
    }

    /// <summary>显示用的位置：上传者没填就用分类名</summary>
    public static string EffectiveFolder(string? folder, string category) => string.IsNullOrWhiteSpace(folder) ? category : folder;

    /// <summary>官方资源的位置 = 它在 NAS 里的文件夹（文件路径去掉文件名）；放在根目录的是空字符串</summary>
    public static string OfficialFolder(string key)
    {
        var slash = key.LastIndexOf('/');
        return slash > 0 ? key[..slash] : "";
    }

    /// <summary>
    /// 不允许上传的扩展名：能直接运行的程序 / 脚本 / 安装包。
    /// 压缩包允许（里面可能夹带程序，审核时界面会提醒管理员注意）。
    /// </summary>
    public static readonly HashSet<string> BlockedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".exe", ".msi", ".msp", ".bat", ".cmd", ".com", ".scr", ".pif", ".cpl", ".dll", ".sys",
        ".ps1", ".psm1", ".psd1", ".vbs", ".vbe", ".js", ".jse", ".wsf", ".wsh", ".hta", ".lnk", ".reg", ".scf",
        ".jar", ".apk", ".apks", ".xapk", ".ipa", ".appx", ".appxbundle", ".msix", ".msixbundle",
        ".sh", ".bash", ".run", ".dmg", ".pkg", ".deb", ".rpm", ".gadget", ".application",
    };

    /// <summary>压缩包：允许上传，但审核时提醒管理员解开看看里面有没有程序</summary>
    public static readonly HashSet<string> ArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".zip", ".rar", ".7z", ".tar", ".gz", ".tgz", ".bz2", ".xz", ".zst", ".cab", ".iso",
    };

    /// <summary>
    /// 管理员审核时可以在浏览器里直接预览的类型 → 预览时强制使用的文件类型。
    /// 其他类型（尤其是网页、SVG）一律按下载处理，避免文件里的脚本在浏览器里执行。
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> PreviewTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png", [".jpg"] = "image/jpeg", [".jpeg"] = "image/jpeg", [".gif"] = "image/gif", [".webp"] = "image/webp",
        [".mp3"] = "audio/mpeg", [".wav"] = "audio/wav", [".ogg"] = "audio/ogg", [".flac"] = "audio/flac", [".m4a"] = "audio/mp4",
        [".mp4"] = "video/mp4", [".webm"] = "video/webm", [".pdf"] = "application/pdf", [".txt"] = "text/plain; charset=utf-8",
    };

    /// <summary>每人同时最多几个「上传中」（防止开一堆上传不传完，占着 NAS 的碎片空间）</summary>
    public const int MaxUploadingPerUser = 3;
    /// <summary>每人最多几个「待审核」（防止刷屏，审核完了才能继续传）</summary>
    public const int MaxPendingPerUser = 20;

    /// <summary>
    /// 清洗文件名：去掉路径、控制字符和看不见的方向控制符、Windows 不允许的字符，首尾的点和空格，限制长度（尽量保住扩展名）。
    /// </summary>
    public static string CleanFileName(string? raw)
    {
        var name = (raw ?? "").Replace('\\', '/');
        name = name[(name.LastIndexOf('/') + 1)..];
        var sb = new StringBuilder(name.Length);
        foreach (var ch in name)
        {
            if (char.IsControl(ch)) continue;
            // 「Format」类是看不见的字符，比如能把 exe.txt 显示成 txt.exe 的方向控制符
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.Format) continue;
            if ("<>:\"|?*".IndexOf(ch) >= 0) continue;
            sb.Append(ch);
        }
        name = sb.ToString().Trim().Trim('.').Trim();
        if (name.Length == 0) name = "file";
        if (name.Length > FileNameMax)
        {
            var ext = Path.GetExtension(name);
            if (ext.Length > 20) ext = "";
            var keep = FileNameMax - ext.Length;
            // 不要把一个汉字 / 表情的两半切开
            if (keep > 0 && char.IsHighSurrogate(name[keep - 1])) keep--;
            name = name[..keep].TrimEnd() + ext;
        }
        return name;
    }

    /// <summary>扩展名（小写，带点；没有就是空字符串）</summary>
    public static string ExtensionOf(string fileName) => Path.GetExtension(fileName ?? "").ToLowerInvariant();

    /// <summary>
    /// 存到 NAS 上的对象名里用的文件名：只留字母、数字、汉字和 . _ -，其他换成 _。
    /// 下载时浏览器拿到的还是原文件名（靠下载链接里的 Content-Disposition），这里只是避免特殊字符（% + # 空格等）让签名出问题。
    /// </summary>
    public static string KeySafeName(string fileName)
    {
        var sb = new StringBuilder(fileName.Length);
        foreach (var ch in fileName)
            sb.Append(char.IsLetterOrDigit(ch) || ch is '.' or '_' or '-' ? ch : '_');
        var s = sb.ToString().Trim('.');
        return s.Length == 0 ? "file" : s;
    }

    /// <summary>用户上传的文件在桶里的位置：uploads/{用户id}/{资源id}/{文件名}</summary>
    public static string ObjectKeyFor(Guid userId, Guid resourceId, string fileName) =>
        $"{UploadPrefix}{userId:D}/{resourceId:D}/{KeySafeName(fileName)}";

    /// <summary>
    /// 是不是「用户上传区」里的对象。旧的匿名下载接口要拦住这些（否则绕过审核、付费），
    /// 所以先把前导的 / 和 ./ 去掉、反斜杠换成 /，再按不区分大小写比较。
    /// </summary>
    public static bool IsUserUploadKey(string? key)
    {
        // 按路径规则化：去掉空段（//）、「.」，处理「..」，防止 a/../uploads/x、/uploads//x 这类写法绕过
        var segments = new List<string>();
        foreach (var raw in (key ?? "").Replace('\\', '/').Split('/'))
        {
            var seg = raw.Trim();
            if (seg.Length == 0 || seg == ".") continue;
            if (seg == "..") { if (segments.Count > 0) segments.RemoveAt(segments.Count - 1); continue; }
            segments.Add(seg);
        }
        return segments.Count > 0 && string.Equals(segments[0], UploadPrefix.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>清洗标签：去掉逗号和首尾空格、去重，最多 8 个、每个最多 16 字</summary>
    public static List<string> CleanTags(IEnumerable<string>? tags)
    {
        var list = new List<string>();
        foreach (var raw in tags ?? Enumerable.Empty<string>())
        {
            var t = new string((raw ?? "").Where(c => !char.IsControl(c) && c != ',' && c != '，').ToArray()).Trim().TrimStart('#').Trim();
            if (t.Length == 0) continue;
            if (t.Length > TagLengthMax) t = t[..TagLengthMax];
            if (list.Any(x => string.Equals(x, t, StringComparison.OrdinalIgnoreCase))) continue;
            list.Add(t);
            if (list.Count >= TagCountMax) break;
        }
        return list;
    }

    /// <summary>浏览器给的文件类型：只接受「xxx/yyy」这种正常格式，其他一律当普通二进制文件</summary>
    public static string CleanContentType(string? raw)
    {
        var t = (raw ?? "").Trim().ToLowerInvariant();
        if (t.Length is 0 or > 100) return "application/octet-stream";
        var slash = t.IndexOf('/');
        if (slash <= 0 || slash == t.Length - 1) return "application/octet-stream";
        return t.All(c => char.IsAsciiLetterOrDigit(c) || c is '/' or '.' or '+' or '-' or '_') ? t : "application/octet-stream";
    }

    /// <summary>
    /// 下载时的 Content-Disposition：让浏览器保存成原文件名。
    /// filename= 放一个纯英文的兜底名（老浏览器），filename*= 放 UTF-8 编码的真名（中文文件名靠这个）。
    /// </summary>
    public static string ContentDisposition(string fileName, bool inline)
    {
        var ascii = new string(fileName.Select(c => c < 128 && !char.IsControl(c) && c != '"' && c != '\\' && c != ';' ? c : '_').ToArray());
        var encoded = Uri.EscapeDataString(fileName);
        return $"{(inline ? "inline" : "attachment")}; filename=\"{ascii}\"; filename*=UTF-8''{encoded}";
    }

    /// <summary>算一个文件要分几片</summary>
    public static int PartCount(long size, long partSize) => size <= 0 ? 1 : (int)((size + partSize - 1) / partSize);
}
