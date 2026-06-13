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

        public const string Canvas = "canvas";     // 🌟 新增：星图白板
        public const string Map = "map";           // 🌟 新增：世界地图

        public static bool IsValid(string type)
        {
            return type switch
            {
                Note or Post or Art or Video or Audio
                or Event or Character or Wiki or Community
                or Canvas or Map => true,  // 🌟 在这里放行新的白板和地图类型
                _ => false
            };
        }

        /// <summary>
        /// 🌟 优化后的规则：百科类和随笔类默认进侧边栏，方便结构化查阅
        /// </summary>
        public static bool ShouldShowInSidebarByDefault(string type)
        {
            return type switch
            {
                // 🌟 让白板和地图创建后默认出现在侧边栏中
                Note or Wiki or Character or Community or Art or Blog or Post or Canvas or Map => true,
                _ => false
            };
        }
    }
}