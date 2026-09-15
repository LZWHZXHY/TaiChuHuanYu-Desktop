namespace TaiChuWeb_V2.Dtos.Club
{
    /// <summary>申请成为打手（含第一个游戏）</summary>
    public class OperatorApplyDto
    {
        // ===== 通用信息 =====
        public string Nickname { get; set; } = "";
        public string ContactType { get; set; } = "";       // QQ / 微信 / Discord / 其他
        public string ContactValue { get; set; } = "";
        public string OnlineTime { get; set; } = "";
        public string? Intro { get; set; }

        // ===== 第一个游戏 =====
        public string GameCode { get; set; } = "";          // delta / apex / ...
        /// <summary>该游戏字段值，如 { "kd": "4-5", "matches": "1000+" }</summary>
        public Dictionary<string, string> Metrics { get; set; } = new();
    }

    /// <summary>追加申请新游戏</summary>
    public class OperatorGameApplyDto
    {
        public string GameCode { get; set; } = "";
        public Dictionary<string, string> Metrics { get; set; } = new();
    }
}