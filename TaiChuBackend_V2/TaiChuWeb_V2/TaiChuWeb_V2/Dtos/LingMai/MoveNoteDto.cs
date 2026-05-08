namespace TaiChuWeb_V2.Dtos.LingMai
{
    public class MoveNoteDto
    {
        /// <summary>
        /// 目标文件夹 ID，若移至根目录则为 null
        /// </summary>
        public Guid? FolderId { get; set; }
    }
}
