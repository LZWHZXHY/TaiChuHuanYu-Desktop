namespace TaiChuWeb_V2.Models.Trade
{
    public class TradeResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Payload { get; set; } // 存放兑换到的 CDKey 或 网盘链接

        // 快捷静态方法：成功
        public static TradeResult Success(string payload) => new TradeResult
        {
            IsSuccess = true,
            Payload = payload,
            Message = "交易成功"
        };

        // 快捷静态方法：失败
        public static TradeResult Fail(string message) => new TradeResult
        {
            IsSuccess = false,
            Message = message
        };
    }
}