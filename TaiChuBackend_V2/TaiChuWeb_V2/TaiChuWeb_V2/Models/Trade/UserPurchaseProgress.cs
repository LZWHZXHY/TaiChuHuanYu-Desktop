namespace TaiChuWeb_V2.Models.Trade
{
    public class UserPurchaseProgress
    {
        public Guid UserId { get; set; }
        public int StoreItemId { get; set; }

        public int PurchaseCount { get; set; } = 0;

        // 导航属性
        public virtual StoreItem Item { get; set; } = null!;
    }
}