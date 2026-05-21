using System.Collections.Generic;

namespace TaiChuWeb_V2.Dtos.Feedback
{
    public class CreateFeedbackDto
    {
        public string Content { get; set; } = string.Empty;

        public string? ContactInfo { get; set; }

        public List<string> Images { get; set; } = new List<string>();

        // 🌟 新增：接收前端传来的匿名开关
        public bool IsAnonymous { get; set; }
    }
}