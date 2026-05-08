namespace TaiChuWeb_V2.Dtos.LingMai
{
    public class QuotaUsageDto
    {
        // 空间维度
        public int UsedSpaces { get; set; }
        public int MaxSpaces { get; set; }
        public int RemainingSpaces => MaxSpaces - UsedSpaces;

        // 节点维度
        public int UsedNotes { get; set; }
        public int MaxNotes { get; set; }
        public int RemainingNotes => MaxNotes - UsedNotes;

        // 进度百分比（可选，方便前端画进度条）
        public double NoteUsagePercent => MaxNotes > 0 ? (double)UsedNotes / MaxNotes * 100 : 0;
    }
}
