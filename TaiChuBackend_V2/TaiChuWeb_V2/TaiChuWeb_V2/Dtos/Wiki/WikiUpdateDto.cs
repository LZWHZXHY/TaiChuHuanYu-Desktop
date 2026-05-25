namespace TaiChuWeb_V2.Dtos.Wiki
{
    public class WikiUpdateDto
    {
        public string ArticleId { get; set; } = string.Empty;
        public string NoteId { get; set; } = string.Empty;
        public string Summary { get; set; } = string.Empty; // 用户填写的编辑备注
    }
}
