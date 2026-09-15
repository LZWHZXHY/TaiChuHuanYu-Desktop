namespace TaiChuWeb_V2.Dtos.Club
{
    /// <summary>拒绝 / 封禁 / 复查不过时填原因</summary>
    public class OperatorAuditDto
    {
        public string? Reason { get; set; }
    }

    /// <summary>考核通过时定级</summary>
    public class OperatorApproveDto
    {
        /// <summary>L1 / L2 / L3 / L4 / L5</summary>
        public string Level { get; set; } = "L1";
    }

    /// <summary>复查不过</summary>
    public class OperatorRecheckFailDto
    {
        public string? Reason { get; set; }
        public bool Demote { get; set; } = false;
    }

    /// <summary>管理端列表项（按游戏维度）</summary>
    public class OperatorPendingItem
    {
        public Guid UserId { get; set; }
        public string Username { get; set; } = "";
        public string? Email { get; set; }
        public string Nickname { get; set; } = "";
        public string ContactType { get; set; } = "";
        public string ContactValue { get; set; } = "";
        public string OnlineTime { get; set; } = "";
        public string? Intro { get; set; }

        public int ReviewCount { get; set; }
        public double AvgOverall { get; set; }

        // ===== 游戏维度 =====
        public string GameCode { get; set; } = "";
        public string GameName { get; set; } = "";
        public Dictionary<string, string> Metrics { get; set; } = new();
        public string AuditStatus { get; set; } = "";
        public string? AuditNote { get; set; }
        public string? Code { get; set; }
        public string? OperatorLevel { get; set; }
        public string RecheckStatus { get; set; } = "none";
        public DateTime AppliedAt { get; set; }
    }

    /// <summary>打手列表项（管理端 / 老板端）</summary>
    public class OperatorListItem
    {
        public Guid UserId { get; set; }
        public string Nickname { get; set; } = "";
        public string GameCode { get; set; } = "";
        public string GameName { get; set; } = "";
        public Dictionary<string, string> Metrics { get; set; } = new();
        public string? Code { get; set; }

        public int ReviewCount { get; set; }
        public double AvgOverall { get; set; }
        public string? OperatorLevel { get; set; }
        public string RecheckStatus { get; set; } = "none";
        public string AuditStatus { get; set; } = "";
        public int OrdersInGame { get; set; }
        public DateTime? ReviewedAt { get; set; }
    }
}
