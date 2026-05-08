namespace TaiChuWeb_V2.Dtos.LingMai
{
    public class CreateFolderDto
    {
        public string Name { get; set; } = string.Empty;
        public Guid SpaceId { get; set; }
        public Guid? FolderId { get; set; } // 🌟 改为 FolderId
    }
}
