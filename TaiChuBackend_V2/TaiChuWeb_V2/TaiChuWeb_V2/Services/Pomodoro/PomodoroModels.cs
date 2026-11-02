namespace TaiChuWeb_V2.Services.Pomodoro;

// 番茄钟接口用到的数据结构。字段名和桌面程序约好了（JSON 是 camelCase），不要改名。
// 时间一律是毫秒时间戳（UTC，epoch ms）。
// 字符串都写成可空：开着 Nullable 时，不可空的 string 会被 [ApiController] 当成「必填」，
// 传空字符串（比如待命时 phase = ""）会被直接拒绝成 400。

/// <summary>现在在计时什么（两端共用的一份）</summary>
public sealed class PomoStateDto
{
    /// <summary>"" 待命 / focus 专注 / short 短休息 / long 长休息</summary>
    public string? Phase { get; set; } = "";
    /// <summary>在做的事（最多 60 字）</summary>
    public string? Task { get; set; } = "";
    /// <summary>这一段开始的时间（专注记录的 clientId 就是 "f" + 它）</summary>
    public long StartedMs { get; set; }
    /// <summary>到点时间；暂停时为 0</summary>
    public long EndsMs { get; set; }
    /// <summary>暂停时还剩多少毫秒；没暂停为 0</summary>
    public long PausedLeftMs { get; set; }
    /// <summary>这一段一共多长</summary>
    public long TotalMs { get; set; }
    /// <summary>这一组已经完成了几轮专注</summary>
    public int Round { get; set; }
    /// <summary>待命时建议的下一段</summary>
    public string? Next { get; set; } = "focus";
    /// <summary>关联的生活管理事项 id（Guid）</summary>
    public string? PlannerEventId { get; set; }
    /// <summary>重复事项是哪一天（"YYYY-MM-DD"）</summary>
    public string? PlannerDate { get; set; }
    public long UpdatedMs { get; set; }
    /// <summary>最后是哪一端改的：desktop / web</summary>
    public string? Source { get; set; } = "";
}

/// <summary>番茄钟设置（跟着账号走，两端一致）</summary>
public sealed class PomoSettingsDto
{
    public int FocusMin { get; set; } = 25;
    public int ShortMin { get; set; } = 5;
    public int LongMin { get; set; } = 15;
    /// <summary>几轮专注后来一次长休息；0 = 不要长休息</summary>
    public int LongEvery { get; set; } = 4;
    public bool AutoBreak { get; set; }
    public bool AutoFocus { get; set; }
}

/// <summary>一条专注记录</summary>
public sealed class PomoSessionDto
{
    /// <summary>两端一致的去重键："f" + 专注开始的 startedMs</summary>
    public string? ClientId { get; set; }
    public long StartMs { get; set; }
    public long EndMs { get; set; }
    /// <summary>实际专注了多少秒（不含暂停）</summary>
    public int FocusSec { get; set; }
    public string? Task { get; set; }
    /// <summary>true 完成 / false 中途放弃</summary>
    public bool Done { get; set; }
    public string? PlannerEventId { get; set; }
    public string? PlannerDate { get; set; }
    public string? Source { get; set; }
}

public sealed class PomoStatePutInput
{
    /// <summary>改之前看到的版本号，和服务器上的不一样就是「另一端刚改过」，返回 409</summary>
    public long BaseVersion { get; set; }
    public PomoStateDto? State { get; set; }
    /// <summary>null = 设置不变</summary>
    public PomoSettingsDto? Settings { get; set; }
    public string? Source { get; set; }
}

public sealed class PomoSessionsInput
{
    public List<PomoSessionDto>? Sessions { get; set; }
}

/// <summary>pomodoro_state 读出来的一行</summary>
public sealed class PomoStateRow
{
    public PomoStateDto State { get; set; } = new();
    public string? SettingsJson { get; set; }
    public long Version { get; set; }
    public DateTime? DesktopSeenAt { get; set; }
}
