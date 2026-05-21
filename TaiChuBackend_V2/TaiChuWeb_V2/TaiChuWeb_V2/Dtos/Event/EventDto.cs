namespace TaiChuWeb_V2.Dtos.Event
{
    public class EventDto
    {
        public string Id { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? StartDate { get; set; }
        public string? EndDate { get; set; }
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public int Status { get; set; } // 传数字给前端，前端根据数字显示不同样式
    }
}
