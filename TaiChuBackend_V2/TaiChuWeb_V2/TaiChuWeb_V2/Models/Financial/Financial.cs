using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TaiChuWeb_V2.Models.Financial
{
    [Table("financiaDataTable")]
    public class Financial
    {
        [Key]
        [Required]
        public int index { get; set; }

        [Required]
        public string ZhiChu { get; set; }

        [Required]
        public string ZhiChuXiangMu { get; set; }

        // 这一列存的是服务器本地时间（不是 UTC），原样输出，不加时区
        [Required]
        [global::System.Text.Json.Serialization.JsonConverter(typeof(global::TaiChuWeb_V2.Utils.NaiveDateTimeConverter))]
        public DateTime date { get; set; }

        [Required]
        public string ShouKuan { get; set; }
        [Required]
        public double Amount { get; set; }
        [Required]
        public int PayReceive { get; set; } // 1 支出 0 收入

        /// <summary>外部订单号（爱发电同步去重用，可空）</summary>
        [MaxLength(100)]
        public string? ExternalOrderId { get; set; }
    }
}
