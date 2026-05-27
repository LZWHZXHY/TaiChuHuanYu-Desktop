namespace TaiChuWeb_V2.Dtos.Wiki
{
    public class WikiUpdateDto
    {
        public string ArticleId { get; set; } = string.Empty;

        // 🌟 核心修改：改为直接接收前端传过来的编辑器内容
        public string Content { get; set; } = string.Empty;
        public string? Summary { get; set; }
        public int? BaseRevisionId { get; set; }
    }
}
