namespace TaiChuWeb_V2.Dtos.LingMai
{
    public class PublishRequest
    {
        public string type { get; set; } = "note";
        public int? categoryId { get; set; }
        public List<string>? tags { get; set; }
    }
}
