using System.ComponentModel.DataAnnotations;

namespace TaiChuWeb_V2.Models.Trade
{
    /// <summary>
    /// 资源中心商品定义：解耦业务逻辑与排版权重
    /// </summary>
    public class StoreItem
    {
        [Key]
        public int Id { get; set; }

        // --- 1. 基础信息 ---
        [Required, MaxLength(50)]
        public string Name { get; set; } = string.Empty; // 空间扩展, 归元密钥...

        [MaxLength(200)]
        public string Description { get; set; } = string.Empty; // 详细的 MD 描述文本

        [MaxLength(20)]
        public string Benefit { get; set; } = string.Empty; // 收益简述：空间 +1

        public ItemCategory Category { get; set; } // 额度、资产、功能、社交

        // --- 2. 价格与库存引擎 ---
        public long BaseCost { get; set; } // 初始经验消耗

        /// <summary>
        /// 价格增长系数。
        /// 个人额度类通常为 1.12 (指数增长)，限量资产类通常为 1.0 (固定价格)
        /// </summary>
        public double PriceMultiplier { get; set; } = 1.0;

        /// <summary>
        /// 全局库存上限。NULL 代表无限供应（如空间扩展），Int 代表限量（如 Steam CDKey）
        /// </summary>
        public int? GlobalStock { get; set; }

        // --- 3. 视觉演化引擎 (支撑前端动态大小) ---
        /// <summary>
        /// 初始视觉分。
        /// 像 CDKey 这种珍稀项初始分高，直接占据 Hero 位；额度类初始分低，靠后期购买成长。
        /// </summary>
        public int BaseWeight { get; set; } = 0;

        public int SortOrder { get; set; } // 默认排序优先级

        // --- 4. 状态控制 ---
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DeliveryType Delivery { get; set; } = DeliveryType.None;

        public string? StaticPayload { get; set; }

        public virtual ICollection<StoreItemSecret>? SecretPool { get; set; }

    }

    public enum ItemCategory
    {
        Quota,   // 额度（个人）
        Asset,   // 资产（全局限量）
        Utility, // 功能（消耗品）
        Social   // 社交
    }

    public class StoreItemSecret
    {
        public int Id { get; set; }
        public int StoreItemId { get; set; }

        [Required]
        public string SecretCode { get; set; } = string.Empty; // 具体的 CDKey 字符串

        public bool IsUsed { get; set; } = false; // 是否已被领走
        public Guid? AssignedUserId { get; set; } // 领走人的 ID
        public DateTime? AssignedAt { get; set; }

        public virtual StoreItem Item { get; set; } = null!;
    }

    public enum DeliveryType { None, Link, SecretKey }
}