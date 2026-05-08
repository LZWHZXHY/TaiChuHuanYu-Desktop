namespace TaiChuWeb_V2.Dtos.Trade
{
    public class StoreItemVm
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public string Benefit { get; set; }
        public long CurrentCost { get; set; } // 🌟 实时计算的个人价格
        public int PurchaseCount { get; set; }
        public int? RemainingStock { get; set; } // 剩余库存
        public double VisualWeight { get; set; } // 🌟 核心：传给前端的排版分值
        public string Category { get; set; }
    }
}
