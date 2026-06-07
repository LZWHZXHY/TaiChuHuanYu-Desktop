namespace TaiChuWeb_V2.Dtos.LingMai
{
    public class NoteSyncDto
    {
        public Guid NoteId { get; set; }
        // 🌟 增加标题同步，防止编辑器改了标题，侧边栏不更新
        public string? Title { get; set; }

        public string? ExtraData { get; set; }
        public List<BlockSyncDto> Blocks { get; set; } = new();
    }

    public class BlockSyncDto
    {
        public string Id { get; set; } = null!; // 🌟 这里的 null! 告诉编译器不要担心
        public string Type { get; set; } = null!;
        public string Data { get; set; } = null!;

        public int? SortOrder { get; set; }
    }
}
