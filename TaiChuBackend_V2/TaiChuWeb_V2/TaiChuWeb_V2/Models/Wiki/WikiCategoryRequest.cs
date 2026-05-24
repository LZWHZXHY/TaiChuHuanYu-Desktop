namespace TaiChuWeb_V2.Models.Wiki
{
    public class WikiCategoryRequest
    {
        public int Id { get; set; }
        public string RequesterId { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int? ParentId { get; set; }
        public string Reason { get; set; } = string.Empty; // 申请理由
        public int Status { get; set; } // 0: 待审, 1: 已通过, 2: 已拒绝
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public int SortOrder { get; set; }
    }
}
