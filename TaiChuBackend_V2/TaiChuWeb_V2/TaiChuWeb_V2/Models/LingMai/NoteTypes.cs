// TaiChuWeb_V2/Models/LingMai/NoteTypes.cs
namespace TaiChuWeb_V2.Models.LingMai
{
    public static class NoteTypes
    {
        public const string Note = "note";         // 基础笔记、长文随笔、博客
        public const string Post = "post";         // 短动态、简语
        public const string Art = "art";           // 艺术作品、画廊
        public const string Video = "video";       // 视频内容
        public const string Audio = "audio";       // 声音/音乐
        public const string Event = "event";       // 世界事件
        public const string Character = "char";    // 角色档案
        public const string Wiki = "wiki";         // 世界观设定/百科
        public const string Blog = "blog";
        public const string Community = "community"; // 社区规则、教程、公共知识
        public const string Schedule = "schedule";   // 🌟 新增：日程安排
        public const string Canvas = "canvas";     // 🌟 新增：星图白板
        public const string Map = "map";           // 🌟 新增：世界地图
        public const string Excel = "excel";       // 🌟 新增：数据表格
        public const string Doc = "doc";

        public static bool IsValid(string type)
        {
            return type switch
            {
                Note or Post or Art or Video or Audio
                or Event or Character or Wiki or Community
                or Canvas or Map or Excel or Schedule or Doc => true,  // 🌟 在这里放行新的白板、地图和数据表格类型
                _ => false
            };
        }

        
        public static bool ShouldShowInSidebarByDefault(string type)
        {
            return type switch
            {
                // 🌟 让白板、地图和数据表格创建后默认出现在侧边栏中
                Note or Wiki or Character or Community or Art or Blog or Post or Canvas or Map or Excel or Schedule or Doc => true,
                _ => false
            };
        }
    }
}